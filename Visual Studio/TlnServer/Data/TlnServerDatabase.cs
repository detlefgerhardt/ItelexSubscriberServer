using ItelexCommon;
using ItelexTlnServer.Data;
using ServerCommon.Utility;
using System.Data.SQLite;
using TlnServer;

namespace CentralexServer.Data
{
	internal class TlnServerDatabase: Database
    {
        private static string TAG = nameof(TlnServerDatabase);

        public object TlnServerLocker { get; set; } = new object();

        private const string TABLE_TEILNEHMER = "teilnehmer";
		private const string TABLE_QUEUE = "queue";
		private const string TABLE_SERVERS = "servers";

		/// <summary>
		/// singleton pattern
		/// </summary>
		private static TlnServerDatabase? instance;
        public static TlnServerDatabase Instance => instance ??= new TlnServerDatabase();

        private TlnServerDatabase() : base(Constants.DATABASE_NAME)
        {
            _logger = GlobalData.Logger;
			if (ConnectDatabase())
			{
				Console.WriteLine($"connected to database {Constants.DATABASE_NAME}");
				_logger.Notice(TAG, nameof(TlnServerDatabase), $"connected to database {Constants.DATABASE_NAME}");
			}
			else
			{
				Console.WriteLine($"failed to connect to database {Constants.DATABASE_NAME}");
				_logger.Error(TAG, nameof(TlnServerDatabase), $"failed to connect to database {Constants.DATABASE_NAME}");
			}
		}

		private List<TeilnehmerItem> _teilnehmerList = null;
		private List<TeilnehmerItem> _teilnehmerListAdmin = null;

		public List<TeilnehmerItem> TeilnehmerLoadAllAdmin()
        {
			if (_teilnehmerListAdmin != null) return _teilnehmerListAdmin;

            List<TeilnehmerItem> tlnItems = new List<TeilnehmerItem>();
            int count = 0;

            lock (Locker)
            {
                try
                {
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} ORDER BY name";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
                    SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
                    while (sqlReader.Read())
                    {
                        TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
                        tlnItems.Add(tlnItem);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(TAG, nameof(TeilnehmerLoadAll), "error", ex);
                    return null;
                }
            }

			_teilnehmerListAdmin = tlnItems;
			return tlnItems;
		}

