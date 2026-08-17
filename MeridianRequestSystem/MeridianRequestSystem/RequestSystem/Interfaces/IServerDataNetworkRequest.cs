using System;
using System.Collections.Generic;
using MeridianRequestSystem.RequestSystem.Client;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface IServerDataNetworkRequest
    {
        bool IsAsync { get; }
        IDataNetworkResponse Execute(IUserClient client);
        void Execute(IUserClient client, Action<IDataNetworkResponse> executeResponse);
        void Map(Dictionary<byte, object> package);
    }
}
