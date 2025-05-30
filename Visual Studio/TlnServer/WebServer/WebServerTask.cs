using System.Diagnostics;
using System.Net;
using ServerCommon.WebServer;
using CentralexServer.Data;
using TlnServer.Config;
using ServerCommon.Logging;

namespace TlnServer.WebServer
{
	class WebServerTask
	{
		private const string TAG = nameof(WebServerTask);
		private const string TAG2 = "";

		private const string SRV_WEB = "web";

		private readonly Logger _logger;

		private string _basicAuthName;
		private string _basicAuthPassword;

		private readonly HttpListenerContext _context;

		public WebServerTask(HttpListenerContext context)
		{
			_context = context;
			_logger = GlobalData.Logger;
		}

		public HttpListenerResponse Start()
		{
			HttpListenerRequest request = _context.Request;

			_logger.Debug(TAG, nameof(Start), TAG2, $"requested url={request.Url}");

			// check for basic authentication
			Authentication authentication = new Authentication();
			string[] namePwd = authentication.GetBasicAuthenticationFromHeader(request);
			if (namePwd != null)
			{
				_basicAuthName = namePwd[0];
				_basicAuthPassword = namePwd[1];
			}

			UrlParameter url = new UrlParameter(request.RawUrl, Constants.BS);

			string content;
			using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				content = reader.ReadToEnd();
			}

			ResponsePages pages = new ResponsePages(_context.Response, Helper.GetVersionMessage());

			HttpListenerResponse response;
			response = WebRecvHandler(url, content);

			return response;
		}

		private HttpListenerResponse WebRecvHandler(UrlParameter url, string content)
		{
			url.Path = $"{Constants.WEB_PATH}";
			url.Fullname = url.Path + Constants.BS + url.Fullname;

			ResponsePages pages = new ResponsePages(_context.Response, Helper.GetVersionMessage());

			HttpListenerResponse response;
			switch (url.PlainUrl)
			{
				case "/info":
					response = pages.SendPageStr($"{GlobalData.Config.ServerName} {Helper.GetVersionMessage()}");
					break;
				case "/index.html":
				case "/":
					_logger.Debug(TAG, nameof(WebRecvHandler), TAG2, $"request {url.PlainUrl}");
					TlnServerMsDatabase.Instance.TeilnehmerCacheClear();
					response = pages.SendIndexHtml(url.Path, url.PlainUrl, _basicAuthName, _basicAuthPassword);
					break;
				case "/download":
					_logger.Info(TAG, nameof(WebRecvHandler), TAG2, $"request {url.PlainUrl}");
					response = pages.SendDownload(url, _basicAuthName, _basicAuthPassword);
					break;
				case "/list":
					response = pages.SendList(content);
					break;
				case "/getSalt":
					response = pages.SendGetSalt();
					break;
				case "/edit":
					_logger.Info(TAG, nameof(WebRecvHandler), TAG2, $"request {url.PlainUrl}");
					response = pages.SendEdit(content);
					break;
				case "/test.json":
					//headerLines = new List<string> { "X-JSON: {\"euro\":100,\"dollar\":120,\"pound\":130}" };
					response = pages.SendJson("{\"euro\":100,\"dollar\":120,\"pound\":130}");
					break;
				default:
					switch (url.Ext)
					{
						case "html":
						case "htm":
							response = pages.SendPageFile(Server.MIME_HTML, url.Fullname, url.PlainUrl);
							break;
						case "txt":
							response = pages.SendPageFile(Server.MIME_PLAIN, url.Fullname, url.PlainUrl);
							break;
						case "xml":
							response = pages.SendPageFile(Server.MIME_XML, url.Fullname, url.PlainUrl);
							break;
						case "js":
							response = pages.SendPageFile(Server.MIME_JS, url.Fullname, url.PlainUrl);
							break;
						case "css":
							response = pages.SendPageFile(Server.MIME_CSS, url.Fullname, url.PlainUrl);
							break;
						case "png":
							response = pages.SendPageFile(Server.MIME_PNG, url.Fullname, url.PlainUrl);
							break;
						case "json":
							response = pages.SendPageFile(Server.MIME_PNG, url.Fullname, url.PlainUrl);
							break;
						case "ico":
							response = pages.SendPageFile(Server.MIME_ICO, url.Fullname, url.PlainUrl);
							break;
						case "ttf":
						case "woff":
						case "woff2":
							response = pages.SendPageFile(Server.MIME_HTML, url.Fullname, url.PlainUrl);
							break;
						default:
							response = pages.SendPageFile(Server.MIME_PLAIN, url.Fullname, url.PlainUrl);
							break;
					}
					break;
			}

			if (response == null)
			{
				response = pages.SendPageNotFound(url.Fullname);
			}

			return response;
		}

#if false
		private int GetNewSessionId()
		{
			Random rand = new Random();
			while (true)
			{
				int id = rand.Next();
				if (GetSession(id) == null) return id;
			}
		}

		private void DeleteSession(int sessionId)
		{
			ConnectServerSession session = GetSession(sessionId);
			if (session != null) _sessions.Remove(session);
		}

		private void DeleteSession(string username)
		{
			ConnectServerSession session = GetSession(username);
			if (session != null) _sessions.Remove(session);
		}

		private ConnectServerSession GetSession(int sessionId)
		{
			return (from s in _sessions where s.SessionId == sessionId select s).FirstOrDefault();
		}

		private ConnectServerSession GetSession(string username)
		{
			return (from s in _sessions where s.Username == username select s).FirstOrDefault();
		}
#endif
	}
}
