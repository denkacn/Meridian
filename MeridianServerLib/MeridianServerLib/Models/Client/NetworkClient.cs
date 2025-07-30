using System;
using MeridianServerLib.LogsLayer.Interfaces;
using NetCoreServer;

namespace MeridianServerLib.Models.Client
{
    internal class NetworkClient : TcpClient
    {
        public event Action NetworkClientConnected;
        public event Action NetworkClientDisconnected;
        public event Action<System.Net.Sockets.SocketError> NetworkClientError;
        public event Action<NetworkClientConnectionStatus> ConnectionStatusChanged;

        private readonly Action<byte[], long, long> _receivedAction;
        private NetworkClientConnectionStatus _connectionStatus = NetworkClientConnectionStatus.Disconnected; 

        public NetworkClient(string address, int port, Action<byte[], long, long> receivedAction, ILogger logger) : base(address, port)
        {
            _receivedAction = receivedAction;

            SetLogger(logger);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _receivedAction?.Invoke(buffer, offset, size);
        }

        public override void ReceiveAsync()
        {
            base.ReceiveAsync();
        }

        protected override void OnConnected()
        {
            base.OnConnected();

            _connectionStatus = NetworkClientConnectionStatus.Connected;

            NetworkClientConnected?.Invoke();
            ConnectionStatusChanged?.Invoke(_connectionStatus);
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();

            _connectionStatus = NetworkClientConnectionStatus.Disconnected;

            NetworkClientDisconnected?.Invoke();
            ConnectionStatusChanged?.Invoke(_connectionStatus);
        }

        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            base.OnError(error);

            NetworkClientError?.Invoke(error);
        }
    }

    public enum NetworkClientConnectionStatus
    {
        None, Connected, Disconnected
    }
}