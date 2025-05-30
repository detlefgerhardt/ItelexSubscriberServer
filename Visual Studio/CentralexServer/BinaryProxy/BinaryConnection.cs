using Centralex.BinaryProxy;
using ServerCommon.Logging;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace CentralexServer.BinaryProxy
{
	internal class BinaryConnection
	{
		private const string TAG = nameof(BinaryConnection);
		private const string TAG2 = "";

		protected TcpClient _tcpClient;

		protected Logger _logger;

		protected string _tag2 = "";

		protected BinaryPacket _lastPacket;

		private object _sendCmdLock = new object();

		protected bool IsConnected;

		protected ConnectionType _connectionType;

		private bool _disconnectActive;

		protected bool _ackRecevied = false;

		private bool _clientReceiveTimerActive;
		private System.Timers.Timer _clientReceiveTimer;
		private object _clientReceiveBufferLock = new object();
		private Queue<byte> _clientReceiveBuffer;

		public IPAddress RemoteIpAddress
		{
			get
			{
				if (_tcpClient?.Client == null) return null;

				try
				{
					IPEndPoint endPoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;
					return endPoint.Address;
				}
				catch (Exception)
				{
					return null;
				}
			}
		}


		public BinaryConnection(TcpClient tcpClient, ConnectionType connType, string logPath, LogTypes logLevel)
		{
			_tcpClient = tcpClient;
			_connectionType = connType;
			_logger = GlobalData.Logger;
			IsConnected = true;
			_disconnectActive = false;
			_lastPacket = null;

			_logger.Info(TAG, nameof(BinaryConnection), _tag2,
					$"--- New binary connection from ip={RemoteIpAddress.ToString()} ---");

			_clientReceiveBuffer = new Queue<byte>();
			StartReceive();

			_clientReceiveTimerActive = false;
			_clientReceiveTimer = new System.Timers.Timer(50);
			_clientReceiveTimer.Elapsed += ClientReceiveTimer_Elapsed;
			_clientReceiveTimer.Start();
		}

		public bool ConnectOut(string remoteHost, int remotePort)
		{
			try
			{
				_tcpClient = new TcpClient();
				if (!_tcpClient.ConnectAsync(remoteHost, remotePort).Wait(2000))
				{
					_logger.Debug(TAG, nameof(ConnectOut), _tag2,
							$"outgoing connection {remoteHost}:{remotePort} failed");
					IsConnected = false;
					return false;
				}

				//IPEndPoint endPoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;
				//RemoteClientAddrStr = $"{endPoint.Address}:{endPoint.Port}";
			}
			catch (Exception ex)
			{
				_logger.Warn(TAG, nameof(ConnectOut), _tag2, $"error {ex.Message}");
				//CallStatus = CallStatusEnum.NoConn;
				return false;
			}

			//ConnectionState = ConnectionStates.TcpConnected;
			return true;
		}

		protected void DisconnectTcp(DisconnectReasons reason)
		{
			_logger.Debug(TAG, nameof(DisconnectTcp), _tag2, $"Disconnect reason={reason}, IsConnected={IsConnected}");

			if (_disconnectActive)
			{
				_logger.Debug(TAG, nameof(DisconnectTcp), _tag2, "Disconnect already active");
				return;
			}
			_disconnectActive = true;

			try
			{
				if (!IsConnected)
				{
					_logger.Debug(TAG, nameof(DisconnectTcp), _tag2, "connection already disconnected");
					return;
				}

				IsConnected = false;
				_logger.Debug(TAG, nameof(DisconnectTcp), _tag2, $"IsConnected={IsConnected}");

				/*
				try
				{
					_clientReceiveTimer?.Stop();
				}
				catch (Exception ex)
				{
				}
				*/

				if (_tcpClient.Client != null)
				{
					_logger.Debug(TAG, nameof(DisconnectTcp), _tag2, "close TcpClient");
					if (_tcpClient.Connected)
					{
						try
						{
							NetworkStream stream = _tcpClient.GetStream();
							stream.Close();
						}
						catch (Exception ex)
						{
							_logger.Warn(TAG, nameof(DisconnectTcp), _tag2, $"stream.Close {ex.Message}");
						}
					}

					try
					{
						_tcpClient.Close();
					}
					catch (Exception ex)
					{
						_logger.Warn(TAG, nameof(DisconnectTcp), _tag2, $"_tcpClient.Close {ex.Message}");
					}
				}

				_logger.Debug(TAG, nameof(DisconnectTcp), _tag2, "connection closed");

				//Thread.Sleep(1000);
			}
			finally
			{
				_disconnectActive = false;
			}
		}


		#region Receive data

		private void StartReceive()
		{
			if (!IsConnected) return;

			byte[] buffer = new byte[1024];
			try
			{
				_tcpClient.Client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, DataReceived, buffer);
			}
			catch (Exception)
			{
			}
		}

		private void DataReceived(IAsyncResult ar)
		{
			try
			{
				int dataReadCount;
				try
				{
					dataReadCount = _tcpClient.Client.EndReceive(ar);
					if (dataReadCount == 0)
					{
						DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
						return;
					}
				}
				catch (Exception ex)
				{
					_logger.Warn(TAG, nameof(ProcessReceivedData), _tag2, $"error {ex.Message}");
					DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
					return;
				}

				byte[] byteData = ar.AsyncState as byte[];
				Array.Resize(ref byteData, dataReadCount);
				AddReceiveBuffer(byteData);

				//lock (_clientReceiveBufferLock)
				{
					try
					{
						if (_clientReceiveBuffer.Count < 2) return;
						byte cmdType = _clientReceiveBuffer.ElementAt(0); // cmd

						byte cmdLen = _clientReceiveBuffer.ElementAt(1); // len
						if (_clientReceiveBuffer.Count < cmdLen + 2) return;

						byte[] packetData = new byte[cmdLen + 2];
						for (int i = 0; i < cmdLen + 2; i++)
						{
							packetData[i] = _clientReceiveBuffer.Dequeue();
						}
						BinaryPacket packet = new BinaryPacket(packetData);
						//Debug.WriteLine($"Client ReceivedPacket={packet}");
						if (packet.CommandType == BinaryCommands.Acknowledge)
						{
							_ackRecevied = true;
							return;
						}

						_lastPacket = packet;
					}
					catch (Exception ex)
					{
						_logger.Warn(TAG, nameof(ProcessReceivedData), _tag2,
							$"_clientReceiveBuffer={_clientReceiveBuffer} {ex.Message}");
					}
				}
			}
			finally
			{
				StartReceive();
			}
		}

		private void AddReceiveBuffer(byte[] data)
		{
			lock (_clientReceiveBufferLock)
			{
				foreach (byte b in data)
				{
					_clientReceiveBuffer.Enqueue(b);
				}
			}
		}

		private void ClientReceiveTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (_clientReceiveTimerActive) return;
			_clientReceiveTimerActive = true;
			try
			{
				ProcessReceivedData();
			}
			finally
			{
				_clientReceiveTimerActive = false;
			}
		}

		private void ProcessReceivedData()
		{
			if (!IsConnected) return;

			try
			{
				if (!_tcpClient.Connected || !_tcpClient.Client.Connected)
				{
					_logger.Debug(TAG, nameof(ProcessReceivedData), _tag2,
							$"_client.Connected={_tcpClient.Connected} _client.Client.Connected={_tcpClient.Client.Connected}");
					//Disconnect(DisconnectReasons.NotConnected);
					//GlobalData.Logger.Debug(TAG, nameof(ProcessReceivedData),
					//		$"_client.Connected={_tcpClient.Connected} _client.Client.Connected={_tcpClient.Client.Connected}");
					DisconnectTcp(DisconnectReasons.TcpDisconnect);
					return;
				}
			}
			catch (Exception ex)
			{
				_logger.Warn(TAG, nameof(ProcessReceivedData), _tag2,
						$"_client.Connected={_tcpClient?.Connected} _client.Client.Connected={_tcpClient?.Client?.Connected} {ex.Message}");
				return;
			}

			byte[] preBuffer = null;
			try
			{
				// early check to save computing time
				if (_tcpClient.Available == 0 && _clientReceiveBuffer.Count == 0) return;

				int avail = _tcpClient.Available;
				if (avail > 0)
				{
					preBuffer = new byte[avail];
					_tcpClient.Client.Receive(preBuffer, avail, SocketFlags.None);
				}
				else
				{
					preBuffer = new byte[0];
				}
			}
			catch (Exception ex)
			{
				_logger.Warn(TAG, nameof(ProcessReceivedData), _tag2, $"preBuffer handling {ex.Message}");
			}

			//lock (_clientReceiveBufferLock)
			{
				try
				{
					foreach (byte b in preBuffer)
					{
						_clientReceiveBuffer.Enqueue(b);
					}

					if (_clientReceiveBuffer.Count == 0) return;

					while (true)
					{
						if (_clientReceiveBuffer.Count < 2) return;
						byte cmdType = _clientReceiveBuffer.ElementAt(0); // cmd

						byte cmdLen = _clientReceiveBuffer.ElementAt(1); // len
						if (_clientReceiveBuffer.Count < cmdLen + 2) return;

						byte[] packetData = new byte[cmdLen + 2];
						for (int i = 0; i < cmdLen + 2; i++)
						{
							packetData[i] = _clientReceiveBuffer.Dequeue();
						}
						BinaryPacket packet = new BinaryPacket(packetData);
						//Debug.WriteLine($"Client ReceivedPacket={packet}");
						if (packet.CommandType == BinaryCommands.Acknowledge)
						{
							_ackRecevied = true;
							return;
						}

						_lastPacket = packet;
					}
				}
				catch (Exception ex)
				{
					_logger.Warn(TAG, nameof(ProcessReceivedData), _tag2,
						$"_clientReceiveBuffer={_clientReceiveBuffer} {ex.Message}");
				}
			}
		}

		#endregion Receive data

		#region Send data

		protected void SendPacket(BinaryPacket packet)
		{
			if (!IsConnected || _tcpClient?.Client == null) return;

			lock (_sendCmdLock)
			{
				if (!IsConnected) return;

				BinaryCommands cmd = packet.CommandType;

				try
				{
					//Console.WriteLine($"SendPacket BeginSend");
					_tcpClient.Client.BeginSend(packet.PacketBuffer, 0, packet.PacketBuffer.Length, SocketFlags.None, EndSend, null);
				}
				catch (SocketException sockEx)
				{
					if ((uint)sockEx.HResult == 0x80004005)
					{
						_logger.Warn(TAG, nameof(SendPacket), _tag2,
							$"cmd={cmd}, connection closed by remote (HResult=0x{(uint)sockEx.HResult:X08})");
					}
					else
					{
						_logger.Warn(TAG, nameof(SendPacket), _tag2, $"{cmd} {sockEx.Message}");
					}
					DisconnectTcp(DisconnectReasons.TcpDisconnect);
				}
				catch (Exception ex)
				{
					_logger.Warn(TAG, nameof(SendPacket), _tag2, $"{cmd} {ex.Message}");
					DisconnectTcp(DisconnectReasons.SendCmdError);
				}
			}
		}

		private void EndSend(IAsyncResult ar)
		{
			if (!IsConnected) return;

			try
			{
				//Console.WriteLine($"SendPacket EndSend");
				_tcpClient.Client.EndSend(ar);
				if (!_tcpClient.Connected)
				{
					DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
				}
			}
			catch (Exception)
			{
			}
		}

		#endregion Send data

	}
}
