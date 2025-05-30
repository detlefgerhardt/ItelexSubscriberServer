using CentralexServer.Data;
using ItelexTlnServer.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerCommon.Logging;
using ServerCommon.Private;
using ServerCommon.Utility;
using ServerCommon.WebServer;
using System;
using System.Net;
using System.Text;
using System.Web;
using System.Xml.Linq;
using TlnServer.BinaryServer;

namespace TlnServer.WebServer
{
	public class ResponsePages
	{
		private const string TAG = nameof(ResponsePages);
		private const string TAG2 = "";

		//private const string EDIT_PWD = PrivateConstants.EDIT_PWD;

		private HttpListenerResponse _response;
		private string _version;
		private Logger _logger;
		private TlnServerMsDatabase _database;

		public ResponsePages(HttpListenerResponse response, string version)
		{
			_response = response;
			_version = version;
			_logger = GlobalData.Logger;
			_database = TlnServerMsDatabase.Instance;
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


		/*
		public HttpListenerResponse SendIco(string path, string url)
		{
			try
			{
				byte[] bytes = File.ReadAllBytes(path);
				return SendPageBytes(bytes, Server.MIME_ICO, HttpStatusCode.OK, null, true);
			}
			catch (Exception)
			{
				return SendPageNotFound(url);
			}
		}
		*/

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

		internal class EditResponse()
		{
			public bool successfull { get; set; }

			public EditMessage message { get; set; }
		}

		internal class EditMessage
		{
			public int code { get; set; }

			public string text { get; set; }
		}

		#region special urls

		public HttpListenerResponse SendIndexHtml(string path, string url, string name, string pwd)
		{
			string html = null;
			//string js = null;
			//string css = null;
			//string svg = null;
			try
			{
				html = File.ReadAllText(path + "/index.html");
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(SendIndexHtml), TAG2, "error", ex);
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

			return SendPageStr(html, Server.MIME_HTML, HttpStatusCode.OK, headers, true);

		}

		public HttpListenerResponse SendDownload(UrlParameter url, string name, string pwd)
		{
			WebHeaderCollection headers = new WebHeaderCollection();

			if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(pwd))
			{
				// no name and password
				headers.Add(HttpResponseHeader.WwwAuthenticate, "Basic realm=\"download\", charset=\"UTF-8\"");
				_logger.Warn(TAG, nameof(SendDownload), TAG2, $"url='{url.Url}' missing name/password");
				return SendPageStr("", Server.MIME_HTML, HttpStatusCode.Unauthorized, headers, true);
			}
			else if (name != WebServerManager.CSVLIST_V1_USER || pwd != WebServerManager.CSVLIST_V1_PWD)
			{
				// wrong name or password
				headers.Add(HttpResponseHeader.WwwAuthenticate, "Basic realm=\"download\", charset=\"UTF-8\"");
				_logger.Warn(TAG, nameof(SendDownload), TAG2, $"url='{url.Url}' wrong name/password");
				return SendPageStr("", Server.MIME_HTML, HttpStatusCode.Unauthorized, headers, true);
			}

			Dictionary<string, string> param = url.param;
			if (param["type"] != "csv" || param["version"] != "1")
			{
				return SendPageStr("error", Server.MIME_PLAIN, HttpStatusCode.OK, null, true);
			}

			List<TeilnehmerItem> tlnItems = _database.TeilnehmerLoadAll();

			StringBuilder sb = new StringBuilder();
			//sb.AppendLine("number,name,type,hostname,ipaddress,port,extension");
			sb.AppendLine("number,name,kg");

			foreach (TeilnehmerItem tln in tlnItems)
			{
				//sb.AppendLine(
				//	$"\"{tln.number}\",\"{tln.name}\",\"{tln.type}\",\"{tln.hostname}\",\"{tln.ipaddress}\"," +
				//	$"\"{tln.port}\",\"{tln.extension}\"");
				sb.AppendLine($"\"{tln.Number}\",\"{tln.Name}\",\"{tln.Answerback}\"");
			}

			headers.Add(HttpResponseHeader.CacheControl, "no-store");

			_logger.Notice(TAG, nameof(SendDownload), TAG2, $"download url='{url.Url}'");

			return SendPageStr(sb.ToString(), Server.MIME_PLAIN, HttpStatusCode.OK, headers, true);
		}

