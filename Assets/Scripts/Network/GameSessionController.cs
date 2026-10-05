using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using PoeClone.Inventory;

namespace PoeClone.Network
{
    /// <summary>
    /// Entry point for the browser session-lock feature. Self-installs into any scene (see
    /// <see cref="Bootstrap"/>) so nothing needed to be wired up in Game.unity: it figures out
    /// whether this browser tab is the "player" or a "spectator" from the URL, connects to the
    /// session server, and holds the whole world paused (Time.timeScale = 0) until it is either
    /// granted one of the play slots (up to 10 people play at once, each in their own game) or
    /// settles into spectating - so a queued player never simulates a live, interactable copy of
    /// the game locally. Spectators instead get their scene turned into a puppet of one player's
    /// game (see <see cref="SpectatorReplica"/>) and can switch between players (<see cref="WatchNext"/>).
    /// </summary>
    public class GameSessionController : MonoBehaviour
    {
        public static GameSessionController Instance { get; private set; }

        public SessionRole Role { get; private set; } = SessionRole.Player;
        public bool Connected { get; private set; }
        public bool PlayGranted { get; private set; }
        public bool RemotePlayerActive { get; private set; }
        public int SpectatorCount { get; private set; }
        public int MaxPlayers { get; private set; }
        /// <summary>Everyone playing right now, oldest first (from the server's status).</summary>
        public System.Collections.Generic.IReadOnlyList<PlayerInfo> Players => players;
        /// <summary>Spectators: the id of the player being watched (0 = nobody).</summary>
        public int WatchingId { get; private set; }

        private PlayerInfo[] players = new PlayerInfo[0];
        public string DenyReason { get; private set; }
        /// <summary>Display name sent to the server; empty lets the server pick a default.</summary>
        public string PlayerName { get; private set; } = string.Empty;

        public event Action StateChanged;
        public event Action<ChatEnvelope> ChatReceived;

        private const string StatePrefix = "{\"type\":\"state\"";
        private const string GearPrefix = "{\"type\":\"gear\"";
        private const string NamePrefKey = "PoeClone.PlayerName";

        private WebSocketClient client;
        private PlayerStateBroadcaster stateBroadcaster;
        private SpectatorReplica replica;
        private NamePromptUI namePrompt;

        public SpectatorReplica Replica => replica;
        private string serverUrl;
        private float reconnectDelay = 2f;
        private Coroutine reconnectRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameSessionController");
            go.AddComponent<GameSessionController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Hold the world paused until we know whether this tab is playing or spectating -
            // otherwise a denied player (or a spectator) would briefly see/simulate a live,
            // controllable game underneath the gate UI while the network round-trip is pending.
            SetWorldActive(false);

            var clientGO = new GameObject("NetworkClient");
            clientGO.transform.SetParent(transform, false);
            client = clientGO.AddComponent<WebSocketClient>();
            client.OnOpen += HandleOpen;
            client.OnMessage += HandleMessage;
            client.OnClose += HandleClose;
            client.OnError += HandleError;

            gameObject.AddComponent<SessionGateUI>();
            gameObject.AddComponent<SpectatorView>();
            gameObject.AddComponent<ChatUI>();
            gameObject.AddComponent<PoeClone.UI.TouchControlsUI>();
            gameObject.AddComponent<PoeClone.UI.SkillBarUI>();
            gameObject.AddComponent<PoeClone.UI.DialogueUI>();
            gameObject.AddComponent<PoeClone.UI.MinimapUI>();
            gameObject.AddComponent<PoeClone.UI.QuestTrackerUI>();
            gameObject.AddComponent<PoeClone.UI.BossBarUI>();
            gameObject.AddComponent<PoeClone.UI.PassiveTreeUI>();
            gameObject.AddComponent<PoeClone.Player.SaveSystem>();
            namePrompt = gameObject.AddComponent<NamePromptUI>();
            gameObject.AddComponent<PoeClone.UI.PatchNotesUI>();
            gameObject.AddComponent<PoeClone.UI.EscapeMenuUI>();

            stateBroadcaster = gameObject.AddComponent<PlayerStateBroadcaster>();
            stateBroadcaster.enabled = false;
            replica = gameObject.AddComponent<SpectatorReplica>();
        }

