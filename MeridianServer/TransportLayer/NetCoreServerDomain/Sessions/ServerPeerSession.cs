using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MeridianServerLib.EncodingLayer.Componators;
using MeridianServerLib.EncodingLayer.Encoders;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;
using MeridianServer.TransportLayer.NetCoreServerDomain;
using NetCoreServer;

namespace MeridianServer.TransportLayer.NetCoreServerDomain.Sessions
{
    public class ServerPeerSession : TcpSession, IServerPeerSession, IUdpServerPeerSession
    {
        public event EventHandler ConnectedEventHandler;
        public event EventHandler DisconnectedEventHandler;
        public event EventHandler<OperationData> ReceivedEventHandler;
        public event AsyncOperationReceivedEventHandler ReceivedAsyncEventHandler;
        public event EventHandler<string> ErrorEventHandler;

        public string SessionId => Id.ToString();
        
        private readonly IBinaryEncoder<OperationData> _encoder;

        private readonly string _id;
        private bool _isBusy = false;
        private int _messageId = 0;

        private readonly object _sendLock = new object();
        private readonly Queue<OperationData> _operations;

        private readonly ISocketMessageComponator _socketMessageComponator;
        private bool _isSocketMessageComponatorDisposed;
        private readonly CancellationTokenSource _disposeCancellationTokenSource;
        private readonly Channel<IncomingOperation> _incomingOperations;
        private readonly Task _incomingProcessingTask;
        private int _pendingUdpOperations;
        private readonly UdpServerGateway _udpGateway;
        private readonly object _udpLock = new object();
        private readonly HashSet<byte> _allowedUdpOperations = new HashSet<byte>();
        private UdpSession _udpSession;
        private IPEndPoint _udpEndpoint;
        private byte[] _udpToken;

        public bool IsUdpReady => _udpSession?.IsReady == true && _udpEndpoint != null;

        public ServerPeerSession(string id, TcpServer server, ILogger logger)
            : this(id, server, (server as ServerBase)?.UdpGateway, logger)
        {
        }

        internal ServerPeerSession(string id, TcpServer server, UdpServerGateway udpGateway, ILogger logger) : base(server)
        {
            SetLogger(logger);

            _id = id;
			_udpGateway = udpGateway;
			_operations = new Queue<OperationData>();
            _encoder = new MessagePackEncoder<OperationData>();
            _disposeCancellationTokenSource = new CancellationTokenSource();
            _incomingOperations = Channel.CreateUnbounded<IncomingOperation>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            _socketMessageComponator = new HeaderSocketMessageComponatorV3(logger);

            _socketMessageComponator.OnReceivedMessage += OnSocketMessageComponatorReceivedMessage;
            _incomingProcessingTask = ProcessIncomingOperationsAsync(_disposeCancellationTokenSource.Token);
        }

        public void Send(OperationData operationData)
        {
            if (EnqueueAndTryBeginSend(operationData, out var nextOperationData, out var messageId))
            {
                DecodeAndSend(nextOperationData, messageId);
            }
        }

        public void AllowUdpOperation(byte operationCode)
        {
            if (operationCode == 255) throw new ArgumentOutOfRangeException(nameof(operationCode));
            lock (_udpLock) _allowedUdpOperations.Add(operationCode);
        }

        public Task<bool> SendUdpAsync(OperationData operation, UdpDeliveryMode mode)
        {
            if (operation.OperationCode == 255) return Task.FromResult(false);
            var session = _udpSession;
            if (session == null || !IsUdpReady)
            {
                Send(operation);
                return Task.FromResult(true);
            }
            return session.SendAsync(operation, mode);
        }

        public Task<bool> SendUdpOnlyAsync(OperationData operation, UdpDeliveryMode mode)
        {
            if (operation.OperationCode == 255) return Task.FromResult(false);
            var session = _udpSession;
            return session == null ? Task.FromResult(false) : session.SendAsync(operation, mode);
        }

