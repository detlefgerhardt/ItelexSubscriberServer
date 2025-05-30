using ServerCommon;
using System.Net.Sockets;
using System.Net;
using CentralexServer.Data;
using ItelexTlnServer.Data;
using ServerCommon.Utility;
using TlnServer.Config;
using ServerCommon.Logging;
using ServerCommon.Private;
using System.Diagnostics;

namespace TlnServer.BinaryServer
{
	internal enum DisconnectReasons { NotConnected, TcpDisconnect, TcpDisconnectByRemote, SendCmdError };

	internal class BinaryServerManager
	{
		private const string TAG = nameof(BinaryServerManager);
		private const string TAG2 = "";

		protected Logger _logger;

		private TlnServerMsDatabase _database;

		private SyncServerData _syncServer;

		private TcpListener _tcpListener;

		private object _receivedUpdatesLock = new object();
		private List<ReceivedUpdateItem> _receivedUpdates;

		private bool _shutDown;

		//public const uint SERVER_PIN = PrivateConstants.SERVER_PIN;
		public const int SERVER_VERSION = 1;

		public const string TLNSERVER_IP1 = PrivateConstants.TLNSERVER_IP1;
		public const string TLNSERVER_IP2 = PrivateConstants.TLNSERVER_IP2;

		private bool _syncTrigger = false;


		public bool SyncTrigger
		{
			get
			{
				return _syncTrigger;
			}
			set
			{
				_syncTrigger = value;
				if (value == true)
				{
					_logger.Debug(TAG, nameof(SyncTrigger), TAG2, "set synctrigger");
				}
			}
		}

		private readonly System.Timers.Timer _syncTimer;
		private volatile bool _syncTimer_Active;
		private TickTimer _syncTicks;

		private readonly System.Timers.Timer _fullQueryTimer;
		private volatile bool _fullQueryTimer_Active;
		private volatile int _fullQueryTimer_lastHour;

		private static BinaryServerManager instance;
		public static BinaryServerManager Instance => instance ??= new BinaryServerManager();

		private BinaryServerManager()
		{
			_logger = GlobalData.Logger;
			_database = TlnServerMsDatabase.Instance;
			_syncServer = ConfigManager.Instance.LoadSyncServerConfig(_logger, SERVER_VERSION);

			_receivedUpdates = new List<ReceivedUpdateItem>();

			_syncTrigger = true;
			_syncTicks = new TickTimer();
			_syncTimer_Active = false;
			_syncTimer = new System.Timers.Timer(1000);
			_syncTimer.Elapsed += SyncTimer_Elapsed;
			_syncTimer.Start();

			_fullQueryTimer_lastHour = -1;
			_fullQueryTimer_Active = false;
			_fullQueryTimer = new System.Timers.Timer(1000 * 60);
			_fullQueryTimer.Elapsed += FullQueryTimer_Elapsed;
			_fullQueryTimer.Start();
		}

		/// <summary>
		/// check every second, if _syncTicks is elapsed (60s) or SyncTrigger was set
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void SyncTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
			string tag2 = "SyncTimer";

			if (!_syncTicks.IsElapsedMilliseconds(60000) && !SyncTrigger) return;

			if (ConnectionCountGet() > 0)
			{
				_logger.Info(TAG, nameof(SyncTimer_Elapsed), tag2, $"Other connection active");
				return;
			}

			//_logger.Debug(TAG, nameof(SyncTimer_Elapsed), tag2, $"_syncTimer_Active={_syncTimer_Active}");

			if (_syncTimer_Active) return;
			_syncTimer_Active = true;

			if (SyncTrigger)
			{
				_logger.Debug(TAG, nameof(SyncTimer_Elapsed), tag2, "SyncTrigger was set");
			}
			SyncTrigger = false; // reset early to allow retrigger during sync process

			Task.Run(() =>
			{
				try
				{
					UpdateQueue();
					SendQueue();
				}
				finally
				{
					_syncTicks.Start();
					_syncTimer_Active = false;
				}
			});
		}

		private void FullQueryTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
			string tag2 = "FullQueryTimer";

			if (ConnectionCountGet() > 0)
			{
				_logger.Info(TAG, nameof(FullQueryTimer_Elapsed), tag2, $"Other connection active");
				return;
			}

