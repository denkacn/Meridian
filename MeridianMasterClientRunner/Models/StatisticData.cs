
namespace MeridianMasterClientRunner.Models
{
	public class StatisticData
	{
		public int RunCount;
		public int ErrorCount;
		public List<string> MasterVersions = [];

		public void AddMasterVersion(string version)
		{
			if (MasterVersions.Contains(version)) return;

			MasterVersions.Add(version);
		}
	}
}
