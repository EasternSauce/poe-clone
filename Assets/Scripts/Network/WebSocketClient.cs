using System;
using System.Runtime.InteropServices;
using UnityEngine;

#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace PoeClone.Network
{
    /// <summary>
    /// Cross-platform WebSocket wrapper. Real WebGL builds can't open raw sockets from the
    /// browser sandbox, so they go through a tiny .jslib bridge (Assets/Plugins/WebGL/WebSocketBridge.jslib)
    /// onto the browser's native WebSocket; the Editor and desktop builds use
    /// System.Net.WebSockets.ClientWebSocket instead, so the whole session flow can be developed
    /// and iterated on by pressing Play, without exporting to WebGL every time.
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        public event Action OnOpen;
        public event Action<string> OnMessage;
        public event Action<string> OnClose;
        public event Action<string> OnError;

        // The WebGL bridge dispatches callbacks via SendMessage(gameObject.name, method, arg),
        // so this object's name must be fixed and unique in the scene.
        private const string GoName = "NetworkClient";

        private Action<string> pendingLocationSearchCallback;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WSConnect(string url, string goName);
        [DllImport("__Internal")] private static extern void WSSend(string goName, string data);
        [DllImport("__Internal")] private static extern void WSClose(string goName);
        [DllImport("__Internal")] private static extern void WSRequestLocationSearch(string goName);
#else
        private ClientWebSocket socket;
        private CancellationTokenSource cts;
        private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
#endif

        private void Awake()
        {
            gameObject.name = GoName;
        }

        public void Connect(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WSConnect(url, GoName);
#else
            ConnectDesktop(url);
#endif
        }

        public void Send(string data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WSSend(GoName, data);
#else
            SendDesktop(data);
#endif
        }

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WSClose(GoName);
#else
            CloseDesktop();
#endif
        }

        /// <summary>
        /// WebGL only: asynchronously fetches window.location.search via the bridge and delivers
        /// it to the callback. In the Editor/desktop builds there is no browser URL, so the
        /// callback fires immediately with <see cref="DevQueryPrefsKey"/> from PlayerPrefs - empty
        /// unless set, e.g. to "?role=spectator&amp;server=ws://localhost:8099" to try the spectator
        /// side in the Editor.
        /// </summary>
        public void RequestLocationSearch(Action<string> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            pendingLocationSearchCallback = callback;
            WSRequestLocationSearch(GoName);
#else
            callback?.Invoke(PlayerPrefs.GetString(DevQueryPrefsKey, string.Empty));
#endif
        }

        public const string DevQueryPrefsKey = "PoeClone.DevQuery";

        // --- Callbacks invoked by WebSocketBridge.jslib via SendMessage (WebGL builds only). ---
        public void OnWSOpen(string _) => OnOpen?.Invoke();
        public void OnWSMessage(string data) => OnMessage?.Invoke(data);
        public void OnWSClose(string reason) => OnClose?.Invoke(reason);
        public void OnWSError(string message) => OnError?.Invoke(message);

        public void OnLocationSearch(string search)
        {
            var callback = pendingLocationSearchCallback;
            pendingLocationSearchCallback = null;
            callback?.Invoke(search ?? string.Empty);
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async void ConnectDesktop(string url)
        {
            try
            {
                socket = new ClientWebSocket();
                cts = new CancellationTokenSource();
                await socket.ConnectAsync(new Uri(url), cts.Token);
                mainThreadActions.Enqueue(() => OnOpen?.Invoke());
                _ = ReceiveLoop();
            }
            catch (Exception e)
            {
                mainThreadActions.Enqueue(() => OnError?.Invoke(e.Message));
                mainThreadActions.Enqueue(() => OnClose?.Invoke(null));
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            string reason = string.IsNullOrEmpty(result.CloseStatusDescription)
                                ? $"Connection closed by the server ({result.CloseStatus})."
                                : $"{result.CloseStatusDescription} ({result.CloseStatus}).";
                            mainThreadActions.Enqueue(() => OnClose?.Invoke(reason));
                            return;
                        }
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    string message = sb.ToString();
                    mainThreadActions.Enqueue(() => OnMessage?.Invoke(message));
                }
            }
            catch (Exception e)
            {
                mainThreadActions.Enqueue(() => OnError?.Invoke(e.Message));
                mainThreadActions.Enqueue(() => OnClose?.Invoke(null));
            }
        }

        private async void SendDesktop(string data)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception e)
            {
                mainThreadActions.Enqueue(() => OnError?.Invoke(e.Message));
            }
        }

        private void CloseDesktop()
        {
            try
            {
                cts?.Cancel();
                socket?.Abort();
            }
            catch
            {
                // already closed/disposed
            }
        }

        private void Update()
        {
            while (mainThreadActions.TryDequeue(out var action))
                action();
        }

        private void OnDestroy()
        {
            CloseDesktop();
        }
#endif
    }
}
