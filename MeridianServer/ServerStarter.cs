using System;
using System.Reflection;
using System.Threading.Tasks;
using MeridianServer.BaseLayer.Interfaces;
using MeridianServer.BaseLayer.Models;
using MeridianServer.LogsLayer;

namespace MeridianServer
{
    static class Program
    {
        private static IServerController _serverController;
        
        [STAThread]
        static void Main()
        {
            LoggerExt.Init();

            SubscribingToUnhandledExceptions();

            LoggerExt.Log("MeridianServer version: " + Assembly.GetExecutingAssembly().GetName().Version);

            try
            {
                _serverController = new ServerHub();
                _serverController.Init();
            }
            catch (Exception e)
            {
                LoggerExt.LogError("[ServerHub] !!Main Process Error!!", e);
                throw;
            }

            Console.ReadLine();
        }
        
        //[DllImport("kernel32.dll", SetLastError = true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //static extern bool AllocConsole();

        static void SubscribingToUnhandledExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                LoggerExt.LogError($"[ServerHub] Unhandled Exceptions: {((Exception)e.ExceptionObject).Message}", (Exception)e.ExceptionObject);

                Console.WriteLine("Wait Correct Abort...");
                Console.ReadKey();
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LoggerExt.LogError($"[ServerHub] Unhandled Exceptions: {e.Exception.Message}", e.Exception);

                Console.WriteLine("Wait Correct Abort...");
                Console.ReadKey();

                e.SetObserved();
            };

        }
    }
}