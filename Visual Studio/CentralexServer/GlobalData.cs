using CentralexServer.Config;
using ServerCommon.Logging;

namespace CentralexServer
{
	internal static class GlobalData
	{
		public static Logger Logger;

		public static SysLog SysLogger;

		public static int LastSessionNo;

		public static System.PlatformID PlatformId;

		//public static SubscriberServerConfig SubscriberServerConfig;

		public static ConfigData Config;
	}
}
