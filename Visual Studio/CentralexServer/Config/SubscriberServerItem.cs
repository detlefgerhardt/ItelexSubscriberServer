using ServerCommon.Logging;

namespace Centralex.Config
{
	internal class SubscriberServerItem
	{
		public string ServerName { get; set; }

		public int Port { get; set; }

		public override string ToString()
		{
			return $"{ServerName} {Port}";
		}

	}
}
