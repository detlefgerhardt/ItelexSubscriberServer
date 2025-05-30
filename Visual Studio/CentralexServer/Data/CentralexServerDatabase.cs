using ItelexCommon;
using ItelexTlnServer.Data;
using Microsoft.Data.Sqlite;

namespace CentralexServer.Data
{
	internal class CentralexServerDatabase: MsDatabase
    {
        private static string TAG = nameof(CentralexServerDatabase);
		private static string TAG2 = "";

		public object CentralexLocker { get; set; } = new object();

        private const string TABLE_CLIENTS = "clients";

        /// <summary>
        /// singleton pattern
        /// </summary>
        private static CentralexServerDatabase? instance;
        public static CentralexServerDatabase Instance => instance ??= new CentralexServerDatabase();

        private CentralexServerDatabase() : base(Constants.DATABASE_NAME)
        {
            _logger = GlobalData.Logger;
			if (ConnectDatabase())
			{
				_logger.ConsoleLog(TAG, nameof(CentralexServerDatabase), TAG2,
						$"connected to database {Constants.DATABASE_NAME}");
			}
			else
			{
				_logger.ConsoleLog(TAG, nameof(CentralexServerDatabase), TAG2, $"failed to connect to database {Constants.DATABASE_NAME}");
				_logger.Error(TAG, nameof(CentralexServerDatabase), TAG2,
						$"failed to connect to database {Constants.DATABASE_NAME}");
			}
		}

		public List<ClientItem> ClientsLoadAll()
        {
            List<ClientItem> items = new List<ClientItem>();
            int count = 0;

            lock (Locker)
            {
                try
                {
                    string sqlStr = $"SELECT * FROM {TABLE_CLIENTS} ORDER BY Number";
                    SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					SqliteDataReader sqlReader = sqlCmd.ExecuteReader();
                    while (sqlReader.Read())
                    {
                        ClientItem item = ItemGetQuery<ClientItem>(sqlReader);
                        items.Add(item);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(TAG, nameof(ClientsLoadAll), TAG2, "error", ex);
                    return null;
                }
            }

			return items;
		}

		public ClientItem ClientsLoadByNumber(int number)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_CLIENTS} WHERE Number={number}";
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					SqliteDataReader sqlReader = sqlCmd.ExecuteReader();
					if (sqlReader.Read())
					{
						ClientItem clientItem = ItemGetQuery<ClientItem>(sqlReader);
						return clientItem;
					}
					else
					{
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsLoadByNumber), TAG2, $"number={number}", ex);
					return null;
				}
			}
		}

		public ClientItem ClientsLoadByPort(int port)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_CLIENTS} WHERE Port={port}";
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					SqliteDataReader sqlReader = sqlCmd.ExecuteReader();
					if (sqlReader.Read())
					{
						ClientItem clientItem = ItemGetQuery<ClientItem>(sqlReader);
						return clientItem;
					}
					else
					{
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsLoadByNumber), TAG2, $"port={port}", ex);
					return null;
				}
			}
		}

		public bool ClientsInsert(ClientItem clientItem)
		{
			lock (Locker)
			{
				try
				{
					string sqlStr = GetItemInsertString(clientItem, TABLE_CLIENTS);
					bool ok = DoSqlNoQuery(sqlStr);
					if (!ok) return false;
					clientItem.ClientId = (Int64)DoSqlScalar($"SELECT last_insert_rowid() FROM {TABLE_CLIENTS}");
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsInsert), TAG2, "error", ex);
					return false;
				}
			}
		}

		public bool ClientsUpdateNameAndPin(int number, int? pin, string name)
		{
			lock (Locker)
			{
				try
				{
					string ts = DateTimeStrToTimestamp(DateTime.UtcNow);
					string pinStr = pin.HasValue ? pin.Value.ToString() : "NULL";
					string updateString =
						$"UPDATE {TABLE_CLIENTS} SET Name='{EscapeSql(name)}', Pin={pinStr}, LastChangedUtc='{ts}' WHERE Number={number}";
					DoSqlNoQuery(updateString);
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsUpdateNameAndPin), TAG2, $"number={number} name={name}", ex);
					return false;
				}
			}
		}

		public bool ClientsUpdatePort(int number, int port)
		{
			lock (Locker)
			{
				try
				{
					string ts = DateTimeStrToTimestamp(DateTime.UtcNow);
					string updateString = $"UPDATE {TABLE_CLIENTS} SET Port={port}, LastChangedUtc='{ts}' WHERE Number={number}";
					DoSqlNoQuery(updateString);
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsUpdatePort), TAG2, $"number={number} port={port}", ex);
					return false;
				}
			}
		}

		public bool ClientsUpdateLastChanged(int number)
		{
			lock (Locker)
			{
				try
				{
					string ts = DateTimeStrToTimestamp(DateTime.UtcNow);
					string updateString = $"UPDATE {TABLE_CLIENTS} SET LastChangedUtc='{ts}' WHERE Number={number}";
					DoSqlNoQuery(updateString);
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ClientsUpdateLastChanged), TAG2, $"number={number}", ex);
					return false;
				}
			}
		}
	}
}
