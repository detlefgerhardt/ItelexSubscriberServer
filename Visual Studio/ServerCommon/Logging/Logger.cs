namespace ServerCommon.Logging
{
	public enum LogTypes { None = 0, Fatal = 1, Error = 2, Warn = 3, Notice = 4, Info = 5, Debug = 6 };

	public class Logger
	{
		public LogTypes _logLevel { get; private set; }

		private string _logPath;

		private string _logName;

		private string _logFullname;

		private SysLog _sysLog;

		private object _lock = new object();

		public Logger(string logPath, string logName, LogTypes logLevel, SysLog sysLog)
		{
			_logPath = logPath;
			_logName = logName;
			_logLevel = logLevel;
			_sysLog = sysLog;

			if (string.IsNullOrEmpty(_logPath) || string.IsNullOrEmpty(_logName))
			{
				_logFullname = "";
			}
			else
			{
				_logFullname = Path.Combine(_logPath, _logName);
			}

			Init();
		}

		private void Init()
		{
			try
			{
				// create directoy if it does not exist
				Directory.CreateDirectory(_logPath);
			}
			catch (Exception ex)
			{
				_sysLog?.Log(LogTypes.Error, nameof(Logger), nameof(Init), $"error creating logpath {_logPath} {ex.Message}");
			}
		}

		public void SetSysLog(SysLog sysLog)
		{
			_sysLog = sysLog;
		}

		public void SetLogLevel(LogTypes logLevel)
		{
			_logLevel = logLevel;
		}

		public void ConsoleLog(string tag, string method, string type, string msg)
		{
			Console.WriteLine($"{DateTime.Now:dd.MM.yy HH:mm.ss} {type.PadRight(25)} {msg}");
			if (tag != null && method != null)
			{
				string text = type + " " + msg;
				Info(tag, method, text);
			}
		}

		public void Debug(string section, string method, string text)
		{
			Log(LogTypes.Debug, section, method, text);
			_sysLog?.Log(LogTypes.Debug, section, method, text);
		}

		public void Info(string section, string method, string text)
		{
			Log(LogTypes.Info, section, method, text);
			_sysLog?.Log(LogTypes.Info, section, method, text);
		}

		public void Notice(string section, string method, string text)
		{
			Log(LogTypes.Notice, section, method, text);
			_sysLog?.Log(LogTypes.Notice, section, method, text);
		}

		public void Warn(string section, string method, string text)
		{
			Log(LogTypes.Warn, section, method, text);
			_sysLog?.Log(LogTypes.Warn, section, method, text);
		}

		public void Error(string section, string method, string text)
		{
			Log(LogTypes.Error, section, method, text);
			_sysLog?.Log(LogTypes.Error, section, method, text);
		}

		public void Error(string section, string method, string text, Exception? ex = null)
		{
			if (ex != null)
			{
				text = $"{text} result={ex.HResult} {ex.Message}\r\n{ex}";
			}
			Log(LogTypes.Error, section, method, text);
			_sysLog?.Log(LogTypes.Error, section, method, text);
		}

		public void Fatal(string section, string method, string text)
		{
			Log(LogTypes.Fatal, section, method, text);
			_sysLog?.Log(LogTypes.Fatal, section, method, text);
		}

		public void Log(LogTypes logType, string section, string method, string text)
		{
			if (IsActiveLevel(logType))
			{
				AppendLog(logType, section, method, text);
			}
		}

		//public void OnLog(LogArgs e)
		//{
		//	RecvLog?.Invoke(this, e);
		//}

		private void AppendLog(LogTypes logType, string section, string method, string text)
		{
			lock (_lock)
			{
				int? id = Task.CurrentId;
				if (!id.HasValue)
				{
					System.Diagnostics.Debug.Write("");
				}
				string prefix = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss.ff} {logType.ToString().PadRight(5)} [{Task.CurrentId}] [{section}]";
				string logStr = $"{prefix} [{method}] {text}\r\n";
				try
				{
					File.AppendAllText(_logFullname, logStr);
				}
				catch
				{
					//_sysLog?.Log(LogTypes.Error, $"[{nameof(Logging)}][{nameof(AppendLog)}]: error writing logfile {LogfileFullname}");

					// try to log in program directory
					/*
					try
					{
						string newName = Path.Combine(Application.StartupPath, _logName);
						File.AppendAllText(newName, $"{prefix} [AppendLog] Error writing logfile to {LogfileFullname}\r\n");
						File.AppendAllText(newName, logStr);
					}
					catch { }
					*/
				}
			}

			/*
			int maxLen = 49; // max. length for Visual SysLog Tag field
			string tag = $"[{section}][{method}]";
			if (tag.Length > maxLen) tag = tag.Substring(0, maxLen);
			_sysLog?.Log(logType, $"{tag}: {text}");
			*/
		}

		private bool IsActiveLevel(LogTypes current)
		{
			return (int)current <= (int)_logLevel;
		}

	}
}
