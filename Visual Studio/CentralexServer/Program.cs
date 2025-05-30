using Centralex.BinaryProxy;
using CentralexServer.CentralexConnections;
using CentralexServer.Config;
using CentralexServer.Data;
using CentralexServer.WebServer;
using ServerCommon;
using ServerCommon.Logging;
using ServerCommon.Mail;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CentralexServer
{
	internal class Program
	{
		private const string TAG = nameof(Program);
		private const string TAG2 = "";

		static void Main(string[] args)
		{
			GlobalData.PlatformId = Environment.OSVersion.Platform;
			string version = Helper.GetVersionMessage();

			Logger logger = new Logger(Constants.LOG_PATH, Constants.LOG_NAME, LogTypes.Debug, null);
			GlobalData.Logger = logger;
			GlobalData.Config = ConfigManager.Instance.LoadConfig(logger);
			if (GlobalData.Config == null)
			{
				logger.ConsoleLog(null, null, TAG2, $"error loading {Constants.CONFIG_NAME}");
				Environment.Exit(1);
			}

			if (GlobalData.PlatformId == PlatformID.Win32NT)
			{
				DisableQuickEditMode();
				Console.SetWindowSize(132, 30);
			}

			logger.SetLogLevel(GlobalData.Config.LogLevel);
			SysLogServer[] sysLogSevers =
				(from s in GlobalData.Config.Syslog
				 select new SysLogServer(s.Server, s.Port, s.Name, s.SeverityEnum, s.Facility)).ToArray();
			GlobalData.SysLogger = new SysLog(sysLogSevers);
			logger.SetSysLog(GlobalData.SysLogger);

			// TaskMonitor.Instance.SetLogger(logger);

			logger.Allways(TAG, nameof(Main), TAG2,
					$"------ Start #{GlobalData.Config.ServerName} {version} {GlobalData.PlatformId}# ------");

			Console.WriteLine("\r\n\r\n");
			logger.ConsoleLog(null, null, TAG2, $"{GlobalData.Config.ServerName} {version} {GlobalData.PlatformId}");
			logger.ConsoleLog(null, null, TAG2, $"running in {Helper.GetExePath()}");
			logger.Info(TAG, nameof(Main), TAG2, $"running in {Helper.GetExePath()}");

			if (GlobalData.Config.Syslog.Length > 0)
			{
				foreach (SysLogConfigItem c in GlobalData.Config.Syslog)
				{
					logger.ConsoleLog(null, null, TAG2, $"syslog {c}");
					logger.Info(TAG, nameof(Main), TAG2, $"syslog {c}");
				}
			}
			else
			{
				logger.ConsoleLog(null, null, TAG2, $"syslog none");
				logger.Info(TAG, nameof(Main), TAG2, $"syslog none");
			}

			MailAgent ma = new MailAgent(logger, null);
			string mailMsg = $"Centralex {GlobalData.Config.ServerName} restart";
			ma.SendMailSmtp2(["mail@dgerhardt.de"], mailMsg, mailMsg, null, null);

			BinaryConnectionManager.Instance.SetRecvOn();
			CentralexConnectionManager.Instance.StartClientListener();
			CentralexConnectionManager.Instance.StartCallerListeners();
			WebServerManager.Instance.Start();

			logger.Debug(TAG, nameof(Main), "test", "tag2");

			logger.ConsoleLog(null, null, TAG2, "Press Ctrl-C to stop");

			if (GlobalData.PlatformId == PlatformID.Win32NT)
			{
				while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
				{
					Thread.Sleep(100);
				}
			}
			else
			{
				while (true)
				{
					Thread.Sleep(100);
				}
			}

			GlobalData.Logger.Info(TAG, nameof(Main), TAG2, $"------ Stop {GlobalData.Config.ServerName} {version} ------");
			Environment.Exit(0);
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		const uint ENABLE_QUICK_EDIT = 0x0040;

		// STD_INPUT_HANDLE (DWORD): -10 is the standard input device.
		const int STD_INPUT_HANDLE = -10;

		private static bool DisableQuickEditMode()
		{
			// Disable QuickEdit Mode
			// Quick Edit mode freezes the app to let users select text.
			// We don't want that. We want the app to run smoothly in the background.
			// - https://stackoverflow.com/q/4453692
			// - https://stackoverflow.com/a/4453779
			// - https://stackoverflow.com/a/30517482

			IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);
			//IntPtr consoleHandle = Process.GetCurrentProcess().MainWindowHandle;

			// get current console mode
			uint consoleMode;
			if (!GetConsoleMode(consoleHandle, out consoleMode))
			{
				// ERROR: Unable to get console mode.
				return false;
			}

			// Clear the quick edit bit in the mode flags
			consoleMode &= ~ENABLE_QUICK_EDIT;

			if (SetConsoleMode(consoleHandle, consoleMode))
			{
				// ERROR: Unable to set console mode.
				return false;
			}

			return true;
		}
	}
}
