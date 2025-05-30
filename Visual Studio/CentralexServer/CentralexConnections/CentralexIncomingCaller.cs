using Centralex.BinaryProxy;
using CentralexCommon;
using ServerCommon.Logging;
using System.Net.Sockets;

namespace CentralexServer.CentralexConnections
{
	internal class CentralexIncomingCaller : CentralexConnection
    {
        private const string TAG = nameof(CentralexIncomingCaller);

		private int _port;
        private string _host;
        //private string _disconnectReason;

        public CentralexIncomingCaller(TcpClient tcpClient, int? clientNumber, int port,
                string logPath, string logName, LogTypes logLevel, string host) :
                base(tcpClient, ConnectionType.CentralexCaller, clientNumber, logPath, logName, logLevel)
        {
            _port = port;
            _host = host;
			_tag2 = $"Incoming caller {port} / {clientNumber} from {host}";

            _logger.Debug(TAG, nameof(CentralexIncomingCaller), _tag2, "new caller connection");

			//_disconnectReason = disconnectReason;
        }

        public void Start(CentralexIncomingClient clientConnection)
        {
            try
            {
                if (clientConnection == null)
                {
                    string disconnectReason = "nc";
                    _logger.Info(TAG, nameof(Start), _tag2,
                            $"reject caller: No client connected (port={_port} reason={disconnectReason})");
                    Reject(disconnectReason);
                    return;
                }

                _logger.Debug(TAG, nameof(Start), _tag2, $"New caller connection, {_port} / {clientConnection.ClientNumber}");

                if (CentralexConnectionManager.Instance.GetClientState(clientConnection.ClientNumber.Value) == ClientStates.Call)
                {
                    _logger.Info(TAG, nameof(Start), _tag2, $"reject caller: occupied");
                    Reject(DisconnectReasons.ClientOccupied);
                    return;
                }

                _logger.Debug(TAG, nameof(Start), _tag2, "connection established");
                StartReceive();
            }
            catch(Exception ex)
            {
				_logger.Error(TAG, nameof(Start), _tag2, "error", ex);
			}

			_clientConnection = clientConnection;
            try
            {

                _clientConnection.CallerConnect(this);

                while (IsConnected)
                {
                    Thread.Sleep(100);
                }

                _clientConnection.CallerDisconnect();
            }
            finally
            {
                _logger.Debug(TAG, nameof(Start), _tag2, "caller connection ends");
                _clientConnection = null;
            }
        }

        /// <summary>
        /// Receive data from caller
        /// </summary>
        /// <param name="packet"></param>
        protected override void ReceivedPacket(ItelexPacket packet)
        {
            if (packet == null) return;

            base.ReceivedPacket(packet);

            ClientSend(packet);

            if (packet.CommandType == ItelexCommands.End)
            {
                Thread.Sleep(2000);
                //_clientConnection?.CallerDisconnect();
                DisconnectTcp(DisconnectReasons.EndCmdReceived);
            }
        }

        #region Communication with client

        private CentralexIncomingClient _clientConnection;

        /// <summary>
        /// Receive disconnect demand from client
        /// </summary>
        public void ClientDisconnect()
        {
            _logger.Debug(TAG, nameof(ClientDisconnect), _tag2, "disconnect");
            SendEndCmd();
            Thread.Sleep(2000);
            DisconnectTcp(DisconnectReasons.DisconnetFromClient);
        }

        /// <summary>
        /// Receive data from client
        /// </summary>
        /// <param name="packet"></param>
        public void ClientReceive(ItelexPacket packet)
        {
            //Debug.WriteLine($"ReceivedFromClient {packet}");
            SendCmd(packet.CommandType, packet.Data);

            //if (packet.CommandType == ItelexCommands.End)
            //{
            //	_clientConnection.CallerDisconnect();
            //	Disconnect(DisconnectReasons.EndCmdReceived);
            //}
        }

        /// <summary>
        /// Send data to client
        /// </summary>
        /// <param name="packet"></param>
        public void ClientSend(ItelexPacket packet)
        {
            if (packet == null) return;
            _clientConnection?.CallerReceive(packet);
        }

        #endregion Communication with client
    }
}
