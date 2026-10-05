using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace NoVelocity.Communicator
{
    /// <summary>
    /// Drop-in component around <see cref="CommunicatorClient"/>. All events arrive on the main thread.
    /// </summary>
    [AddComponentMenu("NoVelocity/Communicator Client")]
    public class CommunicatorBehaviour : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 8765;
        [SerializeField] private bool useTls;

        [Header("Identity")]
        [Tooltip("Id other clients use to address this one. Leave empty to generate a random id. Must be unique on the server.")]
        [SerializeField] private string clientId = "";

        [Header("Behaviour")]
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private float reconnectInitialDelay = 1f;
        [SerializeField] private float reconnectMaxDelay = 15f;

        [Header("Events")]
        public UnityEvent onConnected = new UnityEvent();
        public UnityEvent onDisconnected = new UnityEvent();

        /// <summary>Another client sent us a message. Call <c>request.Reply()</c> (or <c>Fail</c>) for every one.</summary>
        public event Action<IncomingRequest> RequestReceived;
        /// <summary>Delivery result of a message we sent.</summary>
        public event Action<SendResult> SendCompleted;
        /// <summary>The server confirmed one of our replies (task id, result).</summary>
        public event Action<string, string> RequestAcknowledged;

        /// <summary>The underlying client; null until <see cref="Connect"/> was called.</summary>
        public CommunicatorClient Client { get; private set; }

        public bool IsConnected => Client != null && Client.IsConnected;
        public string ClientId => Client != null ? Client.ClientId : clientId;

        private void Start()
        {
            if (connectOnStart) Connect();
        }

        private void OnDestroy()
        {
            Client?.Dispose();
            Client = null;
        }

        /// <summary>Connect (retrying with back-off if <c>autoReconnect</c> is on). Does not throw.</summary>
        public void Connect()
        {
            _ = ConnectWithRetryAsync();
        }

        public async Task DisconnectAsync()
        {
            var c = Client;
            if (c == null) return;
            await c.DisconnectAsync();
            c.Dispose();
            if (Client == c) Client = null;
        }

        /// <summary>Sends a message and waits for the delivery result. See <see cref="CommunicatorClient.SendAsync"/>.</summary>
        public Task<SendResult> SendAsync(string destination, object payload)
        {
            if (Client == null) throw new InvalidOperationException("Not connected. Call Connect() first.");
            return Client.SendAsync(destination, payload);
        }

        /// <summary>Fire-and-forget send; the outcome arrives via <see cref="SendCompleted"/>.</summary>
        public void Send(string destination, object payload)
        {
            SendQuietly(destination, payload);
        }

        private async void SendQuietly(string destination, object payload)
        {
            try { await SendAsync(destination, payload); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private async Task ConnectWithRetryAsync()
        {
            if (Client != null) return;

            var id = string.IsNullOrWhiteSpace(clientId)
                ? "unity-" + Guid.NewGuid().ToString("N").Substring(0, 8)
                : clientId.Trim();

            var c = new CommunicatorClient(host, port, id, useTls)
            {
                AutoReconnect = autoReconnect,
                ReconnectInitialDelay = TimeSpan.FromSeconds(Mathf.Max(0.1f, reconnectInitialDelay)),
                ReconnectMaxDelay = TimeSpan.FromSeconds(Mathf.Max(reconnectInitialDelay, reconnectMaxDelay)),
            };
            c.OnConnected += () => onConnected.Invoke();
            c.OnDisconnected += _ => onDisconnected.Invoke();
            c.OnRequest += r => RequestReceived?.Invoke(r);
            c.OnSendResult += r => SendCompleted?.Invoke(r);
            c.OnAck += (taskId, result) => RequestAcknowledged?.Invoke(taskId, result);
            Client = c;

            var delay = c.ReconnectInitialDelay;
            while (true)
            {
                try
                {
                    await c.ConnectAsync();
                    return;
                }
                catch (Exception ex)
                {
                    if (this == null || Client != c) return;   // destroyed or replaced meanwhile

                    if (!autoReconnect)
                    {
                        Debug.LogException(ex);
                        c.Dispose();
                        Client = null;
                        return;
                    }

                    Debug.LogWarning($"[Communicator] Connection to {c.Uri} failed ({ex.Message}). Retrying in {delay.TotalSeconds:0.#} s.");
                    await Task.Delay(delay);
                    if (this == null || Client != c) return;

                    var next = TimeSpan.FromTicks(delay.Ticks * 2);
                    delay = next > c.ReconnectMaxDelay ? c.ReconnectMaxDelay : next;
                }
            }
        }
    }
}
