using System.Collections.Generic;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface IDataNetworkResponse
    {
        void Map(Dictionary<byte, object> package);
    }
}