        private IEnumerator Start()
        {
            yield return NetworkConfig.Load(cfg => serverUrl = cfg.serverUrl);

            client.RequestLocationSearch(search =>
            {
                Role = ParseRole(search);
                // ?touch=1 forces the phone controls (testing them in a desktop browser).
                if (HasFlag(search, "touch"))
                    TouchMode.SetForced(true);
                string overrideUrl = ParseServerOverride(search);
                if (!string.IsNullOrEmpty(overrideUrl))
                    serverUrl = overrideUrl;

                if (Role == SessionRole.Spectator)
                {
                    // Spectators run the world (so puppets animate and sounds play) but with every
                    // gameplay system switched off by the replica.
                    replica.Enter();
                    SetWorldActive(true);
                }
                StateChanged?.Invoke();

                string confirmLabel = Role == SessionRole.Spectator ? "Watch" : "Play";
                if (Role == SessionRole.Player)
                {
                    PoeClone.Player.SaveSystem.Profiles();
                    string savedId = PlayerPrefs.GetString("PoeClone.ActiveCharacter.v1", "");
                    if (!string.IsNullOrEmpty(savedId)) PoeClone.Player.SaveSystem.SelectProfile(savedId);
                    namePrompt.ShowCharacters(() => { PlayerName = PoeClone.Player.SaveSystem.ActiveCharacterName; client.Connect(serverUrl); });
                    return;
                }
                namePrompt.Show(PlayerPrefs.GetString(NamePrefKey, string.Empty), confirmLabel, name =>
                {
                    PlayerName = name;
                    PlayerPrefs.SetString(NamePrefKey, name);
                    PlayerPrefs.Save();
                    client.Connect(serverUrl);
                });
            });
        }

        public void SendChat(string text)
        {
            if (!Connected || string.IsNullOrWhiteSpace(text)) return;
            client.Send(JsonUtility.ToJson(new ChatOutMessage { text = text }));
        }

        /// <summary>
        /// Spectators: switch to the next (+1) or previous (-1) player in the list, wrapping round.
        /// Shown at once locally; the server confirms with a status and the new player's latest snapshot.
        /// </summary>
        public void WatchNext(int step)
        {
            if (!Connected || Role != SessionRole.Spectator || players.Length < 2) return;

            int index = Array.FindIndex(players, p => p.id == WatchingId);
            if (index < 0) index = 0;
            int next = ((index + step) % players.Length + players.Length) % players.Length;
            if (players[next].id == WatchingId) return;

            WatchingId = players[next].id;
            replica.ResetReplica();
            client.Send(JsonUtility.ToJson(new WatchMessage { id = WatchingId }));
            StateChanged?.Invoke();
        }

        /// <summary>Name of the player being watched, or null.</summary>
        public string WatchingName
        {
            get
            {
                foreach (PlayerInfo p in players)
                {
                    if (p.id == WatchingId)
                        return p.name;
                }
                return null;
            }
        }

        /// <summary>Sends one already-serialized gameplay snapshot (see PlayerStateBroadcaster).</summary>
        public void SendState(string json)
        {
            if (!Connected || Role != SessionRole.Player || !PlayGranted) return;
            client.Send(json);
        }

        /// <summary>Sends the player's menus' contents (see PlayerStateBroadcaster / GearState).</summary>
        public void SendGear(string json)
        {
            if (!Connected || Role != SessionRole.Player || !PlayGranted) return;
            client.Send(json);
        }

        private void HandleOpen()
        {
            Connected = true;
            reconnectDelay = 2f;
            DenyReason = null;

            var hello = new HelloMessage
            {
                role = Role == SessionRole.Spectator ? "spectator" : "player",
                name = PlayerName,
            };
            client.Send(JsonUtility.ToJson(hello));

            StateChanged?.Invoke();
        }

