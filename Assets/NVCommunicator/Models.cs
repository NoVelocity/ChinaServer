using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NoVelocity.Communicator
{
    /// <summary>Result strings used by the Communicator server protocol.</summary>
    public static class ResultCodes
    {
        public const string Ok = "ok";
        public const string NoActiveClient = "ERR_SRV__NO_ACTIVE_CLIENT_WITH_CURRENT_DESTINATION_ID";
        public const string DestinationTimeout = "ERR_SRV__DESTINATION_TIMEOUT";
        public const string DestinationDisconnected = "ERR_SRV__DESTINATION_DISCONNECTED";
        public const string NoDeliveryTask = "ERR_SRV__NO_DELIVERY_TASK_WITH_CURRENT_ID";
        public const string DestinationErrorPrefix = "ERR_DESTINATION__";
    }

    public enum SendStatus
    {
        /// <summary>The destination received the payload and answered "ok".</summary>
        Ok,
        /// <summary>No client with the requested id is connected to the server.</summary>
        NoSuchClient,
        /// <summary>The destination did not answer within the server's delivery timeout.</summary>
        Timeout,
        /// <summary>The destination disconnected before it answered.</summary>
        DestinationDisconnected,
        /// <summary>The destination answered with an error (see <see cref="SendResult.DestinationError"/>).</summary>
        DestinationError,
        /// <summary>A result string this library does not know.</summary>
        Unknown
    }

    /// <summary>Final delivery result for a message sent with <see cref="CommunicatorClient.SendAsync"/>.</summary>
    public readonly struct SendResult
    {
        public SendResult(string taskId, string raw)
        {
            TaskId = taskId;
            Raw = raw ?? string.Empty;
            DestinationError = null;

            if (Raw == ResultCodes.Ok)
                Status = SendStatus.Ok;
            else if (Raw == ResultCodes.NoActiveClient)
                Status = SendStatus.NoSuchClient;
            else if (Raw == ResultCodes.DestinationTimeout)
                Status = SendStatus.Timeout;
            else if (Raw == ResultCodes.DestinationDisconnected)
                Status = SendStatus.DestinationDisconnected;
            else if (Raw.StartsWith(ResultCodes.DestinationErrorPrefix, StringComparison.Ordinal))
            {
                Status = SendStatus.DestinationError;
                DestinationError = Raw.Substring(ResultCodes.DestinationErrorPrefix.Length);
            }
            else
                Status = SendStatus.Unknown;
        }

        /// <summary>Task id assigned by the server.</summary>
        public string TaskId { get; }

        /// <summary>The unmodified result string from the server.</summary>
        public string Raw { get; }

        public SendStatus Status { get; }

        /// <summary>Error text the destination reported (only for <see cref="SendStatus.DestinationError"/>).</summary>
        public string DestinationError { get; }

        public bool IsSuccess => Status == SendStatus.Ok;

        public override string ToString() => $"{Status} ({Raw}) [task {TaskId}]";
    }

    /// <summary>
    /// A message another client sent to us. The sender stays blocked until we call
    /// <see cref="ReplyAsync"/> / <see cref="Reply"/>, or until the server's delivery timeout expires.
    /// </summary>
    public sealed class IncomingRequest
    {
        private readonly CommunicatorClient _client;
        private int _replied;

        internal IncomingRequest(CommunicatorClient client, string id, string source, JToken payload)
        {
            _client = client;
            Id = id;
            Source = source;
            Payload = payload;
        }

        /// <summary>Server-side task id.</summary>
        public string Id { get; }

        /// <summary>Client id of the sender.</summary>
        public string Source { get; }

        /// <summary>The payload, as arbitrary JSON.</summary>
        public JToken Payload { get; }

        public bool IsReplied => Volatile.Read(ref _replied) != 0;

        public T GetPayload<T>() => Payload.ToObject<T>();

        /// <summary>Answer the sender. "ok" means success; any other text is delivered to the sender as an error.</summary>
        public Task ReplyAsync(string result = ResultCodes.Ok)
        {
            if (Interlocked.Exchange(ref _replied, 1) != 0)
                throw new InvalidOperationException("This request has already been answered.");
            return _client.ReplyAsync(Id, result);
        }

        /// <summary>Fire-and-forget version of <see cref="ReplyAsync"/>. Failures go to <see cref="CommunicatorClient.OnError"/>.</summary>
        public void Reply(string result = ResultCodes.Ok)
        {
            ReplyAsync(result).ContinueWith(
                t => _client.Report(t.Exception.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>Answer with an error. The sender receives <c>ERR_DESTINATION__{reason}</c>.</summary>
        public void Fail(string reason) => Reply(string.IsNullOrEmpty(reason) ? "error" : reason);
    }

    public class CommunicatorException : Exception
    {
        public CommunicatorException(string message) : base(message) { }
        public CommunicatorException(string message, Exception inner) : base(message, inner) { }
    }
}
