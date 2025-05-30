using CentralexServer.Data;
using ItelexTlnServer.Data;
using ServerCommon.Logging;
using ServerCommon.Utility;
using SQLitePCL;
using System.Net;
using System.Net.Sockets;

namespace TlnServer.BinaryServer
{
	internal class BinaryOutgoingConnection
	{
		private const string TAG = nameof(BinaryOutgoingConnection);

		private TcpClient _tcpClient;

		private NetworkStream _stream;

		private Logger _logger;

		private string _tag2;

		private string _serverAddress;

		private int _serverPort;

		private uint _serverPin;

		//public string RemoteName { get; set; }

		public IPAddress RemoteIpAddress
		{
			get
			{
				if (_tcpClient?.Client == null) return null;

				try
				{
					return CommonHelper.GetIp4AddrFromHostname(_serverAddress);
				}
				catch (Exception)
				{
					return null;
				}
			}
		}

		public BinaryOutgoingConnection(string serverAddr, int serverPort, uint serverPin,
			string logPath, LogTypes logLevel, string tag2)
		{
			_tag2 = tag2;
			_serverAddress = serverAddr;
			_serverPort = serverPort;
			_serverPin = serverPin;
			_logger = GlobalData.Logger;
			//_database = TlnServerMsDatabase.Instance;
			_tcpClient = null;
			_stream = null;

			//RemoteName = BinaryServerManager.Instance.IpToServerName(serverAddr);

			Log(LogTypes.Debug, nameof(BinaryOutgoingConnection), $"--- New binary connection to {serverAddr} ---");
		}

		~BinaryOutgoingConnection()
		{
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
			if (_disposed) return;

			if (disposing)
			{
				// Free any other managed objects here.
			}
			//_connectionLogger?.End();

			_disposed = true;
		}

		#endregion Dispose

		public bool Connect()
		{
			try
			{
				_tcpClient = new TcpClient();
				if (!_tcpClient.ConnectAsync(RemoteIpAddress, _serverPort).Wait(2000))
				{
					Log(LogTypes.Warn, nameof(Connect), "ConnectAsync timeout");
					return false;
				}

				_tcpClient.ReceiveTimeout = 5000;
				_stream = _tcpClient.GetStream();
			}
			catch (Exception ex)
			{
				Log(LogTypes.Error, nameof(Connect), "error", ex);
				_stream?.Close();
				_tcpClient?.Close();
				_tcpClient = null;
				return false;
			}

			string host = BinaryServerManager.Instance.IpToServerName(RemoteIpAddress);
			_logger.Debug(TAG, nameof(Connect), _tag2, $"outgoing connect {host}:{_serverPort}");

			return true;
		}

		public bool Disconnect()
		{
			Log(LogTypes.Debug, nameof(Disconnect), "Disconnect");
			_stream?.Close();
			_tcpClient?.Close();
			_tcpClient = null;
			return true;
		}

		public bool DoSyncLogin(List<TeilnehmerItem> tlnList)
		{
			Log(LogTypes.Debug, nameof(DoSyncLogin), $"start, tlnList.Length={tlnList?.Count}");

			if (tlnList == null) return false;

			try
			{
				// send sync login
				BinaryPacket packet = BinaryPacket.GetSyncLogin(_serverPin);
				byte[] sendData = packet.PacketBuffer;
				_stream.Write(sendData, 0, sendData.Length);

				// check for acknowledge
				byte[] recvData = new byte[2];
				int recvLen = _stream.Read(recvData, 0, recvData.Length);
				if (recvLen != 2 || recvData[0] != (byte)BinaryCommands.Acknowledge)
				{
					Log(LogTypes.Warn, nameof(DoSyncLogin), $"error: len={recvData.Length} packet={recvData[0]}");
					return false;
				}

				foreach (TeilnehmerItem tlnItem in tlnList)
				{
					/*
					//packet = BinaryPacket.GetPeerReplyV1(tlnItem, true);
					packet = BinaryPacket.GetSyncReplyV2(tlnItem, true);
					sendData = packet.PacketBuffer;
					_stream.Write(sendData, 0, sendData.Length);
					Log(LogTypes.Info, nameof(DoSyncLogin), $"send {tlnItem.Number}");
					*/

					packet = BinaryPacket.TeilnehmerToSyncReplyV3(tlnItem);
					sendData = packet.PacketBuffer;
					_stream.Write(sendData, 0, sendData.Length);
					Log(LogTypes.Info, nameof(DoSyncLogin), $"send {tlnItem.Number}");

					recvData = new byte[2];
					recvLen = _stream.Read(recvData, 0, recvData.Length);
					if (recvLen != 2 || recvData[0] != (byte)BinaryCommands.Acknowledge) return false;
					Log(LogTypes.Debug, nameof(DoSyncLogin), "ack received");

					tlnItem.Processed = true;
				}

				// send end of list
				packet = BinaryPacket.GetEndOfList();
				sendData = packet.PacketBuffer;
				_stream.Write(sendData, 0, sendData.Length);
				Log(LogTypes.Debug, nameof(DoSyncLogin), "send EndOfList");

				Disconnect();

				return true;
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(DoSyncLogin), "error", ex);
				return false;
			}
		}

