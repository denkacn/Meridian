namespace MeridianServerLib.Interfaces.Server
{
	public class MeridianApplicationConfiguration
	{
		public string Id;
		public IMeridianApplication MeridianApplication;
		public int Port;

		public MeridianApplicationConfiguration(string id, IMeridianApplication application, int port)
		{
			Id = id;
			MeridianApplication = application;
			Port = port;
		}
	}
}
