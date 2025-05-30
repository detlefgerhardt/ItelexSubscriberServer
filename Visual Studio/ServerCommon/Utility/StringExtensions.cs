using System.Diagnostics;

namespace ServerCommon.Utility
{
	public static class StringExtensions
	{
		/**
		 * SubString without "out of range" exceptions
		 */
		public static string ExtSubstring(this string str, int start)
		{
			if (str == null) return null;
			return str.ExtSubstring(start, str.Length);
		}

		/**
		 * SubString without "out of range" exceptions
		 */
		public static string ExtSubstring(this string str, int start, int? length = null)
		{
			if (str == null) return null;
			if (str == "" || start < 0 || start >= str.Length) return "";
			if (length.HasValue && length.Value <= 0) return "";
			if (length == null) return str.Substring(start);
			if (start + length > str.Length) length = str.Length - start;
			return str.Substring(start, length.Value);
		}

		/**
		 * keeps the first <param name="len"></param> characters
		 */
		public static string ExtLeftString(this string str, int len)
		{
			if (str == null) return null;
			if (str == "" || len <= 0) return "";
			if (len >= str.Length) return str;
			return str.Substring(0, len);
		}

		/**
		 * keeps all characters left from pattern (excluding the pattern)
		 */
		public static string ExtLeftString(this string str, string pattern)
		{
			if (str == null) return null;
			if (str == "") return "";
			int pos = str.IndexOf(pattern);
			if (pos == -1) return str;
			return str.Substring(0, pos);
		}

		/**
		 * keeps the last <param name="len"></param> characters
		 */
		public static string ExtRightString(this string str, int len)
		{
			if (str == null) return null;
			if (str == "" || len <= 0) return "";
			if (len >= str.Length) return str;
			return str.Substring(str.Length - len, len);
		}

		/**
		 * keeps all characters right from pattern (excluding the pattern)
		 */
		public static string ExtRightString(this string str, string pattern)
		{
			if (str == null) return null;
			if (str == "") return "";
			int pos = str.LastIndexOf(pattern);
			if (pos == -1) return str;
			return str.Substring(pos + pattern.Length);
		}

		/**
		 * removes the first <param name="len"></param> characters
		 */
		public static string ExtCropLeft(this string str, int len)
		{
			if (str == null) return null;
			if (str == "") return "";
			if (len >= str.Length) return "";
			return str.Substring(0, str.Length - len);
		}

		/**
		 * removes the last <param name="len"></param> characters
		 */
		public static string ExtCropRight(this string str, int len)
		{
			if (str == null) return null;
			if (str == "") return "";
			if (len >= str.Length) return "";
			return str.Substring(0, str.Length - len);
		}

		/**
		 * removes all characters from the last occurance of pattern to the right (including the pattern)
		 */
		public static string ExrCropRight(this string str, string pattern)
		{
			if (str == null) return null;
			if (str == "") return "";
			int pos = str.LastIndexOf(pattern);
			if (pos == -1) return str;
			return str.Substring(0, pos);
		}

		/**
		 * removes all character from the first occurance of pattern to the left (including the pattern)
		 */
		public static string ExtCropLeft(this string str, string pattern)
		{
			if (str == null) return null;
			if (str == "") return "";
			int pos = str.IndexOf(pattern);
			if (pos == -1) return str;
			return str.Substring(pos + pattern.Length);
		}

		private static char[] WhiteSpaces = new[] { ' ', '\t' };

		/**
		 * Gets position of first whitespace character
		 */
		public static int ExtIndexOfWhiteSpace(this string str)
		{
			for (int i = 0; i < str.Length; i++)
			{
				if (WhiteSpaces.Contains(str[i])) return i;
			}
			return -1;
		}

		public static string ExtRemoveWhiteSpace(this string str, char quoteChar)
		{
			return str.ExtRemoveWhiteSpace(new[] { quoteChar });
		}

		public static string ExtRemoveWhiteSpace(this string str, char[] quoteChars)
		{
			bool quote = false;
			string newStr = "";
			foreach (char chr in str)
			{
				if (!quote && WhiteSpaces.Contains(chr))
					continue;
				if (quoteChars != null && quoteChars.Contains(chr))
					quote = !quote;
				newStr += chr;
			}
			return newStr;
		}

		public static int ExtIndexQuote(this string str, string searchStr)
		{
			bool quote = false;
			for (int i = 0; i < str.Length; i++)
			{
				if (str[i] == '"')
				{
					quote = !quote;
					continue;
				}
				if (quote) continue;
				if (searchStr == str.ExtSubstring(i, searchStr.Length)) return i;
			}
			return -1;
		}

#if false
		public static string ExtTrim(this string str, char[] trimChr)
		{
			// repeat trim as long as str is changed
			while (str!="")
			{
				string s = str;
				str = str.Trim(trimChr);
				if (str == s)
					return str;
			}
			return str;
		}
#endif

		public static int ExtIntValue(this string str, int defaultValue = 0)
		{
			if (int.TryParse(str, out int value)) return value;
			return defaultValue;
		}

		public static string[] ExtSplitWithQuote(this string str, char delimChar = ',', char quoteChar = '"', char escChar = '\\')
		{
			if (string.IsNullOrEmpty(str)) return new string[0];

			List<string> list = new List<string>();
			string strItem = "";
			bool quote = false;
			bool esc = false;
			for (int i = 0; i < str.Length; i++)
			{
				char chr = str[i];

				Debug.WriteLine($"{chr} {esc} {quote} {strItem}");

				// check for escape character
				if (chr == '\\' && !esc)
				{
					esc = true;
					continue;
				}

				// check for quote
				if (quote && !esc)
				{
					if (chr == quoteChar)
						quote = false;
					strItem += chr;
					continue;
				}

				// check for quote character
				if (chr == quoteChar && !esc)
				{
					quote = true;
					strItem += chr;
					continue;
				}

				// check for delimiter
				if (chr == delimChar && !quote)
				{
					list.Add(strItem);
					strItem = "";
					continue;
				}

				strItem += chr;
				esc = false;
			}

			if (strItem != "") list.Add(strItem);

			return list.ToArray();
		}

		public static bool ExtContainsOnlyDigits(this string str)
		{
			foreach (char c in str)
			{
				if (!char.IsDigit(c)) return false;
			}
			return true;
		}

		public static string ExtToHexDump(this string str)
		{
			if (string.IsNullOrEmpty(str)) return "";

			string dump = "";
			for (int i = 0; i < str.Length; i++)
			{
				dump += $"{(byte)str[i]:X2}";
				if (i < str.Length - 1)
					dump += " ";
			}
			return dump;
		}
	}
}
