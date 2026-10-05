using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCoreServer;

namespace MeridianServerLib.Models.Client
{
    internal sealed class MeridianUdpClient : NetCoreServer.UdpClient
    {
        private readonly Action<byte[], int, int> _receive;

        public MeridianUdpClient(IPEndPoint endpoint, Action<byte[], int, int> receive) : base(endpoint)
        {
            _receive = receive;
        }

        public bool SendPacket(byte[] bytes, int length)
        {
            if (!IsConnected) return false;
            try { return Socket.SendTo(bytes, 0, length, SocketFlags.None, Endpoint) == length; }
            catch (ObjectDisposedException) { return false; }
            catch (SocketException) { return false; }
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            try
            {
                if (size > 0 && endpoint.Equals(Endpoint)) _receive(buffer, (int)offset, (int)size);
            }
            finally
            {
                if (IsConnected) ThreadPool.QueueUserWorkItem(_ => ReceiveAsync());
            }
        }
    }
}
