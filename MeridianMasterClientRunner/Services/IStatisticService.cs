using MeridianMasterClientRunner.Models;

namespace MeridianMasterClientRunner.Services
{
	public interface IStatisticService
	{
		StatisticData Statistic { get; }

		void Init();
		void Save();
		string GetInfo();
	}
}
