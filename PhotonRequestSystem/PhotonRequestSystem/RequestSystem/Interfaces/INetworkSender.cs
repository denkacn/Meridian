using System.Collections.Generic;

namespace PhotonRequestSystem.RequestSystem.Interfaces
{
    public interface INetworkSender
    {
        void Send(byte code, Dictionary<byte, object> package, bool isNecessarily);
    }
}
