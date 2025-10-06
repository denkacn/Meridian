using MeridianMasterClientRunner.Services;

namespace MeridianMasterClientRunner
{
    public class Program
    {
		private static IAppControlService? _appControlService;


		static void Main(string[] args)
        {
	        _appControlService = new AppControlService();

			while (true)
            {
	            var command = Console.ReadLine();
	            ExecCommand(command);
            }

            Console.ReadLine();
		}

        private static void ExecCommand(string? command)
        {
	        switch (command)
	        {
		        case "-info":
			        Console.WriteLine(_appControlService.GetProcessesInfo());
			        break;
		        case "-stat":
			        Console.WriteLine(_appControlService.GetInfo());
			        break;
		        case "-help":
			        Console.WriteLine("Exisrt Command:\n-info\n-stat");
			        break;
	        }
        }
	}
}
