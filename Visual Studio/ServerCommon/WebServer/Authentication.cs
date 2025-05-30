using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.WebServer
{
	public class Authentication
	{
		public string[] GetBasicAuthenticationFromHeader(HttpListenerRequest request)
		{
			string[] auth = request.Headers.GetValues("Authorization");
			if (auth == null || auth.Length == 0) return null;

			string[] authFields = auth[0].Split(' ');
			if (authFields.Length != 2) return null;
			if (authFields[0] != "Basic") return null;

			byte[] dataBin = Convert.FromBase64String(authFields[1]);
			string dataStr = System.Text.Encoding.UTF8.GetString(dataBin);

			string[] namePwd = dataStr.Split(':');
			if (namePwd.Length != 2) return null;

			return namePwd;
		}
	}
}
