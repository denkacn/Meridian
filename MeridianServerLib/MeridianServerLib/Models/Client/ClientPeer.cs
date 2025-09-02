using System;
using MeridianServerLib.EncodingLayer.Componators;
using MeridianServerLib.EncodingLayer.Convertors;
using MeridianServerLib.EncodingLayer.DataObjects;
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
        
        private readonly IBinaryEncoder<RpcData> _encoder;
        private readonly RpcDataConvertor _convertor;
        private readonly NetworkClient _networkClient;
        private readonly ILogger _logger;
        
        private IOperationsReceiver _receiver;
        private readonly ISocketMessageComponator _socketMessageComponator;
        private int _messageId = 0;

        public ClientPeer(string address, int port, ILogger logger = null)
        {
            _encoder = new MessagePackEncoder<RpcData>();
            _convertor = new RpcDataConvertor();
            _networkClient = new NetworkClient(address, port, OnReceived, logger);
            _logger = logger;

            _socketMessageComponator = new HeaderSocketMessageComponatorV3(_logger);

            _networkClient.ConnectionStatusChanged += OnConnectionStatusChanged;
            _socketMessageComponator.OnReceivedMessage += OnSocketMessageComponatorReceivedMessage;
        }

        public bool Connect()
        {
            var isConnect = _networkClient.ConnectAsync();
            if (isConnect)
            {
                _networkClient.ReceiveAsync();
            }

            return isConnect;
        }

        public bool Disconnect()
        {
            return _networkClient.DisconnectAsync();
        }

        public bool Send(OperationData operation)
        {
            try
            {
                _messageId++;

                var packData = _convertor.To(operation);
                var sendBytes = _encoder.Decode(packData);
                var sendBytesWithHeader = _socketMessageComponator.CreateMessageWithHeader(_messageId, sendBytes);

                return _networkClient.SendAsync(sendBytesWithHeader);
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
            if (_networkClient == null) return;

            _networkClient.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }   

        private void OnReceived(byte[] buffer, long offset, long size)
        {
            _socketMessageComponator.Received(buffer, (int)offset, (int)size);    
        }

        private void OnSocketMessageComponatorReceivedMessage(byte[] message)
        {
            try
            {
	            var dataPack = _encoder.Encode(message, _logger);
	            var operationData = _convertor.From(dataPack);
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
    }
}