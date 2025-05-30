using ServerCommon.Logging;
using ServerCommon.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TlnServer.BinaryServer;

namespace TlnServer.Config
{
	internal class ConfigManager
	{
		private const string TAG = nameof(ConfigManager);
		private const string TAG2 = "";

		private static ConfigManager instance;
		public static ConfigManager Instance => instance ??= new ConfigManager();

		public ConfigData LoadConfig(Logger _logger)
		{
			ConfigData config = new ConfigData()
			{
				ServerName = "TlnServer",
				ServerHostName = "",
				ServerId = 0,
				BinaryPort = 0
				WebServerPort = 0
				LogLevel = LogTypes.Info,
				Syslog = null,
			};
			string path = null;
			try
			{
				path = Path.Combine(Helper.GetExePath(), Constants.TLNSERVER_CONFIG_NAME);
				string[] lines = File.ReadAllLines(path);
				List<SysLogConfigItem> syslogServers = new List<SysLogConfigItem>();
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
						case "server_id":
						case "serverid":
							if (parts.Length == 2)
							{
								config.ServerId = int.Parse(parts[1]);
							}
							break;
						case "server_hostname":
						case "serverhostname":
							if (parts.Length == 2)
							{
								config.ServerHostName = parts[1];
							}
							break;
						case "binary_port":
						case "binaryport":
							if (parts.Length == 2)
							{
								config.BinaryPort = int.Parse(parts[1]);
							}
							break;
						case "server_pin":
						case "serverpin":
							if (parts.Length == 2)
							{
								config.ServerPin = int.Parse(parts[1]);
							}
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
						case "webserver_edit_pwd":
						case "webservereditpwd":
							if (parts.Length == 2)
							{
								config.WebServerEditPwd = parts[1];
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

				config.Syslog = syslogServers.ToArray();

				return config;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(LoadConfig), TAG2, $"error reading from {path}", ex);
				return null;
			}
		}

		public SyncServerData LoadSyncServerConfig(Logger _logger, int? version = null)
		{
			SyncServerData config = new SyncServerData();

			string path = null;
			try
			{
				path = Path.Combine(Helper.GetExePath(), Constants.SYNCSERVER_CONFIG_NAME);
				string[] lines = File.ReadAllLines(path);
				List<SyncServerItem> syncServer = new List<SyncServerItem>();
				foreach (string line in lines)
				{
					try
					{
						string line1 = line.Trim();
						if (line1.StartsWith("#")) continue; // comment
						if (string.IsNullOrWhiteSpace(line1)) continue; // empty line

						string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
						if (parts.Length != 5)
						{
							// logger
							_logger.Error(TAG, nameof(LoadConfig), TAG2, $"error in {path}, line='{line}'");
							continue;
						}


						string key = parts[0].ToLower();
						switch (key)
						{
							case "server":
								SyncServerItem item = new SyncServerItem()
								{
									Id = int.Parse(parts[1]),
									Address = parts[2],
									Port = int.Parse(parts[3]),
									Version = int.Parse(parts[4]),
								};
								if (version == null || version.Value == item.Version)
								{
									syncServer.Add(item);
								}
								break;
						}
					}
					catch (Exception ex)
					{
						_logger.Error(TAG, nameof(LoadConfig), TAG2, $"error in {path}, line={line}", ex);
					}
				}
				if (syncServer.Count > 0)
				{
					config.SyncServer = syncServer.ToArray();
					return config;
				}
				else
				{
					_logger.Error(TAG, nameof(LoadConfig), TAG2, $"no sync-servers configured");
					return null;
				}
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(LoadConfig), TAG2,
						$"error reading modem config from {Constants.SYNCSERVER_CONFIG_NAME}", ex);
				return null;
			}
		}

		private LogTypes ParseLogType(string str)
		{
			var values = Enum.GetValues(typeof(LogTypes)); ;
			foreach(var e in values)
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
