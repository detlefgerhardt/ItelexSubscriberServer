using CentralexCommon;
using System.Reflection.Metadata;
using System.Text;

namespace ServerCommon.Logging
{
	public enum ConnectionType { CentralexClient, CentralexCaller, BinIn, BinOut }

	public class ConnectionLogger
	{
		private const string TAG = nameof(ConnectionLogger);

		private LogTypes _logLevel;

		private string _logPath;

		private string _fullName;

		private Logger _logger;

		//private SysLog _sysLog;

		private int _connectionId { get; set; }

		private bool _open { get; set; }

		private StreamWriter _streamWriter;

		private ConnectionType _connType;

		private int? _itelexNumber;

		//private Acknowledge _ack;

		//private ShiftStates _shiftState;

		private object _lockRename = new object();

		public ConnectionLogger(string logPath, Logger logger)
		{
			_logPath = logPath;
			_logger = logger;
		}

		public ConnectionLogger(string logPath, Logger logger, int connectionId, ConnectionType connType, int? number,
			LogTypes logLevel, bool enabled)
		{
			_logger = logger;
			_logPath = logPath;
			_logLevel = logLevel;
			_connectionId = connectionId;
			_connType = connType;
			_itelexNumber = number;
			if (enabled)
			{
				ItelexLog(LogTypes.Info, TAG, nameof(ConnectionLogger), "--- start of logfile ---");
				Init();
			}
		}

		~ConnectionLogger()
		{
			//LogManager.Instance.Logger.Debug(TAG, "~ItelexLogger", "destructor");
			Dispose(false);
		}

		#region Dispose

		// Flag: Has Dispose already been called?
		private bool _disposed = false;

		// Public implementation of Dispose pattern callable by consumers.
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		// Protected implementation of Dispose pattern.
		protected virtual void Dispose(bool disposing)
		{
			//LogManager.Instance.Logger.Debug(TAG, nameof(Dispose), $"_disposed={_disposed} disposing={disposing}");

			if (_disposed) return;

			if (disposing)
			{
				// Free any other managed objects here.
				if (_streamWriter != null) Close();
			}
			_disposed = true;
		}

		#endregion Dispose

		private void Init()
		{
			try
			{
				_fullName = GetName();
				OpenStream(false);
				_open = true;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(Init), "error opening logfile stream {_fullName}", ex);
				//_sysLog.Log(LogTypes.Error, $"error opening logfile stream {_fullName}");
				_open = false;
			}

		}

		private string GetName()
		{
			string numStr = _itelexNumber != null ? $"_{_itelexNumber.Value}" : "";
			string fileName = $"connection_{_connectionId}_{_connType}_{numStr}.log";
			return Path.Combine(_logPath, fileName);
		}

		private void OpenStream(bool append)
		{
			//_logStream = File.OpenWrite(_fullName);
			_streamWriter = new StreamWriter(_fullName, append, Encoding.ASCII);
		}

		//public void SetAck(Acknowledge ack)
		//{
		//	_ack = ack;
		//}

		public void End()
		{
			ItelexLog(LogTypes.Info, TAG, nameof(End), "--- end of logfile ---");
			Close();
		}

		private void Close()
		{
			if (_streamWriter != null && _streamWriter.BaseStream != null)
			{
				try
				{
					if (_streamWriter != null)
					{
						_streamWriter.Close();
						_streamWriter.Dispose();
						_streamWriter = null;
					}
				}
				catch (Exception)
				{
				}
			}
		}

		/// <summary>
		/// set itelex-number an rename log-file
		/// </summary>
		public void SetNumber(int itelexNumber)
		{
			if (_itelexNumber.HasValue || !_open) return;

			_itelexNumber = itelexNumber;
			lock (_lockRename)
			{
				try
				{
					Close();
					string oldName = _fullName;
					_fullName = GetName();
					File.Delete(_fullName);
					File.Move(oldName, _fullName);
					OpenStream(true);
					return;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(SetNumber), $"error", ex);
					_open = false;
				}
			}
		}

		public void ItelexLog(LogTypes logType, string tag, string method, ItelexPacket packet, string sendRecv)
		{
			if (_open)
			{
				//string msg = GetExtDebugData(packet, ref _shiftState, sendRecv);
				string msg = "=";
				//string sendRecvStr = sendRecv == CodeManager.SendRecv.Recv ? "recv" : "send";
				Log($"[{logType.ToString().PadRight(5)}] [{tag}] [{method}] {sendRecv} {msg}");
				SysLog(logType, tag, method, msg);
			}
		}

		public void ItelexLog(LogTypes logType, string tag, string method, string msg)
		{
			if (_open)
			{
				Log($"[{logType.ToString().PadRight(5)}] [{tag}] [{method}] {msg}");
				SysLog(logType, tag, method, msg);
			}
		}

		public void ItelexLog(LogTypes logType, string tag, string method, string msg, Exception ex)
		{
			if (_open)
			{
				Log($"[{logType.ToString().PadRight(5)}] [{tag}] [{method}] {msg} {ex}");
				SysLog(logType, tag, method, msg, ex);
			}
		}

		private void Log(string text)
		{
			if (!_open) return;

			string logStr = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss.fff} {text}";
			try
			{
				if (_streamWriter != null)
				{
					_streamWriter.WriteLine(logStr);
					_streamWriter.Flush();
				}
			}
			catch
			{
				SysLog(LogTypes.Error, TAG, nameof(Log), $"error writing logfile {GetName()}");
			}
		}

		private void SysLog(LogTypes logType, string tag, string method, string msg)
		{
			int maxLen = 49; // max. length for Visual SysLog Tag field
			string vsTag = $"[{tag}][{method}]";
			if (vsTag.Length > maxLen) vsTag = vsTag.Substring(0, maxLen);

			//_sysLog?.Log(logType, $"{vsTag}: {msg}");
		}

		private void SysLog(LogTypes logType, string tag, string method, string msg, Exception ex)
		{
			int maxLen = 49; // max. length for Visual SysLog Tag field
			string vsTag = $"[{tag}][{method}]";
			if (vsTag.Length > maxLen) vsTag = vsTag.Substring(0, maxLen);

			//_sysLog?.Log(logType, $"{vsTag}: {msg} {ex?.Message}");
		}

		private bool IsActiveLevel(LogTypes current)
		{
			return (int)current <= (int)_logLevel;
		}
	}
}
