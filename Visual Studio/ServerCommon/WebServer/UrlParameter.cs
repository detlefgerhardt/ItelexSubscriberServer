using System.Diagnostics;
using ServerCommon.Utility;

namespace ServerCommon.WebServer
{
	public class UrlParameter
	{
		//public string Main { get; set; }    // enthaelt den ersten Teil des Pfades

		public string Url { get; set; }     // der restliche Teil inkl Parameter

		public string PlainUrl { get; set; }    // der restliche Teil ohne Parameter
		public string Path { get; set; }

		public string Fullname { get; set; }

		public string Ext { get; set; }

		public Dictionary<string, string> param { get; set; }

		public UrlParameter(string url, string bs)
		{
			List<string> list;
			this.Url = url;

			list = CommonHelper.Split(url, '/');
			if (list.Count > 0 && list[0] == "")
			{
				// bei fuehrendem "/" ist das erste Elemente leer
				list.RemoveAt(0);
			}

			list = CommonHelper.Split(url, '?');
			if (list.Count == 1)
			{
				PlainUrl = url;
				param = new Dictionary<string, string>();
			}
			else
			{
				PlainUrl = list[0];
				param = GetParam(list[1]);
			}

			list = CommonHelper.Split(PlainUrl, '/');
			if (list.Count > 0 && list[0] == "")
				// bei fuehrendem "/" ist das erste Elemente leer
				list.RemoveAt(0);
			Fullname = string.Join(bs, list);

			list = CommonHelper.Split(PlainUrl, '.');
			if (list.Count > 1)
				Ext = list[list.Count - 1];
		}

		private Dictionary<string, string> GetParam(string paramStr)
		{
			Dictionary<string, string> dict = new Dictionary<string, string>();
			List<string> list = CommonHelper.Split(paramStr, '&');

			foreach (string prm in list)
			{
				List<string> list2 = CommonHelper.Split(prm, '=');
				if (list2.Count == 2)
				{
					dict[list2[0]] = list2[1];
				}
			}
			return dict;
		}

		public static string GetPrmVal(Dictionary<string, string> dict, string key, string defaultVal = "")
		{
			try
			{
				return dict[key];
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				return defaultVal;
			}
		}
	}
}
