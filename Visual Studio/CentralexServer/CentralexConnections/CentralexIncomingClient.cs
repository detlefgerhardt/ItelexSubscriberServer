using CentralexCommon;
using CentralexServer.Data;
using ItelexTlnServer.Data;
using ServerCommon.Logging;
using ServerCommon.SubscriberServer;
using ServerCommon.Utility;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace CentralexServer.CentralexConnections
{
	internal enum ClientStates { NotConnected = 0, Ready = 1, Call = 2 }

    internal enum ClientConnectionStates { Offline, WaitAuth, Standby, Connecting, Connected }

    internal class CentralexIncomingClient : CentralexConnection
    {
        private const string TAG = nameof(CentralexIncomingClient);

        private SubscriberServer _subscriberServer;

        private CentralexServerDatabase _database;

        private CentralexConnectionManager _centralexConnectionManager;

		private ConcurrentQueue<ItelexPacket> _receivedClientPackets;

        private ConcurrentQueue<ItelexPacket> _receivedCallerPackets;

        private ClientConnectionStates _state;

        private ClientItem _clientItem;

        private string _host;

        private bool _callerDisconnect;

        public int? ClientNumber => _clientItem?.Number;

        public int? ClientPort => _clientItem?.Port;

        public CentralexIncomingClient(TcpClient tcpClient, string logPath, string logName, LogTypes logLevel, string host) :
            base(tcpClient, ConnectionType.CentralexClient, null, logPath, logName, logLevel)
        {
			_host = host;
			_tag2 = $"Incoming client connection from {host}";

			try
			{
				_logger.Debug(TAG, nameof(CentralexIncomingClient), _tag2, "new client connection");
				_receivedCallerPackets = new ConcurrentQueue<ItelexPacket>();
                _receivedClientPackets = new ConcurrentQueue<ItelexPacket>();
                _subscriberServer = new SubscriberServer(GlobalData.Config.GetSubscriberServerConfig(), GlobalData.Logger);
                _database = CentralexServerDatabase.Instance;
                _centralexConnectionManager = CentralexConnectionManager.Instance;
                _state = ClientConnectionStates.WaitAuth;
                //ClientState = ClientStates.NotConnected;
                _clientItem = null;
                _caller = null;
                _callerDisconnect = false;
            }
            catch(Exception ex)
            {
                _logger.Error(TAG, nameof(CentralexIncomingClient), _tag2, "error", ex);
            }
        }

        protected override void ReceivedPacket(ItelexPacket packet)
        {
            try
            {
                base.ReceivedPacket(packet);
                _receivedClientPackets.Enqueue(packet);
            }
            catch(Exception ex)
            {
                _logger.Warn(TAG, nameof(ReceivedPacket), _tag2, $"error {ex.Message}");
            }
        }

        public void Start()
        {
            try
            {
                _logger.Debug(TAG, nameof(Start), _tag2, "start client connection");

                string endReason = "";

                TickTimer timeoutTimer = new TickTimer(true);
                TickTimer heartbeatTimer = new TickTimer(true);

                StartReceive();

                while (IsConnected)
                {
                    if (_callerDisconnect)
                    {
                        _logger.Info(TAG, nameof(Start), _tag2, $"caller disconnencts, IsConnected={IsConnected}");
                        if (IsConnected)
                        {
                            SendEndCmd();
                            Thread.Sleep(2000);
                            Disconnect(DisconnectReasons.CallerDisconncets);
                        }
                        _caller = null;
                        break;
                    }

                    Debug.WriteLine(_state);
                    ItelexPacket packet;
                    switch (_state)
                    {
                        case ClientConnectionStates.WaitAuth:
                            if (timeoutTimer.IsElapsedMilliseconds(2000))
                            {
                                _logger.Error(TAG, nameof(Start), _tag2, "AuthTimeout");
                                Disconnect(DisconnectReasons.AuthTimeout);
                            }

                            if (_receivedClientPackets.TryDequeue(out packet))
                            {
                                if (CheckCommand(packet, ItelexCommands.ConnectRemote))
                                {
                                    _logger.Debug(TAG, nameof(Start), _tag2, "Connect remote received from client");
                                    if (!ConnectRemote(packet)) continue;
                                    timeoutTimer.Start();
                                    heartbeatTimer.Start();
                                    _state = ClientConnectionStates.Standby;
                                    _clientItem.SetClientState(ClientStates.Ready);
                                    _tag2 = $"Incoming client connection {_clientItem.Port} / {_clientItem.Number} from {_host} ";
                                    _logger.Info(TAG, nameof(Start), _tag2,
                                            $"new client connection {_clientItem.Port} / {_clientItem.Number}");
                                    continue;
                                }
                            }
                            break;
                        case ClientConnectionStates.Standby:
                            if (heartbeatTimer.IsElapsedMilliseconds(15000))
                            {
                                SendHeartbeatCmd();
                                heartbeatTimer.Start();
                            }
                            if (timeoutTimer.IsElapsedMilliseconds(35000))
                            {
                                // timeout
                                _logger.Notice(TAG, nameof(Start), _tag2, "Heartbeat timeout");
                                Disconnect(DisconnectReasons.HeartbeatTimeout);
                            }

                            if (_receivedClientPackets.TryDequeue(out packet))
                            {
                                if (CheckCommand(packet, ItelexCommands.Heartbeat))
                                {
                                    timeoutTimer.Start();
                                    continue;
                                }
                                if (CheckCommand(packet, ItelexCommands.End))
                                {
                                    byte[] data = packet.Data;
                                    endReason = "nc";
                                    if (data != null && data.Length > 0)
                                    {
                                        endReason = Encoding.ASCII.GetString(data, 0, data.Length).Trim(['\x00']);
                                    }
                                    _logger.ConsoleLog(TAG, nameof(Start), _tag2,
                                            $"{_clientItem.Number} end received from client, reason={endReason}");
                                    _clientItem.DisconnectReason = endReason;

                                    _logger.Info(TAG, nameof(Start), _tag2,
                                            $"end received from caller, reason={endReason}");
                                    Disconnect(DisconnectReasons.EndCmdReceived);
                                    continue;
                                }
                            }
                            break;
                        case ClientConnectionStates.Connecting:
                            if (_receivedClientPackets.TryDequeue(out packet))
                            {
                                if (CheckCommand(packet, ItelexCommands.AcceptCallRemote))
                                {
                                    _logger.Info(TAG, nameof(Start), _tag2, "AcceptCallRemote packet received from client");
                                    _state = ClientConnectionStates.Connected;
                                    continue;
                                }
                            }
                            break;
                        case ClientConnectionStates.Connected:
                            if (_receivedClientPackets.TryDequeue(out packet))
                            {
                                CallerSend(packet);
                            }
                            if (_receivedCallerPackets.TryDequeue(out packet))
                            {
                                SendCmd(packet.CommandType, packet.Data);
                            }
                            break;
                    }
                    Thread.Sleep(10);
                }

                if (_clientItem != null)
                {
                    // update ClientList
                    if (string.IsNullOrEmpty(endReason)) endReason = "nc";
                    _clientItem.SetClientState(ClientStates.NotConnected);
                    _clientItem.DisconnectReason = endReason; // update list

                    if (!_database.ClientsUpdateLastChanged(_clientItem.Number))
                    {
                        _logger.Fatal(TAG, nameof(Start), _tag2, "error updating client");
                    }
                }

                _logger.Info(TAG, nameof(Start), _tag2,
                        $"{_clientItem?.Number} client connection ends reason='{_clientItem?.DisconnectReason}' ({_clientItem?.Port} / {_clientItem?.Number})");
            }
            catch(Exception ex)
            {
				_logger.Error(TAG, nameof(Start), _tag2, "error", ex);
			}

		}

        private bool CheckCommand(ItelexPacket packet, ItelexCommands cmdType)
        {
            return packet != null && packet.CommandType == cmdType;
        }

        private bool ConnectRemote(ItelexPacket packet)
        {
            int remoteNumber;
            int remotePin;

			try
            {
                if (packet.Len != 6)
                {
                    // invalid packet
                    _logger.Debug(TAG, nameof(ConnectRemote), _tag2, $"Invalid packet, len = {packet.Len}");
                    return false;
                }
                // 81 06 4D 97 53 00 21 B4
                remoteNumber = (int)BitConverter.ToUInt32(packet.Data, 0);
                remotePin = BitConverter.ToUInt16(packet.Data, 4);
                if (remoteNumber == 0)
                {
                    // invalid remote number
                    _logger.Warn(TAG, nameof(ConnectRemote), _tag2, $"Invalid number = {remoteNumber}");
                    Reject(DisconnectReasons.InvalidNumber);
                    return false;
                }
            }
            catch(Exception ex)
            {
				_logger.Error(TAG, nameof(ConnectRemote), _tag2, "error", ex);
                return false;
			}

			_logger.Debug(TAG, nameof(ConnectRemote), _tag2, $"{remoteNumber} {remotePin}");

            /*
            if (remotePin == 0)
            {
                // invalid pin
                _logger.Warn(TAG, nameof(ConnectRemote), $"Invalid pin = {remotePin}");
                Reject(DisconnectReasons.InvalidPin);
                return false;
            }
            */

            try
            {
				if (!_subscriberServer.Connect())
                {
                    _logger.Error(TAG, nameof(ConnectRemote), _tag2, "Subscriber server error: connect failed");
                    Reject(DisconnectReasons.SubscriberServerError);
                    return false;
                }

                // check if number exists
                PeerQueryReply queryReply = _subscriberServer.SendPeerQuery(remoteNumber);
                if (!queryReply.Valid)
                {
                    // number not on subscriber server
                    _logger.Warn(TAG, nameof(ConnectRemote), _tag2, $"Unknown client, number={remoteNumber}");
                    Reject(DisconnectReasons.UnknownClient);
                    return false;
                }

				ClientItem existingClient = null;
				ClientItem newClient = null;
				int? port = null;
				lock (_database.CentralexLocker)
                {
					existingClient = _centralexConnectionManager.GetClient(remoteNumber);
                    if (existingClient == null)
                    {
                        // add new client

                        port = _centralexConnectionManager.FindFreePort();
                        if (port == null)
                        {
                            // no free port ???
                            _logger.Error(TAG, nameof(Start), _tag2, $"Error: no free port, number={existingClient.Number}");
                            Reject(DisconnectReasons.InternalError);
                            return false;
                        }
                        DateTime utcNow = DateTime.UtcNow;
						newClient = new ClientItem()
                        {
                            Number = remoteNumber,
                            Port = port.Value,
                            Name = queryReply.Data.LongName,
                            Pin = remotePin,
							CreatedUtc = utcNow,
                            LastChangedUtc = utcNow,
                            State = (int)ClientStates.NotConnected,
                        };
					}
					else
                    {
                        // update existing client

                        //existingClient = _centralexConnectionManager.GetClient(remoteNumber);
                        if (queryReply.Data.LongName != existingClient.Name || remotePin != existingClient.Pin)
                        {
                            // update name and/or pin
                            existingClient.Name = queryReply.Data.LongName;
                            existingClient.Pin = remotePin;
							// update name
							_logger.Info(TAG, nameof(ConnectRemote), _tag2, 
                                    $"Update client name or pin {port } / {remoteNumber}");
							if (!_database.ClientsUpdateNameAndPin(remoteNumber, remotePin, queryReply.Data.LongName))
                            {
                                _logger.Fatal(TAG, nameof(ConnectRemote), _tag2,
									$"Database error: updating name '{queryReply.Data.LongName}");
                                // no disconnect here, error is not fatal
                            }
                        }
                        port = existingClient.Port;
                    }

					// send client update to subscriber server
					_logger.Info(TAG, nameof(ConnectRemote), _tag2, $"Send subscriber server update {port} / {remoteNumber}");
					ClientUpdateReply reply = _subscriberServer.SendClientUpdate(remoteNumber, remotePin, port.Value);
					if (!reply.Success)
					{
						_logger.Error(TAG, nameof(ConnectRemote), _tag2,
								$"Error sending subscriber server update, {port} / {remoteNumber}");
						Reject(reply.Error);
						return false;
					}

                    if (newClient != null)
                    {
						_logger.Info(TAG, nameof(ConnectRemote), _tag2, $"New client: {port} / {remoteNumber}");

						if (!_database.ClientsInsert(newClient))
                        {
                            // database error
                            _logger.Fatal(TAG, nameof(ConnectRemote), _tag2,
									$"Database error: inserting new client {port} / {remoteNumber}");
                            Reject(DisconnectReasons.Error);
                            return false;
                        }
                        _centralexConnectionManager.AddClient(newClient);
                    }
				}

				SendConfirmCmd();

				_clientItem = existingClient;

				return true;
            }
            catch(Exception ex)
            {
				_logger.Error(TAG, nameof(ConnectRemote), _tag2, "error", ex);
                return false;
			}
			finally
            {
                _subscriberServer.Disconnect();
            }
        }

        protected void Disconnect(DisconnectReasons reason, string reasonStr = "")
        {
            _logger.Debug(TAG, nameof(Disconnect), _tag2, $"Client disconnect, reason={reason} '{reasonStr}'");
            if (IsConnected)
            {
                _caller?.ClientDisconnect();
            }
            DisconnectTcp(reason);
        }

        #region Caller communication

        private CentralexIncomingCaller _caller;

        /// <summary>
        /// Incoming connection from caller
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        public bool CallerConnect(CentralexIncomingCaller caller)
        {
            //Console.WriteLine($"{ClientNumber} client: caller connects to client");
            _logger.Info(TAG, nameof(CallerConnect), _tag2, $"Caller connected");
            if (_caller != null)
            {
                if (caller != null) return false; // do not overwrite existing caller with other caller
            }
            _caller = caller;

            SendRemoteCallCmd();
            _state = ClientConnectionStates.Connecting;
            _clientItem.SetClientState(ClientStates.Call);

            return true;
        }

        /// <summary>
        /// Receive disconnect info from caller
        /// </summary>
        public void CallerDisconnect()
        {
            //Console.WriteLine($"{_client.Number} client: caller disconnects from client 1");
            _logger.Debug(TAG, nameof(CallerDisconnect), _tag2, $"client = {_clientItem.Number}");
            _callerDisconnect = true;
        }

        /// <summary>
        /// Receive dara from caller
        /// </summary>
        /// <param name="packet"></param>
        public void CallerReceive(ItelexPacket packet)
        {
            _receivedCallerPackets.Enqueue(packet);
        }

        /// <summary>
        /// Send data to caller
        /// </summary>
        /// <param name="packet"></param>
        private void CallerSend(ItelexPacket packet)
        {
            _caller?.ClientReceive(packet);
        }

		#endregion Caller communication

		public override string ToString()
		{
			return $"{ClientNumber} {ClientPort} {_host}";
		}
	}
}