		public List<TeilnehmerItem> TeilnehmerLoadAllChanged()
		{
			List<TeilnehmerItem> tlnItems = new List<TeilnehmerItem>();
			int count = 0;

			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} WHERE changed=1 ORDER BY name";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					while (sqlReader.Read())
					{
						TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
						tlnItems.Add(tlnItem);
						count++;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadAllChanged), "error", ex);
					return null;
				}
			}

			return tlnItems;
		}

		public List<TeilnehmerItem> TeilnehmerLoadAll()
		{
			if (_teilnehmerList != null) return _teilnehmerList;

			List<TeilnehmerItem> tlnItems = new List<TeilnehmerItem>();
			int count = 0;

			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} WHERE disabled<>1 AND type<>0 ORDER BY name";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					while (sqlReader.Read())
					{
						TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
						tlnItems.Add(tlnItem);
						count++;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadAll), "error", ex);
					return null;
				}
			}

			_teilnehmerList = tlnItems;
			return tlnItems;
		}

		public void TeilnehmerCacheClear()
		{
			_teilnehmerListAdmin = null;
			_teilnehmerList = null;
		}

		public TeilnehmerItem TeilnehmerLoadByUid(long uid)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} WHERE uid={uid}";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					if (sqlReader.Read())
					{
						TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
						return tlnItem;
					}
					else
					{
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadByUid), $"uid={uid}", ex);
					return null;
				}
			}
		}

		public TeilnehmerItem TeilnehmerLoadByNumber(int number)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} WHERE number={number}";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					if (sqlReader.Read())
					{
						TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
						return tlnItem;
					}
					else
					{
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadByNumber), $"number={number}", ex);
					return null;
				}
			}
		}

		public bool TeilnehmerInsert(TeilnehmerItem tlnItem)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = GetItemInsertString(tlnItem, TABLE_TEILNEHMER);
					bool ok = DoSqlNoQuery(sqlStr);
					if (!ok) return false;
					tlnItem.uid = (Int64)DoSqlScalar($"SELECT last_insert_rowid() FROM {TABLE_TEILNEHMER}");
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerInsert), "error", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerUpdateFromClient(int number, string ipaddress, int port, int pin)
		{
			/*
			lock (Locker)
			{
				try
				{
					string updateString = $"UPDATE {TABLE_TEILNEHMER} SET Name='{EscapeSql(name)}' WHERE number={number}";
					DoSqlNoQuery(updateString);
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsUpdateName), $"number={number} name={name}", ex);
					return false;
				}
			}
			*/
			return false;
		}

		public bool TeilnehmerUpdateByUid(TeilnehmerItem tlnItem)
		{
			lock (Locker)
			{
				try
				{
					string updateString = GetItemUpdateString(tlnItem, TABLE_TEILNEHMER, $"uid={tlnItem.uid}");
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerUpdateByUid), $"uid={tlnItem.uid} number={tlnItem.number}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerUpdateByNumber(TeilnehmerItem tlnItem)
		{
			lock (Locker)
			{
				try
				{
					string updateString = GetItemUpdateString(tlnItem, TABLE_TEILNEHMER, $"number={tlnItem.number}");
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerUpdateByNumber), $"number={tlnItem.number}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerResetPin(ulong uid)
		{
			lock (Locker)
			{
				try
				{
					int timestamp = CommonHelper.DateTimeToTimestampUtc(DateTime.UtcNow);
					string updateString =
							$"UPDATE {TABLE_TEILNEHMER} SET pin=NULL, changed=1, timestamp={timestamp} WHERE uid={uid}";
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerResetPin), $"uid={uid}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerSetType(ulong uid, int type)
		{
			lock (Locker)
			{
				try
				{
					int timestamp = CommonHelper.DateTimeToTimestampUtc(DateTime.UtcNow);
					string updateString =
							$"UPDATE {TABLE_TEILNEHMER} SET type={type}, changed=1, timestamp={timestamp} WHERE uid={uid}";
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerSetType), $"uid={uid}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerSetChanged(long uid, bool changed)
		{
			lock (Locker)
			{
				try
				{
					int changedVal = changed ? 1 : 0;
					string updateString =
							$"UPDATE {TABLE_TEILNEHMER} SET changed={changedVal} WHERE uid={uid}";
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerSetChanged), $"uid={uid} changed={changed}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerDelete(ulong uid)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"DELETE FROM {TABLE_TEILNEHMER} WHERE uid={uid}";
					bool ok = DoSqlNoQuery(sqlStr);
					if (!ok) return false;
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerDelete), $"uid={uid}", ex);
					return false;
				}
			}
		}

		public List<ServersItem> ServersLoadAll(int version)
		{
			List<ServersItem> srvItems = new List<ServersItem>();
			int count = 0;

			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_SERVERS} WHERE version={version}";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					while (sqlReader.Read())
					{
						ServersItem srvItem = ItemGetQuery<ServersItem>(sqlReader);
						srvItems.Add(srvItem);
						count++;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ServersLoadAll), "error", ex);
					return null;
				}
			}

			return srvItems;
		}

		public ServersItem ServersLoadByUid(long uid, int version)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_SERVERS} WHERE uid={uid} AND version={version}";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					if (sqlReader.Read())
					{
						ServersItem srvItem = ItemGetQuery<ServersItem>(sqlReader);
						return srvItem;
					}
					else
					{
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ServersLoadByUid), $"uid={uid}, version={version}", ex);
					return null;
				}
			}
		}


		public List<QueueItem> QueueLoadAll()
		{
			List<QueueItem> srvItems = new List<QueueItem>();
			int count = 0;

			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_QUEUE}";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					while (sqlReader.Read())
					{
						QueueItem queItem = ItemGetQuery<QueueItem>(sqlReader);
						srvItems.Add(queItem);
						count++;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueLoadAll), "error", ex);
					return null;
				}
			}

			return srvItems;
		}

		public QueueItem QueueLoadByServerAndMsg(long server, long message)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_QUEUE} WHERE server={server} AND message={message}";
					SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, _sqlConn);
					SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
					if (sqlReader.Read())
					{
						QueueItem queItem = ItemGetQuery<QueueItem>(sqlReader);
						return queItem;
					}
					else
					{
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadByNumber), $"server={server}, message={message}", ex);
					return null;
				}
			}
		}

		public bool QueueInsert(QueueItem queItem)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = GetItemInsertString(queItem, TABLE_QUEUE);
					bool ok = DoSqlNoQuery(sqlStr);
					if (!ok) return false;
					queItem.uid = (Int64)DoSqlScalar($"SELECT last_insert_rowid() FROM {TABLE_QUEUE}");
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueInsert), "error", ex);
					return false;
				}
			}
		}


		public bool QueueUpdateByUid(QueueItem queItem)
		{
			lock (Locker)
			{
				try
				{
					string updateString = GetItemUpdateString(queItem, TABLE_QUEUE, $"uid={queItem.uid}");
					DoSqlNoQuery(updateString);
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueUpdateByUid), $"number={queItem.uid}", ex);
					return false;
				}
			}
		}

		public bool QueueDeleteByUid(long uid)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"DELETE FROM {TABLE_QUEUE} WHERE uid={uid}";
					bool ok = DoSqlNoQuery(sqlStr);
					if (!ok) return false;
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueDeleteByUid), $"uid={uid}", ex);
					return false;
				}
			}
		}

		public bool QueueDeleteByServerAndMsg(long server, long message)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"DELETE FROM {TABLE_QUEUE} WHERE server={server} AND message={message}";
					bool ok = DoSqlNoQuery(sqlStr);
					if (!ok) return false;
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueDeleteByServerAndMsg), $"server={server} message={message}", ex);
					return false;
				}
			}
		}


	}
}
