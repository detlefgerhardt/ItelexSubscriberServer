using System.Net;

namespace TlnServer.BinaryServer
{
	internal class ServerItem
	{
		public string Hostname { get; set; }

		public IPAddress IpAddress { get; set; }

		public ServerItem(string hostname)
		{
			Hostname = hostname;
			IpAddress = null;
		}
	}
}
