using CentralexServer.CentralexConnections;

namespace CentralexServer.WebServer
{
	[Serializable]
	internal class WebData
	{
		public int LastChanged { get; set; }

		public WebClientItem[] Clients { get; set; }

		public int FreePorts { get; set; }
	}

	[Serializable]
	internal class WebClientItem
	{
		public string name { get; set; }

		public int number { get; set; }

		public int port { get; set; }

		public string status { get; set; }

		public int last_change { get; set; }

		public static string StatusToStr(ClientStates status, string reason)
		{
			switch(status)
			{
				case ClientStates.Ready:
					return "bereit";
				case ClientStates.NotConnected:
					if (!string.IsNullOrEmpty(reason))
					{
						return $"getrennt: {reason}";
					}
					else
					{
						return "getrennt";
					}
				case ClientStates.Call:
					return "anruf";
				default:
					return "?";
			}
		}
	}

	internal class WebUpdate
	{
		public int LastChanged { get; set; }

		public WebUpdate(int lastChanged)
		{
			LastChanged = lastChanged;
		}
	}
}
