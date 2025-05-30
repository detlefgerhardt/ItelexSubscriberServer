using CentralexCommon;
using ServerCommon.Logging;
using ServerCommon.Utility;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CentralexServer.CentralexConnections
{
	internal class CentralexConnection
    {
        private const string TAG = nameof(CentralexConnection);

		protected enum DisconnectReasons
        {
            Error,
            TcpDisconnect,
            SendCmdError,
            AuthTimeout,
            HeartbeatTimeout,
            NoClientConnected,
            CallerDisconncets,
            DisconnetFromClient,
            SubscriberServerError,
            SubscribeServerAuth,
            InternalError,
            EndCmdReceived,
            InvalidNumber,
            InvalidPin,
            UnknownClient,
            TcpDisconnectByRemote,
            ClientOccupied,
            /*
			Logoff,
			Reject,
			LoginError,
			ServiceShutdown,
			AckTimeout,
			SendReceiveTimeout,
			TcpStartReceiveError,
			TcpDataReceivedError,
			Dispose,
			MultipleLogin,
			SendPin,
			None
			*/
        }


		protected TcpClient _tcpClient;

        protected Logger _logger;

        protected string _tag2 = "";

        private object _sendCmdLock = new object();
        //private bool _clientReceiveTimerActive { get; set; }
        //private System.Timers.Timer _clientReceiveTimer;

        private bool _disconnectActive;

        private object _clientReceiveBufferLock = new object();
        private Queue<byte> _clientReceiveBuffer;

        protected bool IsConnected;

        public int LocalPort
        {
            get
            {
                if (_tcpClient?.Client == null) return 0;

                try
                {
                    IPEndPoint endPoint = (IPEndPoint)_tcpClient.Client.LocalEndPoint;
                    return endPoint.Port;
                }
                catch
                {
                    return 0;
                }
            }
        }

		public IPAddress RemoteIpAddress
		{
			get
			{
				if (_tcpClient?.Client == null) return null;

				try
				{
					IPEndPoint endPoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;
					string ipStr = endPoint.Address.ToString();
					return CommonHelper.GetIp4AddrFromHostname(ipStr);
				}
				catch (Exception)
				{
					return null;
				}
			}
		}

		public CentralexConnection(TcpClient tcpClient, ConnectionType connType, int? number, string logPath,
            string logName, LogTypes logLevel)
        {
            _tcpClient = tcpClient;
            _logger = GlobalData.Logger;

            IsConnected = true;
            _disconnectActive = false;

			_clientReceiveBuffer = new Queue<byte>();
			//StartReceive();

            /*
            _clientReceiveTimerActive = false;
            _clientReceiveTimer = new System.Timers.Timer(50);
            //_clientReceiveTimer.Elapsed += ClientReceiveTimer_Elapsed;
            _clientReceiveTimer.Start();
            */
        }

        ~CentralexConnection()
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

        #region Send data


        protected void SendHeartbeatCmd()
        {
            SendCmd(ItelexCommands.Heartbeat);
        }

        protected void SendRejectCmd(string reason)
        {
            byte[] data = Encoding.ASCII.GetBytes(reason);
            SendCmd(ItelexCommands.Reject, data);
        }

        protected void SendEndCmd()
        {
            SendCmd(ItelexCommands.End);
        }

        protected void SendConfirmCmd()
        {
            SendCmd(ItelexCommands.RemoteConfirm);
        }

        protected void SendRemoteCallCmd()
        {
            SendCmd(ItelexCommands.RemoteCall);
        }

        protected void SendCmd(ItelexCommands cmd, byte[] data = null)
        {
            if (!IsConnected || _tcpClient?.Client == null) return;

            lock (_sendCmdLock)
            {
                int cmdCode = (int)cmd;

                if (!IsConnected) return;

                byte[] sendData;
                if (data != null)
                {
                    sendData = new byte[data.Length + 2];
                    sendData[0] = (byte)cmdCode;
                    sendData[1] = (byte)data.Length;
                    Buffer.BlockCopy(data, 0, sendData, 2, data.Length);
                }
                else
                {
                    sendData = new byte[2];
                    sendData[0] = (byte)cmdCode;
                    sendData[1] = 0;
                }

                ItelexPacket packet = new ItelexPacket(sendData);

                switch ((ItelexCommands)packet.Command)
                {
                    case ItelexCommands.BaudotData:
                    case ItelexCommands.Heartbeat:
                    case ItelexCommands.Ack:
                    case ItelexCommands.DirectDial:
                    case ItelexCommands.End:
                    case ItelexCommands.Reject:
                    case ItelexCommands.ProtocolVersion:
                    case ItelexCommands.SelfTest:
                    case ItelexCommands.RemoteConfig:
                    case ItelexCommands.ConnectRemote:
                    case ItelexCommands.RemoteConfirm:
                    case ItelexCommands.RemoteCall:
                    case ItelexCommands.AcceptCallRemote:
                        break;
                }

                try
                {
                    _tcpClient.Client.BeginSend(sendData, 0, sendData.Length, SocketFlags.None, EndSend, null);
                }
                catch (SocketException sockEx)
                {
                    if ((uint)sockEx.HResult == 0x80004005)
                    {
                        _logger.Warn(TAG, nameof(SendCmd), _tag2,
                            $"cmd={cmd}, connection closed by remote (HResult=0x{(uint)sockEx.HResult:X08})");
                    }
                    else
                    {
                        _logger.Warn(TAG, nameof(SendCmd), _tag2, $"{cmd} {sockEx.Message}");
                    }
                    DisconnectTcp(DisconnectReasons.TcpDisconnect);
                }
                catch (Exception ex)
				{
                    _logger.Warn(TAG, nameof(SendCmd), _tag2, $"{cmd} {ex.Message}");
                    DisconnectTcp(DisconnectReasons.SendCmdError);
                }
            }
        }

        private void EndSend(IAsyncResult ar)
        {
            if (!IsConnected) return;

            try
            {
                _tcpClient.Client.EndSend(ar);
                if (!_tcpClient.Connected)
                {
                    DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
                }
            }
            catch
            {
            }
        }

		#endregion Send data

		#region Receive data

		protected void StartReceive()
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
            if (!IsConnected) return;

			int avail = _tcpClient.Available;
			if (avail > 0)
			{
				byte[] byteData = new byte[avail];
				_tcpClient.Client.Receive(byteData, avail, SocketFlags.None);
				AddReceiveBuffer(byteData);
			}

			try
			{
                int dataReadCount;
                try
                {
                    dataReadCount = _tcpClient.Client.EndReceive(ar);
                    if (dataReadCount == 0)
                    {
						_logger.Debug(TAG, nameof(DataReceived), _tag2, "dataReadCount == 0");
						DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
                        return;
                    }
                }
                catch (Exception ex)
                {
					_logger.Warn(TAG, nameof(DataReceived), _tag2, $"error EndReceive {ex.Message}");
					DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
                    return;
                }

                byte[] byteData = ar.AsyncState as byte[];
                Array.Resize(ref byteData, dataReadCount);

				AddReceiveBuffer(byteData);

                lock (_clientReceiveBufferLock)
                {
                    try
                    {
                        while (true)
                        {
							if (_clientReceiveBuffer.Count < 2) return;

                            byte cmdType = _clientReceiveBuffer.ElementAt(0); // cmd
						
                            if (cmdType > 0x09 && cmdType < 0x81 || cmdType > 0x84)
                            {
								// remove invalid cmdType
								_clientReceiveBuffer.Dequeue();
                                return;
                            }

                            byte cmdLen = _clientReceiveBuffer.ElementAt(1); // len
							if (_clientReceiveBuffer.Count < cmdLen + 2) return;

                            byte[] packetData = new byte[cmdLen + 2];
                            for (int i = 0; i < cmdLen + 2; i++)
                            {
                                packetData[i] = _clientReceiveBuffer.Dequeue();
                            }
							ItelexPacket packet = new ItelexPacket(packetData);
							//Debug.WriteLine($"Client ReceivedPacket={packet}");
							ReceivedPacket(packet);
							StartReceive();
						}
					}
                    catch (Exception ex)
                    {
                        _logger.Warn(TAG, nameof(DataReceived), _tag2,
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

#if false
		private void ClientReceiveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_clientReceiveTimerActive) return;
            _clientReceiveTimerActive = true;
            try
            {
                //ProcessReceivedData();
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
                    _connectionLogger.ItelexLog(LogTypes.Debug, TAG, nameof(ProcessReceivedData),
                            $"_client.Connected={_tcpClient.Connected} _client.Client.Connected={_tcpClient.Client.Connected}");
                    //Disconnect(DisconnectReasons.NotConnected);
                    GlobalData.Logger.Debug(TAG, nameof(ProcessReceivedData),
                            $"_client.Connected={_tcpClient.Connected} _client.Client.Connected={_tcpClient.Client.Connected}");
                    Console.WriteLine(
                            $"_client.Connected={_tcpClient.Connected} _client.Client.Connected={_tcpClient.Client.Connected}");
                    DisconnectTcp(DisconnectReasons.TcpDisconnect);
                    return;
                }
            }
            catch (Exception ex)
            {
                _connectionLogger?.ItelexLog(LogTypes.Error, TAG, nameof(ProcessReceivedData),
                        $"_client.Connected={_tcpClient?.Connected} _client.Client.Connected={_tcpClient?.Client?.Connected}", ex);
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
                _connectionLogger?.ItelexLog(LogTypes.Error, TAG, nameof(ProcessReceivedData), "preBuffer handling", ex);
            }

            lock (_clientReceiveBufferLock)
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
                        if (cmdType > 0x09 && cmdType < 0x81 || cmdType > 0x84)
                        {
                            // remove invalid cmdType
                            _clientReceiveBuffer.Dequeue();
                            continue;
                        }

                        byte cmdLen = _clientReceiveBuffer.ElementAt(1); // len
                        if (_clientReceiveBuffer.Count < cmdLen + 2) return;

                        byte[] packetData = new byte[cmdLen + 2];
                        for (int i = 0; i < cmdLen + 2; i++)
                        {
                            packetData[i] = _clientReceiveBuffer.Dequeue();
                        }
                        ItelexPacket packet = new ItelexPacket(packetData);
                        //Debug.WriteLine($"Client ReceivedPacket={packet}");
                        ReceivedPacket(packet);
                    }
                }
                catch (Exception ex)
                {
                    _connectionLogger?.ItelexLog(LogTypes.Error, TAG, nameof(ProcessReceivedData),
                        $"_clientReceiveBuffer={_clientReceiveBuffer} ", ex);
                }
            }
        }
