using CentralexServer;
using CentralexServer.BinaryProxy;
using ServerCommon.Logging;
using ServerCommon.SubscriberServer;
using ServerCommon.Utility;
using System.Net.Sockets;
using System.Text;

namespace Centralex.BinaryProxy
{
	internal class BinaryIncommingConnection: BinaryConnection
	{
		private const string TAG = nameof(BinaryConnection);
		private const string TAG2 = "";

		private SubscriberServer _subscriberServer;

		public BinaryIncommingConnection(TcpClient tcpClient, string logPath, LogTypes logLevel):
			base(tcpClient, ConnectionType.BinIn, logPath, logLevel)
		{
			_logger.Info(TAG, nameof(BinaryIncommingConnection), TAG2, "new incoming binary connection");
			_subscriberServer = new SubscriberServer(GlobalData.Config.GetSubscriberServerConfig(), GlobalData.Logger);
		}

		~BinaryIncommingConnection()
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

			_disposed = true;
		}

		#endregion Dispose

		public void Start()
		{
			_logger.Info(TAG, nameof(Start), TAG2, "incoming binary connection start");

			// connect to main subscriber-server
			_subscriberServer.Connect();

			while (IsConnected)
			{
				if (_lastPacket != null)
				{
					HandlePacket(_lastPacket);
					_lastPacket = null;
				}
				Thread.Sleep(100);
			}

			_subscriberServer.Disconnect();

			_logger.Info(TAG, nameof(Start), TAG2, "incoming binary connection end");
		}


		/// <summary>
		/// Receive data from client
		/// </summary>
		/// <param name="packet"></param>
		private void HandlePacket(BinaryPacket packet)
		{
			if (packet == null) return;

			switch(packet.CommandType)
			{
				case BinaryCommands.ClientUpdate:
					HandleClientUpdate(packet);
					break;
				case BinaryCommands.PeerQuery:
					HandlePeerQuery(packet);
					break;
				case BinaryCommands.Acknowledge:
					_ackRecevied = true;
					break;
				case BinaryCommands.PeerSearch:
					HandlePeerSearch(packet);
					break;
			}
		}

		private void HandleClientUpdate(BinaryPacket packet)
		{
			//string typeStr = $"recv proxy ClientUpdate {RemoteIpAddress}";

			byte[] data = packet.Data;
			int number = BitConverter.ToInt32(data, 0);
			int pin = BitConverter.ToUInt16(data, 4);
			int port = BitConverter.ToUInt16(data, 6);

			_logger.ConsoleLog(TAG, nameof(HandleClientUpdate), TAG2,
				$"error: direct client update not possible via centralex server, number={number}");
			SendPacket(BinaryPacket.GetError("invalid operation"));
			return;
		}

		private void HandlePeerQuery(BinaryPacket packet)
		{
			//string typeStr = $"recv proxy PeerQuery {RemoteIpAddress}";

			byte[] data = packet.Data;
			int len = data.Length;
			int version = data[4];

			if (len != 5 || version != 1)
			{
				_logger.ConsoleLog(TAG, nameof(HandlePeerQuery), TAG2,
						$"invalid peer query, len={len} version={version}");
				SendPacket(BinaryPacket.GetError("invalid packet"));
				return;
			}

			int number = BitConverter.ToInt32(data, 0);
			_logger.ConsoleLog(TAG, nameof(HandlePeerQuery), TAG2, $"BinaryPeerQuery number={number}");

			BinaryPeerQueryReply reply = _subscriberServer.BinarySendPeerQuery(packet.PacketBuffer);
			if (!reply.Valid)
			{
				_logger.ConsoleLog(TAG, nameof(HandlePeerQuery), TAG2, $"subscriber server reply: invalid");
				SendPacket(BinaryPacket.GetError(reply.Error));
				return;
			}

			SendPacket(new BinaryPacket(reply.PacketData));
		}

		private void HandlePeerSearch(BinaryPacket packet)
		{
			//string typeStr = $"recv proxy PeerSearch {RemoteIpAddress}";

			byte[] data = packet.Data;
			int version = data[0];
			int len = data.Length;
			if (len != 41 || version != 1)
			{
				_logger.ConsoleLog(TAG, nameof(HandlePeerQuery), TAG2,
						$"invalid peer search, len={len} version={version}");
				SendPacket(BinaryPacket.GetError("invalid packet"));
				return;
			}

			string search = Encoding.ASCII.GetString(data, 1, data.Length - 1).Trim([ '\x00' ]);
			_logger.ConsoleLog(TAG, nameof(HandlePeerSearch), TAG2, $"search = '{search}'");

			BinaryPeerSearchReply reply = _subscriberServer.BinarySendPeerSearch(packet.PacketBuffer);
			if (!reply.Valid)
			{
				_logger.ConsoleLog(TAG, nameof(HandlePeerSearch), TAG2, $"subscriber server reply: invalid");
				SendPacket(BinaryPacket.GetError(reply.Error));
				return;
			}

			_logger.ConsoleLog(TAG, nameof(HandlePeerSearch), TAG2, $"{reply.List.Count} peers found");

			foreach (byte[] d in reply.List)
			{
				_ackRecevied = false;

				SendPacket(new BinaryPacket(d));

				TickTimer timeout = new TickTimer();
				while (true)
				{
					if (timeout.IsElapsedMilliseconds(5000))
					{
						// timeout
						_logger.ConsoleLog(TAG, nameof(HandlePeerSearch), TAG2, "PeerSearch ack timeout");
						SendPacket(BinaryPacket.GetError("ack timeout"));
						return;
					}
					if (_ackRecevied)
					{
						_logger.ConsoleLog(TAG, nameof(HandlePeerSearch), TAG2,
								$"ack received {timeout.ElapsedMilliseconds}");
						break;
					}
					Thread.Sleep(10);
				}
			}

			SendPacket(BinaryPacket.GetEndOfList());
		}
	}
}
