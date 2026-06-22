using System;
using System.Collections.Generic;
using System.Net.Sockets;
using MeridianServerLib.EncodingLayer.Componators;
using MeridianServerLib.EncodingLayer.Encoders;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;
using NetCoreServer;

namespace MeridianServer.TransportLayer.NetCoreServerDomain.Sessions
{
    public class ServerPeerSession : TcpSession, IServerPeerSession
    {
        public event EventHandler ConnectedEventHandler;
        public event EventHandler DisconnectedEventHandler;
        public event EventHandler<OperationData> ReceivedEventHandler;
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

        public ServerPeerSession(string id, TcpServer server, ILogger logger) : base(server)
        {
            SetLogger(logger);

            _id = id;
			_operations = new Queue<OperationData>();
            _encoder = new MessagePackEncoder<OperationData>();

            _socketMessageComponator = new HeaderSocketMessageComponatorV3(logger);

            _socketMessageComponator.OnReceivedMessage += OnSocketMessageComponatorReceivedMessage;
        }

        public void Send(OperationData operationData)
        {
            if (EnqueueAndTryBeginSend(operationData, out var nextOperationData, out var messageId))
            {
                DecodeAndSend(nextOperationData, messageId);
            }
        }

        public void Update()
        {
	        if (!IsSocketConnected()) Disconnect();
        }

        protected override void OnConnected(object userToken)
        {
            _logger?.Log($"[NetworkSession] ({_id}) Session with Id {Id} connected! UserToken: " + userToken);

            ConnectedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDisconnected()
        {
            _logger?.Log($"[NetworkSession] ({_id}) Session with Id {Id} disconnected!");

            DisposeSocketMessageComponator();
            
            DisconnectedEventHandler?.Invoke(this, EventArgs.Empty);
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

        private void OnSocketMessageComponatorReceivedMessage(byte[] message)
        {        
            try
            {
                var operationData = _encoder.Deserialize(message, _logger);
				ReceivedEvent(operationData);
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

        private void ReceivedEvent(OperationData operationData)
        {
	        ReceivedEventHandler?.Invoke(this, operationData);
	        //await Task.Run(() => { ReceivedEventHandler?.Invoke(this, operationData); });  
        }
    }
}
