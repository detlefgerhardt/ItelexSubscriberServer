using CentralexServer.CentralexConnections;
using Newtonsoft.Json;
using ServerCommon.Logging;
using ServerCommon.WebServer;
using System.Net;
using System.Text;

namespace CentralexServer.WebServer
{
	public class ResponsePages
	{
		private const string TAG = nameof(ResponsePages);
		private const string TAG2 = "";

		private HttpListenerResponse _response;
		private string _version;
		private Logger _logger;

		public ResponsePages(HttpListenerResponse response, string version)
		{
			_response = response;
			_version = version;
			_logger = GlobalData.Logger;
		}

		public string MakeHtmlPage(string title, string page, short refresh = 0, string refreshUrl = "")
		{
			string buffer;

			buffer = $"<html><head><title>{title}</title>\r\n";
			buffer += "<meta http-equiv=\"CACHE-CONTROL\" content=\"no-store\">\r\n";
			if (refresh > 0 && refreshUrl != "")
				buffer += "<meta http-equiv=\"refresh\" content=\"" + Convert.ToSingle(refresh) + "; URL=" + refreshUrl + "\">\r\n";
			buffer += "</head>\r\n<body>\r\n";
			buffer += page;
			buffer += "</body>\r\n</html>\r\n";
			return buffer;
		}

		/// <summary>
		/// Sendet eine Datei (Text oder Image)
		/// </summary>
		/// <param name="contentType"></param>
		/// <param name="path"></param>
		/// <param name="url"></param>
		/// <returns></returns>
		public HttpListenerResponse SendPageFile(string contentType, string path, string url)
		{
			try
			{
				byte[] bytes = File.ReadAllBytes(path);
				return SendPageBytes(bytes, contentType, HttpStatusCode.OK, null, true);
			}
			catch (Exception)
			{
				return SendPageNotFound(url);
			}
		}

		public HttpListenerResponse SendIndexHtml(string path, string url, string name, string pwd)
		{
			string html = null;
			string js = null;
			string css = null;
			string svg = null;
			try
			{
				html = File.ReadAllText(path + "/index.html");
				js = File.ReadAllText(path + "/main.js");
				css = File.ReadAllText(path + "/main.css");
				svg = File.ReadAllText(path + "/connected.svg");
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(SendIndexHtml), TAG2, "", ex);
				return SendPageNotFound(url);
			}

			WebHeaderCollection headers = new WebHeaderCollection();
			headers.Add(HttpResponseHeader.CacheControl, "no-cache");

			if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(pwd))
			{
				// no name and password
				headers.Add(HttpResponseHeader.WwwAuthenticate, "Basic realm=\"index\", charset=\"UTF-8\"");
				return SendPageStr("", Server.MIME_HTML, HttpStatusCode.Unauthorized, headers, true);
			}
			else if (name != GlobalData.Config.WebServerUser || pwd != GlobalData.Config.WebServerPwd)
			{
				// wrong name or password
				headers.Add(HttpResponseHeader.WwwAuthenticate, "Basic realm=\"index\", charset=\"UTF-8\"");
				return SendPageStr("", Server.MIME_HTML, HttpStatusCode.Unauthorized, headers, true);
			}

			string head = "<style>{css}</style><script>{js}</script>";
			head = head.Replace("{css}", css);
			head = head.Replace("{js}", js);

			html = html.Replace("<!--INSERT HEAD CONTENT HERE-->", head);
			html = html.Replace("<!--INSERT SVG HERE-->", svg);
			html = html.Replace("#INSERT VERSION HERE#", Helper.GetVersionNumber());

			return SendPageStr(html, Server.MIME_HTML, HttpStatusCode.OK, headers, true);

		}

		public HttpListenerResponse SendUpdate(string url)
		{
			WebHeaderCollection headers = new WebHeaderCollection();
			headers.Add(HttpResponseHeader.CacheControl, "no-store");

			int lastChanged = Helper.DateTimeToTimestampUtc(CentralexConnectionManager.Instance.GetClientLastChanged());
			string jsonStr = JsonConvert.SerializeObject(new WebUpdate(lastChanged));
			return SendPageStr(jsonStr, Server.MIME_JSON, HttpStatusCode.OK, headers, true);
		}

		public HttpListenerResponse SendData(string path, string url)
		{
			string data = File.ReadAllText(path + "/data.json");

			WebHeaderCollection headers = new WebHeaderCollection();
			headers.Add(HttpResponseHeader.CacheControl, "no-store");

			return SendPageStr(data, Server.MIME_JSON, HttpStatusCode.OK, headers, true);
		}

		public HttpListenerResponse SendTable(string path, string url)
		{
			//string data = File.ReadAllText(path + "/table.json");

			WebData webData = WebServerManager.Instance.GetWebData();
			/*
			foreach (WebClientItem item in webData.Clients)
			{
				Console.WriteLine($"{item.number} {item.status} {item.last_change} {Helper.TimestampToDateTimeUtc(item.last_change)}");
			}
			Console.WriteLine(webData.FreePorts);
			*/

			string jsonStr = "{}";
			try
			{
				jsonStr = JsonConvert.SerializeObject(webData);
			}
			catch(Exception)
			{
			}

			WebHeaderCollection headers = new WebHeaderCollection();
			headers.Add(HttpResponseHeader.CacheControl, "no-store");

			return SendPageStr(jsonStr, Server.MIME_JSON, HttpStatusCode.OK, headers, true);
		}

		public HttpListenerResponse SendPageNotFound(string url)
		{
			return SendPageStr(url + " not found", Server.MIME_HTML, HttpStatusCode.NotFound);
		}

		public HttpListenerResponse HttpFaultHeader(string errStr, HttpStatusCode statusCode)
		{
			return SendPageStr(errStr, Server.MIME_HTML, statusCode);
		}

		public HttpListenerResponse SendJson(string jsonStr, bool doLog = false)
		{
			//HttpListenerResponse response = _context.Response;

			WebHeaderCollection headers = new WebHeaderCollection();
			//headers.Add("X-JSON", jsonStr);
			headers.Add(HttpResponseHeader.Pragma, "no-cache");
			headers.Add(HttpResponseHeader.CacheControl, "post-check=0, pre-check=0");

			return SendPageStr(jsonStr, Server.MIME_HTML, HttpStatusCode.OK, headers);
		}

		public HttpListenerResponse SendPageStr(string pageStr, string contentType = Server.MIME_HTML,
			HttpStatusCode statusCode = HttpStatusCode.OK, WebHeaderCollection headers = null, bool doLog = false)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(pageStr);
			return SendPageBytes(bytes, contentType, statusCode, headers, doLog);
		}

		public HttpListenerResponse SendPageBytes(byte[] bytes, string contentType = Server.MIME_HTML,
			HttpStatusCode statusCode = HttpStatusCode.OK, WebHeaderCollection headers = null, bool doLog = false)
		{
			if (headers == null)
				headers = new WebHeaderCollection();
			_response.ContentType = contentType;
			headers.Add(HttpResponseHeader.ContentType, contentType);
			// funktioniert nicht, da HTTP.SYS immer seinen eigenen Server-Eintrag erzeugt
			headers.Add(HttpResponseHeader.Server, _version);
			_response.Headers = headers;

			_response.ContentLength64 = bytes.Length;
			_response.StatusCode = (int)statusCode;

			_response.OutputStream.Write(bytes, 0, bytes.Length);

			return _response;
		}

	}
}