		public HttpListenerResponse SendList(string content)
		{
			string res = HttpUtility.UrlDecode(content);
			Dictionary<string, string> parms = ParamDecode.Decode(res);
			string code = parms["salt"] + GlobalData.Config.WebServerEditPwd;
			string hash1 = CommonHelper.GetHashSh256(code);
			string hash2 = parms["token"];
			bool admin = hash1 == hash2;

			string webListJson = null;
			if (admin)
			{
				WebTeilnehmerItemAdmin[] webList = WebServerManager.Instance.GetWebDataAdmin().ToArray();
				try
				{
					webListJson = JsonConvert.SerializeObject(webList);
				}
				catch (Exception)
				{
				}
			}
			else
			{
				WebTeilnehmerItem[] webList = WebServerManager.Instance.GetWebData().ToArray();
				try
				{
					webListJson = JsonConvert.SerializeObject(webList);
				}
				catch (Exception)
				{
				}
			}

			/*
			JObject resp;
			if (true)
			{
				resp = JObject.Parse(@"{ 'successful': true, 'result': " + webListJson + "}");
			}
			else
			{
				resp = JObject.Parse(@"{
					'successful': false,
					'message': {
						'code': 0,
						'text': 'error'
					}
				}");
			}
			*/
			JObject resp = JObject.Parse(@"{ 'successful': true, 'result': " + webListJson + "}");

			string jsonStr;
			try
			{
				jsonStr = JsonConvert.SerializeObject(resp);
			}
			catch (Exception)
			{
				jsonStr = "{}";
			}

			WebHeaderCollection headers = new WebHeaderCollection();
			headers.Add(HttpResponseHeader.CacheControl, "no-store");

			return SendPageStr(jsonStr, Server.MIME_JSON, HttpStatusCode.OK, headers, true);
		}

		public HttpListenerResponse SendGetSalt(bool doLog = false)
		{
			var res = new
			{
				successful = true,
				salt = "abc"
			};

			string jsonStr;
			try
			{
				jsonStr = JsonConvert.SerializeObject(res);
			}
			catch (Exception)
			{
				jsonStr = "{}";
			}

			WebHeaderCollection headers = new WebHeaderCollection();
			//headers.Add("X-JSON", jsonStr);
			headers.Add(HttpResponseHeader.Pragma, "no-cache");

			return SendPageStr(jsonStr, Server.MIME_JSON, HttpStatusCode.OK, headers);
		}

		public HttpListenerResponse SendEdit(string content)
		{
			WebHeaderCollection headers = new WebHeaderCollection();
			//headers.Add("X-JSON", jsonStr);
			headers.Add(HttpResponseHeader.Pragma, "no-cache");

			string res = HttpUtility.UrlDecode(content);
			Dictionary<string, string> parms = ParamDecode.Decode(res);

			WebResponseData data = null;
			try
			{
				string dataJson = parms["data"];
				data = JsonConvert.DeserializeObject<WebResponseData>(dataJson);
			}
			catch(Exception ex)
			{
				_logger.Error(TAG, nameof(SendEdit), TAG2, "json error", ex);
				return SendPageStr("{}", Server.MIME_JSON, HttpStatusCode.OK, headers);
			}
			string job = data.job;

			WebEditResponse resp = null;
			bool syncTrigger = false;
			switch (job)
			{
				case "confirm password":
					_logger.Info(TAG, nameof(HttpListenerResponse), TAG2, "job confirm password");
					resp = SendEditConfirmPassword(parms);
					break;
				case "resetPin":
					_logger.Info(TAG, nameof(HttpListenerResponse), TAG2, "job reset pin");
					resp = SendEditResetPin(parms, data);
					syncTrigger = true;
					break;
				case "delete":
					_logger.Info(TAG, nameof(HttpListenerResponse), TAG2, "job delete");
					resp = SendEditDelete(parms, data);
					syncTrigger = true;
					break;
				case "edit":
					_logger.Info(TAG, nameof(HttpListenerResponse), TAG2, "job edit");
					resp = SendEditEdit(parms, data);
					syncTrigger = true;
					break;
				case "new":
					_logger.Info(TAG, nameof(HttpListenerResponse), TAG2, "job new");
					resp = SendEditNew(parms, data);
					syncTrigger = true;
					break;
				case "copy":
					_logger.Info(TAG, nameof(HttpListenerResponse), TAG2, "job new");
					resp = SendEditCopy(parms, data);
					syncTrigger = true;
					break;
				default:
					_logger.Warn(TAG, nameof(HttpListenerResponse), TAG2, $"job invalid '{job}'");
					resp = new WebEditResponse(false, "invalid job");
					break;
			}

			string jsonStr;
			try
			{
				jsonStr = JsonConvert.SerializeObject(resp);
			}
			catch (Exception)
			{
				jsonStr = "{}";
			}

			if (syncTrigger)
			{
				BinaryServerManager.Instance.SyncTrigger = true;
			}

			return SendPageStr(jsonStr, Server.MIME_JSON, HttpStatusCode.OK, headers);
		}

