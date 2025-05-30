using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ServerCommon.Utility
{
	public static class CommonHelper
	{
		public static List<string> Split(string str, char delim, int max = int.MaxValue)
		{
			return str.Split(new char[] { delim }, max, StringSplitOptions.RemoveEmptyEntries).ToList();
		}

		public static DateTime TimestampToDateTimeUtc1900(uint timestamp)
		{
			DateTime dt = new DateTime(1900, 1, 1);
			dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
			dt = dt.AddSeconds(timestamp);
			return dt;
		}

		public static uint DateTimeToTimestampUtc1900(DateTime dt)
		{
			try
			{
				long epochTicks = new DateTime(1900, 1, 1).Ticks;
				long unixTime = (dt.Ticks - epochTicks) / TimeSpan.TicksPerSecond;
				return (uint)unixTime;
			}
			catch (Exception)
			{
				return 0;
			}
		}

		public static DateTime TimestampToDateTimeUtc(int timestamp)
		{

			DateTime dt = new DateTime(1970, 1, 1);
			dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
			dt = dt.AddSeconds(timestamp);
			return dt;
		}

		public static int DateTimeToTimestampUtc(DateTime dt)
		{
			try
			{
				long epochTicks = new DateTime(1970, 1, 1).Ticks;
				long unixTime = (dt.Ticks - epochTicks) / TimeSpan.TicksPerSecond;
				return (int)unixTime;
			}
			catch (Exception)
			{
				return 0;
			}
		}

		public static string GetHashSh256(string value)
		{
			SHA256 sha256Hash = SHA256.Create();
			byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(value));
			StringBuilder hash = new StringBuilder();
			foreach (byte theByte in data)
			{
				hash.Append(theByte.ToString("x2"));
			}
			return hash.ToString();
		}

		public static string GetIp4AddrFromHostname(string host)
		{
			IPHostEntry hostEntry = null;
			if (IPAddress.TryParse(host, out _) == true) return host;

			try
			{
				hostEntry = Dns.GetHostEntry(host);
			}
			catch (Exception ex)
			{
				//Logging.Instance.Warn(TAG, nameof(SelectIp4Addr), $"dns request failed, host={host} ex={ex}");
				return host;
			}

			if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
			{
				//Logging.Instance.Warn(TAG, nameof(SelectIp4Addr), $"dns request failed, no hostEntry, host={host}");
				return host;
			}

			string ipv4Str = null;
			for (int i = 0; i < hostEntry.AddressList.Length; i++)
			{
				IPAddress ipAddr = hostEntry.AddressList[i];
				if (ipv4Str == null && ipAddr.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
				{
					// ipv4 address
					ipv4Str = ipAddr.ToString();
				}
				//Logging.Instance.Debug(TAG, nameof(SelectIp4Addr),
				//	$"{i + 1}: ipAddr={ipAddr} mapToIPv4={ipAddr.MapToIPv4()} addressFamily={ipAddr.AddressFamily}");
			}
			//Logging.Instance.Debug(TAG, nameof(SelectIp4Addr), $"ipv4addr = {ipv4Str}");
			return ipv4Str;
		}
	}
}
