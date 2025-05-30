using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;

namespace ServerCommon.Logging
{
	/// <summary>
	/// RFC 3164 syslog client
	/// format: "<PRIVAL>TIMESTAMP HOSTNAME TAG: MESSAGE"
	/// </summary>
	public class SysLog
	{
		public enum Facility
		{
			kernel = 0,
			user = 1,
			mail = 2,
			daemon = 3,
			auth = 4,
			syslog = 5,
			lpr = 6,
			news = 7,
			uucp = 8,
			cron = 9,
			authpriv = 10,
			ftp = 11,
			ntp = 12,
			logaudit = 13,
			logalert = 14,
			clock = 15,
			local0 = 16,
			local1 = 17,
			local2 = 18,
			local3 = 19,
			local4 = 20,
			local5 = 21,
			local6 = 22,
			local7 = 23
		}

		public enum Severity
		{
			Emergency = 0,
			Alert = 1,
			Critical = 2,
			Error = 3,
			Warning = 4,
			Notice = 5,
			Informational = 6,
			Debug = 7
		}

		private SysLogServer[] _syslogServers;

		public SysLog(SysLogServer[] syslogServers)
		{
			_syslogServers = syslogServers;
		}

		public void Log(LogTypes logType, string msg)
		{
			Log(logType, "", "", msg);
		}

		public void Log(LogTypes logType, string tag, string method, string msg)
		{
			Severity severity = LogTypeToSeverity(logType);
			string sysLogTag = $"[{tag}][{method}]";
			Log(severity, sysLogTag, msg);
		}

		public void Log(Severity severity, string sysLogTag, string msg)
		{
			foreach (SysLogServer s in _syslogServers)
			{
				int pri = (int)s.Facility * 8 + (int)severity;
				string utcNowStr = DateTime.UtcNow.ToString("MMM dd HH':'mm':'ss");
				utcNowStr = utcNowStr.Replace(".", "");
				string syslogMsg = $"<{pri}>{utcNowStr} {s.ClientName} {sysLogTag}: {msg}";

				UdpClient udpClient = new UdpClient();
				IPEndPoint ipEndPoint = new IPEndPoint(s.ServerIp, s.Port);
				//IPAddress ipAddress = Dns.GetHostEntry(_host).AddressList[3];
				//IPAddress ipAddress = IPAddress.Parse(_host);

				byte[] data = Encoding.ASCII.GetBytes(syslogMsg);
				try
				{
					udpClient.Send(data, data.Length, ipEndPoint);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.ToString());
				}
			}
		}

		public static Severity LogTypeToSeverity(LogTypes logType)
		{
			switch (logType)
			{
				case LogTypes.Fatal:
					return Severity.Emergency;
				case LogTypes.Error:
					return Severity.Error;
				case LogTypes.Warn:
					return Severity.Warning;
				case LogTypes.Notice:
					return Severity.Notice;
				case LogTypes.Info:
					return Severity.Informational;
				case LogTypes.Debug:
					return Severity.Debug;
				default:
					return Severity.Debug;
			}
		}

	}
}
