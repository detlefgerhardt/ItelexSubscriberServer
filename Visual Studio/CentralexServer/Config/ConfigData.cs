using ServerCommon.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centralex.Config;
using ServerCommon.SubscriberServer;

namespace CentralexServer.Config
{
	internal class ConfigData
	{
		public string ServerName { get; set; }

		public int ClientPortsDefault { get; set; }

		public int ClientPortsStart { get; set; }

		public int ClientPortsEnd { get; set; }

		public int CentralexPort { get; set; }

		public int WebServerPort { get; set; }

		public string WebServerUser { get; set; }

		public string WebServerPwd { get; set; }

		public int? TlnServerProxyPort { get; set; }

		public SubscriberServerItem[] SubscriberServers { get; set; }

		public LogTypes LogLevel { get; set; }

		public SysLogConfigItem[] Syslog { get; set; }

		public SubscriberServerConfigItem[] GetSubscriberServerConfig()
		{
			return (from s in SubscriberServers select new SubscriberServerConfigItem(s.ServerName, s.Port)).ToArray();
		}
	}
}