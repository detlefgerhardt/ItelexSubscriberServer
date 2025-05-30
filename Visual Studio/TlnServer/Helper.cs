using System.Globalization;
using System.Reflection;

namespace TlnServer
{
	internal static class Helper
	{
		public static string GetVersionMessage()
		{
			DateTime buildTime = GetBuildDate(Assembly.GetExecutingAssembly());
			//buildTime = buildTime.ToLocalTime();
#if DEBUG
			// show date and time in debug version
			//string buildTime = Properties.Resources.BuildDate.Trim(new char[] { '\n', '\r' }) + " Debug";
			//string buildTime = ConfigurationManager.AppSettings.Get("builddate") + " Debug";
			string buildTimeStr = $"{buildTime:dd.MM.yyyy HH:mm} Debug";
#else
			// show only date in release version
			//string buildTime = Properties.Resources.BuildDate.Trim(new char[] { '\n', '\r' });
			string buildTimeStr = buildTime.ToString("dd.MM.yyyy");
#endif
			string versionStr = GetVersionNumber();
			string betaStr = Constants.BETA ? "beta" : "";
			return $"V{versionStr}{betaStr}  (Build={buildTimeStr})";
		}

		public static string GetVersionNumber()
		{
			Version? version = Assembly.GetEntryAssembly().GetName().Version;
			return $"{version.Major}.{version.Minor}.{version.Build}";
		}

		public static DateTime GetBuildDate(Assembly assembly)
		{
			const string BuildVersionMetadataPrefix = "+build";

			var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (attribute?.InformationalVersion != null)
			{
				var value = attribute.InformationalVersion;
				var index = value.IndexOf(BuildVersionMetadataPrefix);
				if (index > 0)
				{
					value = value.Substring(index + BuildVersionMetadataPrefix.Length);
					if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
					{
						return result.ToLocalTime();
					}
				}
			}
			return default;
		}

#if false
		private static object _sessionLock = new object();

		private const string SESSION_NAME = "session.dat";

		public static int GetNewSessionNo(int lastSessionNo)
		{
			lock (_sessionLock)
			{
				try
				{
					string fullName = Path.Combine(GetExePath(), SESSION_NAME);
					int sessionNo = lastSessionNo;
					if (File.Exists(fullName))
					{
						string[] lines = File.ReadAllLines(fullName);
						if (lines.Length > 0)
						{
							if (int.TryParse(lines[0], out int result))
							{
								sessionNo = result;
							}
						}
					}
					sessionNo++;
					File.WriteAllText(fullName, $"{sessionNo}\r\n");
					return sessionNo;
				}
				catch (Exception)
				{
					return lastSessionNo + 1;
				}
			}
		}
#endif

		public static string GetExePath()
		{
			return AppDomain.CurrentDomain.BaseDirectory;
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
	}
}
