namespace MeridianServerLib.Interfaces.Server
{
	public class MeridianApplicationConfiguration
	{
		public string Id;
		public IMeridianApplication MeridianApplication;
		public int Port;
		public string Path;

		public MeridianApplicationConfiguration(string id, IMeridianApplication application, int port, string path)
		{
			Id = id;
			MeridianApplication = application;
			Port = port;
			Path = path;
		}
	}
}
