using ServerCommon;

namespace CentralexServer
{
	internal static class Constants
	{
		public static bool BETA = true;

		public static string LOG_NAME = "CentralexServer.log";
		public static bool CONNECTION_LOGGING = false;

		public static string BS = Path.DirectorySeparatorChar.ToString();

		public static string CONFIG_NAME = $".{BS}centralex.conf";
		public static string LOG_PATH = $".{BS}log";
		public static string DATABASE_NAME = $".{BS}CentralexServer.sqlite";
		public static string WEB_PATH = $".{BS}web";
	}
}
