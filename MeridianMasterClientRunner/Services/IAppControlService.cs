
namespace MeridianMasterClientRunner.Services
{
	public interface IAppControlService
	{
		void RunMasterClient(string roomId, string masterVersion, bool isBatched);
		string GetProcessesInfo();
		string GetInfo();
	}
}
