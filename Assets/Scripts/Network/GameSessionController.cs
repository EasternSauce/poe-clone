using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PoeClone.Network
{
    /// <summary>
    /// Entry point for the browser session-lock feature. Self-installs into any scene (see
    /// <see cref="Bootstrap"/>) so nothing needed to be wired up in Game.unity: it figures out
    /// whether this browser tab is the "player" or a "spectator" from the URL, connects to the
    /// session server, and holds the whole world paused (Time.timeScale = 0) until it is either
    /// granted the single play slot or settles into spectating - so a denied/second player and
    /// every spectator never simulate or render a live, interactable copy of the game locally.
    /// </summary>
    public class GameSessionController : MonoBehaviour
    {
        public static GameSessionController Instance { get; private set; }

        public SessionRole Role { get; private set; } = SessionRole.Player;
        public bool Connected { get; private set; }
        public bool PlayGranted { get; private set; }
        public bool RemotePlayerActive { get; private set; }
        public int SpectatorCount { get; private set; }
        public string DenyReason { get; private set; }

        public event Action StateChanged;
        public event Action<FrameEnvelope> FrameReceived;
        public event Action<ChatEnvelope> ChatReceived;

        private WebSocketClient client;
        private PlayerFrameBroadcaster frameBroadcaster;
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

            frameBroadcaster = gameObject.AddComponent<PlayerFrameBroadcaster>();
            frameBroadcaster.enabled = false;
        }

        private IEnumerator Start()
        {
            yield return NetworkConfig.Load(cfg => serverUrl = cfg.serverUrl);

            client.RequestLocationSearch(search =>
            {
                Role = ParseRole(search);
                string overrideUrl = ParseServerOverride(search);
                if (!string.IsNullOrEmpty(overrideUrl))
                    serverUrl = overrideUrl;

                client.Connect(serverUrl);
            });
        }

        public void SendChat(string text)
        {
            if (!Connected || string.IsNullOrWhiteSpace(text)) return;
            client.Send(JsonUtility.ToJson(new ChatOutMessage { text = text }));
        }

        public void SendFrame(string base64Jpeg, HudPayload hud)
        {
            if (!Connected || Role != SessionRole.Player || !PlayGranted) return;
            client.Send(JsonUtility.ToJson(new FrameMessage { image = base64Jpeg, hud = hud }));
        }

        private void HandleOpen()
        {
            Connected = true;
            reconnectDelay = 2f;
            DenyReason = null;

            var hello = new HelloMessage { role = Role == SessionRole.Spectator ? "spectator" : "player" };
            client.Send(JsonUtility.ToJson(hello));

            StateChanged?.Invoke();
        }

        private void HandleMessage(string json)
        {
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
                        frameBroadcaster.enabled = msg.granted;
                    }
                    StateChanged?.Invoke();
                    break;

                case "status":
                    RemotePlayerActive = msg.playerActive;
                    SpectatorCount = msg.spectatorCount;
                    StateChanged?.Invoke();
                    break;

                case "frame":
                    FrameReceived?.Invoke(new FrameEnvelope(msg.image, msg.hud, msg.ts));
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
            SetWorldActive(false);
            frameBroadcaster.enabled = false;
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
