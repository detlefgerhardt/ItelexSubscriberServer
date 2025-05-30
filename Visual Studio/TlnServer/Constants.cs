using ServerCommon;

namespace TlnServer
{
	internal static class Constants
	{
		//public static string PROGRAM_NAME = "TlnServer";

		public static bool BETA = true;

		public static string LOG_NAME = "TlnServer.log";
		//public static LogTypes LOG_LEVEL = LogTypes.Debug;
		public static bool CONNECTION_LOGGING = false;

		//public static string SYSLOG_HOST = "192.168.0.1";
		//public static string SYSLOG_HOST = "itelex.srvdns.de";
		//public static int SYSLOG_PORT = 514;
		//public static SysLog.Facility SYSLOG_FACILITY = SysLog.Facility.local0;
		//public const string SYSLOG_NAME = "TlnServer4";

		public static string BS = Path.DirectorySeparatorChar.ToString();

		public static string TLNSERVER_CONFIG_NAME = $".{BS}tlnserver.conf";
		public static string SYNCSERVER_CONFIG_NAME = $".{BS}server.conf";
		public static string LOG_PATH = $".{BS}log";
		public static string BACKUP_PATH = $".{BS}backup";
		public static string DATABASE_NAME = $".{BS}telefonbuch.db";
		public static string WEB_PATH = $".{BS}web";
	}
}
