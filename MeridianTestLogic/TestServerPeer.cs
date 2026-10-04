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
using MeridianTestContracts;
using System.Collections.Generic;

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
            AllowUdpOperation(TestOperationCodes.UdpProbe);
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
            if (messageData.OperationCode == TestOperationCodes.UdpProbe)
            {
                return HandleUdpProbeAsync(messageData);
            }

            if (TraceMessages)
            {
                Console.WriteLine("[MeridianTestLogic] Received operation: " + messageData.OperationCode);
            }

            return _requestAdapter.OnReceivedMessageAsync(sender, messageData, cancellationToken);
        }

        private async Task HandleUdpProbeAsync(OperationData operation)
        {
            var fields = operation.Parameters;
            if (fields == null || !fields.TryGetValue(UdpProbeFields.Sequence, out var sequence) ||
                !fields.TryGetValue(UdpProbeFields.Reliable, out var reliableValue) ||
                !(reliableValue is bool reliable))
            {
                return;
            }

            var response = new OperationData(TestOperationCodes.UdpProbeResponse,
                new Dictionary<byte, object>
                {
                    [UdpProbeFields.Sequence] = sequence,
                    [UdpProbeFields.Payload] = fields.TryGetValue(UdpProbeFields.Payload, out var payload) ? payload : string.Empty
                });
            await SendUdpAsync(response, reliable ? UdpDeliveryMode.Reliable : UdpDeliveryMode.Unreliable)
                .ConfigureAwait(false);
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
