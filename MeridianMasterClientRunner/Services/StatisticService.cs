using MeridianMasterClientRunner.Models;
using Newtonsoft.Json;

namespace MeridianMasterClientRunner.Services
{
	public class StatisticService : IStatisticService
	{
		private readonly ISettingsService _settingsService;

		private StatisticData _statisticData;

		public StatisticData Statistic => _statisticData;

		public StatisticService(ISettingsService settingsService)
		{
			_settingsService = settingsService;
		}

		public void Init()
		{
			if (File.Exists("Statistic.txt"))
			{
				var fileContent = File.ReadAllText("Statistic.txt");
				_statisticData = JsonConvert.DeserializeObject<StatisticData>(fileContent);
			}
			else
			{
				_statisticData = new StatisticData();
			}
		}

		public void Save()
		{
			var jsonContent = JsonConvert.SerializeObject(_statisticData);
			File.WriteAllText("Statistic.txt", jsonContent);
		}

		public string GetInfo()
		{
			var separator = ISettingsService.Line + "STATS:\n";
			var versions = string.Join(";", _statisticData.MasterVersions);

			return $"{separator}" +
			       $"All Run Count: {_statisticData.RunCount}\n" +
			       $"Error Count: {_statisticData.ErrorCount}\n" +
			       $"Versions: {versions}\n" +
			       $"{ISettingsService.Line}";
		}
	}
}
