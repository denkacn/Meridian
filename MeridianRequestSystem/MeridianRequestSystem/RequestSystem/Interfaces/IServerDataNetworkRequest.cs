using System;
using MeridianRequestSystem.RequestSystem.Client;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface IServerDataNetworkRequest
    {
        bool IsAsync { get; }
        IDataNetworkResponse Execute(IUserClient client);
        void Execute(IUserClient client, Action<IDataNetworkResponse> executeResponse);
    }
}
