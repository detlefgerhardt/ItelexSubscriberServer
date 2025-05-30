using ServerCommon.Logging;
using TlnServer.Config;

namespace TlnServer
{
	internal static class GlobalData
	{
		public static Logger Logger;

		public static SysLog SysLogger;

		public static int LastSessionNo;

		public static System.PlatformID PlatformId;

		public static ConfigData Config;
	}
}
