using ServerCommon.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.SubscriberServer
{
	public class SubscriberServerConfigItem
	{
		public const int DEFAULT_PORT = 11811;

		public const int DEFAULT_TIMEOUT = 2000;

		public string ServerName { get; set; }

		public IPAddress ServerIp { get; set; }

		public int Port { get; set; } = DEFAULT_PORT;

		public int Timeout { get; set; } = DEFAULT_TIMEOUT;

		public SubscriberServerConfigItem()
		{
		}

		public SubscriberServerConfigItem(string serverName, int port)
		{
			ServerName = serverName;
			Port = port;

			try
			{
				ServerIp = IPAddress.Parse(CommonHelper.GetIp4AddrFromHostname(serverName));
			}
			catch (Exception)
			{
				ServerIp = null;
			}
		}
	}
}
