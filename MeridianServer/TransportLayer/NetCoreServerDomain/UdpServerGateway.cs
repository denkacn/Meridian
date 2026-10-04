using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;
using NetCoreServer;

namespace MeridianServer.TransportLayer.NetCoreServerDomain
{
    internal sealed class UdpServerGateway : UdpServer
    {
        private readonly ConcurrentDictionary<string, ServerPeerSession> _sessions =
            new ConcurrentDictionary<string, ServerPeerSession>();
        private readonly ILogger _logger;

        public UdpServerGateway(IPAddress address, int port, ILogger logger) : base(address, port)
        {
            _logger = logger;
        }

        public byte[] Register(ServerPeerSession session)
        {
            using (var random = RandomNumberGenerator.Create())
            {
                while (true)
                {
                    var token = new byte[16];
                    random.GetBytes(token);
                    if (_sessions.TryAdd(Convert.ToBase64String(token), session)) return token;
                }
            }
        }

        public void Unregister(string token)
        {
            if (token != null) _sessions.TryRemove(token, out _);
        }

        public bool SendTo(IPEndPoint endpoint, byte[] bytes)
        {
            return endpoint != null && IsStarted && Send(endpoint, bytes) == bytes.Length;
        }

        protected override void OnStarted()
        {
            ReceiveAsync();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            try
            {
                if (endpoint is IPEndPoint remote &&
                    UdpPacket.TryParse(buffer, (int)offset, (int)size, out var packet) &&
                    _sessions.TryGetValue(packet.Token, out var session))
                    session.ReceiveUdp(remote, packet);
            }
            catch (Exception ex)
            {
                _logger?.LogError("[UdpServerGateway] UDP receive failed", ex);
            }
            finally
            {
                if (IsStarted) ThreadPool.QueueUserWorkItem(_ => ReceiveAsync());
            }
        }
    }
}
