using System.Collections.Generic;

namespace PhotonRequestSystem.RequestSystem.Interfaces
{
    public interface IDataNetworkResponse
    {
        void Map(Dictionary<byte, object> package);
    }
}