		private WebEditResponse SendEditConfirmPassword(Dictionary<string, string> prms)
		{
			bool success = false;
			string msg = "";
			try
			{
				if (IsHashValid(prms, GlobalData.Config.WebServerEditPwd))
				{
					success = true;
					msg = "password is correct";
				}
				else
				{
					success = false;
					msg = "wrong password!";
				}
				return new WebEditResponse(success, msg);
			}
			catch (Exception)
			{
				return new WebEditResponse(false, "exception error");
			}
		}

		private WebEditResponse SendEditResetPin(Dictionary<string, string> prms, WebResponseData data)
		{
			bool success = false;
			string msg = "";
			try
			{
				if (IsHashValid(prms, GlobalData.Config.WebServerEditPwd))
				{
					if (_database.TeilnehmerSetPin(data.uid, null))
					{
						success = true;
						msg = "ok";
					}
					else
					{
						success = false;
						msg = "database error";
						_logger.Fatal(TAG, nameof(SendEditCopy), TAG2, $"error setting Teilnehmer pin in database, uid={data.uid}");
					}
				}
				else
				{
					success = false;
					msg = "hash error";
				}
				return new WebEditResponse(success, msg);
			}
			catch (Exception)
			{
				return new WebEditResponse(false, "exception error");
			}
		}

		private WebEditResponse SendEditDelete(Dictionary<string, string> prms, WebResponseData data)
		{
			bool success = false;
			string msg = "";
			try
			{
				if (IsHashValid(prms, GlobalData.Config.WebServerEditPwd))
				{
					if (_database.TeilnehmerSetType(data.uid, 0))
					{
						success = true;
						msg = "ok";
					}
					else
					{
						success = false;
						msg = "database error";
						_logger.Fatal(TAG, nameof(SendEditCopy), TAG2,
								$"error setting Teilnehmer type in database, uid={data.uid}");
					}
				}
				else
				{
					success = false;
					msg = "hash error";
				}

				return new WebEditResponse(success, msg);
			}
			catch (Exception)
			{
				return new WebEditResponse(false, "exception error");
			}
		}

		private WebEditResponse SendEditEdit(Dictionary<string, string> prms, WebResponseData data)
		{
			bool success = false;
			string msg = "";
			try
			{
				if (IsHashValid(prms, GlobalData.Config.WebServerEditPwd))
				{
					TeilnehmerItem oldTln = _database.TeilnehmerLoadByUid((Int64)data.uid);

					TeilnehmerItem tlnItem = new TeilnehmerItem()
					{
						Uid = (Int64)data.uid,
						Number = data.number,
						Name = data.name,
						Type = data.type.GetValueOrDefault(0),
						Hostname = data.hostname,
						IpAddress = data.ipaddress,
						Port = data.port,
						Pin = oldTln.Pin,
						Extension = data.extension.ToString(),
						Disabled = data.disabled.GetValueOrDefault(0) == 1,
						Remove = oldTln.Remove,
						LeadingEntry = oldTln.LeadingEntry,
						UpdatedBy = oldTln.UpdatedBy,
						ChangedBy = GlobalData.Config.ServerId,
						UserId = oldTln.UserId,
						DeviceId = oldTln.DeviceId,
						MainNumber = oldTln.MainNumber,
						TimestampUtc = DateTime.UtcNow,
						UpdateTimeUtc = DateTime.UtcNow,
						Changed = true
					};

					string diff = oldTln.CompareToString(tlnItem, out string diffErr);
					if (diffErr != null) diff = diffErr;

					_logger.Notice(TAG, nameof(SendEditEdit), TAG2, $"change tln {tlnItem.Number} diff={diff}");

					if (_database.TeilnehmerUpdateByUid(tlnItem))
					{
						success = true;
						msg = "ok";
					}
					else
					{
						success = false;
						msg = "database error";
						_logger.Fatal(TAG, nameof(SendEditCopy), TAG2, 
								$"error updating Teilnehmer in database, number={data.number}");
					}
				}
				else
				{
					success = false;
					msg = "hash error";
				}
				_logger.Debug(TAG, nameof(SendEditEdit), TAG2, $"success={success} msg={msg}");
				return new WebEditResponse(success, msg);
			}
			catch(Exception ex)
			{
				_logger.Error(TAG, nameof(SendEditEdit), TAG2, "error", ex);
				return new WebEditResponse(false, "exception error");
			}
		}

