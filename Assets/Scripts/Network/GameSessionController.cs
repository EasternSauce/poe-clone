using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Starts the chosen character locally. Chat and spectator streaming connect in the
    /// background; server availability never controls local gameplay.
    /// </summary>
    public class GameSessionController : MonoBehaviour
    {
        public static GameSessionController Instance { get; private set; }

        public SessionRole Role { get; private set; } = SessionRole.Player;
        public bool Connected { get; private set; }
        /// <summary>A character was selected for local play, independently of the server.</summary>
        public bool PlayGranted { get; private set; }
        public bool GameplayReady { get; private set; }
        public bool RemotePlayerActive { get; private set; }
        public int SpectatorCount { get; private set; }
        public int MaxPlayers { get; private set; }
        /// <summary>Everyone playing right now, oldest first (from the server's status).</summary>
        public System.Collections.Generic.IReadOnlyList<PlayerInfo> Players => players;
        /// <summary>Spectators: the id of the player being watched (0 = nobody).</summary>
        public int WatchingId { get; private set; }

        private PlayerInfo[] players = new PlayerInfo[0];
        public string DenyReason { get; private set; }
        public string DisconnectReason { get; private set; }
        public bool Reconnecting { get; private set; }
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

        // Co-op: what the player picked in the co-op menu, until the game starts (then coop runs it).
        private enum CoopIntent { None, Host, Join }
        private CoopIntent coopIntent;
        private CoopSession coop;
        private PlayerInfo[] lobby = new PlayerInfo[0];

        /// <summary>Shown on the start menu after a co-op game ended (the scene reloads for it).</summary>
        private static string startNotice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            startNotice = null;
        }
        private NamePromptUI namePrompt;
        private AudioListener menuAudioListener;
        private GameObject scenePlayer;
        private readonly System.Collections.Generic.List<GameObject> suspendedRoots = new System.Collections.Generic.List<GameObject>();
        private bool gameplayCreated;

        public SpectatorReplica Replica => replica;
        private string serverUrl;
        private float reconnectDelay = 2f;
        private Coroutine reconnectRoutine;
        private bool returningToCharacters;
        private bool serverPlayGranted;
        private bool chatDisconnected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("PoeClone.LootSimulator.Active", false)) return;