        internal void ReceiveUdp(IPEndPoint endpoint, UdpPacket packet)
        {
            UdpSession session;
            lock (_udpLock)
            {
                session = _udpSession;
                if (session == null) return;
                if (packet.Kind == UdpPacket.Hello)
                    _udpEndpoint = endpoint;
                else if (_udpEndpoint == null || !_udpEndpoint.Equals(endpoint))
                    return;
            }
            session.Receive(packet);
        }

        private bool TryEnqueueUdpOperation(OperationData operation)
        {
            lock (_udpLock)
                if (!_allowedUdpOperations.Contains(operation.OperationCode)) return false;
            if (Interlocked.Increment(ref _pendingUdpOperations) > 128)
            {
                Interlocked.Decrement(ref _pendingUdpOperations);
                return false;
            }
            if (_incomingOperations.Writer.TryWrite(new IncomingOperation(operation, true))) return true;
            Interlocked.Decrement(ref _pendingUdpOperations);
            return false;
        }

        private bool TryHandleUdpEnable(OperationData operation)
        {
            if (operation.OperationCode != 255 || operation.Parameters == null ||
                !operation.Parameters.TryGetValue(0, out var marker) ||
                marker as string != "MERIDIAN_UDP_ENABLE_V1") return false;

            if (_udpGateway == null || !_udpGateway.IsStarted) return true;
            lock (_udpLock)
            {
                if (_udpSession == null)
                {
                    var token = _udpGateway.Register(this);
                    _udpToken = token;
                    _udpSession = new UdpSession(token, (bytes, length) => _udpGateway.SendTo(_udpEndpoint, bytes, length),
                        TryEnqueueUdpOperation);
                }
                Send(new OperationData(255, new Dictionary<byte, object>
                {
                    [0] = "MERIDIAN_UDP_OFFER_V1",
                    [1] = Convert.ToBase64String(_udpToken),
                    [2] = _udpGateway.Port
                }));
            }
            return true;
        }

        public void Update()
        {
	        if (!IsSocketConnected()) Disconnect();
        }

        protected override void OnConnected(object userToken)
        {
            _logger?.Log($"[NetworkSession] ({_id}) Session with Id {Id} connected! UserToken: " + userToken);

            try
            {
                ConnectedEventHandler?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Connected", ex);
            }
        }

        protected override void OnDisconnected()
        {
            _logger?.Log($"[NetworkSession] ({_id}) Session with Id {Id} disconnected!");

            DisposeSocketMessageComponator();
            
            try
            {
                DisconnectedEventHandler?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Disconnected", ex);
            }
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                //_logger?.Log($"[ServerPeerSession MeridianEncoderException] ({_id}) OnReceived: {buffer.Length} size: {size}");

                _socketMessageComponator.Received(buffer, (int)offset, (int)size);
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError($"[ServerPeerSession MeridianEncoderException] ({_id}) Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Received", ex);
            }
        }

