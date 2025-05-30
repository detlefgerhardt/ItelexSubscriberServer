using CentralexServer.Data;
using ItelexTlnServer.Data;
using ServerCommon;
using ServerCommon.Logging;
using ServerCommon.Utility;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using static System.Net.Mime.MediaTypeNames;

namespace CentralexServer.CentralexConnections
{
	internal class CentralexConnectionManager
    {
        private const string TAG = nameof(CentralexConnectionManager);
		//private const string TAG2 = "";

		//private int[] PortList = [10000, 10001, 10002, 10003, 10004];

		//private TcpListener _centralexListener;

		private CentralexServerDatabase _database;

        private Logger _logger;

        private List<CallerListenerItem> _callerListeners = new List<CallerListenerItem>();
        private object _tcpListenersLock = new object();

        private bool _shutDown;

        public List<CentralexIncomingClient> ClientConnectionList { get; set; }

        public List<ClientItem> ClientList { get; set; }

        //private System.Timers.Timer _threadListTimer;

        /// <summary>
        /// singleton pattern
        /// </summary>
        private static CentralexConnectionManager? instance;
        public static CentralexConnectionManager Instance => instance ??= new CentralexConnectionManager();

        private CentralexConnectionManager()
        {
            _logger = GlobalData.Logger;
            _database = CentralexServerDatabase.Instance;
            CheckAllClientPorts();
            LoadClientList();
            ClientConnectionList = new List<CentralexIncomingClient>();
            //_threadListTimer = new System.Timers.Timer(1000 * 60);
			//_threadListTimer.Elapsed += ThreadListTimer_Elapsed;
            //_threadListTimer.Start();
        }

		/*
	    private void ThreadListTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
	    {
		    _logger.ConsoleLog(TAG, nameof(ThreadListTimer_Elapsed), TAG2,
				    $"{Processes.GetThreadlist().Count} processes");
	    }
		*/

		public void StartClientListener()
        {
            _shutDown = false;
            try
            {
				//Debug.WriteLine($"start ClientListener port {GlobalData.Config.BinaryPort}");
				ThreadPool.QueueUserWorkItem(ClientListener);
            }
            catch (Exception ex)
            {
				_logger.Error(TAG, nameof(StartClientListener), "StartClientListener", "error", ex);
			}
		}

        private volatile bool _startClientConnections = false;

        private void ClientListener(object state)
        {
            string tag2 = "Incoming client";

			while(!_startClientConnections)
			{
				Thread.Sleep(1000);
			}
			_logger.Notice(TAG, nameof(CallerListener), tag2, "enable incoming client connections");

			TcpListener tcpListener;
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, GlobalData.Config.CentralexPort);
                tcpListener.Start();

                //_logger.Info(TAG, nameof(ClientListener), tag2,
				//  $"waiting for client connections at port {GlobalData.Config.CentralexPort}");
            }
            catch (Exception ex)
            {
				_logger.ConsoleLog(TAG, nameof(ClientListener), tag2,
						$"error setting listener for port {GlobalData.Config.CentralexPort}");
                _logger.Error(TAG, nameof(CallerListener), tag2, $"error {GlobalData.Config.CentralexPort}", ex);
                return;
            }

