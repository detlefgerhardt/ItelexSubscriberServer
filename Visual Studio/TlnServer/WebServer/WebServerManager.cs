using CentralexServer.Data;
using ItelexTlnServer.Data;
using ServerCommon.Logging;
using ServerCommon.Private;
using ServerCommon.WebServer;
using System.Net;

namespace TlnServer.WebServer
{
	class WebServerManager
	{
		private const string TAG = nameof(WebServerManager);
		private const string TAG2 = "";

		// show: netsh http show urlacl
		// add : netsh http add urlacl url=http://+:4880/ user=dg1\detlef
		// del: netsh http delete urlacl url=http://+:4880/

		//public const string INDEX_USER = PrivateConstants.INDEX_USER;
		//public const string INDEX_PWD = PrivateConstants.INDEX_PWD;

		public const string CSVLIST_V1_USER = PrivateConstants.CSVLIST_V1_USER;
		public const string CSVLIST_V1_PWD = PrivateConstants.CSVLIST_V1_PWD;

		//public const string EDIT_PWD = PrivateConstants.EDIT_PWD;

		private const string HOST_IP = "+";

		private Server _webServer;

		private readonly Logger _logger;

		private TlnServerMsDatabase _database;

		public string Url;

		private bool disposed;

		private static WebServerManager instance;
		public static WebServerManager Instance => instance ??= new WebServerManager();

		private WebServerManager()
		{
			_logger = GlobalData.Logger;
			_database = TlnServerMsDatabase.Instance;
		}

		/*
		~WebServerManager()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			if (!this.disposed)
			{
				this.Dispose(true);
				GC.SuppressFinalize(this);
				this.disposed = true;
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				// clean up managed resources
				_webServer.Stop();
			}

			// clean up unmanaged resources
		}
		*/

		public bool Start()
		{
			string[] prefixes = [$"http://{HOST_IP}:{GlobalData.Config.WebServerPort}/"];

			// test with two different ports
			//string[] prefixes = [$"http://{HOST_IP}:{GlobalData.Config.WebServerPort}/",
			//					$"http://{HOST_IP}:{4881}/"];
			//_logger.Info(TAG, nameof(Start), $"start webserver {prefixes[0]}");

			try
			{
				_webServer = new Server(prefixes, SendResponse, _logger);
				_webServer.Run();
				Url = prefixes[0];
				_logger.ConsoleLog(TAG, nameof(Start), TAG2, $"Webserver startet at {Url}");
				return true;
			}
			catch (Exception ex)
			{
				_logger.ConsoleLog(null, null, TAG2, $"Error starting Webserver at {Url}");
				_logger.Error(TAG, nameof(Start), TAG2, $"Error starting webserver at {Url}", ex);
				Url = "error";
				return false;
			}
		}

		public void Stop()
		{
			_webServer.Stop();
		}

		private HttpListenerResponse SendResponse(HttpListenerContext context)
		{
			WebServerTask job = new WebServerTask(context);
			HttpListenerResponse response = job.Start();
			return response;
		}

		public List<WebTeilnehmerItem> GetWebData()
		{
			List<WebTeilnehmerItem> webItems = new List<WebTeilnehmerItem>();

			List<TeilnehmerItem> tlnList = _database.TeilnehmerLoadAll();
			if (tlnList == null)
			{
				_logger.Fatal(TAG, nameof(GetWebData), TAG2, "error loding Teilnehmer from database");
				return webItems;
			}

			if (tlnList != null)
			{
				foreach (TeilnehmerItem tln in tlnList)
				{
					if (tln.Disabled || tln.Type == 0) continue;

					WebTeilnehmerItem webItem = new WebTeilnehmerItem()
					{
						uid = (ulong)tln.Uid,
						number = tln.Number,
						name = tln.Name,
						extension = tln.Extension,
						ipaddress = tln.IpAddress,
						hostname = tln.Hostname,
						port = tln.Port ?? 0,
						type = tln.Type,
						timestamp = Helper.DateTimeToTimestampUtc(tln.TimestampUtc),
					};
					webItems.Add(webItem);
				}
			}

			return webItems;
		}

		public List<WebTeilnehmerItemAdmin> GetWebDataAdmin()
		{
			List<WebTeilnehmerItemAdmin> webItems = new List<WebTeilnehmerItemAdmin>();

			List<TeilnehmerItem> tlnList = _database.TeilnehmerLoadAllAdmin();
			if (tlnList == null)
			{
				_logger.Fatal(TAG, nameof(GetWebData), TAG2, "error loding Teilnehmer from database");
				return webItems;
			}
			tlnList = (from t in tlnList where !t.Remove select t).ToList();

			if (tlnList != null)
			{
				foreach (TeilnehmerItem tln in tlnList)
				{
					//if (tln.disabled.GetValueOrDefault() || tln.type == 0) continue;

					WebTeilnehmerItemAdmin webItem = new WebTeilnehmerItemAdmin()
					{
						uid = (ulong)tln.Uid,
						number = tln.Number,
						name = tln.Name,
						extension = tln.Extension,
						ipaddress = tln.IpAddress,
						hostname = tln.Hostname,
						port = tln.Port ?? 0,
						type = tln.Type,
						disabled = tln.Disabled ? 1 : 0,
						timestamp = Helper.DateTimeToTimestampUtc(tln.TimestampUtc),
					};
					webItems.Add(webItem);
				}
			}

			return webItems;
		}
	}
}
