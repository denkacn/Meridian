using System;
using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Models.Server;

namespace MeridianServerLib.Interfaces.Server
{
    public interface IMeridianApplication
    {
	    event Action<OutsideCommandType> MeridianApplicationCommand;

		void InitServerPeer(IServerPeerSession peerSession);
        void Setup(string id, string path);
        ServerPeer CreateClient(IServerPeerSession transportClient);
        void Discard();
    }

    public interface IAsyncMeridianApplication
    {
        Task InitServerPeerAsync(IServerPeerSession peerSession, CancellationToken cancellationToken);
        Task SetupAsync(string id, string path, CancellationToken cancellationToken);
        Task DiscardAsync(CancellationToken cancellationToken);
    }
}