			while (true)
            {
                if (_shutDown)
                {
                    tcpListener.Stop();
                    return;
                }

                try
                {
                    //_logger.Debug(TAG, nameof(ClientListener), tag2,
                    //        $"Wait for centralex client connection port={GlobalData.Config.CentralexPort}");
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();

                    if (!tcpClient.Connected)
                    {
                        _logger.Warn(TAG, nameof(ClientListener), tag2,
                                $"Incoming client connection on port {GlobalData.Config.CentralexPort}, " +
                                $"connected={tcpClient.Connected}");
                    }

					Task.Run(() =>
                    {
						try
						{
							TaskMonitor.Instance.AddTask(Task.CurrentId, TAG, nameof(ClientListener));

							IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
							string ipStr = endPoint.Address.ToString();
							IPAddress ipAddr = CommonHelper.GetIp4AddrFromHostname(ipStr);
							string host = $"{ipAddr}:{endPoint.Port}";

							//_logger.Debug(TAG, nameof(ClientListener), tag2,
							//		$"client: Incoming connection {GlobalData.Config.CentralexPort}");
							CentralexIncomingClient conn = new CentralexIncomingClient(tcpClient, Constants.LOG_PATH,
                                Constants.LOG_NAME, GlobalData.Config.LogLevel, host);
							//_logger.Debug(TAG, nameof(CallerListener), tag2, $"client connection created id={idNumber}");

							ClientConnectionList.Add(conn);
                            conn.Start();
                            ClientConnectionList.Remove(conn);
                            conn.Dispose();
						}
                        catch(Exception ex)
                        {
							_logger.Error(TAG, nameof(ClientListener), tag2, "error", ex);
						}

						TaskMonitor.Instance.RemoveTask(Task.CurrentId);
					});
                }
                catch (Exception ex)
                {
                    if (ex.HResult == -2147467259)
                    {
                        _logger.Error(TAG, nameof(ClientListener), tag2, $"ex={ex.Message}");
                    }
                    else
                    {
                        _logger.Error(TAG, nameof(ClientListener), tag2, "error", ex);
                    }
                }
            }
        }

        private volatile int _CallerListenersToStart;

        public void StartCallerListeners()
        {
            _logger.Info(TAG, nameof(StartCallerListeners), nameof(StartCallerListeners),
                    $"starting {ClientList.Count} caller listener");

            _shutDown = false;
            _callerListeners = new List<CallerListenerItem>();
			_CallerListenersToStart = ClientList.Count;
			foreach (ClientItem client in ClientList)
            {
                StartCallerListener(client);
            }
        }

        private void StartCallerListener(ClientItem client)
        {
            //Console.WriteLine($"start Listeners for port {client.Port}");
            CallerListenerItem item = new CallerListenerItem()
            {
                Number = client.Number,
                Port = client.Port,
                Stopped = false
            };

            try
            {
                ThreadPool.QueueUserWorkItem(CallerListener, item);
                lock (_tcpListenersLock)
                {
                    _callerListeners.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(TAG, nameof(StartCallerListeners),
                        $"StartCallerListener {client.Port} / {client.Number}", "error", ex);
            }
        }

		private void CallerListener(object state)
        {
            if (state == null) return;

			CallerListenerItem listenerItem = (CallerListenerItem)state;
			string tag2 = $"Incoming caller {listenerItem.Port} / {listenerItem.Number}";
            TcpListener tcpListener;
			try
			{
                tcpListener = new TcpListener(IPAddress.Any, listenerItem.Port);
                tcpListener.Start();

                _logger.Debug(TAG, nameof(CallerListener), tag2, "waiting for caller connections");
            }
            catch(Exception ex)
            {
				_logger.Error(TAG, nameof(CallerListener), tag2, "error setting listener for port", ex);
                return;
            }

            _CallerListenersToStart--;
            if (_CallerListenersToStart == 0)
            {
                _startClientConnections = true;
			}

			while (true)
            {
                if (_shutDown)
                {
					tcpListener.Stop();
					lock (_tcpListenersLock)
                    {
                        listenerItem.Stopped = true;
                        return;
                    }
                }

                try
                {
					//_logger.Debug(TAG, nameof(CallerListener), tag2, $"waiting for caller connection");

					TcpClient tcpClient = tcpListener.AcceptTcpClient();

                    if (!tcpClient.Connected)
                    {
                        _logger.Warn(TAG, nameof(CallerListener), tag2,
                            $"incoming caller connection, connected={tcpClient.Connected}");
                    }

					Task.Run(() =>
                    {
						TaskMonitor.Instance.AddTask(Task.CurrentId, TAG, nameof(CallerListener));
						try
						{
                            int number = listenerItem.Number;
							IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
							int port = endPoint.Port;
							string ipStr = endPoint.Address.ToString();
							IPAddress ipAddr = CommonHelper.GetIp4AddrFromHostname(ipStr);
							string host = $"{ipAddr}:{endPoint.Port}";

							TickTimer waitClient = new TickTimer();
							CentralexIncomingClient clientConn = null;
							while (!waitClient.IsElapsedMilliseconds(2000))
                            {
                                clientConn = (from s in ClientConnectionList
                                              where s.ClientPort == listenerItem.Port
											  select s).FirstOrDefault();
                                if (clientConn != null) break;
                            }

							//string disconnectReason = GetClientEndReason(port);
							//if (string.IsNullOrEmpty(disconnectReason)) disconnectReason = "nc";
							_logger.Debug(TAG, nameof(CallerListener), tag2, $"clientConn = {clientConn}");

							CentralexIncomingCaller callerConn = new CentralexIncomingCaller(tcpClient, number,
								listenerItem.Port, Constants.LOG_PATH, Constants.LOG_NAME, GlobalData.Config.LogLevel, host);

                            callerConn.Start(clientConn);
                            callerConn.Dispose();

                            TaskMonitor.Instance.RemoveTask(Task.CurrentId);
						}
						finally
                        {
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex.HResult == -2147467259)
                    {
                        _logger.Error(TAG, nameof(CallerListener), tag2, $"ex={ex.Message}");
                    }
                    else
                    {
                        _logger.Error(TAG, nameof(CallerListener), tag2, "error", ex);
                    }
                }
            }
        }

		public void SetClientName(int number, string name)
		{
			ClientItem item = GetClient(number);
			if (item != null) item.Name = name;
		}

        /*
		public void SetClientState(int number, ClientStates state)
        {
            //Console.WriteLine($"SetClientState number={number} state={state}");
            ClientItem item = (from c in ClientList where c.Number == number select c).FirstOrDefault();
            if (item != null)
            {
                item.State = (int)state;
                item.LastChangedUtc = DateTime.UtcNow;
            }
        }
        */

        public void LoadClientList()
        {
			ClientList = _database.ClientsLoadAll();
            if (ClientList == null)
            {
                _logger.Fatal(TAG, nameof(LoadClientList), "LoadClientList", "error loading clients from database");
                return;
            }

			foreach (ClientItem item in ClientList)
			{
				item.DisconnectReason = "nc";
			}
		}

        public void AddClient(ClientItem clientItem)
        {
            //Console.WriteLine($"AddClient {clientItem}");
			clientItem.DisconnectReason = "nc";
			ClientList.Add(clientItem);
            StartCallerListener(clientItem);
        }

        /*
		public void ChangeClient(ClientItem clientItem)
		{
			ClientList.RemoveAll(x => x.Number == clientItem.Number);
			ClientList.Add(clientItem);
		}
        */

		public ClientItem GetClient(int number)
		{
			return (from c in ClientList where c.Number == number select c).FirstOrDefault();
		}

		public ClientStates GetClientState(int number)
        {
            ClientItem item = GetClient(number);
            return item != null ? item.StateEnum : ClientStates.NotConnected;
        }

        public DateTime GetClientLastChanged()
        {
            DateTime lastDate = new DateTime(1970, 1, 1);
            foreach (ClientItem item in ClientList)
            {
                if (item.LastChangedUtc > lastDate) lastDate = item.LastChangedUtc;
            }
            return lastDate;
		}

        public int? FindFreePort()
        {
            return FindFreePort(ClientList);
        }

		public int? FindFreePort(List<ClientItem> clientList)
		{
			if (clientList.Count == 0) return GlobalData.Config.ClientPortsStart;

			int portCnt = GlobalData.Config.ClientPortsEnd - GlobalData.Config.ClientPortsStart + 1;
			bool[] ports = new bool[portCnt];

			foreach (ClientItem s in clientList)
			{
                if (s.Port >= GlobalData.Config.ClientPortsStart && s.Port < GlobalData.Config.ClientPortsEnd)
                {
                    ports[s.Port - GlobalData.Config.ClientPortsStart] = true;
                }
			}

			for (int i = 0; i < portCnt; i++)
			{
				if (!ports[i]) return GlobalData.Config.ClientPortsStart + i;
			}

			return null;
		}



		public int GetFreePorts()
        {
            return GlobalData.Config.ClientPortsEnd - GlobalData.Config.ClientPortsStart + 1 - ClientList.Count;
        }

        /*
        public void SetClientEndReason(int port, string reason)
        {
            _logger.Debug(TAG, nameof(SetClientEndReason), $"port={port} reason{reason}");
            ClientItem item = (from c in ClientList where c.Port == port select c).FirstOrDefault();
            if (item != null) item.DisconnectReason = reason;
        }
        */

		public string GetClientEndReason(int port)
		{
			ClientItem item = (from c in ClientList where c.Port == port select c).FirstOrDefault();
            string reason = item != null ? item.DisconnectReason : "";
            return reason;
		}

        /// <summary>
        /// Check if all client port are valid
        /// </summary>
        /// <returns></returns>

        public bool CheckAllClientPorts()
        {
            string tag2 = nameof(CheckAllClientPorts);

			_logger.Info(TAG, nameof(CheckAllClientPorts), tag2, "");

			List<ClientItem> clients = _database.ClientsLoadAll();
            if (clients == null)
            {
				_logger.Fatal(TAG, nameof(CheckAllClientPorts), tag2, "error loading clients from database");
				return false; // error;
            }

            int correctionCount = 0;
            foreach(ClientItem client in clients)
            {
                string reason = "";
                bool invalid = (client.Port < GlobalData.Config.ClientPortsStart || client.Port > GlobalData.Config.ClientPortsEnd);
                if (invalid) reason = "out of range";

                if (!invalid)
                {
                    // check if other client has my port
                    invalid = (from c in clients
                                       where c.Port == client.Port && c.Number != client.Number
                                       select c).Any();
                    if (invalid) reason = "duplicated";
                }

				if (invalid)
                {
					_logger.Warn(TAG, nameof(CheckAllClientPorts), tag2,
						$"client {client.Number} has invalid port {client.Port} ({reason})");

					client.Port = FindFreePort(clients).Value;
                    if (!_database.ClientsUpdatePort(client.Number, client.Port))
                    {
                        _logger.Fatal(TAG, nameof(CheckAllClientPorts), tag2, 
                                $"error updating client port {client.Number} in database");
                        return false;
                    }
                    correctionCount++;
                }
            }
            if (correctionCount > 0)
            {
                _logger.Warn(TAG, nameof(CheckAllClientPorts), tag2, $"{correctionCount} client ports updated");
            }

            return true;
        }
	}

	internal class CallerListenerItem
    {
        public int Number { get; set; }

        public int Port { get; set; }

        //public CentralexUserItem User { get; set; }

        public bool Stopped { get; set; }
    }

}
