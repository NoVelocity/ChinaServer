using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NoVelocity.Communicator
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// Client for NoVelocity's Communicator Server.
    ///
    /// Protocol (JSON over WebSocket at ws://host:port/{clientId}):
    ///   we send     {"destination": id, "payload": any}   -> server relays it to the destination
    ///   we receive  {"id", "source", "payload"}            -> a request from another client; answer with {"id","result"}
    ///   we receive  {"id", "result"}                       -> delivery result for our send, or ack of our own reply
    ///
    /// The server does not tell the sender which task id a send got, so sends are serialized:
    /// only one send is "in flight" at a time and the next result addressed to us belongs to it.
    ///
    /// Events are raised on the SynchronizationContext that was current when <see cref="ConnectAsync"/>
    /// was called (the Unity main thread if you call it from a MonoBehaviour).
    /// </summary>
    public sealed class CommunicatorClient : IDisposable
    {
        private static readonly HttpClient Http = new HttpClient();

        private readonly string _host;
        private readonly int _port;
        private readonly bool _useTls;
        private readonly Uri _uri;

        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);   // one send in flight
        private readonly SemaphoreSlim _wsLock = new SemaphoreSlim(1, 1);     // ClientWebSocket allows one concurrent SendAsync
        private readonly ConcurrentDictionary<string, byte> _awaitingAck = new ConcurrentDictionary<string, byte>();

        private TaskCompletionSource<SendResult> _inFlight;
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private SynchronizationContext _context;
        private int _state;
        private bool _disposed;

        public CommunicatorClient(string host, int port, string clientId, bool useTls = false)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client id is required.", nameof(clientId));

            _host = host;
            _port = port;
            _useTls = useTls;
            ClientId = clientId;
            _uri = new Uri($"{(useTls ? "wss" : "ws")}://{host}:{port}/{Uri.EscapeDataString(clientId)}");
        }

        public string ClientId { get; }
        public Uri Uri => _uri;

        public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);
        public bool IsConnected => State == ConnectionState.Connected;

        /// <summary>Reconnect automatically after the connection drops.</summary>
        public bool AutoReconnect { get; set; } = true;
        public TimeSpan ReconnectInitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Safety net: how long <see cref="SendAsync"/> waits for the server's result.
        /// The server itself answers within its delivery timeout (15 s by default), so keep this larger.
        /// </summary>
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);

        // ---- events -------------------------------------------------------------------------

        public event Action OnConnected;
        /// <summary>Raised after every connection loss or manual disconnect. The exception is null for a clean close.</summary>
        public event Action<Exception> OnDisconnected;
        /// <summary>Another client sent us something. Call <see cref="IncomingRequest.Reply"/> or the sender will time out.</summary>
        public event Action<IncomingRequest> OnRequest;
        /// <summary>Delivery result of a message we sent (raised in addition to completing the SendAsync task).</summary>
        public event Action<SendResult> OnSendResult;
        /// <summary>The server confirmed our reply to a request (task id, result; "ok" on success).</summary>
        public event Action<string, string> OnAck;
        public event Action<Exception> OnError;

        // ---- connect / disconnect -----------------------------------------------------------

        /// <summary>Opens the connection. Throws if the first attempt fails; later drops are handled by <see cref="AutoReconnect"/>.</summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CommunicatorClient));
            if (Interlocked.CompareExchange(ref _state, (int)ConnectionState.Connecting, (int)ConnectionState.Disconnected)
                != (int)ConnectionState.Disconnected)
                throw new InvalidOperationException("The client is already connected or connecting.");

            _context = SynchronizationContext.Current;
            var cts = new CancellationTokenSource();
            try
            {
                using (cancellationToken.Register(cts.Cancel))
                    await OpenSocketAsync(cts.Token);
            }
            catch
            {
                SetState(ConnectionState.Disconnected);
                throw;
            }

            _cts = cts;
            SetState(ConnectionState.Connected);
            _loopTask = Task.Run(() => RunLoopAsync(cts.Token));
            Post(() => OnConnected?.Invoke());
        }

        /// <summary>Closes the connection and stops reconnecting.</summary>
        public async Task DisconnectAsync()
        {
            var cts = _cts;
            var loop = _loopTask;
            if (cts == null || loop == null || State == ConnectionState.Disconnected) return;

            cts.Cancel();

            var ws = _ws;
            if (ws != null && ws.State == WebSocketState.Open)
            {
                try
                {
                    using (var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", closeCts.Token);
                }
                catch { /* socket already broken, Abort below handles it */ }
            }

            if (await Task.WhenAny(loop, Task.Delay(TimeSpan.FromSeconds(2))) != loop)
            {
                _ws?.Abort();
                await loop;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
            _ws?.Abort();
        }

        // ---- sending ------------------------------------------------------------------------

        /// <summary>
        /// Sends <paramref name="payload"/> (any JSON-serializable object, or a JToken) to another client and
        /// waits until the server reports the delivery result. Calls are queued: one send at a time.
        /// Cancelling only stops the waiting; the message itself has already been sent.
        /// </summary>
        public async Task<SendResult> SendAsync(string destination, object payload, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(destination)) throw new ArgumentException("Destination is required.", nameof(destination));

            // The server crashes the connection if "payload" is missing, so it is always present (JSON null at minimum).
            JToken token = payload is JToken jt ? jt : payload == null ? JValue.CreateNull() : JToken.FromObject(payload);
            var message = new JObject { ["destination"] = destination, ["payload"] = token }.ToString(Formatting.None);

            await _sendGate.WaitAsync(cancellationToken);

            var tcs = new TaskCompletionSource<SendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _inFlight = tcs;
            try
            {
                await SendTextAsync(message, cancellationToken);
            }
            catch
            {
                CompleteInFlight(tcs, t => { });   // releases the gate
                throw;
            }

            StartWatchdog(tcs);

            if (!cancellationToken.CanBeCanceled)
                return await tcs.Task;

            var cancelled = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(tcs.Task, cancelled.Task) != tcs.Task)
                    throw new OperationCanceledException(cancellationToken);
            }
            return await tcs.Task;
        }

        /// <summary>Answers a request received earlier. Normally you use <see cref="IncomingRequest.ReplyAsync"/>.</summary>
        public async Task ReplyAsync(string taskId, string result = ResultCodes.Ok, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(taskId)) throw new ArgumentException("Task id is required.", nameof(taskId));

            var message = new JObject { ["id"] = taskId, ["result"] = result ?? string.Empty }.ToString(Formatting.None);

            _awaitingAck.TryAdd(taskId, 0);   // register before sending so the ack cannot be mistaken for a send result
            try
            {
                await SendTextAsync(message, cancellationToken);
            }
            catch
            {
                _awaitingAck.TryRemove(taskId, out _);
                throw;
            }
        }

        // ---- server query -------------------------------------------------------------------

        /// <summary>Ids of all clients currently connected to the server (GET /).</summary>
        public async Task<string[]> GetActiveClientsAsync(CancellationToken cancellationToken = default)
        {
            var url = $"{(_useTls ? "https" : "http")}://{_host}:{_port}/";
            using (var response = await Http.GetAsync(url, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JArray.Parse(json).ToObject<string[]>();
            }
        }

        // ---- internals ----------------------------------------------------------------------

        private async Task OpenSocketAsync(CancellationToken ct)
        {
            var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(_uri, ct);
            }
            catch
            {
                ws.Dispose();
                throw;
            }
            _ws = ws;
        }

        private async Task SendTextAsync(string text, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _wsLock.WaitAsync(ct);
            try
            {
                var ws = _ws;
                if (ws == null || ws.State != WebSocketState.Open)
                    throw new CommunicatorException("Not connected to the Communicator server.");
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
            finally
            {
                _wsLock.Release();
            }
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    Exception reason = null;
                    try
                    {
                        await ReceiveLoopAsync();
                    }
                    catch (Exception ex)
                    {
                        if (!ct.IsCancellationRequested) reason = ex;
                    }

                    _awaitingAck.Clear();
                    CompleteInFlight(null, t => t.TrySetException(new CommunicatorException("Connection to the server was lost.")));
                    var dead = Interlocked.Exchange(ref _ws, null);
                    dead?.Dispose();

                    var r = reason;
                    Post(() => OnDisconnected?.Invoke(r));

                    if (ct.IsCancellationRequested || !AutoReconnect) break;

                    SetState(ConnectionState.Reconnecting);
                    if (!await ReconnectAsync(ct)) break;
                    SetState(ConnectionState.Connected);
                    Post(() => OnConnected?.Invoke());
                }
            }
            catch (Exception ex)
            {
                Report(ex);
            }
            finally
            {
                SetState(ConnectionState.Disconnected);
            }
        }

        private async Task<bool> ReconnectAsync(CancellationToken ct)
        {
            var delay = ReconnectInitialDelay;
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { return false; }

                try
                {
                    await OpenSocketAsync(ct);
                    return true;
                }
                catch (OperationCanceledException) { return false; }
                catch (Exception)
                {
                    var next = TimeSpan.FromTicks(delay.Ticks * 2);
                    delay = next > ReconnectMaxDelay ? ReconnectMaxDelay : next;
                }
            }
            return false;
        }

        private async Task ReceiveLoopAsync()
        {
            var ws = _ws;
            var buffer = new byte[8192];
            using (var ms = new MemoryStream())
            {
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None); }
                        catch { /* we may already have sent our own close frame */ }
                        return;
                    }

                    ms.Write(buffer, 0, r.Count);
                    if (!r.EndOfMessage) continue;

                    if (r.MessageType == WebSocketMessageType.Text)
                    {
                        try { HandleMessage(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)); }
                        catch (Exception ex) { Report(ex); }
                    }
                    ms.SetLength(0);
                }
            }
        }

        private void HandleMessage(string text)
        {
            JObject obj;
            try { obj = JObject.Parse(text); }
            catch (JsonException ex) { Report(ex); return; }

            var id = (string)obj["id"];
            if (id == null)
            {
                Report(new CommunicatorException("Server message without an id: " + text));
                return;
            }

            // {"id", "source", "payload"} - somebody wants something from us
            if (obj.TryGetValue("source", out var sourceToken) && obj.TryGetValue("payload", out var payload))
            {
                var request = new IncomingRequest(this, id, (string)sourceToken, payload);
                Post(() => OnRequest?.Invoke(request));
                return;
            }

            // {"id", "result"} - either the ack of a reply we sent, or the delivery result of a send of ours
            if (obj.TryGetValue("result", out var resultToken))
            {
                var result = (string)resultToken ?? string.Empty;

                if (_awaitingAck.TryRemove(id, out _))
                {
                    Post(() => OnAck?.Invoke(id, result));
                    return;
                }

                var sendResult = new SendResult(id, result);
                CompleteInFlight(null, t => t.TrySetResult(sendResult));
                Post(() => OnSendResult?.Invoke(sendResult));
            }
        }

        /// <summary>
        /// Completes the in-flight send and releases the send gate (exactly once).
        /// With <paramref name="expected"/> it only acts if that send is still the current one.
        /// </summary>
        private bool CompleteInFlight(TaskCompletionSource<SendResult> expected, Action<TaskCompletionSource<SendResult>> complete)
        {
            TaskCompletionSource<SendResult> tcs;
            if (expected == null)
                tcs = Interlocked.Exchange(ref _inFlight, null);
            else
                tcs = Interlocked.CompareExchange(ref _inFlight, null, expected) == expected ? expected : null;

            if (tcs == null) return false;
            complete(tcs);
            _sendGate.Release();
            return true;
        }

        private void StartWatchdog(TaskCompletionSource<SendResult> tcs)
        {
            var timeout = SendTimeout;
            Task.Delay(timeout).ContinueWith(
                _ => CompleteInFlight(tcs, t => t.TrySetException(
                    new TimeoutException($"No delivery result from the server within {timeout.TotalSeconds:0.#} s."))),
                TaskScheduler.Default);
        }

        private void SetState(ConnectionState state) => Volatile.Write(ref _state, (int)state);

        private void Post(Action action)
        {
            var ctx = _context;
            if (ctx == null) Safe(action);
            else ctx.Post(_ => Safe(action), null);
        }

        private static void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex) { Debug.LogException(ex); }   // exception thrown by a user handler
        }

        internal void Report(Exception ex)
        {
            Post(() =>
            {
                var handler = OnError;
                if (handler != null) handler(ex);
                else Debug.LogException(ex);
            });
        }
    }
}
