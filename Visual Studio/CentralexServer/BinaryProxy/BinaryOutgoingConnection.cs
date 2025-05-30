using CentralexServer.BinaryProxy;
using ServerCommon.Logging;
using ServerCommon.SubscriberServer;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Centralex.BinaryProxy
{
	class BinaryOutgoingConnection: BinaryConnection
	{
		private const string TAG = nameof(BinaryOutgoingConnection);
		private const string TAG2 = "";

		public static readonly string[] TLN_SERVER_ADDRS = new string[]
		{
			"tlnserv2.teleprinter.net",
			"tlnserv.teleprinter.net",
			"tlnserv3.teleprinter.net",
		};
		private const int TLN_SERVER_PORT = 11811;

		private const int TIMEOUT = 2000;

		private NetworkStream _stream = null;

		public BinaryPacket LastPacket
		{
			get
			{
				return _lastPacket;
			}
			set
			{
				_lastPacket = value;
			}
		}

		public BinaryOutgoingConnection(int idNumber, string logPath, LogTypes logLevel) :
			base(null, ConnectionType.BinOut, logPath, logLevel)
		{
		}

		public bool Connect(string[] address, int? port)
		{

			if (address == null) address = TLN_SERVER_ADDRS;
			if (port == null) port = TLN_SERVER_PORT;

			for (int i = 0; i < address.Length; i++)
			{
				try
				{
					_logger?.Debug(TAG, nameof(Connect), TAG2, $"connect to {address[i]}:{port.Value}, try {i + 1}");
					_tcpClient = new TcpClient();
					if (!_tcpClient.ConnectAsync(address[i], port.Value).Wait(TIMEOUT))
					{
						string errStr = $"timeout connecting to subscriber server {address[i]}:{port}";
						_logger?.Debug(TAG, nameof(Connect), TAG2, errStr);
					}
					_logger?.Debug(TAG, nameof(Connect), TAG2, "connected");
					_tcpClient.ReceiveTimeout = 2000;
					_stream = _tcpClient.GetStream();
					_logger?.Debug(TAG, nameof(Connect), TAG2, "stream ok");

					// check connection (work-around)
					PeerSearchReply reply = SendPeerSearch("check");
					if (!reply.Valid)
					{
						string errStr = $"error in subscriber server communication {address[i]}:{port}";
						_logger?.Debug(TAG, nameof(Connect), TAG2, errStr);
						_stream?.Close();
						_tcpClient?.Close();
						continue; // try next subscriber server
					}

					return true;
				}
				catch (Exception ex)
				{
					string errStr = $"error connecting to subscriber server {address[i]}:{port}";
					_logger?.Error(TAG, nameof(Connect), TAG2, errStr, ex);
				}
			}

			_stream?.Close();
			_tcpClient?.Close();
			_tcpClient = null;
			return false;
		}

		/// <summary>
		/// Query for number
		/// </summary>
		/// <param name="number"></param>
		/// <returns>peer or null</returns>
		public PeerQueryReply SendPeerQuery(int number)
		{
			int[] invalidNumbers = { }; // test

			//Log(LogTypes.Debug, nameof(SendPeerQuery), $"number='{number}'");

			if (invalidNumbers.Contains(number))
			{
				//Log(LogTypes.Notice, nameof(SendPeerQuery), $"peer not found*");
				return new PeerQueryReply()
				{
					Error = $"peer not found {number}",
					Valid = false,
				};
			}

			PeerQueryReply reply = new PeerQueryReply();

			if (_tcpClient == null)
			{
				//Log(LogTypes.Error, nameof(SendPeerQuery), "no server connection");
				reply.Error = "no server connection";
				reply.Valid = false;
				return reply;
			}

			if (number == 0)
			{
				reply.Error = "no query number";
				reply.Valid = false;
				return reply;
			}

			byte[] sendData = new byte[2 + 5];
			sendData[0] = 0x03; // Peer_query
			sendData[1] = 0x05; // length
			byte[] numData = BitConverter.GetBytes(number);
			Buffer.BlockCopy(numData, 0, sendData, 2, 4);
			sendData[6] = 0x01; ; // version 1

			try
			{
				_stream.Write(sendData, 0, sendData.Length);
			}
			catch (Exception)
			{
				//Log(LogTypes.Error, nameof(SendPeerQuery), $"error sending data to subscriber server", ex);
				reply.Error = "reply server error";
				return reply;
			}

			byte[] recvData = new byte[102];
			int recvLen;
			try
			{
				recvLen = _stream.Read(recvData, 0, recvData.Length);
			}
			catch (Exception)
			{
				//Log(LogTypes.Error, nameof(SendPeerQuery), $"error receiving data from subscriber server", ex);
				reply.Error = "reply server error";
				return reply;
			}

			if (recvLen == 0)
			{
				reply.Error = $"no data received";
				return reply;
			}

			if (recvData[0] == 0x04)
			{
				// peer not found
				//Log(LogTypes.Notice, nameof(SendPeerQuery), $"peer not found");
				reply.Error = $"peer not found {number}";
				reply.Valid = false;
				return reply;
			}

			if (recvData[0] != 0x05)
			{
				// invalid packet
				//Log(LogTypes.Error, nameof(SendPeerQuery), $"invalid packet id ({recvData[0]:X02})");
				reply.Error = $"invalid packet id ({recvData[0]:X02})";
				reply.Valid = false;
				return reply;
			}

			if (recvLen < 2 + 0x64)
			{
				//Log(LogTypes.Error, nameof(SendPeerQuery), $"received data to short ({recvLen} bytes)");
				reply.Error = $"received data to short ({recvLen} bytes)";
				reply.Valid = false;
				return reply;
			}

			if (recvData[1] != 0x64)
			{
				//Log(LogTypes.Error, nameof(SendPeerQuery), $"invalid length value ({recvData[1]})");
				reply.Error = $"invalid length value ({recvData[1]})";
				reply.Valid = false;
				return reply;
			}

			reply.Data = ByteArrayToPeerData(recvData, 2);

			if (!string.IsNullOrEmpty(reply.Data.HostName) && string.IsNullOrEmpty(reply.Data.IpAddress))
			{
				// get ip address from host name
				reply.Data.IpAddress = Dns.GetHostAddresses(reply.Data.HostName)?.First(addr => addr.AddressFamily == AddressFamily.InterNetwork)?.ToString();
				//Log(LogTypes.Debug, nameof(SendPeerQuery), $"host {reply.Data.IpAddress} -> ipaddress {reply.Data.IpAddress}");
				// Dns.GetHostEntry(reply.Data.HostName).AddressList.First(addr => addr.AddressFamily == AddressFamily.InterNetwork);
			}

			reply.Valid = true;
			reply.Error = "ok";

			return reply;
		}

		/// <summary>
		/// Query for search string
		/// </summary>
		/// <param name="name"></param>
		/// <returns>search reply with list of peers</returns>
		public PeerSearchReply SendPeerSearch(string name)
		{
			//Log(LogTypes.Debug, nameof(SendPeerSearch), $"name='{name}'");
			PeerSearchReply reply = new PeerSearchReply();

			if (_tcpClient == null)
			{
				//Log(LogTypes.Error, nameof(SendPeerSearch), "no server connection");
				reply.Error = "no server connection";
				reply.Valid = false;
				return reply;
			}

			if (string.IsNullOrEmpty(name))
			{
				reply.Error = "no search name";
				reply.Valid = false;
				return reply;
			}

			byte[] sendData = new byte[43];
			sendData[0] = 0x0A; // Peer_search
			sendData[1] = 0x29; // length
			sendData[2] = 0x01; ; // version 1
			byte[] txt = Encoding.ASCII.GetBytes(name);
			Buffer.BlockCopy(txt, 0, sendData, 3, txt.Length);
			try
			{
				_stream.Write(sendData, 0, sendData.Length);
			}
			catch (Exception)
			{
				//Message?.Invoke(LngText(LngKeys.Message_SubscribeServerError));
				//Log(LogTypes.Error, nameof(SendPeerSearch), $"error sending data to subscriber server", ex);
				reply.Valid = false;
				reply.Error = "reply server error";
				return reply;
			}

			byte[] ack = new byte[] { 0x08, 0x00 };
			List<PeerQueryData> list = new List<PeerQueryData>();
			while (true)
			{
				byte[] recvData = new byte[102];
				int recvLen = 0;
				try
				{
					recvLen = _stream.Read(recvData, 0, recvData.Length);
				}
				catch (Exception)
				{
					//Log(LogTypes.Error, nameof(SendPeerSearch), $"error receiving data from subscriber server", ex);
					reply.Valid = false;
					reply.Error = "reply server error";
					return reply;
				}
				//Logging.Instance.Log(LogTypes.Debug, TAG, nameof(SendPeerSearch), $"recvLen={recvLen}");

				if (recvLen == 0)
				{
					//Log(LogTypes.Error, nameof(SendPeerSearch), $"recvLen=0");
					reply.Error = $"no data received";
					return reply;
				}

				if (recvData[0] == 0x09)
				{
					// end of list
					break;
				}

				if (recvLen < 2 + 0x64)
				{
					//Log(LogTypes.Warn, nameof(SendPeerSearch), $"received data to short ({recvLen} bytes)");
					reply.Error = $"received data to short ({recvLen} bytes)";
					continue;
				}

				if (recvData[1] != 0x64)
				{
					//Log(LogTypes.Warn, nameof(SendPeerSearch), $"invalid length value ({recvData[1]})");
					reply.Error = $"invalid length value ({recvData[1]})";
					continue;
				}

				PeerQueryData data = ByteArrayToPeerData(recvData, 2);
				//Log(LogTypes.Debug, nameof(SendPeerSearch), $"found {data}");

				list.Add(data);

				// send ack
				try
				{
					_stream.Write(ack, 0, ack.Length);
				}
				catch (Exception)
				{
					//Log(LogTypes.Error, nameof(SendPeerSearch), $"error sending data to subscriber server", ex);
					return null;
				}
			}

			reply.List = list.ToArray();
			reply.Valid = true;
			reply.Error = "ok";

			return reply;
		}

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
	}
}
