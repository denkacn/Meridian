
namespace MeridianMasterClientRunner.Models
{
	public class GameVersionData
	{
		public string Version = "1.0.0";
		public int VersionCode = 0;

		public int GetCorrectVersionCode() => this.VersionCode;
	}
}
