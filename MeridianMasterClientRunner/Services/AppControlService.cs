using MeridianMasterClientRunner.Models;
using MeridianMasterClientRunner.Network.Client;
using MeridianMasterClientRunnerCommon;
using Newtonsoft.Json;
using System.Diagnostics;

namespace MeridianMasterClientRunner.Services
{
	public class AppControlService : IAppControlService
	{
		private readonly ISettingsService _settingsService;
		private readonly IStatisticService _statisticService;
		private readonly IMeridianMasterClient _client;
		private readonly List<Process> _runProcesses = new List<Process>();
		private readonly Timer _checkTimer;

		public AppControlService()
		{
			Console.WriteLine("Version: 2.0.0.1");

			_settingsService = new SettingsService();
			_settingsService.Init();

			_statisticService = new StatisticService(_settingsService);
			_statisticService.Init();
			
			_client = new MeridianMasterClient(this, _settingsService);
			_checkTimer = new Timer(CheckProcesses, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
		}

		public void RunMasterClient(string roomId, string masterVersion, bool isBatched)
		{
			var runConfigurationData = _settingsService.GetActualMasterClientPath(masterVersion);

			if (runConfigurationData == null || runConfigurationData.IsError)
			{
				Console.WriteLine("ERROR RUN MASTER CLIENT, CLIENT IS NULL");

				_client.Send(8, new Dictionary<byte, object>() {
					{ 1, McrNetworkCommandType.MCR_RUN_CLIENT_ERROR },
					{ 2, roomId },
					{ 3, "ERROR RUN MASTER CLIENT, CLIENT IS NULL" }
				});
				return;
			}

			var exeName = string.IsNullOrEmpty(runConfigurationData.ExeOveride) ? _settingsService.Configuration.ExeName : runConfigurationData.ExeOveride;
			var executablePath = $"{runConfigurationData.LastMasterPath}/Master/{exeName}.exe";
			var arguments = isBatched ? "-batchmode -nographics " : string.Empty;
			var additionalArgs = $"roomId:{roomId} roomTime:{300} serverIp:{_settingsService.Configuration.ServerIp}:{_settingsService.Configuration.ServerPort}";
			var fullArguments = $"{arguments}{additionalArgs}";

			Console.WriteLine($"path: {executablePath}\nargument: {fullArguments} batchmode: {arguments}");
			Console.WriteLine("WorkingDirectory: " + Path.GetDirectoryName(executablePath));

			var process = Process.Start(new ProcessStartInfo()
			{
				FileName = executablePath,
				Arguments = fullArguments,
				UseShellExecute = true,
				CreateNoWindow = false,
				WorkingDirectory = Path.GetDirectoryName(executablePath)
			});

			if (process != null)
			{
				_runProcesses.Add(process);

				process.EnableRaisingEvents = true;
				process.Exited += (s, e) =>
				{
					Console.WriteLine($"Process {process.Id} exited");
					lock (_runProcesses)
						_runProcesses.Remove(process);
				};
			}

			_statisticService.Statistic.RunCount++;

			var versionInfoPath = runConfigurationData.LastMasterPath + _settingsService.Configuration.VersionInfoPath; //"/Master/BorderWorlds_Data/StreamingAssets/Version/VersionInfo.json";

			if (File.Exists(versionInfoPath))
			{
				var versionInfo = JsonConvert.DeserializeObject<GameVersionData>(File.ReadAllText(versionInfoPath));
				_statisticService.Statistic.AddMasterVersion(versionInfo.Version);

				Console.WriteLine(string.Format("{0}\n RUN MASTER VERSION {1} CODE {2}\n{0} IP: {3}",
					ISettingsService.Line,
					versionInfo.Version,
					versionInfo.VersionCode,
					_settingsService.Configuration.ServerIp));
			}

			_statisticService.Save();
		}

		public string GetProcessesInfo()
		{
			var infoHeader = ISettingsService.Line + "INFO:\n";
			var processInfo = "";
			var runningCount = 0;

			foreach (var runProcess in _runProcesses)
			{
				if (runProcess != null && !runProcess.HasExited)
				{
					++runningCount;
					processInfo += $"[{runProcess.Id}]Name: {runProcess.ProcessName}, " +
								  $"RunTime: {runProcess.StartTime}, " +
								  $"TotalProcessorTime: {runProcess.TotalProcessorTime}\n";
				}
			}

			return $"{infoHeader}All Processes: {_runProcesses.Count}\n" +
				   $"Run Prosessed: {runningCount}\n" +
				   $"Processes Info:{processInfo}\n{ISettingsService.Line}";
		}

		public string GetInfo()
		{
			return _statisticService.GetInfo();
		}

		//public int GetActiveProcessCount()
		//{
		//	lock (_runProcesses)
		//	{
		//		return _runProcesses.Count(p => !p.HasExited);
		//	}
		//}

		private void CheckProcesses(object state)
		{
			lock (_runProcesses)
			{
				_runProcesses.RemoveAll(p => p.HasExited);

				var amount = _runProcesses.Count; 
				Console.WriteLine($"[{DateTime.Now}] Active processes: {amount}");

				_client.SendStatus(new McrStatusData() { InsAmount = amount });
			}
		}
	}
}
