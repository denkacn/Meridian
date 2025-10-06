
using MeridianMasterClientRunnerCommon;

namespace MeridianMasterClientRunner.Network.Client
{
	public interface IMeridianMasterClient
	{
		void SendStatus(McrStatusData mcrStatusData);
		void Send(int operationCodes, Dictionary<byte, object> parameters);
	}
}
