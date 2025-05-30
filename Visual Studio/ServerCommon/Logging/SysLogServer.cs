using ServerCommon.Utility;
using System.Net;

namespace ServerCommon.Logging
{
	public class SysLogServer
	{
		public string ServerName { get; set; }

		public IPAddress ServerIp { get; set; }

		public int Port { get; set; }

		public string ClientName { get; set; }

		public SysLog.Facility Facility { get; set; }

		public SysLogServer(string serverName, int port, string clientName, SysLog.Facility facility)
		{
			ServerName = serverName;
			Port = port;
			ClientName = clientName;
			Facility = facility;

			try
			{
				ServerIp = IPAddress.Parse(CommonHelper.GetIp4AddrFromHostname(serverName));
			}
			catch(Exception)
			{
				ServerIp = null;
			}
		}

		public override string ToString()
		{
			return $"{ServerName} {ServerIp} {Port} {ClientName} {Facility}";
		}
	}
}
