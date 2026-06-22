using System;
using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Interfaces.Server;

namespace MeridianServerLib.Models.Server
{
    public abstract class MeridianApplication : IMeridianApplication, IAsyncMeridianApplication
    {
	    public event Action<OutsideCommandType> MeridianApplicationCommand;

	    void IMeridianApplication.InitServerPeer(IServerPeerSession peerSession)
        {
            CreateClient(peerSession);
        }

        async Task IAsyncMeridianApplication.InitServerPeerAsync(IServerPeerSession peerSession, CancellationToken cancellationToken)
        {
            await CreateClientAsync(peerSession, cancellationToken).ConfigureAwait(false);
        }

        void IMeridianApplication.Discard()
        {
            Discard();
        }

        Task IAsyncMeridianApplication.DiscardAsync(CancellationToken cancellationToken)
        {
            return DiscardAsync(cancellationToken);
        }

        Task IAsyncMeridianApplication.SetupAsync(string id, string path, CancellationToken cancellationToken)
        {
            return SetupAsync(id, path, cancellationToken);
        }

        public void SendOutsideCommand(OutsideCommandType command)
        {
	        MeridianApplicationCommand?.Invoke(command);
        }

        public abstract void Setup(string id, string path);
        public abstract ServerPeer CreateClient(IServerPeerSession peerSession);

        protected virtual void Discard(){}

        protected virtual Task SetupAsync(string id, string path, CancellationToken cancellationToken)
        {
            Setup(id, path);
            return Task.CompletedTask;
        }

        protected virtual Task<ServerPeer> CreateClientAsync(IServerPeerSession peerSession, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateClient(peerSession));
        }

        protected virtual Task DiscardAsync(CancellationToken cancellationToken)
        {
            Discard();
            return Task.CompletedTask;
        }
    }
}