			// to ensure that not all servers synchronize at the same time, the sync hour depends on the ServerId
			int hour = DateTime.UtcNow.AddHours(2).Hour;
			if (hour != GlobalData.Config.ServerId) return;
			//if (hour != 11) return;

			try
			{
				if (_fullQueryTimer_Active) return;
				_fullQueryTimer_Active = true;

				//_logger.Info(TAG, nameof(FullQueryTimer_Elapsed), tag2,
				//	$"ServerId={GlobalData.Config.ServerId}, hour={hour}, lastHour={_fullQueryTimer_lastHour}");

				// try to backup database for 1 hour (may be locked)
				_database.Backup();

				if (hour == _fullQueryTimer_lastHour) return;
				_fullQueryTimer_lastHour = hour;

				// sync with all servers
				foreach(SyncServerItem syncServer in _syncServer.SyncServer)
				{
					DoFullQuery(syncServer.Address, false);
				}
			}
			finally
			{
				_fullQueryTimer_Active = false;
			}
		}

		public bool SetRecvOn()
		{
			//_logger.ConsoleLog(null, null, "", $"waiting for binary connection at port {GlobalData.Config.BinaryPort}");
			_logger.Info(TAG, nameof(SetRecvOn), TAG2, $"waiting for binary connection at port {GlobalData.Config.BinaryPort}");

			try
			{
				_tcpListener = new TcpListener(IPAddress.Any, GlobalData.Config.BinaryPort);
				_tcpListener.Start();

				// start listener task for incoming connections
				Task _listenerTask = Task.Run(() => Listener());
				return true;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(SetRecvOn), TAG2, "error", ex);
				return false;
			}
		}

		public bool SetRecvOff()
		{
			_logger.Debug(TAG, nameof(SetRecvOff), TAG2, "");
			_tcpListener.Stop();
			return true;
		}

		private void Listener()
		{
			TaskMonitor.Instance.AddTask(Task.CurrentId, TAG, nameof(Listener));

			while (true)
			{
				if (_shutDown)
				{
					SetRecvOff();
					TaskMonitor.Instance.RemoveTask(Task.CurrentId);
					return;
				}

				try
				{
					int threadCnt = Process.GetCurrentProcess().Threads.Count;
					_logger.Debug(TAG, nameof(Listener), TAG2, $"Waiting for binary connection, threadCnt={threadCnt}");

					// wait for connection
					//TickTimer pendingTimer = new TickTimer();
					//while (!_tcpListener.Pending())
					//{
					//	Thread.Sleep(50);
					//}
					TcpClient tcpClient = _tcpListener.AcceptTcpClient();

					IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
					string ipStr = endPoint.Address.ToString();
					//IPAddress ipv4Str = CommonHelper.GetIp4AddrFromHostname(ipStr);
					string host = $"{IpToServerName(ipStr)}:{endPoint.Port} ({ipStr})";
					//string tag2 = $"Incoming from {IpToServerName(ipv4Str)}:{endPoint.Port}";
					//_logger.Debug(TAG, nameof(Listener), tag2, $"incoming remote {TickTimer.GetTicksMs() % 100000}ms");

					ConnectionCountInc();

					Task.Run(async () =>
					{
						//TaskMonitor.Instance.AddTask(Task.CurrentId, TAG, nameof(Listener));

						try
						{
							BinaryIncommingConnection conn = new BinaryIncommingConnection(tcpClient,
								Constants.LOG_PATH, GlobalData.Config.LogLevel, host);
							await conn.Start();
							conn.Dispose();
						}
						finally
						{
							ConnectionCountDec();
						}

						//TaskMonitor.Instance.RemoveTask(Task.CurrentId);
					});
				}
				catch (Exception ex)
				{
					if (ex.HResult == -2147467259)
					{
						_logger.Error(TAG, nameof(Listener), TAG2, $"Listener ex={ex.Message}");
					}
					else
					{
						_logger.Error(TAG, nameof(Listener), TAG2, "error", ex);
					}
				}
			}
		}

#if DEBUG
		public void TestSendSyncLoginBurst()
		{
			List<TeilnehmerItem> tlnItems = _database.TeilnehmerLoadAll();
			int cnt = 0;
			foreach (TeilnehmerItem tlnItem in tlnItems)
			{
				Task.Run(() =>
				{
					DoSyncLogin("192.168.0.176", GlobalData.Config.BinaryPort, (uint)GlobalData.Config.ServerPin,
						new List<TeilnehmerItem>() { tlnItem });
				});
				cnt++;
				if (cnt >= 20) break;
			}
		}
