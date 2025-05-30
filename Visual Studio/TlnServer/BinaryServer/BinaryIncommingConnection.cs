using CentralexServer.Data;
using ItelexTlnServer.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerCommon.Logging;
using ServerCommon.Utility;
using ServerCommon.WebServer;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TlnServer.BinaryServer
{
	internal class BinaryIncommingConnection
	{
		private const string TAG = nameof(BinaryIncommingConnection);

		private TcpClient _tcpClient;

		private Logger _logger;

		private TlnServerMsDatabase _database;

		private BinaryPacket _lastPacket;

		private object _sendCmdLock = new object();

		private volatile bool IsConnected;

		private string _host;
		private string _tag2;

		private volatile bool _disconnectActive;

		private object _clientReceiveBufferLock = new object();
		private Queue<byte> _clientReceiveBuffer;

		private bool _ackRecevied = false;

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

		//public string RemoteName { get; set; }

		public BinaryIncommingConnection(TcpClient tcpClient, string logPath, LogTypes logLevel, string host)
		{
			_host = host;
			_tag2 = $"Binary incoming from {host}";

			_tcpClient = tcpClient;
			_logger = GlobalData.Logger;
			_database = TlnServerMsDatabase.Instance;
			IsConnected = true;
			_disconnectActive = false;
			_lastPacket = null;

			//RemoteName = BinaryServerManager.Instance.IpToServerName(RemoteIpAddress);

			Log(LogTypes.Debug, nameof(BinaryIncommingConnection), $"--- New binary connection from ip={_host} ---");

			_clientReceiveBuffer = new Queue<byte>();
			StartReceive();

			int avail = _tcpClient.Available;
			if (avail > 0)
			{
				Log(LogTypes.Warn, nameof(BinaryIncommingConnection), $"client buffer={avail}");
				byte[] preBuffer = new byte[avail];
				_tcpClient.Client.Receive(preBuffer, avail, SocketFlags.None);
				List<string> dumpLines = CommonHelper.DumpByteArrayStr(preBuffer, 0);
				foreach (string s in dumpLines)
				{
					Log(LogTypes.Warn, nameof(BinaryIncommingConnection), s);
				}
			}
			/*
			if (avail > 0)
			{
				byte[] preBuffer = new byte[avail];
				_tcpClient.Client.Receive(preBuffer, avail, SocketFlags.None);
			}
			*/
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

		public async Task Start()
		{
			Log(LogTypes.Debug, nameof(Start), $"incoming remote {TickTimer.GetTicksMs() % 100000}ms");

			TickTimer timeout = new TickTimer();
			while (IsConnected)
			{
				if (timeout.IsElapsedMilliseconds(5000))
				{
					Log(LogTypes.Warn, nameof(Start), "packet timeout");
					DisconnectTcp(DisconnectReasons.TcpDisconnect);
					break;
				}

				if (_lastPacket != null)
				{
					HandlePacket(_lastPacket);
					_lastPacket = null;
					timeout.Start();
				}
				await Task.Delay(100);
				//CommonHelper.TaskSleep(50);
			}
		}

		protected void DisconnectTcp(DisconnectReasons reason)
		{
			Log(LogTypes.Debug, nameof(DisconnectTcp), $"Disconnect reason={reason}, IsConnected={IsConnected}");

			if (_disconnectActive)
			{
				Log(LogTypes.Debug, nameof(DisconnectTcp), "Disconnect already active");
				return;
			}
			_disconnectActive = true;

			try
			{
				if (!IsConnected)
				{
					Log(LogTypes.Debug, nameof(DisconnectTcp), "connection already disconnected");
					return;
				}

				IsConnected = false;
				Log(LogTypes.Debug, nameof(DisconnectTcp), $"IsConnected={IsConnected}");

				/*
				try
				{
					_clientReceiveTimer?.Stop();
				}
				catch (Exception ex)
				{
					_connectionLogger.ItelexLog(LogTypes.Error, TAG, nameof(DisconnectTcp), $"stop timers", ex);
				}
				*/

				if (_tcpClient.Client != null)
				{
					Log(LogTypes.Debug, nameof(DisconnectTcp), "close TcpClient");
					if (_tcpClient.Connected)
					{
						try
						{
							NetworkStream stream = _tcpClient.GetStream();
							stream.Close();
						}
						catch (Exception ex)
						{
							Log(LogTypes.Error, nameof(DisconnectTcp), $"stream.Close", ex);
						}
					}

					try
					{
						_tcpClient.Close();
					}
					catch (Exception ex)
					{
						Log(LogTypes.Error, nameof(DisconnectTcp), $"_tcpClient.Close", ex);
					}
				}

				Log(LogTypes.Debug, nameof(DisconnectTcp), "connection closed");

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

			//Log(LogTypes.Debug, nameof(StartReceive), $"StartReceive {TickTimer.GetTicksMs() % 100000}ms");

			byte[] buffer = new byte[1024];
			try
			{
				_tcpClient.Client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, DataReceived, buffer);
				Log(LogTypes.Debug, nameof(StartReceive), "start receive");
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(StartReceive), "error", ex);
			}
		}

		private void DataReceived(IAsyncResult ar)
		{
			try
			{
				if (_tcpClient.Client == null || !IsConnected) return;

				int avail = _tcpClient.Available;
				Log(LogTypes.Debug, nameof(DataReceived), $"client buffer={avail}");

				int dataReadCount;
				try
				{
					dataReadCount = _tcpClient.Client.EndReceive(ar);
					Log(LogTypes.Debug, nameof(DataReceived), $"dataReadCount = {dataReadCount}");

					if (dataReadCount == 0)
					{
						DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
						return;
					}
				}
				catch (Exception)
				{
					DisconnectTcp(DisconnectReasons.TcpDisconnectByRemote);
					return;
				}

				byte[] byteData = ar.AsyncState as byte[];
				Array.Resize(ref byteData, dataReadCount);

				//List<string> dump = CommonHelper.DumpByteArrayStr(byteData, 0);
				//foreach(string s in dump)
				//{
				//	Log(LogTypes.Debug, nameof(DataReceived), $"{s}");
				//}

				AddReceiveBuffer(byteData);

				byte[] packetData;
				//lock (_clientReceiveBufferLock)
				{
					if (_clientReceiveBuffer.Count < 2) return;

					byte pktType = _clientReceiveBuffer.ElementAt(0); // cmd
					byte pktLen = _clientReceiveBuffer.ElementAt(1); // len

					if (_clientReceiveBuffer.Count < pktLen + 2) return;

					// complete packet received
					packetData = new byte[pktLen + 2];
					for (int i = 0; i < pktLen + 2; i++)
					{
						packetData[i] = _clientReceiveBuffer.Dequeue();
					}
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
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(DataReceived), $"error receiving packet", ex);
			}
			finally
			{
				StartReceive();
			}
		}

		private void AddReceiveBuffer(byte[] data)
		{
			//lock (_clientReceiveBufferLock)
			{
				foreach (byte b in data)
				{
					_clientReceiveBuffer.Enqueue(b);
				}
			}
		}

		/// <summary>
		/// Receive data from client
		/// </summary>
		/// <param name="packet"></param>
		private void HandlePacket(BinaryPacket packet)
		{
			if (packet == null) return;

			string ipStr = RemoteIpAddress.ToString();

			switch(packet.CommandType)
			{
				case BinaryCommands.ClientUpdate:
					HandleClientUpdate(packet);
					break;
				case BinaryCommands.PeerQuery:
					HandlePeerQuery(packet);
					break;
				case BinaryCommands.SyncFullQuery:
					HandleSyncFullQuery(packet);
					break;
				case BinaryCommands.SyncLogin:
					HandleSyncLogin(packet);
					break;
				/*
				case BinaryCommands.Acknowledge:
					_ackRecevied = true;
					break;
				*/
				case BinaryCommands.PeerSearch:
					HandlePeerSearch(packet);
					break;
			}
		}

		private void HandleClientUpdate(BinaryPacket packet)
		{
			//Log(LogTypes.Debug, nameof(HandleClientUpdate), "start");
			_tag2 = $"ClientUpdate from {_host}";

			try
			{
				byte[] data = packet.Data;
				int number = BitConverter.ToInt32(data, 0);
				int? pin = BitConverter.ToUInt16(data, 4);
				if (pin == 0) pin = null;
				int port = BitConverter.ToUInt16(data, 6);

				Log(LogTypes.Debug, nameof(HandleClientUpdate), "start client-update");

				TeilnehmerItem tlnUpdate = _database.TeilnehmerLoadByNumber(number);
				if (tlnUpdate == null || tlnUpdate.Disabled || tlnUpdate.Remove)
				{
					Log(LogTypes.Notice, nameof(HandleClientUpdate), $"unknown or disabled number {number}");
					SendPacket(BinaryPacket.GetError("unknown client"));
					return;
				}

				if (tlnUpdate.Type != (int)ClientAddressTypes.Baudot_DynIp)
				{
					Log(LogTypes.Notice, nameof(HandleClientUpdate),
							$"can not update address type {(ClientAddressTypes)tlnUpdate.Type} ({tlnUpdate.Type}) " +
							$"for number {number}");
					SendPacket(BinaryPacket.GetError("wrong address type"));
					return;
				}

				if (pin == null)
				{
					if (number == 184545)
					{
						Log(LogTypes.Notice, nameof(HandleClientUpdate), $"special 184545 pin handling ;-)");
					}
					else
					{
						// TODO: block updates with pin == null
						Log(LogTypes.Notice, nameof(HandleClientUpdate), $"pin is 'null' for nmber {number}");
						// SendPacket(BinaryPacket.GetError("wrong client pin"));
						// return;
					}
				}
				if (tlnUpdate.PinOrNull == null)
				{
					// that's ok
					Log(LogTypes.Notice, nameof(HandleClientUpdate), $"client pin is 'null' in database for number {number}");
				}

				if (tlnUpdate.PinOrNull != null && tlnUpdate.Pin != pin)
				{
					Log(LogTypes.Notice, nameof(HandleClientUpdate), $"wrong pin '{pin}' for number {number}");
					SendPacket(BinaryPacket.GetError("wrong client pin"));
					return;
				}

				if (tlnUpdate.IpAddress == RemoteIpAddress.ToString() && tlnUpdate.Port == port && tlnUpdate.Pin == pin)
				{
					Log(LogTypes.Info, nameof(HandleClientUpdate), $"no changes for number {number}");
					BinaryPacket sendPacket2 = BinaryPacket.GetAddressConfirm(RemoteIpAddress.GetAddressBytes());
					SendPacket(sendPacket2);
					return;
				}

				List<TeilnehmerItem> tlnList = _database.TeilnehmerLoadAllAdmin();
				if (tlnList == null)
				{
					_logger.Fatal(TAG, nameof(HandleClientUpdate), _tag2, "error loading Teilnehmer list from database");
					SendPacket(BinaryPacket.GetError("internal error"));
					return;
				}

				// look for joined entries

				string name15 = tlnUpdate.Name.ExtLeftString(15);
				List<TeilnehmerItem> joinedTlnList;
				if (tlnUpdate.Name.Length >= 15)
				{
					joinedTlnList =
						(from t in tlnList
						 where !t.Disabled && !t.Remove &&
							t.Type == (int)ClientAddressTypes.Baudot_DynIp &&
							t.Name.Length >= 15 && t.Name.ExtLeftString(15) == name15 &&
							t.Port == tlnUpdate.Port &&
							(t.PinOrNull == null || t.Pin == tlnUpdate.Pin)
						 select t).ToList();
				}
				else
				{
					// no joined entries, only one entry
					joinedTlnList = new List<TeilnehmerItem>() { tlnUpdate };
				}

				// check for other numbers with same IP data and that or not joined

				List<int> sameIpData = (from t in tlnList
												   where t.Type == (int)ClientAddressTypes.Baudot_DynIp &&
												   t.IpAddress == RemoteIpAddress.ToString() &&
												   t.Port == port &&
												   (t.Name.Length < 15 || t.Name.ExtLeftString(15) != name15)
												   select t.Number).ToList();
				if (sameIpData.Count > 0)
				{
					string sameIpNums = string.Join(',', sameIpData);
					_logger.Error(TAG, nameof(HandleClientUpdate), _tag2,
							$"entries with the same IP data exist: {sameIpNums}");
					return;
				}

				// update joined entries

				foreach (TeilnehmerItem tln in joinedTlnList)
				{
					DateTime dt = DateTime.UtcNow;
					tln.IpAddress = RemoteIpAddress.ToString();
					tln.Port = port;
					tln.TimestampUtc = dt;
					tln.UpdateTimeUtc = dt;
					tln.UpdatedBy = GlobalData.Config.ServerId;
					tln.LeadingEntry = tln.Number == number;
					tln.Changed = true;

					if (tln.PinOrNull == null && pin != null)
					{
						Log(LogTypes.Info, nameof(HandleClientUpdate), $"set new pin for number {tln.Number}");
						tln.Pin = pin;
					}
					if (!_database.TeilnehmerUpdateByUid(tln))
					{
						_logger.Fatal(TAG, nameof(HandleClientUpdate), _tag2, $"error updating number {tln.Number} in database");
						SendPacket(BinaryPacket.GetError("internal error"));
						return;
					}
					Log(LogTypes.Info, nameof(HandleClientUpdate), $"update {tln.Number}");
				}

				BinaryPacket sendPacket = BinaryPacket.GetAddressConfirm(RemoteIpAddress.GetAddressBytes());
				SendPacket(sendPacket);

				BinaryServerManager.Instance.SyncTrigger = true;
				Log(LogTypes.Debug, nameof(HandleClientUpdate), "set SyncTrigger");
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(HandleClientUpdate), "error", ex);
			}
		}

		private void HandlePeerSearch(BinaryPacket packet)
		{
			_tag2 = $"PeerSearch from {_host}";

			try
			{

				byte[] data = packet.Data;
				int version = data[0];
				string search = Encoding.ASCII.GetString(data, 1, data.Length - 1);
				search = search.TrimEnd('\x00');

				Log(LogTypes.Info, nameof(HandlePeerSearch), $"search='{search}'");

				if (string.IsNullOrWhiteSpace(search))
				{
					SendPacket(BinaryPacket.GetEndOfList());
					return;
				}

				List<TeilnehmerItem> tlnList = _database.TeilnehmerLoadAll();
				if (tlnList == null)
				{
					_logger.Fatal(TAG, nameof(HandlePeerSearch), _tag2, "error loading Teilnehmer from database");
					SendPacket(BinaryPacket.GetError("internal error"));
					return;
				}

				List<TeilnehmerItem> found = (from t in tlnList
											  where t.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
											  select t).ToList();

				BinaryPacket sendPacket;
				foreach (TeilnehmerItem tln in found)
				{
					sendPacket = BinaryPacket.TeilnehmerToPeerReplyV1(tln);
					_ackRecevied = false;
					SendPacket(sendPacket);
					Log(LogTypes.Debug, nameof(HandlePeerSearch), $"send tln={tln.Number}");

					TickTimer timeout = new TickTimer();
					while (true)
					{
						if (timeout.IsElapsedMilliseconds(5000))
						{
							// timeout
							Log(LogTypes.Warn, nameof(HandlePeerSearch), $"peer-search: ack timeout {timeout.ElapsedMilliseconds}");
							return;
						}
						if (_ackRecevied)
						{
							Log(LogTypes.Debug, nameof(HandlePeerSearch), "peer-search: ack received");
							break;
						}
						Thread.Sleep(50);
					}
				}

				Log(LogTypes.Info, nameof(HandlePeerSearch), $"peer-search: {found.Count} enties sent");

				Log(LogTypes.Debug, nameof(HandlePeerSearch), "peer-search: send EndOfList");
				SendPacket(BinaryPacket.GetEndOfList());
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(HandlePeerSearch), "error", ex);
			}
		}

		private void HandleSyncFullQuery(BinaryPacket packet)
		{
			_tag2 = $"SyncFullQuery from {_host}";

			Log(LogTypes.Info, nameof(HandleSyncFullQuery), "start");

			try
			{

				byte[] data = packet.Data;
				if (data.Length != 5)
				{
					Log(LogTypes.Warn, nameof(HandleSyncFullQuery), $"invalid packet");
					SendPacket(BinaryPacket.GetError("invalid packet"));
					return;
				}

				int version = data[0];
				if (version != 1)
				{
					Log(LogTypes.Warn, nameof(HandleSyncFullQuery), $"wrong version ({version})");
					SendPacket(BinaryPacket.GetError("wrong version"));
					return;
				}

				uint pin = BitConverter.ToUInt32(data, 1);
				if (pin != GlobalData.Config.ServerPin)
				{
					Log(LogTypes.Warn, nameof(HandleSyncFullQuery), $"wrong server pin ({pin})");
					SendPacket(BinaryPacket.GetError("wrong server pin"));
					return;
				}

				List<TeilnehmerItem> tlnList = _database.TeilnehmerLoadAllAdmin();
				if (tlnList == null)
				{
					Log(LogTypes.Fatal, nameof(HandleSyncFullQuery), "error reading Teilnehmer from database");
					SendPacket(BinaryPacket.GetError("internal error"));
					return;
				}

				BinaryPacket sendPacket;
				int ackTimeoutCnt = 0;
				TickTimer ackTimeout = new TickTimer();
				foreach (TeilnehmerItem tlnItem in tlnList)
				{
					// send sync reply
					_ackRecevied = false;
					sendPacket = BinaryPacket.TeilnehmerToSyncReplyV3(tlnItem);
					SendPacket(sendPacket);
					Log(LogTypes.Debug, nameof(HandleSyncFullQuery), $"send tln={tlnItem.Number} v{3}");

					// wait for acknowledge
					ackTimeout.Start();
					while (true)
					{
						if (ackTimeout.IsElapsedMilliseconds(5000))
						{
							ackTimeoutCnt++;
							Log(LogTypes.Warn, nameof(HandleSyncFullQuery), $"ack timeout {ackTimeoutCnt}");
							if (ackTimeoutCnt >= 3)
							{
								Log(LogTypes.Warn, nameof(HandleSyncFullQuery), "aborting after 3 timeouts");
								return;
							}
							// continue with next entry
							break;
						}

						if (_ackRecevied)
						{
							ackTimeoutCnt = 0;
							Log(LogTypes.Debug, nameof(HandleSyncFullQuery), $"{BinaryCommands.Acknowledge} received");
							break;
						}
						Thread.Sleep(50);
					}
				}

				Log(LogTypes.Info, nameof(HandleSyncFullQuery), $"{tlnList.Count} entries sent");

				// send end of list
				sendPacket = BinaryPacket.GetEndOfList();
				SendPacket(sendPacket);

			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(HandleSyncFullQuery), "error", ex);
			}
		}

		private void HandleSyncLogin(BinaryPacket packet)
		{
			_tag2 = $"SyncLogin from {_host}";

			Log(LogTypes.Debug, nameof(HandleSyncLogin), "start");

			try
			{

				byte[] data = packet.Data;
				if (data.Length != 5)
				{
					Log(LogTypes.Warn, nameof(HandleSyncLogin), "invalid packet");
					SendPacket(BinaryPacket.GetError("invalid packet"));
					return;
				}

				int version = data[0];
				if (version != 1)
				{
					Log(LogTypes.Warn, nameof(HandleSyncLogin), "wrong version");
					SendPacket(BinaryPacket.GetError("wrong version"));
					return;
				}

				uint pin = BitConverter.ToUInt32(data, 1);
				if (pin != (uint)GlobalData.Config.ServerPin)
				{
					Log(LogTypes.Warn, nameof(HandleSyncLogin), $"sync-login: wrong server pin {pin}");
					SendPacket(BinaryPacket.GetError("wrong server pin"));
					return;
				}

				_lastPacket = null;
				BinaryPacket sendPacket = BinaryPacket.GetAcknowledge();
				SendPacket(sendPacket);

				bool syncTrigger = false;

				TickTimer timeout = new TickTimer();
				while (true)
				{
					if (timeout.IsElapsedMilliseconds(5000))
					{
						Log(LogTypes.Warn, nameof(HandleSyncLogin), $"timeout {timeout.ElapsedMilliseconds}");
						return;
					}

					if (_lastPacket != null)
					{
						BinaryPacket recvPacket = _lastPacket;
						_lastPacket = null;
						Log(LogTypes.Debug, nameof(HandleSyncLogin), $"received packet {recvPacket.CommandType}");
						if (recvPacket.CommandType == BinaryCommands.SyncReplyV2 ||
							recvPacket.CommandType == BinaryCommands.SyncReplyV3)
						{
							UpdateFromServer(recvPacket, ref syncTrigger);
							sendPacket = BinaryPacket.GetAcknowledge();
							SendPacket(sendPacket);
						}
						else if (recvPacket.CommandType == BinaryCommands.EndOfList)
						{
							break;
						}
						else
						{
							Log(LogTypes.Warn, nameof(HandleSyncLogin), $"invalid packet {recvPacket.CommandType}");
						}
						timeout.Start();
					}
					Thread.Sleep(100);
				}

				if (syncTrigger)
				{
					BinaryServerManager.Instance.SyncTrigger = true;
					Log(LogTypes.Debug, nameof(HandleSyncLogin), "set SyncTrigger");
				}
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(HandleSyncLogin), "error", ex);
			}
		}

		private bool UpdateFromServer(BinaryPacket replyPacket, ref bool syncTrigger)
		{
			string tag2_backup = _tag2;

			try
			{
				TeilnehmerItem newTln = null;
				int version = 0;
				if (replyPacket.CommandType == BinaryCommands.SyncReplyV2)
				{
					version = 2;
					newTln = BinaryPacket.SyncReplyV2ToTeilnehmer(replyPacket);
				}
				else if (replyPacket.CommandType == BinaryCommands.SyncReplyV3)
				{
					version = 3;
					newTln = BinaryPacket.SyncReplyV3ToTeilnehmer(replyPacket);
				}

				_tag2 = $"{_tag2} V{version}";

				if (newTln == null)
				{
					Log(LogTypes.Warn, nameof(UpdateFromServer), $"recv invalid packet");
					SendPacket(BinaryPacket.GetError("invalid packet"));
					return false;
				}

				newTln.Changed = true;
				//newTln.timestamp = DateTime.UtcNow;

				TeilnehmerItem oldTln = _database.TeilnehmerLoadByNumber(newTln.Number);

				if (newTln.Remove && newTln.IpAddress.ToString() == "55.55.55.55" && newTln.Hostname == "#remove#" &&
					newTln.Pin == 55555)
				{
					Log(LogTypes.Warn, nameof(UpdateFromServer), $"remove requested for number {newTln.Number}");
					// delete here...
					return true;
				}

				if (oldTln == null)
				{
					bool databaseOk = _database.TeilnehmerInsert(newTln);
					if (databaseOk)
					{
						syncTrigger = true;
						Log(LogTypes.Info, nameof(UpdateFromServer), $"inserted new number {newTln.Number} V{version}");
					}
					else
					{
						Log(LogTypes.Fatal, nameof(UpdateFromServer), $"error inserting new number {newTln.Number} in database");
						return false;
					}
				}
				else
				{
					string diff = oldTln.CompareToString(newTln, out string diffErr);
					if (diffErr != null) diff = diffErr;

					if (newTln.TimestampUtc < oldTln.TimestampUtc)
					{
						Log(LogTypes.Notice, nameof(UpdateFromServer),
								$"{newTln.Number} update is outdated, diff={diff} " +
								$"(local={oldTln.TimestampUtc:dd.MM.yyyy HH:mm:ss} " + 
								$"remote={newTln.TimestampUtc:dd.MM.yyyy HH:mm:ss})");
						return true; // uptodate is outdated
					}
					else if (newTln.TimestampUtc == oldTln.TimestampUtc)
					{
						Log(LogTypes.Info, nameof(UpdateFromServer), $"{newTln.Number} is uptodate");
						return true; // already uptodate
					}

					newTln.Uid = oldTln.Uid;
					bool databaseOk = _database.TeilnehmerUpdateByNumber(newTln);
					if (databaseOk)
					{
						syncTrigger = true;
						Log(LogTypes.Info, nameof(UpdateFromServer), $"update v{version} {newTln.Number}, diff={diff}");
					}
					else
					{
						Log(LogTypes.Fatal, nameof(UpdateFromServer), $"error updating {newTln.Number} in database");
						return false;
					}
				}

				BinaryServerManager.Instance.ReceivedUpdateItemAdd(newTln.Uid, newTln.TimestampUtc, RemoteIpAddress);

				return true;
			}
			catch(Exception ex)
			{
				Log(LogTypes.Error, nameof(UpdateFromServer), "error", ex);
				return false;
			}
			finally
			{
				_tag2 = tag2_backup;
			}
		}

		private void HandlePeerQuery(BinaryPacket packet)
		{
			_tag2 = $"PeerQuery from {_host}";

			byte[] data = packet.Data;
			int number = BitConverter.ToInt32(data, 0);

			Log(LogTypes.Debug, nameof(HandlePeerQuery), $"peer-query for number {number}");

			TeilnehmerItem tln = _database.TeilnehmerLoadByNumber(number);
			BinaryPacket sendPacket;
			if (tln != null && !tln.Disabled && !tln.Remove)
			{
				sendPacket = BinaryPacket.TeilnehmerToPeerReplyV1(tln);
				Log(LogTypes.Info, nameof(HandlePeerQuery), $"send {tln.Number}");
			}
			else
			{
				sendPacket = BinaryPacket.GetPeerNotFound();
				Log(LogTypes.Notice, nameof(HandlePeerQuery), $"number {number} not found");
			}
			SendPacket(sendPacket);
		}

		public void HandleResetCentralexClients(uint serverPin, BinaryPacket packet)
		{
			string _tag2 = $"ResetCentralexClients from {_host}";

			byte[] data = packet.Data;
			if (data.Length != 49)
			{
				Log(LogTypes.Warn, nameof(HandleSyncFullQuery), "invalid packet");
				SendPacket(BinaryPacket.GetError("invalid packet"));
				return;
			}

			uint pin = BitConverter.ToUInt32(data, 1);
			if (pin != GlobalData.Config.ServerPin)
			{
				Log(LogTypes.Warn, nameof(HandleSyncFullQuery), $"wrong server pin ({pin})");
				SendPacket(BinaryPacket.GetError("wrong server pin"));
				return;
			}

			ResetCentralexClientsItem item = BinaryPacket.PacketToResetCentralexClients(packet);
			if (item == null)
			{
				Log(LogTypes.Warn, nameof(HandleSyncFullQuery), "invalid packet");
				SendPacket(BinaryPacket.GetError("invalid packet"));
				return;
			}

			List<TeilnehmerItem> tlns = _database.TeilnehmerLoadAllAdmin();
			foreach(TeilnehmerItem tln in tlns)
			{
				if (tln.Disabled || tln.Remove) continue;
				if (tln.Type != (int)ClientAddressTypes.Baudot_DynIp) continue;
				if (tln.IpAddress != item.IpAddress) continue;
				if (!tln.Port.HasValue || tln.Port < item.FromPort || tln.Port > item.ToPort) continue;

				tln.Port = 134;
				tln.IpAddress = "1.1.1.1";
				if (!_database.TeilnehmerUpdateByUid(tln))
				{
					_logger.Fatal(TAG, nameof(HandleResetCentralexClients), _tag2,
						$"error updating {tln.Number} in database");
					return;
				}
				_logger.Notice(TAG, nameof(HandleResetCentralexClients), _tag2,
					$"resetted centralex client {tln.Number} in database");
			}
		}


		#endregion Receive data

		#region Send data

		private void SendPacket(BinaryPacket packet)
		{
			if (!IsConnected || _tcpClient?.Client == null) return;

			lock (_sendCmdLock)
			{
				if (!IsConnected) return;

				BinaryCommands cmd = packet.CommandType;

				try
				{
					_tcpClient.Client.BeginSend(packet.PacketBuffer, 0, packet.PacketBuffer.Length, SocketFlags.None, EndSend, null);
				}
				catch (SocketException sockEx)
				{
					if ((uint)sockEx.HResult == 0x80004005)
					{
						Log(LogTypes.Warn, nameof(SendPacket),
							$"cmd={cmd}, connection closed by remote (HResult=0x{(uint)sockEx.HResult:X08})");
					}
					else
					{
						Log(LogTypes.Warn, nameof(SendPacket), cmd.ToString(), sockEx);
					}
					DisconnectTcp(DisconnectReasons.TcpDisconnect);
				}
				catch (Exception ex)
				{
					Log(LogTypes.Warn, nameof(SendPacket), cmd.ToString(), ex);
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
			catch(Exception)
			{
			}
		}

		#endregion Send data

		/*
		private string GetSessionContext()
		{
			return RemoteName ?? RemoteIpAddress.ToString();
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
