using CentralexServer.Data;
using ItelexTlnServer.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Utilities;
using ServerCommon;
using ServerCommon.Logging;
using ServerCommon.Mail;
using ServerCommon.Utility;
using SQLitePCL;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using TlnServer.BinaryServer;
using TlnServer.Config;
using TlnServer.WebServer;

namespace TlnServer
{
	internal class Program
	{
		private const string TAG = nameof(Program);
		private const string TAG2 = "";

		static void Main(string[] args)
		{

			//IPAddress ipv4Str = CommonHelper.GetIp4AddrFromHostname("itelex.srvdns.de");

			GlobalData.PlatformId = Environment.OSVersion.Platform;
			string version = Helper.GetVersionMessage();

			Logger logger = new Logger(Constants.LOG_PATH, Constants.LOG_NAME, LogTypes.Debug, null);
			GlobalData.Logger = logger;

			GlobalData.Config = ConfigManager.Instance.LoadConfig(logger);
			if (GlobalData.Config == null)
			{
				logger.ConsoleLog(null, null, TAG2, $"error loading {Constants.TLNSERVER_CONFIG_NAME}");
				Environment.Exit(1);
			}

			/*
			var builder = Host.CreateApplicationBuilder(args);
			builder.Services.AddSystemd();
			builder.Services.AddHostedService<Worker>();
			IHost host = builder.Build();
			await host.RunAsync();
			*/

			if (GlobalData.PlatformId == PlatformID.Win32NT)
			{
				DisableQuickEditMode();
				Console.SetWindowSize(132, 30);
			}

			SysLogServer[] sysLogSevers =
				(from s in GlobalData.Config.Syslog
				 select new SysLogServer(s.Server, s.Port, s.Name, s.SeverityEnum, s.Facility)).ToArray();
			foreach(SysLogServer s in sysLogSevers)
			{
				logger.Info(TAG, nameof(Main), TAG2, $"syslog: {s}");
			}

			GlobalData.SysLogger = new SysLog(sysLogSevers);
			logger.SetSysLog(GlobalData.SysLogger);
			logger.SetLogLevel(GlobalData.Config.LogLevel);

			logger.Allways(TAG, nameof(Main), TAG2,
				$"------ Start #{GlobalData.Config.ServerName} {version} {GlobalData.PlatformId}# ------");

			Console.WriteLine("\r\n\r\n");
			logger.ConsoleLog(null, null, TAG2, $"{GlobalData.Config.ServerName} {version} {GlobalData.PlatformId}");
			logger.ConsoleLog(TAG, nameof(Main), TAG2, $"hostname {GlobalData.Config.ServerHostName}");
			logger.ConsoleLog(TAG, nameof(Main), TAG2, $"running in {Helper.GetExePath()}");

			string ipMsg = CommonHelper.GetLocalIpMessage();
			logger.ConsoleLog(TAG, nameof(Main), TAG2, ipMsg);

			//TlnServerMsDatabase.Instance.Backup();
			//TlnServerMsDatabase.Instance.TeilnehmerBackupCsv();

			if (GlobalData.Config.Syslog.Length > 0)
			{
				foreach (SysLogConfigItem c in GlobalData.Config.Syslog)
				{
					logger.ConsoleLog(TAG, nameof(Main), TAG2, $"syslog {c}");
				}
			}
			else
			{
				logger.ConsoleLog(TAG, nameof(Main), TAG2, $"syslog none");
			}

			// server start notification by mail
			MailAgent ma = new MailAgent(logger, null);
			string mailMsg = $"TlnServer {GlobalData.Config.ServerName} restart";
			ma.SendMailSmtp2(["mail@dgerhardt.de"], mailMsg, mailMsg, null, null);

			TlnServerMsDatabase.Instance.UpdateAnswerback();

			BinaryServerManager.Instance.SetRecvOn();
			WebServerManager.Instance.Start();

			Tests();

			logger.ConsoleLog(null, null, TAG2, "Press Ctrl-C to stop");
			//while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
			while (true)
			{
					Thread.Sleep(100);
			}

			GlobalData.Logger.Info(TAG, nameof(Main), TAG2, $"------ Stop {GlobalData.Config.ServerName} {version} ------");
			Environment.Exit(0);
		}

		private static void Tests()
		{
			// test read
			List<TeilnehmerItem> tlns = TlnServerMsDatabase.Instance.TeilnehmerLoadAllAdmin();
			//ConfigManager.Instance.LoadSyncServerConfig(logger);

			//BinaryServerManager.Instance.DoFullQuery("itelex.srvdns.de", false);
			//BinaryServerManager.Instance.TestSyncLogin();

			//TaskMonitor.Instance.SetLogger(logger);

			//BinaryServerManager.Instance.TestSendSyncLoginBurst();
			/*
			List<TeilnehmerItem> tlns = TlnServerMsDatabase.Instance.TeilnehmerLoadAllAdminNoPin();
			StringBuilder sb = new StringBuilder();
			foreach(TeilnehmerItem tln in tlns)
			{
				sb.AppendLine(tln.ToString());
			}
			File.WriteAllText("nopin.txt", sb.ToString());
			*/

			/*
			int[] removeList = new int[] { 11200, 11201, 4184019, 123457, 124578, 2108219, 242347, 28821, 413804,
				4184019, 512221, 647501, 647502, 71920, 721310, 886747, 91113, 981692};
			BinaryServerManager.Instance.RemoveByList(removeList);
			*/
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
