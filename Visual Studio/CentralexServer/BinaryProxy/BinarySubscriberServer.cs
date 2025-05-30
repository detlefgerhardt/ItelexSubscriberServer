using ServerCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;

namespace CentralexServer.BinaryProxy
{
	public class BinarySubscriberServer
	{
		public delegate void MessageEventHandler(string message);
		public event MessageEventHandler Message;

		private const string TAG = nameof(BinarySubscriberServer);

		private TcpClient _tcpClient = null;
		private NetworkStream _stream = null;

		private Logger _logger;

		//private string _serverConfigPath;
		//private SubscriberServerConfig _subscribeServerConfig;

		public BinarySubscriberServer(Logger logger)
		{
			_logger = logger;

			//_serverConfigPath = serverConfigPath;
			//_subscribeServerConfig = LoadSubscribeServerConfig(_serverConfigPath);
		}

		public bool Connect()
		{
			if (GlobalData.SubscriberServerConfig == null) return false;

			string[] address = GlobalData.SubscriberServerConfig.ServerAddresses;
			int port = GlobalData.SubscriberServerConfig.ServerPort;
			int timeout = GlobalData.SubscriberServerConfig.Timeout;

			for (int i = 0; i < address.Length; i++)
			{
				try
				{
					_logger?.Debug(TAG, nameof(Connect), $"connect to {address[i]}:{port}, try {i + 1}");
					_tcpClient = new TcpClient();
					if (!_tcpClient.ConnectAsync(address[i], port).Wait(timeout))
					{
						string errStr = $"timeout connecting to subscribe server {address[i]}:{port}";
						_logger?.Debug(TAG, nameof(Connect), errStr);
						Message?.Invoke(errStr);
					}
					_logger?.Debug(TAG, nameof(Connect), "connected");
					_tcpClient.ReceiveTimeout = 2000;
					_stream = _tcpClient.GetStream();
					_logger?.Debug(TAG, nameof(Connect), "stream ok");

					/*
					// check connection (work-around)
					PeerSearchReply reply = SendPeerSearch("check");
					if (!reply.Valid)
					{
						string errStr = $"error in subscribe server communication {address[i]}:{port}";
						_logger?.Debug(TAG, nameof(Connect), errStr);
						_stream?.Close();
						_tcpClient?.Close();
						continue; // try next subscribe server
					}
					*/

					return true;
				}
				catch (Exception ex)
				{
					string errStr = $"error connecting to subscribe server {address[i]}:{port}";
					_logger?.Debug(TAG, nameof(Connect), errStr);
					Message?.Invoke(errStr);
				}
			}

			_stream?.Close();
			_tcpClient?.Close();
			_tcpClient = null;
			return false;
		}

		public bool Disconnect()
		{
			_stream?.Close();
			_tcpClient?.Close();
			_tcpClient = null;
			return true;
		}


		/*
		private PeerQueryData ByteArrayToPeerData(byte[] bytes, int offset)
		{
			if (offset + 100 > bytes.Length)
			{
				return null;
			}

			PeerQueryData data = new PeerQueryData
			{
				Number = BitConverter.ToInt32(bytes, offset),
				LongName = Encoding.ASCII.GetString(bytes, offset + 4, 40).Trim(new char[] { '\x00' }),
				SpecialAttribute = BitConverter.ToUInt16(bytes, offset + 44),
				PeerType = bytes[offset + 46],
				HostName = Encoding.ASCII.GetString(bytes, offset + 47, 40).Trim(new char[] { '\x00' }),
				IpAddress = $"{bytes[offset + 87]}.{bytes[offset + 88]}.{bytes[offset + 89]}.{bytes[offset + 90]}",
				PortNumber = BitConverter.ToUInt16(bytes, offset + 91),
				ExtensionNumber = bytes[offset + 93],
				Pin = BitConverter.ToUInt16(bytes, offset + 94)
			};

			UInt32 timestamp = BitConverter.ToUInt32(bytes, offset + 96);
			DateTime dt = new DateTime(1900, 1, 1, 0, 0, 0, 0);
			data.LastChange = dt.AddSeconds(timestamp);

			return data;
		}
		*/


		/// <summary>
		/// Load subscriber server config from text file.
		/// </summary>
		/// <param name="fullname"></param>
		/// <returns></returns>
		internal static SubscriberServerConfig LoadSubscribeServerConfig(string fullname)
		{
			SubscriberServerConfig config = new SubscriberServerConfig();

			string[] lines;
			try
			{
				lines = File.ReadAllLines(fullname);
			}
			catch(Exception ex)
			{
				//_logger.Error(TAG, nameof(SubscriberServerConfig), $"error loading {fullname}");
				return null;
			}

			List<string> addresses = new List<string>();
			foreach(string line in lines)
			{
				string line1 = line.ToLower().Trim();
				if (line1.StartsWith(';')) continue; // comment
				int pos = line1.IndexOf(' ');
				if (pos == -1) continue; // invalid line
				string key = line1.Substring(0, pos);
				string value = line1.Substring(pos + 1);
				switch(key)
				{
					case "port":
						if (int.TryParse(value, out int port))
						{
							config.ServerPort = port;
						}
						break;
					case "timeout":
						if (int.TryParse(value, out int timeout))
						{
							config.Timeout = timeout;
						}
						break;
					case "address":
						addresses.Add(value);
						break;
				}
			}

			config.ServerAddresses = addresses.ToArray();
			if (config.ServerPort == 0 || config.ServerAddresses.Length == 0)
			{
				//_logger.Error(TAG, nameof(SubscriberServerConfig), $"invalid config {fullname}");
				return null; // invalid config
			}

			//_logger.Error(TAG, nameof(SubscriberServerConfig), $"config loaded {fullname}");

			return config;
		}

		private static void Log(LogTypes logType, string methode, string msg, Exception ex = null)
		{
			if (logType == LogTypes.Error && ex != null)
			{
				//LogManager.Instance.Logger.Error(TAG, methode, msg, ex);
			}
			else
			{
				//LogManager.Instance.Logger.Log(logType, TAG, methode, msg);
			}
		}
	}

	/*
	class AsciiQueryResult
	{
		public string Number { get; set; }
		public string Name { get; set; }
		public int? Type { get; set; }
		public string HostName { get; set; }
		public int? Port { get; set; }
		public int? ExtensionNumber { get; set; }

		public override string ToString()
		{
			return $"{Number} '{Name}' {Type} {HostName} {Port} {ExtensionNumber}";
		}
	}
	*/

	/*
	public class ClientUpdateReply
	{
		public bool Success { get; set; } = false;
		public string Error { get; set; }
		public string IpAddress { get; set; }
	}
	*/

	internal class SubscriberServerConfig
	{
		public const int DEFAULT_PORT = 11811;

		public const int DEFAULT_TIMEOUT = 2000;

		public string[] ServerAddresses { get; set; }

		public int ServerPort { get; set; } = DEFAULT_PORT;

		public int Timeout { get; set; } = DEFAULT_TIMEOUT;
	}
}

