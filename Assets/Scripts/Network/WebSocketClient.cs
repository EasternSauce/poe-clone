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
        [DllImport("__Internal")] private static extern int WSBufferedAmount(string goName);
        [DllImport("__Internal")] private static extern void WSClose(string goName);
        [DllImport("__Internal")] private static extern void WSRequestLocationSearch(string goName);
#else
        private ClientWebSocket socket;
        private CancellationTokenSource cts;
        private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
        // ClientWebSocket allows one SendAsync at a time, so sends go out in order from a queue.
        private readonly System.Collections.Generic.Queue<byte[]> sendQueue = new System.Collections.Generic.Queue<byte[]>();
        private int queuedBytes;
        private ClientWebSocket sendingOn; // the socket a send loop is running for
        private ClientWebSocket closingSocket; // closed by us, still sending what was queued
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

        /// <summary>
        /// Bytes sent but not yet handed to the network. Grows when the upload can't keep up;
        /// callers skip disposable snapshots then, so chat and events don't wait behind them.
        /// </summary>
        public int BufferedAmount
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return WSBufferedAmount(GoName);
#else
                return queuedBytes;
#endif
            }
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
                sendQueue.Clear();
                queuedBytes = 0;
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
            var target = socket;
            var token = cts.Token;
            var buffer = new byte[64 * 1024];
            try
            {
                while (target.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await target.ReceiveAsync(new ArraySegment<byte>(buffer), token);
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

        private void SendDesktop(string data)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(data);
            sendQueue.Enqueue(bytes);
            queuedBytes += bytes.Length;
            if (sendingOn != socket)
                PumpSends(socket);
        }

        private async void PumpSends(ClientWebSocket target)
        {
            sendingOn = target;
            try
            {
                while (sendQueue.Count > 0 && (socket == target || closingSocket == target))
                {
                    byte[] bytes = sendQueue.Peek();
                    await target.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    if (socket != target && closingSocket != target) break;
                    sendQueue.Dequeue();
                    queuedBytes -= bytes.Length;
                }
            }
            catch (Exception e)
            {
                if (socket == target)
                {
                    sendQueue.Clear();
                    queuedBytes = 0;
                    mainThreadActions.Enqueue(() => OnError?.Invoke(e.Message));
                }
            }
            finally
            {
                if (sendingOn == target) sendingOn = null;
            }
        }

        // Like a browser's close(): messages already sent (e.g. a co-op leave right before going
        // back to the menu) go out first, then the socket closes. Abort is the fallback after 1s.
        private void CloseDesktop()
        {
            var target = socket;
            var source = cts;
            if (target == null) return;
            socket = null;
            cts = null;
            closingSocket = target;
            CloseGracefully(target, source);
        }

        private async void CloseGracefully(ClientWebSocket target, CancellationTokenSource source)
        {
            try
            {
                if (sendQueue.Count > 0 && sendingOn != target)
                    PumpSends(target);
                var deadline = DateTime.UtcNow.AddSeconds(1);
                while (sendingOn == target && DateTime.UtcNow < deadline)
                    await Task.Delay(10);
                if (target.State == WebSocketState.Open)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await target.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", timeout.Token);
                }
            }
            catch
            {
                // already closed/disposed
            }
            finally
            {
                if (closingSocket == target) closingSocket = null;
                source.Cancel();
                target.Abort();
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