		private WebEditResponse SendEditNew(Dictionary<string, string> prms, WebResponseData data)
		{
			bool success = false;
			string msg = "";
			try
			{
				if (IsHashValid(prms, GlobalData.Config.WebServerEditPwd))
				{
					DateTime dt = DateTime.UtcNow;
					TeilnehmerItem tlnItem = new TeilnehmerItem()
					{
						Uid = (Int64)data.uid,
						Number = data.number,
						Name = data.name,
						Type = data.type.GetValueOrDefault(0),
						Hostname = data.hostname,
						IpAddress = data.ipaddress,
						Port = data.port,
						Pin = null,
						Extension = data.extension.ToString(),
						Disabled = data.disabled.GetValueOrDefault(0) == 1,
						Remove = false,
						LeadingEntry = false,
						UpdatedBy = 0,
						ChangedBy = GlobalData.Config.ServerId,
						UserId = 0,
						DeviceId = 0,
						MainNumber = false,
						TimestampUtc = dt,
						UpdateTimeUtc = dt,
						CreateTimeUtc = dt,
						Changed = true
					};

					if (_database.TeilnehmerInsert(tlnItem))
					{
						success = true;
						msg = "ok";
					}
					else
					{
						success = false;
						msg = "database error";
						_logger.Fatal(TAG, nameof(SendEditCopy), TAG2, 
								$"error inserting Teilnehmer in database, number={data.number}");
					}
				}
				else
				{
					success = false;
					msg = "hash error";
				}
				return new WebEditResponse(success, msg);
			}
			catch (Exception)
			{
				return new WebEditResponse(false, "exception error");
			}
		}

		/// <summary>
		/// Same as 'new' ?
		/// </summary>
		/// <param name="prms"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		private WebEditResponse SendEditCopy(Dictionary<string, string> prms, WebResponseData data)
		{
			bool success = false;
			string msg = "";
			try
			{
				if (IsHashValid(prms, GlobalData.Config.WebServerEditPwd))
				{
					DateTime dt = DateTime.UtcNow;
					TeilnehmerItem tlnItem = new TeilnehmerItem()
					{
						Uid = (Int64)data.uid,
						Number = data.number,
						Name = data.name,
						Type = data.type.GetValueOrDefault(0),
						Hostname = data.hostname,
						IpAddress = data.ipaddress,
						Port = data.port,
						Extension = data.extension.ToString(),
						Disabled = data.disabled.GetValueOrDefault(0) == 1,
						Remove = false,
						LeadingEntry = false,
						UpdatedBy = 0,
						ChangedBy = GlobalData.Config.ServerId,
						UserId = 0,
						DeviceId = 0,
						MainNumber = false,
						TimestampUtc = dt,
						UpdateTimeUtc = dt,
						CreateTimeUtc = dt,
						Changed = true
					};

					if (_database.TeilnehmerInsert(tlnItem))
					{
						success = true;
						msg = "ok";
					}
					else
					{
						success = false;
						msg = "database error";
						_logger.Fatal(TAG, nameof(SendEditCopy), TAG2, $"error inserting Teilnehmer, number={data.number}");
					}
				}
				else
				{
					success = false;
					msg = "hash error";
				}
				return new WebEditResponse(success, msg);
			}
			catch (Exception)
			{
				return new WebEditResponse(false, "exception error");
			}
		}


		private bool IsHashValid(Dictionary<string, string> prms, string password)
		{
			string code = prms["salt"] + password + prms["data"];
			string hash1 = CommonHelper.GetHashSh256(code);
			string hash2 = prms["token"];
			return hash1 == hash2;
		}

		#endregion special urls
	}
}
