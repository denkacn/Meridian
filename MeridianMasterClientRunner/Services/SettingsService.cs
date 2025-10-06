using MeridianMasterClientRunner.Models;
using Newtonsoft.Json;

namespace MeridianMasterClientRunner.Services
{
	public class SettingsService : ISettingsService
	{
		private ConfigurationData? _configurationData;
		public ConfigurationData Configuration => _configurationData;

		public void Init()
		{
			LoadConfig();
		}

		public RunConfigurationData GetActualMasterClientPath(string masterVersion)
		{
			var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			var masterDirectory = new DirectoryInfo(Path.Combine(baseDirectory, "Master"));

			return GetLatestWrittenFileInDirectory(masterDirectory, masterVersion);
		}

		private void LoadConfig()
		{
			var configurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeridianMasterClientRunnerConfiguration.json");
			var configurationJson = File.ReadAllText(configurationPath);

			_configurationData = JsonConvert.DeserializeObject<ConfigurationData>(configurationJson);
		}

		private RunConfigurationData GetLatestWrittenFileInDirectory(DirectoryInfo directoryInfo, string masterVersion)
		{
			if (directoryInfo == null || !directoryInfo.Exists) return null;

			var runConfigurationData = new RunConfigurationData();
			var latestWriteTime = DateTime.MinValue;

			foreach (var dirInfo in directoryInfo.GetDirectories())
			{
				if (IsUploadAndRightVersion(dirInfo, masterVersion, out var exeOverride) && dirInfo.LastWriteTime > latestWriteTime)
				{
					latestWriteTime = dirInfo.LastWriteTime;
					runConfigurationData.LastMasterPath = dirInfo.FullName;
					runConfigurationData.ExeOveride = exeOverride;
				}
			}

			return runConfigurationData;
		}

		private  bool IsUploadAndRightVersion(DirectoryInfo dirInfo, string masterVersion, out string exeOverride)
		{
			exeOverride = string.Empty;

			if (!_configurationData.IsCheckOnReadyJson) return true;

			var onReadyPath = Path.Combine(dirInfo.FullName, "Master", "OnReady", "OnReady.json");

			if (!File.Exists(onReadyPath)) return false;

			var jsonContent = File.ReadAllText(onReadyPath);

			if (string.IsNullOrEmpty(jsonContent)) return false;
			
			//Console.WriteLine($"path: {onReadyPath} jsonData: {jsonContent} masterVersion: {masterVersion}");

			try
			{
				var onReadyConfig = JsonConvert.DeserializeObject<OnReadyConfigurationData>(jsonContent);
				exeOverride = onReadyConfig.ExeOverride;
				return onReadyConfig.Version == masterVersion;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Ошибка при десериализации JSON: {ex.Message}");
				return false;
			}
		}
	}
}