#endif

		#endregion Receive data

		protected void Reject(DisconnectReasons reason)
        {
            string rejectStr = "";
            switch (reason)
            {
                case DisconnectReasons.Error:
                case DisconnectReasons.SubscriberServerError:
                case DisconnectReasons.SubscribeServerAuth:
                case DisconnectReasons.InternalError:
                case DisconnectReasons.InvalidNumber:
                case DisconnectReasons.InvalidPin:
                    rejectStr = "der";
                    break;
                case DisconnectReasons.AuthTimeout:
                case DisconnectReasons.HeartbeatTimeout:
                    rejectStr = "timeout";
                    break;
                case DisconnectReasons.UnknownClient:
                    rejectStr = "unknown client";
                    break;
                case DisconnectReasons.ClientOccupied:
                    rejectStr = "occ";
                    break;
                case DisconnectReasons.NoClientConnected:
                    rejectStr = "nc";
                    break;
                default:
                    rejectStr = "der";
                    break;
            }

            Reject(rejectStr);
        }

		protected void Reject(string reason)
		{
			_logger.Debug(TAG, nameof(Reject), _tag2, $"Reject reason={reason} '{reason}' ");

			SendRejectCmd(reason);
			Thread.Sleep(2000);
			DisconnectTcp(reason);
		}

        protected void DisconnectTcp(DisconnectReasons reason)
        {
            DisconnectTcp(reason.ToString());
        }

		protected void DisconnectTcp(string reason)
        {
            //Console.WriteLine($"DisconnectTcp reason={reason}");
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

        protected virtual void ReceivedPacket(ItelexPacket packet)
        {
        }
    }
}