#endif

		public bool DoSyncLogin(string serverAddr, int serverPort, uint serverPin, List<TeilnehmerItem> tlnList)
		{
			string tag2 = $"SyncLogin to {serverAddr}";

			_logger.Debug(TAG, nameof(DoSyncLogin), tag2, $"start {serverAddr}:{serverPort} count={tlnList.Count}");

			try
			{
				//int id = Helper.GetNewSessionNo(GlobalData.LastSessionNo);
				BinaryOutgoingConnection conn = new BinaryOutgoingConnection(serverAddr, serverPort, serverPin,
					Constants.LOG_PATH, GlobalData.Config.LogLevel, tag2);

				if (!conn.Connect())
				{
					_logger.Error(TAG, nameof(DoSyncLogin), tag2, $"error connecting to {serverAddr}:{serverPort}");
					return false;
				}

				bool success = conn.DoSyncLogin(tlnList);

				conn.Disconnect();
				conn.Dispose();

				int processed = tlnList.Count(t => t.Processed);

				return success;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(DoSyncLogin), tag2, "error", ex);
				return false;
			}
		}

		public void DoFullQuery(string serverAddr, bool checkOnly)
		{
			string tag2 = $"SyncFullQuery to {serverAddr}";
			//_logger.Info(TAG, nameof(DoFullQuery), TAG2, "start");
			List<TeilnehmerItem> tlnList = DoSyncFullQuery(serverAddr, GlobalData.Config.BinaryPort,
					(uint)GlobalData.Config.ServerPin, tag2);
			UpdateDatabaseFromFullQuery(tlnList, checkOnly, tag2);
		}

		public List<TeilnehmerItem> DoSyncFullQuery(string serverAddr, int serverPort, uint serverPin, string tag2)
		{
			_logger.Info(TAG, nameof(DoSyncFullQuery), tag2, "do SyncFullQuery");

			//int id = Helper.GetNewSessionNo(GlobalData.LastSessionNo);
			BinaryOutgoingConnection conn = new BinaryOutgoingConnection(serverAddr, serverPort, serverPin,
				Constants.LOG_PATH, GlobalData.Config.LogLevel, tag2);

			if (!conn.Connect()) return null;

			List<TeilnehmerItem> tlnList = conn.DoSyncFullQuery();

			conn.Disconnect();
			conn.Dispose();

			return tlnList;
		}

		public void UpdateDatabaseFromFullQuery(List<TeilnehmerItem> tlnList, bool checkOnly, string tag2)
		{
			_logger.Info(TAG, nameof(UpdateDatabaseFromFullQuery), tag2,
					$"Update database from SyncFullQuery ({tlnList?.Count} tln) checkonly={checkOnly}");

			if (tlnList == null) return;

			int insertedCnt = 0;
			int updatedCnt = 0;
			int localIsNewerCnt = 0;
			int uptodateCnt = 0;
			try
			{
				foreach (TeilnehmerItem tlnItem in tlnList)
				{
					TeilnehmerItem existing = _database.TeilnehmerLoadByNumber(tlnItem.Number);
					if (existing == null)
					{
						// insert
						_logger.Notice(TAG, nameof(UpdateDatabaseFromFullQuery), TAG2, $"insert entry {tlnItem.Number}");
						if (!checkOnly)
						{
							tlnItem.Changed = true;
							if (!_database.TeilnehmerInsert(tlnItem))
							{
								_logger.Fatal(TAG, nameof(UpdateDatabaseFromFullQuery), tag2, 
										$"error updating Teilnehmer, number={tlnItem.Number}");
							}
							else
							{
								insertedCnt++;
							}
						}
						else
						{
							insertedCnt++;
						}
					}
					else
					{
						string diff = existing.CompareToString(tlnItem, out string diffErr);
						if (diffErr != null) diff = diffErr;
						if (existing.TimestampUtc < tlnItem.TimestampUtc)
						{
							// update
							if (!checkOnly)
							{
								_logger.Notice(TAG, nameof(UpdateDatabaseFromFullQuery), tag2,
										$"update entry {tlnItem.Number}, diff={diff}");
								tlnItem.Changed = true;
								if (!_database.TeilnehmerUpdateByNumber(tlnItem))
								{
									_logger.Fatal(TAG, nameof(UpdateDatabaseFromFullQuery), tag2,
											$"error updating Teilnehmer, number={tlnItem.Number}");
								}
								else
								{
									updatedCnt++;
								}
							}
							else
							{
								updatedCnt++;
							}
						}
						else if (existing.TimestampUtc > tlnItem.TimestampUtc)
						{
							// local is newer
							_logger.Notice(TAG, nameof(UpdateDatabaseFromFullQuery), tag2,
									$"local entry is newer {tlnItem.Number}, " +
									$"diff={diff} (local={existing.TimestampUtc:dd.MM.yyyy HH:mm:ss} " + 
									$"remote={tlnItem.TimestampUtc:dd.MM.yyyy HH:mm:ss}");
							localIsNewerCnt++;
						}
						else
						{
							// local is uptodate
							_logger.Debug(TAG, nameof(UpdateDatabaseFromFullQuery), tag2,
									$"local entry is uptodate {tlnItem.Number}, diff={diff}");
							uptodateCnt++;
						}
					}
				}
				string chkStr = checkOnly ? "checkonly " : "";
				LogTypes logType = (insertedCnt > 0 || updatedCnt > 0 || localIsNewerCnt > 0) ? LogTypes.Warn : LogTypes.Info;
				_logger.Log(logType, TAG, nameof(UpdateDatabaseFromFullQuery), tag2,
						$"{chkStr}timestamps inserted/updated/uptodate/newer = " +
						$"{insertedCnt}/{updatedCnt}/{uptodateCnt}/{localIsNewerCnt}");
			}
			catch(Exception ex)
			{
				_logger.Error(TAG, nameof(UpdateDatabaseFromFullQuery), tag2, $"error", ex);
			}
		}

		private void UpdateQueue()
		{
			string tag2 = "UpdateQueue";

			if (_syncServer == null)
			{
				_logger.Warn(TAG, nameof(UpdateQueue), tag2, "no sync server configured");
			}

			List<TeilnehmerItem> changedList = _database.TeilnehmerLoadAllChanged();
			if (changedList == null)
			{
				_logger.Fatal(TAG, nameof(UpdateQueue), tag2, "error loading changed Teilnehmer from database");
				return;
			}

			if (changedList.Count == 0) return; // nothing todo

			string chgListStr = "";
			foreach(TeilnehmerItem ti in changedList)
			{
				if (chgListStr != "") chgListStr += ",";
				chgListStr += ti.Number;
			}

			//SyncServerData syncServer = ConfigManager.Instance.LoadSyncServerConfig(_logger, SERVER_VERSION);

			_logger.Info(TAG, nameof(UpdateQueue), tag2, $"{changedList.Count} changed number(s) ({chgListStr})");

			int queueCnt = 0;
			DateTime utcNow = DateTime.UtcNow;
			foreach (TeilnehmerItem tlnItem in changedList)
			{
				foreach (SyncServerItem srvItem in _syncServer.SyncServer)
				{
					QueueItem queItem = _database.QueueLoadByServerAndMsg(srvItem.Id, tlnItem.Uid);
					if (queItem != null)
					{
						// queue entry for this number already exists -> update
						queItem.timestamp = utcNow;
						if (!_database.QueueUpdateByUid(queItem))
						{
							_logger.Fatal(TAG, nameof(UpdateQueue), tag2,
									$"error updating Queue in database, uid={queItem.uid}");
							break;
						}
					}
					else
					{
						// new queue entry
						queItem = new QueueItem()
						{
							server = srvItem.Id,
							message = tlnItem.Uid,
							timestamp = utcNow
						};
						if (!_database.QueueInsert(queItem))
						{
							_logger.Fatal(TAG, nameof(UpdateQueue), tag2, "error inserting in Queue in database");
							break;
						}
						queueCnt++;
					}
				}
				if (!_database.TeilnehmerSetChanged(tlnItem.Uid, false))
				{
					_logger.Fatal(TAG, nameof(UpdateQueue), tag2,
							$"error updating teilnehmer in database, uid={tlnItem.Uid}");
					break;
				}
				//_logger.Debug(TAG, nameof(UpdateQueue), $"new queue entry {tlnItem.number}");
			}

			if (queueCnt > 0)
			{
				_logger.Debug(TAG, nameof(UpdateQueue), tag2, $"{queueCnt} new queue entries");
			}
		}

		private void SendQueue()
		{
			string tag2 = "SendQueue";

			List<QueueItem> queList = _database.QueueLoadAll();
			if (queList == null)
			{
				_logger.Fatal(TAG, nameof(SendQueue), tag2, "error loading Queue from database");
				return;
			}

			if (queList.Count == 0) return; // nothing todo

			if (_syncServer == null) return;

			_logger.Info(TAG, nameof(SendQueue), tag2, $"{queList.Count} queue entries");

			Dictionary<long, List<TeilnehmerItem>> syncList = new Dictionary<long, List<TeilnehmerItem>>();

			foreach (QueueItem queItem in queList)
			{
				SyncServerItem srvItem = (from s in _syncServer.SyncServer
										  where s.Id == queItem.server
										  select s).FirstOrDefault();
				if (srvItem == null)
				{
					_logger.Warn(TAG, nameof(SendQueue), tag2,
							$"server {queItem.server} with version {SERVER_VERSION} no longer exists");
					if (!_database.QueueDeleteByUid(queItem.uid))
					{
						_logger.Fatal(TAG, nameof(SendQueue), tag2, $"error deleting queue, uid={queItem.uid}");
					}
					continue;
				}

				TeilnehmerItem tlnItem = _database.TeilnehmerLoadByUid(queItem.message);
				if (tlnItem == null)
				{
					_database.QueueDeleteByUid(queItem.uid);
					_logger.Warn(TAG, nameof(SendQueue), tag2, $"tln entry {queItem.message} does not exist");
					continue;
				}

				// DEBUG!!!!
				tlnItem.Processed = false;
				//tlnItem.timestamp = DateTime.UtcNow;

				ReceivedUpdateItem recvItem = new ReceivedUpdateItem()
				{
					TlnId = tlnItem.Uid,
					Timestamp = tlnItem.TimestampUtc,
					ServerId = srvItem.Id
				};
				if (ReceivedUpdateItemExists(recvItem))
				{
					// do not return to sender
					_logger.Info(TAG, nameof(SendQueue), tag2,
							$"do not return {tlnItem.Number} to sender {srvItem.Address}");
					ReceivedUpdateItemRemove(recvItem);
					if (!_database.QueueDeleteByServerAndMsg(srvItem.Id, tlnItem.Uid))
					{
						_logger.Fatal(TAG, nameof(SendQueue), tag2,
								$"error deleting queue entry server={srvItem.Id} id={tlnItem.Uid}");
					}
					continue;
				}

				if (!syncList.ContainsKey(queItem.server))
				{
					// init sync list for server
					syncList[queItem.server] = new List<TeilnehmerItem>();
				}
				syncList[queItem.server].Add(tlnItem);
			}

			// sync each server
			foreach (SyncServerItem srvItem in _syncServer.SyncServer)
			{
				List<TeilnehmerItem> tlnList = null;
				if (syncList.ContainsKey(srvItem.Id))
				{
					tlnList = syncList[srvItem.Id];
				}
				if (tlnList == null || tlnList.Count == 0) continue; // this should not happen

				_logger.Debug(TAG, nameof(SendQueue), tag2, $"{tlnList.Count} enries for server {srvItem.Address}");

				if (DoSyncLogin(srvItem.Address, srvItem.Port, (uint)GlobalData.Config.ServerPin, tlnList))
				{
					// delete processed queue entries
					foreach (TeilnehmerItem tlnItem in tlnList)
					{
						if (tlnItem.Processed)
						{
							if (!_database.QueueDeleteByServerAndMsg(srvItem.Id, tlnItem.Uid))
							{
								_logger.Fatal(TAG, nameof(SendQueue), tag2,
										$"error deleting queue entry server={srvItem.Id} message={tlnItem.Uid}");
							}
						}
					}
				}
				else
				{
					_logger.Error(TAG, nameof(SendQueue), tag2, $"error sync server {srvItem.Address}");
				}
			}
		}

		public void Shutdown()
		{
			_logger.Info(TAG, nameof(Shutdown), TAG2, "");
			_shutDown = true;
		}

		private ServerItem[] _knownServer =
			[
				new ServerItem("tlnserv.teleprinter.net"),
				new ServerItem("tlnserv2.teleprinter.net"),
				new ServerItem("tlnserv3.teleprinter.net"),
				new ServerItem("tlnserv4.teleprinter.net"),
				new ServerItem("itelex.srvdns.de"),
				// new ServerItem("192.168.0.176"),
			];

		public string IpToServerName(string ipAddrStr)
		{
			IPAddress ipAddr = CommonHelper.GetIp4AddrFromHostname(ipAddrStr);
			return IpToServerName(ipAddr);
		}

		public string IpToServerName(IPAddress ipAddr)
		{
			try
			{
				ServerItem srvItem = (from s in _knownServer where ipAddr == s.IpAddress select s).FirstOrDefault();
				if (srvItem != null) return srvItem.Hostname;

				// not found, refresh ip-addresses
				foreach (ServerItem item in _knownServer)
				{
					item.IpAddress = CommonHelper.GetIp4AddrFromHostname(item.Hostname);
				}

				// search again
				srvItem = (from s in _knownServer where ipAddr.Equals(s.IpAddress) select s).FirstOrDefault();
				if (srvItem != null) return srvItem.Hostname;

				return ipAddr.ToString();
			}
			catch(Exception ex)
			{
				_logger.Error(TAG, nameof(IpToServerName), TAG2, "error", ex);
				return null;
			}
		}

		public void ReceivedUpdateItemAdd(long msgId, DateTime timestamp, IPAddress ipAddr)
		{
			SyncServerItem srv = null;
			foreach (SyncServerItem srvItem in _syncServer.SyncServer)
			{
				if (CommonHelper.GetIp4AddrFromHostname(srvItem.Address).Equals(ipAddr))
				{
					srv = srvItem;
					break;
				}
			}
			if (srv == null) return; // server address unknown

			ReceivedUpdateItem recvItem = new ReceivedUpdateItem()
			{
				TlnId = msgId,
				Timestamp = timestamp,
				ServerId = srv.Id
			};

			lock(_receivedUpdatesLock)
			{
				_receivedUpdates.Add(recvItem);
			}
		}

		public bool ReceivedUpdateItemExists(ReceivedUpdateItem recvItem)
		{
			lock (_receivedUpdatesLock)
			{
				return (from q in _receivedUpdates
								  where q.TlnId == recvItem.TlnId && q.ServerId == recvItem.ServerId &&
									q.Timestamp == recvItem.Timestamp
								  select q).Any();
			}
		}

		public void ReceivedUpdateItemRemove(ReceivedUpdateItem recvItem)
		{
			lock (_receivedUpdatesLock)
			{
				ReceivedUpdateItem item = (from q in _receivedUpdates 
										   where q.TlnId == recvItem.TlnId && q.ServerId == recvItem.ServerId &&
										   q.Timestamp == recvItem.Timestamp 
										   select q).FirstOrDefault();
				_receivedUpdates.Remove(item);
			}
		}

		private object _connectionCountLock = new object();
		private volatile int _connectionCount = 0;

		public void ConnectionCountInc()
		{
			lock (_connectionCountLock)
			{
				_connectionCount++;
			}
		}

		public void ConnectionCountDec()
		{
			lock (_connectionCountLock)
			{
				_connectionCount--;
			}
		}

		public int ConnectionCountGet()
		{
			lock (_connectionCountLock)
			{
				return _connectionCount;
			}
		}

		public void RemoveByList(int[] numbers)
		{
			foreach(int number in numbers)
			{
				TeilnehmerItem tln = _database.TeilnehmerLoadByNumber(number);
				if (tln == null)
				{
					_logger.Warn(TAG, nameof(RemoveByList), "Remove", $"number {number} not found");
				}
				if (tln.Type == 0)
				{
					tln.Pin = null;
					tln.Disabled = true;
					tln.Remove = true;
					tln.ChangedBy = GlobalData.Config.ServerId;
					tln.TimestampUtc = DateTime.UtcNow;
					tln.UpdateTimeUtc = DateTime.UtcNow;
					tln.Changed = true;
				}
				if (!_database.TeilnehmerUpdateByUid(tln))
				{
					_logger.Error(TAG, nameof(RemoveByList), "Remove", $"error updating number {number}");
				}
			}
		}
	}
}
