using System;
using System.Net;
using System.Threading;
using NetCoreServer;

namespace MeridianServerLib.Models.Client
{
    internal sealed class MeridianUdpClient : UdpClient
    {
        private readonly Action<byte[], int, int> _receive;

        public MeridianUdpClient(IPEndPoint endpoint, Action<byte[], int, int> receive) : base(endpoint)
        {
            _receive = receive;
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