#endif
            if (MinimalCombatMode.Enabled) return;
            if (Instance != null) return;
            var go = new GameObject("GameSessionController");
            go.AddComponent<GameSessionController>();
            SceneManager.sceneLoaded += SuspendStartupScene;
        }

        private static void SuspendStartupScene(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= SuspendStartupScene;
            Instance?.SuspendScene(scene);
        }

        private void SuspendScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == gameObject || !root.activeSelf || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
                suspendedRoots.Add(root);
                root.SetActive(false);
            }
        }

        private void PrepareGameplay()
        {
            if (gameplayCreated) return;
            gameplayCreated = true;
            if (menuAudioListener != null) menuAudioListener.enabled = false;
            foreach (GameObject root in suspendedRoots)
                if (root != null) root.SetActive(true);
            suspendedRoots.Clear();
            var player = FindAnyObjectByType<PlayerStats>();
            scenePlayer = player != null ? player.gameObject : null;
            WorldBuilder.EnsureBuilt();
            CreateGameplaySystems();
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

            menuAudioListener = gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<PoeClone.Audio.MusicPlayer>().Play("Dark Quest");

            // Pause while choosing and loading the local character.
            SetWorldActive(false);

            var clientGO = new GameObject("NetworkClient");
            clientGO.transform.SetParent(transform, false);
            client = clientGO.AddComponent<WebSocketClient>();
            client.OnOpen += HandleOpen;
            client.OnMessage += HandleMessage;
            client.OnClose += HandleClose;
            client.OnError += HandleError;

            gameObject.AddComponent<SessionGateUI>();
            gameObject.AddComponent<PoeClone.Player.SaveSystem>();
            namePrompt = gameObject.AddComponent<NamePromptUI>();
            gameObject.AddComponent<PoeClone.UI.PatchNotesUI>();
            gameObject.AddComponent<PoeClone.UI.EscapeMenuUI>();

            stateBroadcaster = gameObject.AddComponent<PlayerStateBroadcaster>();
            stateBroadcaster.enabled = false;
            replica = gameObject.AddComponent<SpectatorReplica>();
        }

        private void CreateGameplaySystems()
        {
            gameObject.AddComponent<SpectatorView>();
            gameObject.AddComponent<ChatUI>();
            gameObject.AddComponent<PoeClone.UI.TouchControlsUI>();
            gameObject.AddComponent<PoeClone.UI.SkillBarUI>();
            gameObject.AddComponent<PoeClone.UI.DialogueUI>();
            gameObject.AddComponent<PoeClone.UI.MinimapUI>();
            gameObject.AddComponent<PoeClone.UI.QuestTrackerUI>();
            gameObject.AddComponent<PoeClone.UI.BossBarUI>();
            gameObject.AddComponent<PoeClone.UI.PassiveTreeUI>();
        }

        private IEnumerator Start()
        {
            yield return null;

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
                    PrepareGameplay();
                    if (scenePlayer != null) scenePlayer.SetActive(true);
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
                    ShowStartMenu();
                    return;
                }
                namePrompt.Show(PlayerPrefs.GetString(NamePrefKey, string.Empty), confirmLabel, name =>
                {
                    PlayerName = name;
                    PlayerPrefs.SetString(NamePrefKey, name);
                    PlayerPrefs.Save();
                    StartCoroutine(ConnectToServer());
                });
            });
        }

        // ------------------------------------------------------------------ start menu and co-op

        private void ShowStartMenu()
        {
            string notice = startNotice;
            startNotice = null;
            coopIntent = CoopIntent.None;
            namePrompt.ShowStartMenu(() => ChooseCharacter(CoopIntent.None), ShowCoopMenu, notice);
        }

        private void ShowCoopMenu()
        {
            LeaveCoopLobby();
            namePrompt.ShowCoopMenu(() => ChooseCharacter(CoopIntent.Host), () => ChooseCharacter(CoopIntent.Join), ShowStartMenu);
        }

        private void ChooseCharacter(CoopIntent intent)
        {
            LeaveCoopLobby();
            namePrompt.ShowCharacters(() =>
            {
                PlayerName = SaveSystem.ActiveCharacterName;
                if (intent == CoopIntent.None)
                    StartGame();
                else
                    EnterCoopLobby(intent);
            }, intent == CoopIntent.None ? (Action)ShowStartMenu : ShowCoopMenu);
        }

        private void StartGame()
        {
            namePrompt.HideScreens();
            PlayGranted = true;
            PrepareGameplay();
            StartCoroutine(LoadChosenCharacter());
            StateChanged?.Invoke();
            if (!Connected)
                StartCoroutine(ConnectToServer());
        }

        // Hosting needs the server: connect first if single player's background connection isn't up yet.
        private void EnterCoopLobby(CoopIntent intent)
        {
            coopIntent = intent;
            namePrompt.ShowCoopStatus("Connecting...", null, BackToCharacters);
            if (Connected && serverPlayGranted)
                RequestCoop();
            else if (!Connected && reconnectRoutine == null)
                StartCoroutine(ConnectToServer());
        }

        private void BackToCharacters()
        {
            CoopIntent intent = coopIntent;
            LeaveCoopLobby();
            ChooseCharacter(intent);
        }

        private void LeaveCoopLobby()
        {
            if (coopIntent != CoopIntent.None && Connected && coop == null)
                client.Send(JsonUtility.ToJson(new CoopLeaveMessage()));
            coopIntent = CoopIntent.None;
        }

        private void RequestCoop()
        {
            if (coopIntent == CoopIntent.Host)
            {
                client.Send(JsonUtility.ToJson(new CoopHostMessage { name = PlayerName }));
                namePrompt.ShowCoopStatus("Waiting for another player...", "Your game is listed for others to join.", BackToCharacters);
            }
            else if (coopIntent == CoopIntent.Join)
            {
                client.Send(JsonUtility.ToJson(new CoopListMessage()));
                ShowLobby();
            }
        }

        private void ShowLobby()
        {
            namePrompt.ShowCoopLobby(lobby, JoinGame, BackToCharacters);
        }

        private void JoinGame(PlayerInfo host)
        {
            client.Send(JsonUtility.ToJson(new CoopJoinMessage { id = host.id, name = PlayerName }));
            namePrompt.ShowCoopStatus("Joining " + host.name + "'s game...", null, BackToCharacters);
        }

        private void HandleCoopMessage(ServerMessage msg)
        {
            switch (msg.state)
            {
                case "failed":
                    if (coopIntent == CoopIntent.Join)
                        ShowLobby();
                    break;

                case "started":
                    if (coopIntent == CoopIntent.None || coop != null)
                        return;
                    coopIntent = CoopIntent.None;
                    StartGame();
                    coop = gameObject.AddComponent<CoopSession>();
                    coop.Begin(msg.coopRole == "host", msg.partnerName, stateBroadcaster, replica);
                    break;

                case "ended":
                    if (coop == null)
                        return;
                    bool wasHost = coop.IsHost;
                    string partner = coop.PartnerName;
                    Destroy(coop);
                    coop = null;
                    if (wasHost)
                    {
                        // The world was ours all along: carry on alone.
                        ChatReceived?.Invoke(new ChatEnvelope("", "system", partner + " left the game.", 0));
                    }
                    else
                    {
                        // Our enemies were the host's: back to the menu, character and progress saved.
                        startNotice = partner + " ended the co-op game. Your character's progress is saved.";
                        ReturnToCharacters();
                    }
                    break;
            }
        }

        /// <summary>Co-op: one already-serialized snapshot or event for the partner.</summary>
        public void SendCoop(string json)
        {
            if (Connected && coop != null)
                client.Send(json);
        }

        public void SendChat(string text)
        {
            if (!Connected || string.IsNullOrWhiteSpace(text)) return;
            client.Send(JsonUtility.ToJson(new ChatOutMessage { text = text }));
        }

        private IEnumerator LoadChosenCharacter()
        {
            // Allow the reactivated scene's Start methods to bind stats, gear and skills
            // before applying the character's saved values and bindings.
            yield return null;
            GameplayReady = true;
            SaveSystem.LoadSelectedProfile();
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
            if (!Connected || Role != SessionRole.Player || !serverPlayGranted) return;
            client.Send(json);
        }

        /// <summary>Sends the player's menus' contents (see PlayerStateBroadcaster / GearState).</summary>
        public void SendGear(string json)
        {
            if (!Connected || Role != SessionRole.Player || !serverPlayGranted) return;
            client.Send(json);
        }

        private void HandleOpen()
        {
            Connected = true;
            reconnectDelay = 2f;
            DenyReason = null;
            DisconnectReason = null;
            Reconnecting = false;

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

            if (json != null && json.StartsWith(CoopProtocol.SnapshotPrefix, StringComparison.Ordinal))
            {
                coop?.HandleSnapshot(json);
                return;
            }

            if (json != null && json.StartsWith(CoopProtocol.EventPrefix, StringComparison.Ordinal))
            {
                coop?.HandleEvent(json);
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
                    serverPlayGranted = msg.granted;
                    DenyReason = msg.reason;
                    if (Role == SessionRole.Player)
                    {
                        // Server permission controls spectator streaming only.
                        stateBroadcaster.enabled = msg.granted;
                    }
                    if (msg.granted && chatDisconnected)
                    {
                        chatDisconnected = false;
                        ChatReceived?.Invoke(new ChatEnvelope("", "system", "Reconnected to chat.", 0));
                    }
                    if (coopIntent != CoopIntent.None)
                    {
                        if (msg.granted)
                            RequestCoop();
                        else
                            namePrompt.ShowCoopStatus("Can't start co-op", msg.reason, BackToCharacters);
                    }
                    StateChanged?.Invoke();
                    break;

                case "lobby":
                    lobby = msg.hosts ?? new PlayerInfo[0];
                    if (coopIntent == CoopIntent.Join)
                        ShowLobby();
                    break;

                case "coop":
                    HandleCoopMessage(msg);
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

        private void HandleClose(string reason)
        {
            if (returningToCharacters) return;
            if (Connected)
            {
                chatDisconnected = true;
                ChatReceived?.Invoke(new ChatEnvelope("", "system", "Disconnected from chat.", 0));
            }
            if (!string.IsNullOrWhiteSpace(reason)) DisconnectReason = reason;
            if (string.IsNullOrWhiteSpace(DisconnectReason)) DisconnectReason = "Connection closed unexpectedly.";
            if (coopIntent != CoopIntent.None)
                namePrompt.ShowCoopStatus("Can't reach the co-op server", "Retrying... You can go back and play alone.", BackToCharacters);
            if (coop != null)
            {
                // The server pairs the two games; without it the partner is gone for good. A guest's
                // enemies were the host's, so it goes back to the menu (its progress is saved).
                bool wasHost = coop.IsHost;
                Destroy(coop);
                coop = null;
                if (!wasHost)
                {
                    startNotice = "Lost the connection to the co-op game. Your character's progress is saved.";
                    ReturnToCharacters();
                    return;
                }
                ChatReceived?.Invoke(new ChatEnvelope("", "system", "Co-op connection lost.", 0));
            }
            Reconnecting = true;
            Connected = false;
            serverPlayGranted = false;
            DenyReason = null;
            RemotePlayerActive = false;
            players = new PlayerInfo[0];
            WatchingId = 0;
            stateBroadcaster.enabled = false;
            if (Role == SessionRole.Spectator)
                replica.ResetReplica(); // the server resends the latest snapshot on reconnect
            StateChanged?.Invoke();

            // Always reschedule: ScheduleReconnect() itself stops any previous pending attempt
            // first, so this is safe to call after every close, including repeated failed retries
            // (a Coroutine reference doesn't become null just because it already finished).
            ScheduleReconnect();
        }

        private void HandleError(string message)
        {
            Debug.LogWarning($"GameSessionController: socket error: {message}");
            if (!string.IsNullOrWhiteSpace(message) && message != "WebSocket error")
                DisconnectReason = message;
        }

        public void NotifyCharacterLoaded()
        {
            if (Role == SessionRole.Player && PlayGranted)
            {
                if (scenePlayer != null) scenePlayer.SetActive(true);
                SetWorldActive(true);
            }
            StateChanged?.Invoke();
        }

        private IEnumerator ConnectToServer()
        {
            if (string.IsNullOrEmpty(serverUrl))
                yield return NetworkConfig.Load(cfg => serverUrl = cfg.serverUrl);
            if (!returningToCharacters && !string.IsNullOrEmpty(serverUrl))
                client.Connect(serverUrl);
            else if (coopIntent != CoopIntent.None)
                namePrompt.ShowCoopStatus("Co-op isn't available", "No server is configured for this build.", BackToCharacters);
        }

        private void ScheduleReconnect()
        {
            if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
            reconnectRoutine = StartCoroutine(ReconnectAfterDelay());
        }

        private void OnApplicationFocus(bool focused)
        {
            // A background tab may have suspended its reconnect timer. Resume promptly when the
            // browser gives the game focus again; a healthy socket needs no new connection.
            if (focused && !Connected && reconnectRoutine != null && !returningToCharacters && !string.IsNullOrEmpty(serverUrl) &&
                !string.IsNullOrEmpty(PlayerName))
                ScheduleReconnect();
        }

        public void ReturnToCharacters()
        {
            if (Role != SessionRole.Player || returningToCharacters) return;
            SaveSystem.SaveBeforeCharacterSwitch();
            returningToCharacters = true;
            if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
            stateBroadcaster.enabled = false;
            client.Close();
            SetWorldActive(false);
            SceneManager.sceneLoaded += RestartAfterCharacterSwitch;
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void RestartAfterCharacterSwitch(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= RestartAfterCharacterSwitch;
            var session = new GameObject("GameSessionController").AddComponent<GameSessionController>();
            session.SuspendScene(scene);
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
        // audio the same way. Both are undone once the local character has loaded.
        private void SetWorldActive(bool active)
        {
            bool pausedByMenu = GetComponent<PoeClone.UI.EscapeMenuUI>()?.IsOpen == true;
            Time.timeScale = active && !pausedByMenu ? 1f : 0f;
            AudioListener.pause = !active || pausedByMenu;
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
