
namespace MeridianMasterClientRunner.Models
{
	public class ConfigurationData
	{
		public string ServerName { get; set; }
		public string ServerIp { get; set; }
		public int ServerPort { get; set; }

		public string ExeName { get; set; }
		public string MrcId { get; set; }

		public bool IsBatched { get; set; }
		public bool IsCheckOnReadyJson { get; set; }
		public string VersionInfoPath { get; set; }
	}
}