        private void HandleMessage(string json)
        {
            // Fast path for the 10Hz gameplay stream: skip the generic parse below, the replica
            // parses it into its own type. The server always writes "type" first.
            if (json != null && json.StartsWith(StatePrefix, StringComparison.Ordinal))
            {
                if (Role == SessionRole.Spectator)
                    replica.HandleState(json);
                return;
            }

            if (json != null && json.StartsWith(GearPrefix, StringComparison.Ordinal))
            {
                if (Role == SessionRole.Spectator)
                    replica.HandleGear(json);
                return;
            }

            ServerMessage msg;
            try
            {
                msg = JsonUtility.FromJson<ServerMessage>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameSessionController: malformed message ignored: {e.Message}");
                return;
            }

            if (msg == null || string.IsNullOrEmpty(msg.type)) return;

            switch (msg.type)
            {
                case "welcome":
                    PlayGranted = msg.granted;
                    DenyReason = msg.reason;
                    if (Role == SessionRole.Player)
                    {
                        SetWorldActive(msg.granted);
                        stateBroadcaster.enabled = msg.granted;
                    }
                    StateChanged?.Invoke();
                    break;

                case "status":
                    if (!msg.playerActive && Role == SessionRole.Spectator)
                        replica.ResetReplica();
                    else if (Role == SessionRole.Spectator && WatchingId != 0 && WatchingId != msg.watching)
                        replica.ResetReplica();
                    RemotePlayerActive = msg.playerActive;
                    SpectatorCount = msg.spectatorCount;
                    MaxPlayers = msg.maxPlayers;
                    players = msg.players ?? new PlayerInfo[0];
                    WatchingId = msg.watching;
                    StateChanged?.Invoke();
                    break;

                case "chat":
                    ChatReceived?.Invoke(new ChatEnvelope(msg.from, msg.role, msg.text, msg.ts));
                    break;
            }
        }

        private void HandleClose()
        {
            Connected = false;
            PlayGranted = false;
            RemotePlayerActive = false;
            players = new PlayerInfo[0];
            WatchingId = 0;
            stateBroadcaster.enabled = false;
            if (Role == SessionRole.Spectator)
                replica.ResetReplica(); // the server resends the latest snapshot on reconnect
            else
                SetWorldActive(false);
            StateChanged?.Invoke();

            // Always reschedule: ScheduleReconnect() itself stops any previous pending attempt
            // first, so this is safe to call after every close, including repeated failed retries
            // (a Coroutine reference doesn't become null just because it already finished).
            ScheduleReconnect();
        }

        private void HandleError(string message)
        {
            Debug.LogWarning($"GameSessionController: socket error: {message}");
        }

        private void ScheduleReconnect()
        {
            if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
            reconnectRoutine = StartCoroutine(ReconnectAfterDelay());
        }

        private IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSecondsRealtime(reconnectDelay);
            reconnectDelay = Mathf.Min(reconnectDelay * 1.5f, 15f);

            if (!string.IsNullOrEmpty(serverUrl))
                client.Connect(serverUrl);
        }

        // Time.timeScale = 0 freezes every Update()-driven system in the scene (movement, enemy
        // AI, spawners, animators) in one shot; AudioListener.pause silences and stops processing
        // audio the same way. Both are undone the instant this tab is granted the play slot.
        private void SetWorldActive(bool active)
        {
            Time.timeScale = active ? 1f : 0f;
            AudioListener.pause = !active;
        }

        private static SessionRole ParseRole(string queryString)
        {
            foreach (var (key, value) in EnumerateQuery(queryString))
            {
                if (key == "role" && value.StartsWith("spectat", StringComparison.OrdinalIgnoreCase))
                    return SessionRole.Spectator;
            }
            return SessionRole.Player;
        }

        private static bool HasFlag(string queryString, string name)
        {
            foreach (var (key, value) in EnumerateQuery(queryString))
            {
                if (key == name && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static string ParseServerOverride(string queryString)
        {
            foreach (var (key, value) in EnumerateQuery(queryString))
            {
                if (key == "server")
                    return UnityWebRequest.UnEscapeURL(value);
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<(string key, string value)> EnumerateQuery(string queryString)
        {
            if (string.IsNullOrEmpty(queryString)) yield break;

            string q = queryString.TrimStart('?');
            foreach (var pair in q.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                yield return (pair.Substring(0, eq), pair.Substring(eq + 1));
            }
        }
    }
}
