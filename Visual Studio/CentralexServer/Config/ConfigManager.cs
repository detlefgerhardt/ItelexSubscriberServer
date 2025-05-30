using Centralex.Config;
using ServerCommon.Logging;

namespace CentralexServer.Config
{
	internal class ConfigManager
	{
		private const string TAG = nameof(ConfigManager);
		private const string TAG2 = "";

		private static ConfigManager instance;
		public static ConfigManager Instance => instance ??= new ConfigManager();

		public ConfigData LoadConfig(Logger logger)
		{
			logger.Debug(TAG, nameof(LoadConfig), TAG2, "");

			ConfigData config = new ConfigData()
			{
				ServerName = "Centralex",
				ClientPortsDefault = 11000,
				ClientPortsStart = 11001,
				ClientPortsEnd = 11199,
				CentralexPort = 0,
				TlnServerProxyPort = null,
				WebServerPort = 0,
				SubscriberServers = null,
				LogLevel = LogTypes.Info,
				Syslog = null,
			};

			string path = null;
			try
			{
				path = Path.Combine(Helper.GetExePath(), Constants.CONFIG_NAME);
				string[] lines = File.ReadAllLines(path);
				List<SysLogConfigItem> syslogServers = new List<SysLogConfigItem>();
				List<SubscriberServerItem> subscriberServers = new List<SubscriberServerItem>();
				foreach (string line in lines)
				{
					string line1 = line.Trim();
					if (line1.StartsWith("#")) continue; // comment

					int pos = line1.IndexOf(' ');
					if (pos == -1) continue;

					string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
					string key = parts[0].ToLower();
					switch (key)
					{
						case "server_name":
						case "servername":
							if (parts.Length == 2)
							{
								config.ServerName = parts[1];
							}
							break;
						case "centralex_port":
						case "centralexport":
							if (parts.Length == 2)
							{
								config.CentralexPort = int.Parse(parts[1]);
							};
							break;
						case "webserver_port":
						case "webserverport":
							if (parts.Length == 2)
							{
								config.WebServerPort = int.Parse(parts[1]);
							}
							break;
						case "webserver_user":
						case "webserveruser":
							if (parts.Length == 2)
							{
								config.WebServerUser = parts[1];
							}
							break;
						case "webserver_pwd":
						case "webserverpwd":
							if (parts.Length == 2)
							{
								config.WebServerPwd = parts[1];
							}
							break;
						case "client_ports_default":
						case "clientportsdefault":
							if (parts.Length == 2)
							{
								config.ClientPortsDefault = int.Parse(parts[1]);
							}
							break;
						case "client_ports_start":
						case "clientportsstart":
							if (parts.Length == 2)
							{
								config.ClientPortsStart = int.Parse(parts[1]);
							}
							break;
						case "client_ports_end":
						case "clientportsend":
							if (parts.Length == 2)
							{
								config.ClientPortsEnd = int.Parse(parts[1]);
							}
							break;
						case "tlnserver_proxy_port":
						case "tlnserverproxyport":
							if (parts.Length == 2)
							{
								config.TlnServerProxyPort = int.Parse(parts[1]);
							}
							break;
						case "subscriber_server":
						case "subscriberserver":
							if (parts.Length == 3)
							{
								SubscriberServerItem subsServer = new SubscriberServerItem()
								{
									ServerName = parts[1],
									Port = int.Parse(parts[2]),
								};
								subscriberServers.Add(subsServer);
							}
							break;
						case "loglevel":
							if (parts.Length == 2)
							{
								config.LogLevel = ParseLogType(parts[1]);
							}
							break;
						case "syslog_server":
						case "syslogserver":
							if (parts.Length == 5 || parts.Length == 6)
							{
								SysLogConfigItem syslogServer = new SysLogConfigItem()
								{
									Server = parts[1],
									Port = int.Parse(parts[2]),
									Name = parts[3],
									Facility = int.Parse(parts[4]),
								};
								syslogServers.Add(syslogServer);
								if (parts.Length == 6)
								{
									syslogServer.Severity = parts[5];
								}
							}
							break;
					}
				}

				logger.Debug(TAG, nameof(LoadConfig), TAG2, $"subscriberServers.Count = {subscriberServers.Count}");

				if (subscriberServers.Count > 0)
				{
					config.SubscriberServers = subscriberServers.ToArray();
				}

				config.Syslog = syslogServers.ToArray();

				return config;
			}
			catch (Exception ex)
			{
				logger.Error(TAG, nameof(LoadConfig), TAG2, $"error reading config from {path}", ex);
				return null;
			}
		}
		private LogTypes ParseLogType(string str)
		{
			var values = Enum.GetValues(typeof(LogTypes)); ;
			foreach (var e in values)
			{
				string enumStr = e.ToString();
				if (str.Length > enumStr.Length) continue;
				if (string.Compare(str, enumStr.Substring(0, str.Length), true) == 0)
				{
					return (LogTypes)e;
				}
			}
			return LogTypes.Debug;
		}
	}
}
