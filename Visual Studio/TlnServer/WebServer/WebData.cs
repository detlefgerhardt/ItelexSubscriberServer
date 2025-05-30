namespace TlnServer.WebServer
{
	[Serializable]
	internal class WebDataAdmin
	{
		public int LastChanged { get; set; }

		public WebTeilnehmerItemAdmin[] Teilnehmer { get; set; }
	}

	[Serializable]
	internal class WebData
	{
		public int LastChanged { get; set; }

		public WebTeilnehmerItem[] Teilnehmer { get; set; }
	}

	[Serializable]
	internal class WebTeilnehmerItem
	{
		public ulong uid { get; set; }

		public int number { get; set; }

		public string name { get; set; }

		public string extension { get; set; }

		public string ipaddress { get; set; }

		public string hostname { get; set; }

		public int port { get; set; }

		public int type { get; set; }

		public int timestamp { get; set; }
	}

	[Serializable]
	internal class WebTeilnehmerItemAdmin: WebTeilnehmerItem
	{
		public int disabled { get; set; }
	}


	internal class WebUpdate
	{
		public int LastChanged { get; set; }

		public WebUpdate(int lastChanged)
		{
			LastChanged = lastChanged;
		}
	}

	internal class WebEditResponse
	{
		public bool successfull { get; set; }

		public WebEditResponseMessage message { get; set; }

		public WebEditResponse(bool success, string msg)
		{
			successfull = success;
			message = new WebEditResponseMessage()
			{
				code = success ? 1 : 0,
				text = msg
			};
		}
	}

	internal class WebEditResponseMessage
	{
		public int code { get; set; }

		public string text { get; set; }
	}


	internal class WebResponseData
	{
		public string job { get; set; }

		public ulong uid { get; set; }

		public int number { get; set; }

		public string name { get; set; }

		public int? type { get; set; }

		public string hostname { get; set; }

		public string ipaddress { get; set; }

		public int? port { get; set; }

		public int? extension { get; set; }

		public int? disabled { get; set; }

	}
}
