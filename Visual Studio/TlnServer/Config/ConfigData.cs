using ServerCommon.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TlnServer.Config
{
	internal class ConfigData
	{
		public string ServerName { get; set; }

		public int ServerId { get; set; }

		public string ServerHostName { get; set; }

		public int BinaryPort { get; set; }

		public int ServerPin { get; set; }

		public int WebServerPort { get; set; }

		public string WebServerUser { get; set; }

		public string WebServerPwd { get; set; }

		public string WebServerEditPwd { get; set; }

		public LogTypes LogLevel { get; set; }

		public SysLogConfigItem[] Syslog { get; set; }
	}
}