        private void OnSocketMessageComponatorReceivedMessage(ReadOnlyMemory<byte> message)
        {        
            try
            {
                var operationData = _encoder.Deserialize(message, _logger);
                if (TryHandleUdpEnable(operationData)) return;
                if (!_incomingOperations.Writer.TryWrite(new IncomingOperation(operationData, false)))
                {
                    _logger?.Log($"[ServerPeerSession] ({_id}) Incoming operation queue is closed");
                }
			}
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError($"[ServerPeerSession MeridianEncoderException] ({_id}) Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Received", ex);
            }       
        }

        protected override void OnEmpty()
        {
            MarkSendCompletedAndTrySendNext();
        }

        protected override void OnError(SocketError error)
        {
            _logger?.Log($"[NetworkSession] ({_id}) OnError Session caught an error with code {error}");

            ErrorEventHandler?.Invoke(this, error.ToString());
        }

        protected override void Dispose(bool disposingManagedResources)
        {
            if (disposingManagedResources)
            {
                DisposeSocketMessageComponator();
            }

            base.Dispose(disposingManagedResources);
        }

        private void DisposeSocketMessageComponator()
        {
            lock (_udpLock)
            {
                _udpGateway?.Unregister(_udpToken);
                _udpSession?.Dispose();
                _udpSession = null;
                _udpEndpoint = null;
                _udpToken = null;
            }
            lock (_sendLock)
            {
                if (_isSocketMessageComponatorDisposed)
                {
                    return;
                }

                _operations.Clear();
                _isBusy = false;
                _isSocketMessageComponatorDisposed = true;
            }

            _disposeCancellationTokenSource.Cancel();
            _incomingOperations.Writer.TryComplete();
            _socketMessageComponator.OnReceivedMessage -= OnSocketMessageComponatorReceivedMessage;
            _socketMessageComponator.Dispose();
        }

        private bool EnqueueAndTryBeginSend(OperationData operationData, out OperationData nextOperationData, out int messageId)
        {
            lock (_sendLock)
            {
                if (_isSocketMessageComponatorDisposed)
                {
                    nextOperationData = default;
                    messageId = 0;
                    return false;
                }

                _operations.Enqueue(operationData);

                return TryBeginSendLocked(out nextOperationData, out messageId);
            }
        }

        private bool TryBeginSendLocked(out OperationData operationData, out int messageId)
        {
            operationData = default;
            messageId = 0;

            if (_isBusy || _operations.Count == 0)
            {
                return false;
            }

            _isBusy = true;
            _messageId++;

            operationData = _operations.Dequeue();
            messageId = _messageId;

            return true;
        }

        private void MarkSendCompletedAndTrySendNext()
        {
            OperationData nextOperationData;
            int messageId;

            lock (_sendLock)
            {
                if (_isSocketMessageComponatorDisposed)
                {
                    return;
                }

                _isBusy = false;

                if (!TryBeginSendLocked(out nextOperationData, out messageId))
                {
                    return;
                }
            }

            DecodeAndSend(nextOperationData, messageId);
        }

        private void DecodeAndSend(OperationData operationData, int messageId)
        {
            try
            {
                var sendBytesWithHeader = _socketMessageComponator.CreateMessageWithHeader(
                    messageId,
                    writer => _encoder.Serialize(writer, operationData, _logger));

                //_logger.Log("[ServerPeerSession MeridianEncoder] Send " + operationData.OperationCode + " Length: " + sendBytesWithHeader.Length);

                if (!SendAsync(sendBytesWithHeader.Span))
                {
                    throw new InvalidOperationException("SendAsync returned false.");
                }
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError($"[ServerPeerSession MeridianEncoderException] ({_id}) Error Send", ex);
                MarkSendCompletedAndTrySendNext();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession Exception] ({_id}) Error Send", ex);
                MarkSendCompletedAndTrySendNext();
            }
        }

        private async Task ProcessIncomingOperationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var incoming in _incomingOperations.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (incoming.IsUdp) Interlocked.Decrement(ref _pendingUdpOperations);
                    await ProcessIncomingOperationAsync(incoming.Operation, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Incoming operation worker error", ex);
            }
        }

        private async Task ProcessIncomingOperationAsync(OperationData operationData, CancellationToken cancellationToken)
        {
            try
            {
                ReceivedEventHandler?.Invoke(this, operationData);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Sync Received", ex);
            }

            await InvokeReceivedAsyncEventHandlers(operationData, cancellationToken).ConfigureAwait(false);
        }

        private async Task InvokeReceivedAsyncEventHandlers(OperationData operationData, CancellationToken cancellationToken)
        {
            var handlers = ReceivedAsyncEventHandler;
            if (handlers == null)
            {
                return;
            }

            foreach (AsyncOperationReceivedEventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    await handler(this, operationData, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Async Received", ex);
                }
            }
        }

        private readonly struct IncomingOperation
        {
            public OperationData Operation { get; }
            public bool IsUdp { get; }

            public IncomingOperation(OperationData operation, bool isUdp)
            {
                Operation = operation;
                IsUdp = isUdp;
            }
        }
    }
}
