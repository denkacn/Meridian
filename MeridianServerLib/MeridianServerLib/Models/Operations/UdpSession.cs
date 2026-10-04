using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.EncodingLayer.Encoders;

namespace MeridianServerLib.Models.Operations
{
    public sealed class UdpSession : IDisposable
    {
        private const int RetryMilliseconds = 250;
        private const int MaxAttempts = 5;
        private const int MaxPending = 128;
        private const int HistorySize = 1024;
        private readonly byte[] _token;
        private readonly string _tokenString;
        private readonly Func<byte[], bool> _send;
        private readonly Func<OperationData, bool> _receive;
        private readonly MessagePackEncoder<OperationData> _encoder = new MessagePackEncoder<OperationData>();
        private readonly Dictionary<uint, PendingPacket> _pending = new Dictionary<uint, PendingPacket>();
        private readonly HashSet<uint> _receivedReliable = new HashSet<uint>();
        private readonly Queue<uint> _receivedOrder = new Queue<uint>();
        private readonly object _gate = new object();
        private readonly object _receiveGate = new object();
        private readonly Timer _retryTimer;
        private uint _nextSequence;
        private uint _lastUnreliableSequence;
        private bool _hasUnreliableSequence;
        private bool _ready;
        private bool _disposed;

        public UdpSession(byte[] token, Func<byte[], bool> send, Func<OperationData, bool> receive)
        {
            if (token == null || token.Length != 16) throw new ArgumentException("A UDP token must contain 16 bytes.", nameof(token));
            _token = (byte[])token.Clone();
            _tokenString = Convert.ToBase64String(_token);
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _receive = receive ?? throw new ArgumentNullException(nameof(receive));
            _retryTimer = new Timer(RetryPending, null, RetryMilliseconds, RetryMilliseconds);
        }

        public bool IsReady { get { lock (_gate) return _ready && !_disposed; } }

        public bool StartHandshake()
        {
            lock (_gate) if (_disposed) return false;
            return SafeSend(UdpPacket.Create(UdpPacket.Hello, _token, 0));
        }

        public Task<bool> SendAsync(OperationData operation, UdpDeliveryMode mode)
        {
            byte[] payload;
            try { payload = _encoder.Serialize(operation); }
            catch { return Task.FromResult(false); }
            if (payload.Length + UdpPacket.HeaderSize > UdpPacket.MaxDatagramSize) return Task.FromResult(false);
            PendingPacket pending = null;
            byte[] bytes;
            lock (_gate)
            {
                if (!_ready || _disposed || (mode == UdpDeliveryMode.Reliable && _pending.Count >= MaxPending))
                    return Task.FromResult(false);
                var sequence = ++_nextSequence;
                bytes = UdpPacket.Create(mode == UdpDeliveryMode.Reliable ? UdpPacket.ReliableData : UdpPacket.Data,
                    _token, sequence, payload);
                if (mode == UdpDeliveryMode.Reliable)
                {
                    pending = new PendingPacket(bytes);
                    _pending.Add(sequence, pending);
                }
            }
            var sent = SafeSend(bytes);
            return pending == null ? Task.FromResult(sent) : pending.Completion.Task;
        }

        public void Receive(UdpPacket packet)
        {
            if (packet == null || packet.Token != _tokenString) return;
            if (packet.Kind == UdpPacket.Hello)
            {
                lock (_gate) { if (_disposed) return; _ready = true; }
                SafeSend(UdpPacket.Create(UdpPacket.HelloAck, _token, 0));
                return;
            }
            if (packet.Kind == UdpPacket.HelloAck)
            {
                lock (_gate) if (!_disposed) _ready = true;
                return;
            }
            if (packet.Kind == UdpPacket.Ack)
            {
                PendingPacket completed;
                lock (_gate)
                {
                    if (!_pending.TryGetValue(packet.Sequence, out completed)) return;
                    _pending.Remove(packet.Sequence);
                }
                completed.Completion.TrySetResult(true);
                return;
            }
            lock (_receiveGate)
            {
                bool duplicate;
                lock (_gate)
                {
                    if (!_ready || _disposed) return;
                    duplicate = packet.Kind == UdpPacket.ReliableData && _receivedReliable.Contains(packet.Sequence);
                    if (packet.Kind == UdpPacket.Data && _hasUnreliableSequence && packet.Sequence <= _lastUnreliableSequence) return;
                }
                if (duplicate)
                {
                    SafeSend(UdpPacket.Create(UdpPacket.Ack, _token, packet.Sequence));
                    return;
                }
                OperationData operation;
                try { operation = _encoder.Deserialize(packet.Payload); }
                catch { return; }
                if (!_receive(operation)) return;
                lock (_gate)
                {
                    if (_disposed) return;
                    if (packet.Kind == UdpPacket.Data)
                    {
                        _lastUnreliableSequence = packet.Sequence;
                        _hasUnreliableSequence = true;
                    }
                    else
                    {
                        _receivedReliable.Add(packet.Sequence);
                        _receivedOrder.Enqueue(packet.Sequence);
                        if (_receivedOrder.Count > HistorySize) _receivedReliable.Remove(_receivedOrder.Dequeue());
                    }
                }
                if (packet.Kind == UdpPacket.ReliableData)
                    SafeSend(UdpPacket.Create(UdpPacket.Ack, _token, packet.Sequence));
            }
        }

        private void RetryPending(object state)
        {
            var resend = new List<byte[]>();
            var failed = new List<PendingPacket>();
            lock (_gate)
            {
                if (_disposed) return;
                var now = DateTime.UtcNow;
                var expired = new List<uint>();
                foreach (var entry in _pending)
                {
                    var pending = entry.Value;
                    if (now < pending.NextAttempt) continue;
                    if (pending.Attempts >= MaxAttempts) { expired.Add(entry.Key); failed.Add(pending); }
                    else
                    {
                        pending.Attempts++;
                        pending.NextAttempt = now.AddMilliseconds(RetryMilliseconds);
                        resend.Add(pending.Bytes);
                    }
                }
                foreach (var sequence in expired) _pending.Remove(sequence);
            }
            foreach (var bytes in resend) SafeSend(bytes);
            foreach (var pending in failed) pending.Completion.TrySetResult(false);
        }

        private bool SafeSend(byte[] bytes)
        {
            try { return _send(bytes); }
            catch (ObjectDisposedException) { return false; }
            catch (System.Net.Sockets.SocketException) { return false; }
        }

        public void Dispose()
        {
            List<PendingPacket> pending;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _ready = false;
                pending = new List<PendingPacket>(_pending.Values);
                _pending.Clear();
            }
            _retryTimer.Dispose();
            foreach (var item in pending) item.Completion.TrySetResult(false);
        }

        private sealed class PendingPacket
        {
            public byte[] Bytes { get; }
            public TaskCompletionSource<bool> Completion { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTime NextAttempt { get; set; } = DateTime.UtcNow.AddMilliseconds(RetryMilliseconds);
            public int Attempts { get; set; } = 1;
            public PendingPacket(byte[] bytes) { Bytes = bytes; }
        }
    }
}
