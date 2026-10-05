using System;
using System.Buffers;
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
        private readonly Func<byte[], int, bool> _send;
        private readonly Func<OperationData, bool> _receive;
        private readonly MessagePackEncoder<OperationData> _encoder = new MessagePackEncoder<OperationData>();
        private readonly Dictionary<uint, PendingPacket> _pending = new Dictionary<uint, PendingPacket>();
        private readonly HashSet<uint> _receivedReliable = new HashSet<uint>();
        private readonly Queue<uint> _receivedOrder = new Queue<uint>();
        private readonly PendingPacket[] _retryBuffer = new PendingPacket[MaxPending];
        private readonly PendingPacket[] _failedBuffer = new PendingPacket[MaxPending];
        private readonly uint[] _expiredKeys = new uint[MaxPending];
        private readonly object _gate = new object();
        private readonly object _receiveGate = new object();
        private readonly Timer _retryTimer;
        private uint _nextSequence;
        private uint _lastUnreliableSequence;
        private bool _hasUnreliableSequence;
        private bool _ready;
        private bool _disposed;
        private int _retrying;

        public UdpSession(byte[] token, Func<byte[], int, bool> send, Func<OperationData, bool> receive)
        {
            if (token == null || token.Length != 16)
                throw new ArgumentException("A UDP token must contain 16 bytes.", nameof(token));
            _token = (byte[])token.Clone();
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _receive = receive ?? throw new ArgumentNullException(nameof(receive));
            _retryTimer = new Timer(RetryPending, null, RetryMilliseconds, RetryMilliseconds);
        }

        public bool IsReady { get { lock (_gate) return _ready && !_disposed; } }

        public bool StartHandshake()
        {
            lock (_gate) if (_disposed) return false;
            var bytes = UdpPacket.Create(UdpPacket.Hello, _token, 0);
            return SafeSend(bytes, bytes.Length);
        }

        public Task<bool> SendAsync(OperationData operation, UdpDeliveryMode mode)
        {
            lock (_gate)
            {
                if (_disposed || !_ready || (mode == UdpDeliveryMode.Reliable && _pending.Count >= MaxPending))
                    return Task.FromResult(false);
            }

            var writer = new DatagramWriter();
            try
            {
                _encoder.Serialize(writer, operation);
            }
            catch
            {
                return Task.FromResult(false);
            }

            if (writer.WrittenCount == UdpPacket.HeaderSize) return Task.FromResult(false);

            PendingPacket pending = null;
            lock (_gate)
            {
                if (_disposed || !_ready || (mode == UdpDeliveryMode.Reliable && _pending.Count >= MaxPending))
                    return Task.FromResult(false);

                var sequence = ++_nextSequence;
                UdpPacket.WriteHeader(writer.Bytes.AsSpan(0, UdpPacket.HeaderSize),
                    mode == UdpDeliveryMode.Reliable ? UdpPacket.ReliableData : UdpPacket.Data,
                    _token, sequence);

                if (mode == UdpDeliveryMode.Reliable)
                {
                    pending = new PendingPacket(writer.Bytes, writer.WrittenCount);
                    _pending.Add(sequence, pending);
                }
            }

            var sent = SafeSend(writer.Bytes, writer.WrittenCount);
            return pending == null ? Task.FromResult(sent) : pending.Completion.Task;
        }

        public void Receive(UdpPacket packet)
        {
            if (!packet.Token.SequenceEqual(_token)) return;

            if (packet.Kind == UdpPacket.Hello)
            {
                lock (_gate) { if (_disposed) return; _ready = true; }
                var helloAck = UdpPacket.Create(UdpPacket.HelloAck, _token, 0);
                SafeSend(helloAck, helloAck.Length);
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
                    if (packet.Kind == UdpPacket.Data && _hasUnreliableSequence &&
                        packet.Sequence <= _lastUnreliableSequence) return;
                }

                if (duplicate)
                {
                    var duplicateAck = UdpPacket.Create(UdpPacket.Ack, _token, packet.Sequence);
                    SafeSend(duplicateAck, duplicateAck.Length);
                    return;
                }

                OperationData operation;
                try { operation = _encoder.Deserialize(packet.Payload); }
                catch { return; }

                // Deserialization creates owned operation data before it enters an asynchronous queue.
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
                        if (_receivedOrder.Count > HistorySize)
                            _receivedReliable.Remove(_receivedOrder.Dequeue());
                    }
                }

                if (packet.Kind == UdpPacket.ReliableData)
                {
                    var ack = UdpPacket.Create(UdpPacket.Ack, _token, packet.Sequence);
                    SafeSend(ack, ack.Length);
                }
            }
        }

        private void RetryPending(object state)
        {
            if (Interlocked.CompareExchange(ref _retrying, 1, 0) != 0) return;

            var resendCount = 0;
            var failedCount = 0;
            try
            {
                lock (_gate)
                {
                    if (_disposed || _pending.Count == 0) return;

                    var now = DateTime.UtcNow;
                    foreach (var entry in _pending)
                    {
                        var pending = entry.Value;
                        if (now < pending.NextAttempt) continue;
                        if (pending.Attempts >= MaxAttempts)
                        {
                            _expiredKeys[failedCount] = entry.Key;
                            _failedBuffer[failedCount++] = pending;
                        }
                        else
                        {
                            pending.Attempts++;
                            pending.NextAttempt = now.AddMilliseconds(RetryMilliseconds);
                            _retryBuffer[resendCount++] = pending;
                        }
                    }
                    for (var i = 0; i < failedCount; i++) _pending.Remove(_expiredKeys[i]);
                }

                for (var i = 0; i < resendCount; i++)
                {
                    var pending = _retryBuffer[i];
                    if (!pending.Completion.Task.IsCompleted)
                        SafeSend(pending.Bytes, pending.Length);
                }
                for (var i = 0; i < failedCount; i++)
                    _failedBuffer[i].Completion.TrySetResult(false);
            }
            finally
            {
                Array.Clear(_retryBuffer, 0, resendCount);
                Array.Clear(_failedBuffer, 0, failedCount);
                Volatile.Write(ref _retrying, 0);
            }
        }

        private bool SafeSend(byte[] bytes, int length)
        {
            lock (_gate) if (_disposed) return false;
            try { return _send(bytes, length); }
            catch (ObjectDisposedException) { return false; }
            catch (System.Net.Sockets.SocketException) { return false; }
        }

        public void Dispose()
        {
            PendingPacket[] pending;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _ready = false;
                pending = new PendingPacket[_pending.Count];
                _pending.Values.CopyTo(pending, 0);
                _pending.Clear();
            }

            _retryTimer.Dispose();
            foreach (var item in pending) item.Completion.TrySetResult(false);
        }

        private sealed class PendingPacket
        {
            public byte[] Bytes { get; }
            public int Length { get; }
            public TaskCompletionSource<bool> Completion { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTime NextAttempt { get; set; } = DateTime.UtcNow.AddMilliseconds(RetryMilliseconds);
            public int Attempts { get; set; } = 1;

            public PendingPacket(byte[] bytes, int length)
            {
                Bytes = bytes;
                Length = length;
            }
        }

        private sealed class DatagramWriter : IBufferWriter<byte>
        {
            public byte[] Bytes { get; } = new byte[UdpPacket.MaxDatagramSize];
            public int WrittenCount { get; private set; } = UdpPacket.HeaderSize;

            public void Advance(int count)
            {
                if (count < 0 || count > Bytes.Length - WrittenCount)
                    throw new ArgumentOutOfRangeException(nameof(count));
                WrittenCount += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                if (sizeHint < 0 || sizeHint > Bytes.Length - WrittenCount)
                    throw new InvalidOperationException("UDP datagram exceeds the size limit.");
                return Bytes.AsMemory(WrittenCount);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                if (sizeHint < 0 || sizeHint > Bytes.Length - WrittenCount)
                    throw new InvalidOperationException("UDP datagram exceeds the size limit.");
                return Bytes.AsSpan(WrittenCount);
            }
        }
    }
}
