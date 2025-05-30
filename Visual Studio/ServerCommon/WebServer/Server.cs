using ServerCommon.Logging;
using System.Diagnostics;
using System.Net;

namespace ServerCommon.WebServer
{
	// Cmd: netsh http add urlacl url=http://+:4885/ user=dg1\detlef
	//		netsh http show urlacl 
	//		netsh http del urlacl url=http://+:4885/

	public class Server: IDisposable
	{
		private const string TAG = nameof(Server);

		public const string MIME_PLAIN = "text/plain";
		public const string MIME_HTML = "text/html; charset=utf-8";
		public const string MIME_XML = "text/xml";
		public const string MIME_JS = "text/javascript";
		public const string MIME_CSS = "text/css";
		public const string MIME_JSON = "application/json";
		public const string MIME_PNG = "image/png";
		public const string MIME_EVENTSTREAM = "text/event-stream";
		public const string MIME_ICO = "image/x-icon";

		private readonly HttpListener _listener = new HttpListener();
		private readonly Func<HttpListenerContext, HttpListenerResponse> _responderMethod;

		private readonly Logger _webLogger;

		private bool disposed;

		public Server(string[] prefixes, Func<HttpListenerContext, HttpListenerResponse> method, Logger logger)
		{
			_webLogger = logger;

			if (!HttpListener.IsSupported) throw new NotSupportedException("HttpListener not supported on this OS");

			// URI prefixes are required, for example 
			// "http://localhost:8085/index/".
			if (prefixes == null || prefixes.Length == 0) throw new ArgumentException("prefixes");

			// A responder method is required
			if (method == null) throw new ArgumentException("method");

			foreach (string s in prefixes)
			{
				_listener.Prefixes.Add(s);
			}

			_responderMethod = method;
			_listener.Start();
		}

		//public Server(Func<HttpListenerContext, HttpListenerResponse> method, IWebLogger logger, string[] prefixes)
		//	: this(prefixes, method, logger) { }

		~Server()
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
				Stop();
			}

			// clean up unmanaged resources
		}


		public void Run()
		{
			ThreadPool.QueueUserWorkItem(o =>
			{
				//_logger.Info(SECTION, "Webserver running...");
				try
				{
					while (_listener.IsListening)
					{
						ThreadPool.QueueUserWorkItem(c =>
						{
							var ctx = c as HttpListenerContext;
							try
							{
								HttpListenerResponse data = _responderMethod(ctx);

								//string header =
								//		"HTTP/1.1 200 OK\r\n" +
								//		"Date: " + DateTime.Now.ToShortDateString() + "\r\n" +
								//		"Content-Type: " + contentType + "; charset=iso-8859-1\r\n" +
								//		"Content-Length: " + Convert.ToString(len) + "\r\n" +
								//		"Server: " + Konstanten.prgmString + "\r\n" +
								//		"Pragme: no-cache\r\n" +
								//		"Cache-Control: post-check=0, pre-check=0\r\n" +
								//		"Connection: Keep-Alive\r\n";

								//var context = (HttpListenerContext)listenerContext;
								//context.Response.StatusCode = (int)HttpStatusCode.OK;
								//context.Response.AddHeader("Content-Type", "text/html; charset=utf-8");
								//var msg = Encoding.UTF8.GetBytes("<h1>Hello World</h1>");
								//context.Response.ContentLength64 = msg.Length;
								//context.Response.OutputStream.Write(msg, 0, msg.Length);
								//context.Response.OutputStream.Close();
							}
							catch (Exception ex)
							{
								Debug.WriteLine(ex.Message);
							}
							finally
							{
								// always close the stream
								ctx.Response.OutputStream.Close();
							}
						}, _listener.GetContext());
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
					_webLogger.Error(TAG, nameof(Run), "", ex);
				}
			});
		}

		public void Stop()
		{
			_listener.Stop();
			_listener.Close();
		}
	}
}
