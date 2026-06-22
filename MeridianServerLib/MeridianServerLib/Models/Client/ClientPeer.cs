using System;
using MeridianServerLib.EncodingLayer.Componators;
using MeridianServerLib.EncodingLayer.Encoders;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Client;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Models.Client
{
    public sealed class ClientPeer : IClientPeer
    {
        public event Action ClientConnected;
        public event Action ClientDisconnected;
        public event Action<System.Net.Sockets.SocketError> ClientError;
        public event Action<NetworkClientConnectionStatus> ClientConnectionStatusChanged;

        public bool IsConnected => _networkClient.IsConnected;
        
        private readonly IBinaryEncoder<OperationData> _encoder;
        private readonly NetworkClient _networkClient;
        private readonly ILogger _logger;
        
        private IOperationsReceiver _receiver;
        private readonly ISocketMessageComponator _socketMessageComponator;
        private int _messageId = 0;
        private bool _isDiscarded;

        public ClientPeer(string address, int port, ILogger logger = null)
        {
            _encoder = new MessagePackEncoder<OperationData>();
            _networkClient = new NetworkClient(address, port, OnReceived, logger);
            _logger = logger;

            _socketMessageComponator = new HeaderSocketMessageComponatorV3(_logger);

            _networkClient.NetworkClientConnected += OnNetworkClientConnected;
            _networkClient.NetworkClientDisconnected += OnNetworkClientDisconnected;
            _networkClient.NetworkClientError += OnNetworkClientError;
            _networkClient.ConnectionStatusChanged += OnConnectionStatusChanged;
            _socketMessageComponator.OnReceivedMessage += OnSocketMessageComponatorReceivedMessage;
        }

        public bool Connect()
        {
            if (_isDiscarded)
            {
                return false;
            }

            var isConnect = _networkClient.ConnectAsync();
            if (isConnect)
            {
                _networkClient.ReceiveAsync();
            }

            return isConnect;
        }

        public bool Disconnect()
        {
            if (_isDiscarded)
            {
                return false;
            }

            return _networkClient.DisconnectAsync();
        }

        public bool Send(OperationData operation)
        {
            if (_isDiscarded)
            {
                return false;
            }

            try
            {
                _messageId++;

                var sendBytesWithHeader = _socketMessageComponator.CreateMessageWithHeader(
                    _messageId,
                    writer => _encoder.Serialize(writer, operation, _logger));

                return _networkClient.SendAsync(sendBytesWithHeader.Span);
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError("[ClientPeer MeridianEncoderException] Error Send", ex);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ClientPeer] Try Send Error", ex);
                return false;
            }
        }

        public void SetOperationsReceiver(IOperationsReceiver receiver)
        {
            _receiver = receiver;
        }

        public void Discard()
        {
            if (_isDiscarded) return;

            _networkClient.NetworkClientConnected -= OnNetworkClientConnected;
            _networkClient.NetworkClientDisconnected -= OnNetworkClientDisconnected;
            _networkClient.NetworkClientError -= OnNetworkClientError;
            _networkClient.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _socketMessageComponator.OnReceivedMessage -= OnSocketMessageComponatorReceivedMessage;

            if (_networkClient.IsConnected)
            {
                _networkClient.DisconnectAsync();
            }

            _networkClient.Dispose();
            _socketMessageComponator.Dispose();

            _receiver = null;
            _isDiscarded = true;
        }   

        private void OnReceived(byte[] buffer, long offset, long size)
        {
            _socketMessageComponator.Received(buffer, (int)offset, (int)size);    
        }

        private void OnSocketMessageComponatorReceivedMessage(byte[] message)
        {
            try
            {
	            var operationData = _encoder.Deserialize(message, _logger);
	            _receiver?.OnOperationReceived(operationData);
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError("[ClientPeer MeridianEncoderException] Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ClientPeer Exception] On Received", ex);
            }
        }

        private void OnConnectionStatusChanged(NetworkClientConnectionStatus status) => ClientConnectionStatusChanged?.Invoke(status);

        private void OnNetworkClientConnected() => ClientConnected?.Invoke();

        private void OnNetworkClientDisconnected() => ClientDisconnected?.Invoke();

        private void OnNetworkClientError(System.Net.Sockets.SocketError error) => ClientError?.Invoke(error);
    }
}
