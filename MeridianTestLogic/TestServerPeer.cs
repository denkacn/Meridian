using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Creators;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianRequestSystem.RequestSystem.Server;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.Models.Operations;
using MeridianServerLib.Models.Server;

namespace MeridianTestLogic
{
    public sealed class TestServerPeer : ServerPeer, IUserClient
    {
        private static readonly bool TraceMessages =
            string.Equals(
                Environment.GetEnvironmentVariable("MERIDIAN_TEST_TRACE_MESSAGES"),
                "1",
                StringComparison.OrdinalIgnoreCase);

        private readonly MeridianServerPeerRequestAdapter _requestAdapter;

        public int UserId { get; private set; }

        public TestServerPeer(IServerPeerSession serverPeerSession)
            : base(serverPeerSession)
        {
            var requestCreator = new MeridianServerRequestCreator(Assembly.GetExecutingAssembly());
            var requestSystem = new MeridianRequestSystem.RequestSystem.Server.MeridianRequestSystem(
                requestCreator,
                new PeerNetworkSender(this));

            _requestAdapter = new MeridianServerPeerRequestAdapter(requestSystem, this);
        }

        public void SetUserId(int userId)
        {
            UserId = userId;
        }

        protected override void OnConnected(object sender, EventArgs e)
        {
            Console.WriteLine("[MeridianTestLogic] Connected: " + Id);
        }

        protected override void OnDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine("[MeridianTestLogic] Disconnected: " + Id);
        }

        protected override Task OnReceivedMessageAsync(
            object sender,
            OperationData messageData,
            CancellationToken cancellationToken)
        {
            if (TraceMessages)
            {
                Console.WriteLine("[MeridianTestLogic] Received operation: " + messageData.OperationCode);
            }

            return _requestAdapter.OnReceivedMessageAsync(sender, messageData, cancellationToken);
        }

        private sealed class PeerNetworkSender : INetworkSender
        {
            private readonly ServerPeer _peer;

            public PeerNetworkSender(ServerPeer peer)
            {
                _peer = peer;
            }

            public void Send(byte code, System.Collections.Generic.Dictionary<byte, object> package, bool isNecessarily)
            {
                _peer.Send(new OperationData(code, package));
            }
        }
    }
}
