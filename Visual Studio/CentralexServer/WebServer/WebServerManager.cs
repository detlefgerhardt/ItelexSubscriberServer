using CentralexServer.CentralexConnections;
using ItelexTlnServer.Data;
using ServerCommon.Logging;
using ServerCommon.Private;
using ServerCommon.WebServer;
using System.Net;

namespace CentralexServer.WebServer
{
	class WebServerManager
	{
		private const string TAG = nameof(WebServerManager);
		private const string TAG2 = "";

		// show: netsh http show urlacl
		// add : netsh http add urlacl url=http://+:4885/ user=dg1\detlef
		// del: netsh http delete urlacl url=http://+:4885/

		//public const string INDEX_USER = PrivateConstants.INDEX_USER;
		//public const string INDEX_PWD = PrivateConstants.INDEX_PWD;

		private const string HOST_IP = "+";
		private const int HOST_PORT = 4885;

		private Server _webServer;

		private readonly Logger _logger;

		public string Url;

		private bool disposed;

		private static WebServerManager instance;
		public static WebServerManager Instance => instance ??= new WebServerManager();

		private WebServerManager()
		{
			_logger = GlobalData.Logger;
			//UpdateWebData(CentralexManager.Instance.ClientList);
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
			string[] prefixes = [$"http://{HOST_IP}:{HOST_PORT}/"];
			_logger.Debug(TAG, nameof(Start), TAG2, $"start webserver {prefixes[0]}");

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
				_logger.Error(TAG, nameof(Start), TAG2, "Error starting webserver", ex);
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

		public WebData GetWebData()
		{
			List<WebClientItem> webItems = new List<WebClientItem>();
			List<ClientItem> clients = CentralexConnectionManager.Instance.ClientList;
			if (clients != null)
			{
				foreach (ClientItem client in clients)
				{
					WebClientItem webItem = new WebClientItem()
					{
						name = client.Name,
						number = client.Number,
						port = client.Port,
						status = WebClientItem.StatusToStr(client.StateEnum, client.DisconnectReason),
						last_change = Helper.DateTimeToTimestampUtc(client.LastChangedUtc),
					};
					webItems.Add(webItem);
				}
			}

			return new WebData()
			{
				Clients = webItems.ToArray(),
				FreePorts = CentralexConnectionManager.Instance.GetFreePorts(),
				LastChanged = Helper.DateTimeToTimestampUtc(CentralexConnectionManager.Instance.GetClientLastChanged())
			};
		}
	}
}
