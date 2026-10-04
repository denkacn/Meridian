using System;
using MeridianServerLib.EncodingLayer.Componators;
using MeridianServerLib.EncodingLayer.Encoders;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Client;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace MeridianServerLib.Models.Client
{
    public sealed class ClientPeer : IClientPeer, IUdpClientPeer
    {
        public event Action ClientConnected;
        public event Action ClientDisconnected;
        public event Action<System.Net.Sockets.SocketError> ClientError;
        public event Action<NetworkClientConnectionStatus> ClientConnectionStatusChanged;

        public bool IsConnected => _networkClient.IsConnected;
        
        private readonly IBinaryEncoder<OperationData> _encoder;
        private readonly NetworkClient _networkClient;
        private readonly ILogger _logger;
        private readonly object _sendLock = new object();
        
        private IOperationsReceiver _receiver;
        private readonly ISocketMessageComponator _socketMessageComponator;
        private int _messageId = 0;
        private bool _isDiscarded;
        private MeridianUdpClient _udpClient;
        private UdpSession _udpSession;
        private readonly object _udpLock = new object();

        public bool IsUdpReady => _udpSession?.IsReady == true;

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

            DisposeUdp();
            return _networkClient.DisconnectAsync();
        }

        public bool EnableUdp()
        {
            if (!IsConnected || _isDiscarded) return false;
            return Send(new OperationData(255, new Dictionary<byte, object>
            {
                [0] = "MERIDIAN_UDP_ENABLE_V1"
            }));
        }

        public Task<bool> SendUdpAsync(OperationData operation, UdpDeliveryMode mode)
        {
            if (!IsConnected || _isDiscarded || operation.OperationCode == 255) return Task.FromResult(false);
            var session = _udpSession;
            if (session == null || !session.IsReady) return Task.FromResult(Send(operation));
            return session.SendAsync(operation, mode);
        }

        public bool Send(OperationData operation)
        {
            if (_isDiscarded)
            {
                return false;
            }

            try
            {
                lock (_sendLock)
                {
                    _messageId++;

                    var sendBytesWithHeader = _socketMessageComponator.CreateMessageWithHeader(
                        _messageId,
                        writer => _encoder.Serialize(writer, operation, _logger));

                    return _networkClient.SendAsync(sendBytesWithHeader.Span);
                }
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

            DisposeUdp();

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

        private void OnSocketMessageComponatorReceivedMessage(ReadOnlyMemory<byte> message)
        {
            try
            {
	            var operationData = _encoder.Deserialize(message, _logger);
	            if (TryHandleUdpOffer(operationData)) return;
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

        private void OnNetworkClientDisconnected()
        {
            DisposeUdp();
            ClientDisconnected?.Invoke();
        }

        private bool TryHandleUdpOffer(OperationData operation)
        {
            if (operation.OperationCode != 255 || operation.Parameters == null ||
                !operation.Parameters.TryGetValue(0, out var marker) ||
                marker as string != "MERIDIAN_UDP_OFFER_V1") return false;

            if (!operation.Parameters.TryGetValue(1, out var tokenValue) ||
                !(tokenValue is string tokenString) ||
                !operation.Parameters.TryGetValue(2, out var portValue)) return true;

            try
            {
                var token = Convert.FromBase64String(tokenString);
                var port = Convert.ToInt32(portValue);
                var addresses = Dns.GetHostAddresses(_networkClient.Address);
                IPAddress ip = null;
                foreach (var candidate in addresses)
                    if (candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = candidate; break; }
                if (ip == null || token.Length != 16) return true;

                lock (_udpLock)
                {
                    DisposeUdpLocked();
                    var udpClient = new MeridianUdpClient(new IPEndPoint(ip, port), OnUdpReceived);
                    _udpClient = udpClient;
                    _udpSession = new UdpSession(token, packet => udpClient.Send(packet) == packet.Length,
                        received => { _receiver?.OnOperationReceived(received); return true; });
                    if (_udpClient.Connect())
                    {
                        _udpClient.ReceiveAsync();
                        _udpSession.StartHandshake();
                    }
                    else DisposeUdpLocked();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ClientPeer] UDP setup failed", ex);
                DisposeUdp();
            }
            return true;
        }

        private void OnUdpReceived(byte[] buffer, int offset, int size)
        {
            if (!UdpPacket.TryParse(buffer, offset, size, out var packet)) return;
            try { _udpSession?.Receive(packet); }
            catch (Exception ex) { _logger?.LogError("[ClientPeer] UDP receive failed", ex); }
        }

        private void DisposeUdp()
        {
            lock (_udpLock) DisposeUdpLocked();
        }

        private void DisposeUdpLocked()
        {
            _udpSession?.Dispose();
            _udpSession = null;
            _udpClient?.Disconnect();
            _udpClient?.Dispose();
            _udpClient = null;
        }

        private void OnNetworkClientError(System.Net.Sockets.SocketError error) => ClientError?.Invoke(error);
    }
}
