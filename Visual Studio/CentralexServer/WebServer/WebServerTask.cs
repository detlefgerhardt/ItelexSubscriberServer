using System.Net;
using ServerCommon.Logging;
using ServerCommon.WebServer;

namespace CentralexServer.WebServer
{
	class WebServerTask
	{
		private const string TAG = nameof(WebServerTask);
		private const string TAG2 = "";

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

			//_logger.Debug(TAG, nameof(Start), $"{request.Url}");

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
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				content = reader.ReadToEnd();
			}

			ResponsePages pages = new ResponsePages(_context.Response, Helper.GetVersionMessage());

			return WebRecvHandler(url);
		}

		private HttpListenerResponse WebRecvHandler(UrlParameter url)
		{
			url.Path = $"{Constants.WEB_PATH}";
			url.Fullname = url.Path + Constants.BS + url.Fullname;

			ResponsePages pages = new ResponsePages(_context.Response, Helper.GetVersionMessage());

			HttpListenerResponse response;
			switch (url.PlainUrl)
			{
				case "/info":
					response = pages.SendPageStr($"{Helper.GetVersionMessage()}");
					break;
				case "/index.html":
				case "/":
					_logger.ConsoleLog(TAG, nameof(WebRecvHandler), TAG2, $"request {url.PlainUrl}");
					response = pages.SendIndexHtml(url.Path, url.PlainUrl, _basicAuthName, _basicAuthPassword);
					break;
				case "/update":
					response = pages.SendUpdate(url.PlainUrl);
					break;
				case "/data":
					response = pages.SendData(url.Path, url.PlainUrl);
					break;
				case "/table":
					response = pages.SendTable(url.Path, url.PlainUrl);
					break;
				/*
				case "/test.json":
					//headerLines = new List<string> { "X-JSON: {\"euro\":100,\"dollar\":120,\"pound\":130}" };
					response = pages.SendJson("{\"euro\":100,\"dollar\":120,\"pound\":130}");
					break;
				*/
				default:
					switch (url.Ext)
					{
						case "html":
						case "htm":
							//Console.WriteLine("send html");
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
	}
}