		public List<TeilnehmerItem> DoSyncFullQuery()
		{
			Log(LogTypes.Info, nameof(DoSyncFullQuery), "do syncfullquery");

			try
			{
				// send sync login
				BinaryPacket packet = BinaryPacket.GetSyncFullQuery(_serverPin);
				byte[] sendData = packet.PacketBuffer;
				_stream.Write(sendData, 0, sendData.Length);

				byte[] recvData;
				int recvLen;
				List<TeilnehmerItem> tlnList = new List<TeilnehmerItem>();
				while (true)
				{
					recvData = new byte[512];
					recvLen = _stream.Read(recvData, 0, recvData.Length);
					Array.Resize(ref recvData, recvLen);

					if (recvLen == 2 && recvData[0] == (byte)BinaryCommands.EndOfList)
					{
						Log(LogTypes.Debug, nameof(DoSyncFullQuery), "EndOfList received");
						// end of list received
						break;
					}

					TeilnehmerItem tlnItem = null;
					int version = 0;
					if (recvData[0] == (byte)BinaryCommands.SyncReplyV2)
					{
						// peer reply v2 received
						packet = new BinaryPacket(recvData);
						tlnItem = BinaryPacket.SyncReplyV2ToTeilnehmer(packet);
						version = 2;
					}
					else if (recvData[0] == (byte)BinaryCommands.SyncReplyV3)
					{
						// peer reply v3 received
						packet = new BinaryPacket(recvData);
						tlnItem = BinaryPacket.SyncReplyV3ToTeilnehmer(packet);
						version = 3;
					}
					else
					{
						// error, invalid data received
						Log(LogTypes.Warn, nameof(DoSyncFullQuery), $"recv error len={recvLen} packet={recvData[0]}");
						break;
					}

					if (tlnItem == null)
					{
						Log(LogTypes.Warn, nameof(DoSyncFullQuery), $"invalid packet received");
						break;
					}

					Log(LogTypes.Debug, nameof(DoSyncFullQuery), $"v{version} recv {tlnItem.Number}");
					tlnList.Add(tlnItem);

					// send acknowledge
					packet = BinaryPacket.GetAcknowledge();
					sendData = packet.PacketBuffer;
					_stream.Write(sendData, 0, sendData.Length);
				}

				Log(LogTypes.Info, nameof(DoSyncFullQuery), $"{tlnList.Count} entries received");
				return tlnList;
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(DoSyncFullQuery), "error", ex);
				return null;
			}

		}

		/*
		private string GetSessionContext()
		{
			return RemoteName ?? RemoteIpAddress?.ToString();
		}
		*/

		private void Log(LogTypes logTypes, string method, string msg)
		{
			_logger.Log(logTypes, TAG, method, _tag2, msg);
		}

		private void Log(LogTypes logTypes, string method, string msg, Exception ex)
		{
			_logger.Error(TAG, method, _tag2, msg, ex);
		}

	}
}
