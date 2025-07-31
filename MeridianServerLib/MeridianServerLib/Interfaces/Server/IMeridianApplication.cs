using System;
using MeridianServerLib.Models.Server;

namespace MeridianServerLib.Interfaces.Server
{
    public interface IMeridianApplication
    {
	    event Action<OutsideCommandType> MeridianApplicationCommand;

		void InitServerPeer(IServerPeerSession peerSession);
        void Setup(string id);
        ServerPeer CreateClient(IServerPeerSession transportClient);
        void Discard();
    }
}