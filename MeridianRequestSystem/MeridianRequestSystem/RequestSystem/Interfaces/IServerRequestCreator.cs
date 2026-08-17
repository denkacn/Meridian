using System.Collections.Generic;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface IServerRequestCreator
    {
        IServerDataNetworkRequest CreateServerDataNetworkRequest(Dictionary<byte, object> package);
    }
}
