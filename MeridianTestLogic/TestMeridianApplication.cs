using System;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.Models.Server;

namespace MeridianTestLogic
{
    public sealed class TestMeridianApplication : MeridianApplication
    {
        private string _id;
        private string _path;

        public override void Setup(string id, string path)
        {
            _id = id;
            _path = path;
            Console.WriteLine("[MeridianTestLogic] Setup id: " + _id + " path: " + _path);
        }

        public override ServerPeer CreateClient(IServerPeerSession peerSession)
        {
            Console.WriteLine("[MeridianTestLogic] Create client: " + peerSession.SessionId);
            return new TestServerPeer(peerSession);
        }
    }
}
