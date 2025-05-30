namespace TlnServer.WebServer
{
	internal static class ParamDecode
	{
		public static Dictionary<string, string>Decode(string prm)
		{
			if (string.IsNullOrEmpty(prm)) return null;

			string[] parts = prm.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<string, string> keyval = new Dictionary<string, string>();
			foreach (string item in parts)
			{
				int pos = item.IndexOf('='); // find first (!) '='
				if (pos > 0)
				{
					string key = item.Substring(0, pos);
					string val = item.Substring(pos + 1);
					keyval[key] = val;
				}
			}
			return keyval;
		}
	}
}
