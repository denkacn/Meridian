using MeridianMasterClientRunner.Models;

namespace MeridianMasterClientRunner.Services
{
	public interface ISettingsService
	{
		public static string Line = "-----------------------------------------------------------------\n";

		void Init();
		RunConfigurationData GetActualMasterClientPath(string masterVersion);
		ConfigurationData Configuration { get; }
	}
}
