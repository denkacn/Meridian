using System.Collections.Generic;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface INetworkSender
    {
        void Send(byte code, Dictionary<byte, object> package, bool isNecessarily);
    }
}
