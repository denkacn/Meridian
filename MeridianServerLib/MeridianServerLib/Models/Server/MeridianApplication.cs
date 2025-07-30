using System;
using MeridianServerLib.Interfaces.Server;

namespace MeridianServerLib.Models.Server
{
    public abstract class MeridianApplication : IMeridianApplication
    {
	    public event Action<OutsideCommandType> MeridianApplicationCommand;

	    void IMeridianApplication.InitServerPeer(IServerPeerSession peerSession)
        {
            var client = CreateClient(peerSession);
        }

        void IMeridianApplication.Discard()
        {
        }

        public void SendOutsideCommand(OutsideCommandType command)
        {
	        MeridianApplicationCommand?.Invoke(command);
        }

        public abstract void Setup();
        public abstract ServerPeer CreateClient(IServerPeerSession peerSession);
    }
}