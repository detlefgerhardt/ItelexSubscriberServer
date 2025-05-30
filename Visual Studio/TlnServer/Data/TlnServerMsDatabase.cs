using ItelexCommon;
using ItelexTlnServer.Data;
using Microsoft.Data.Sqlite;
using MimeKit.Cryptography;
using Org.BouncyCastle.Tls.Crypto;
using ServerCommon;
using ServerCommon.Logging;
using ServerCommon.Utility;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using TlnServer;
using TlnServer.Data;

namespace CentralexServer.Data
{
	internal class TlnServerMsDatabase: MsDatabase
    {
        private static string TAG = nameof(TlnServerMsDatabase);
		private static string TAG2 = "";

        private const string TABLE_TEILNEHMER = "teilnehmer";
		private const string TABLE_QUEUE = "queue";
		//private const string TABLE_SERVERS = "servers";

		/// <summary>
		/// singleton pattern
		/// </summary>
		private static TlnServerMsDatabase? instance;
        public static TlnServerMsDatabase Instance => instance ??= new TlnServerMsDatabase();

        private TlnServerMsDatabase() : base(Constants.DATABASE_NAME)
        {
            _logger = GlobalData.Logger;
			if (ConnectDatabase())
			{
				_logger.Info(TAG, nameof(TlnServerMsDatabase), TAG2, $"connected to database {Constants.DATABASE_NAME}");
				//UpdateTables_20250518();
			}
			else
			{
				_logger.Error(TAG, nameof(TlnServerMsDatabase), TAG2, $"error connecting to database {Constants.DATABASE_NAME}");
			}
		}

		private void UpdateTables_20250518()
		{
			const string TABLE_TEILN_TEMP = "teilnehmer_temp";
			const string TABLE_TEILN_OLD2 = "teilnehmer_old2";
			const string TABLE_TEILN_OLD3 = "teilnehmer_old3";
			const string TABLE_TEILN_OLD4 = "teilnehmer_old4";
			const string TABLE_TEILN_OLD5 = "teilnehmer_old5";

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD2}");
			_logger.Info(TAG, nameof(UpdateTables_20250518), "SQL-Update", $"{TABLE_TEILN_OLD2} dropped");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD3}");
			_logger.Info(TAG, nameof(UpdateTables_20250518), "SQL-Update", $"{TABLE_TEILN_OLD3} dropped");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD4}");
			_logger.Info(TAG, nameof(UpdateTables_20250518), "SQL-Update", $"{TABLE_TEILN_OLD4} dropped");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD5}");
			_logger.Info(TAG, nameof(UpdateTables_20250518), "SQL-Update", $"{TABLE_TEILN_OLD5} dropped");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			_logger.Info(TAG, nameof(UpdateTables_20250518), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");
		}

		private void UpdateTables_20250515()
		{
			const string TABLE_TEILN_TEMP = "teilnehmer_temp";
			const string TABLE_TEILN_OLD2 = "teilnehmer_old2";
			const string TABLE_TEILN_OLD3 = "teilnehmer_old3";
			const string TABLE_TEILN_OLD5 = "teilnehmer_old5";

			if (TableExists(TABLE_TEILN_OLD5))
			{
				_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILN_OLD5} exists");
				return;
			}

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD2}");
			_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILN_OLD2} dropped");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD3}");
			_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILN_OLD3} dropped");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");

			bool comitted = false;
			try
			{
				if (!DoSqlNoQuery("BEGIN TRANSACTION")) return;
				_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"transaction started");

				// Das " bei PRIMARY KEY beachten! ' funktioniert hier nicht.
				if (!CreateNewTable($"{TABLE_TEILN_TEMP}",
					"'Uid' INTEGER NOT NULL," +
					"'Number' int unsigned NOT NULL UNIQUE," +
					"'Name' varchar(40)," +
					"'Type' tinyint unsigned NOT NULL DEFAULT 0," +
					"'Hostname' varchar(40)," +
					"'IpAddress' varchar(15)," +
					"'Port' smallint unsigned," +
					"'Extension' varchar(2)," +
					"'Pin' smallint unsigned," +
					"'Answerback' varchar(30) DEFAULT NULL," +
					"'Disabled' tinyint NOT NULL DEFAULT 1," +
					"'Remove' tinyint NOT NULL DEFAULT 0," +
					"'LeadingEntry' tinyint NOT NULL DEFAULT 0," +
					"'UpdatedBy' tinyint NOT NULL DEFAULT 0," +
					"'ChangedBy' tinyint NOT NULL DEFAULT 0," +
					"'UserId' smallint NOT NULL DEFAULT 0," +
					"'DeviceId' smallint NOT NULL DEFAULT 0," +
					"'MainNumber' tinyint NOT NULL DEFAULT 0," +
					"'TimestampUtc' int unsigned NOT NULL DEFAULT 0," +
					"'UpdateTimeUtc' varchar(19) NOT NULL," +
					"'CreateTimeUtc' varchar(19) NOT NULL DEFAULT '1970-01-01 00:00:00'," +
					"'Changed' tinyint NOT NULL DEFAULT 1," +
					"PRIMARY KEY(\"Uid\" AUTOINCREMENT)")) return;

				/*
				List<TeilnehmerItemOld> tlns2 = TeilnehmerLoadAllAdminOld(TABLE_TEILNEHMER);
				foreach (TeilnehmerItemOld tln in tlns2)
				{
					tln.UpdatedBy = 0;
					tln.UserId = 0;
					tln.DeviceId = 0;
					tln.MainNumber = false;
					tln.CreateTimeUtc = CommonHelper.TimestampToDateTimeUtc1900(0);
					if (!TeilnehmerUpdateByUid(tln))
					{
						_logger.Error(TAG, nameof(UpdateTables), "SQL-Update", "error updating Teilnehmer");
						return;
					}
				}
				*/
				string[] skipFields = new string[] { "UserId", "DeviceId", "MainNumber", "CreateTimeUtc" };
				if (!CopyTable(TABLE_TEILNEHMER, TABLE_TEILN_TEMP, skipFields)) return;
				_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILNEHMER} copyied to {TABLE_TEILN_TEMP}");

				if (!RenameTable(TABLE_TEILNEHMER, TABLE_TEILN_OLD5)) return;
				_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILNEHMER} renamed to {TABLE_TEILN_OLD5}");

				if (!RenameTable(TABLE_TEILN_TEMP, TABLE_TEILNEHMER)) return;
				_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", $"{TABLE_TEILN_TEMP} renamed to {TABLE_TEILNEHMER}");

				if (!DoSqlNoQuery("COMMIT")) return;
				_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", "committed");

				comitted = true;
			}
			finally
			{
				if (!comitted)
				{
					if (!DoSqlNoQuery("ROLLBACK"))
					{
						_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", "rollback error");
					}
					else
					{
						_logger.Info(TAG, nameof(UpdateTables_20250515), "SQL-Update", "rollback");
					}
				}
			}
		}

		private void UpdateTables_20250514()
		{

			const string TABLE_TEILN_TEMP = "teilnehmer_temp";
			const string TABLE_TEILN_OLD4 = "teilnehmer_old4";

			if (TableExists(TABLE_TEILN_OLD4))
			{
				_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", $"{TABLE_TEILN_OLD4} exists");
				return;
			}

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");

			bool comitted = false;
			try
			{
				if (!DoSqlNoQuery("BEGIN TRANSACTION")) return;
				_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", $"transaction started");

				// Das " bei PRIMARY KEY beachten! ' funktioniert hier nicht.
				if (!CreateNewTable($"{TABLE_TEILN_TEMP}",
					"'Uid' INTEGER NOT NULL," +
					"'Number' int unsigned NOT NULL UNIQUE," +
					"'Name' varchar(40)," +
					"'Type' tinyint unsigned NOT NULL DEFAULT 0," +
					"'Hostname' varchar(40)," +
					"'IpAddress' varchar(15)," +
					"'Port' smallint unsigned," +
					"'Extension' varchar(2)," +
					"'Pin' smallint unsigned," +
					"'Answerback' varchar(30) DEFAULT NULL," +
					"'Disabled' tinyint NOT NULL DEFAULT 1," +
					"'Remove' tinyint NOT NULL DEFAULT 0," +
					"'LeadingEntry' tinyint NOT NULL DEFAULT 0," +
					"'ChangedBy' tinyint NOT NULL DEFAULT 0," +
					"'UserId' smallint DEFAULT NULL," +
					"'DeviceId' smallint DEFAULT NULL," +
					"'MainNumber' tinyint DEFAULT NULL," +
					"'TimestampUtc' int unsigned NOT NULL DEFAULT 0," +
					"'UpdateTimeUtc' varchar(19)," +
					"'CreateTimeUtc' varchar(19)," +
					"'Changed' tinyint NOT NULL DEFAULT 1," +
					"PRIMARY KEY(\"Uid\" AUTOINCREMENT)")) return;

				if (!CopyTable(TABLE_TEILNEHMER, TABLE_TEILN_TEMP, null)) return;
				_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", $"{TABLE_TEILNEHMER} copyied to {TABLE_TEILN_TEMP}");

				if (!RenameTable(TABLE_TEILNEHMER, TABLE_TEILN_OLD4)) return;
				_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", $"{TABLE_TEILNEHMER} renamed to {TABLE_TEILN_OLD4}");

				if (!RenameTable(TABLE_TEILN_TEMP, TABLE_TEILNEHMER)) return;
				_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", $"{TABLE_TEILN_TEMP} renamed to {TABLE_TEILNEHMER}");

				List<TeilnehmerItem> tlns2 = TeilnehmerLoadAllAdmin();
				foreach (TeilnehmerItem tln in tlns2)
				{
					tln.UpdateTimeUtc = tln.TimestampUtc;
					if (!TeilnehmerUpdateByUid(tln))
					{
						_logger.Error(TAG, nameof(UpdateTables_20250514), "SQL-Update", "error updating Teilnehmer");
						return;
					}
				}

				if (!DoSqlNoQuery("COMMIT")) return;
				_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", "committed");

				comitted = true;
			}
			finally
			{
				if (!comitted)
				{
					if (!DoSqlNoQuery("ROLLBACK"))
					{
						_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", "rollback error");
					}
					else
					{
						_logger.Info(TAG, nameof(UpdateTables_20250514), "SQL-Update", "rollback");
					}
				}
			}
		}

		private void UpdateTables_20250513()
		{
			const string TABLE_TEILN_TEMP = "teilnehmer_temp";
			const string TABLE_TEILN_OLD3 = "teilnehmer_old3";

			if (TableExists(TABLE_TEILN_OLD3))
			{
				_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", $"{TABLE_TEILN_OLD3} exists");
				return;
			}

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");

			if (!DoSqlNoQuery("BEGIN TRANSACTION")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", $"transaction started");

			// Das " bei PRIMARY KEY beachten! ' funktioniert hier nicht.
			if (!CreateNewTable($"{TABLE_TEILN_TEMP}",
				"'Uid' INTEGER NOT NULL," +
				"'Number' int unsigned NOT NULL UNIQUE," +
				"'Name' varchar(40)," +
				"'Type' tinyint unsigned NOT NULL DEFAULT 0," +
				"'Hostname' varchar(40)," +
				"'IpAddress' varchar(15)," +
				"'Port' smallint unsigned," +
				"'Extension' varchar(2)," +
				"'Pin' smallint unsigned," +
				"'Answerback' varchar(30) DEFAULT NULL," +
				"'Disabled' tinyint NOT NULL DEFAULT 1," +
				"'Remove' tinyint NOT NULL DEFAULT 0," +
				"'LeadingEntry' tinyint NOT NULL DEFAULT 0," +
				"'ChangedBy' tinyint NOT NULL DEFAULT 0," +
				"'UserId' smallint DEFAULT NULL," +
				"'DeviceId' smallint DEFAULT NULL," +
				"'MainNumber' tinyint DEFAULT NULL," +
				"'TimestampUtc' int unsigned NOT NULL DEFAULT 0," +
				"'Changed' tinyint NOT NULL DEFAULT 1," +
				"PRIMARY KEY(\"Uid\" AUTOINCREMENT)")) return;

			if (!CopyTable(TABLE_TEILNEHMER, TABLE_TEILN_TEMP, null)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", $"{TABLE_TEILNEHMER} copyied to {TABLE_TEILN_TEMP}");

			if (!RenameTable(TABLE_TEILNEHMER, TABLE_TEILN_OLD3)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", $"{TABLE_TEILNEHMER} renamed to {TABLE_TEILN_OLD3}");

			if (!RenameTable(TABLE_TEILN_TEMP, TABLE_TEILNEHMER)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", $"{TABLE_TEILN_TEMP} renamed to {TABLE_TEILNEHMER}");

			if (!DoSqlNoQuery("COMMIT")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250513), "SQL-Update", "committed");

		}


		private void UpdateTables_20250512()
		{
			const string TABLE_TEILN_TEMP = "teilnehmer_temp";
			const string TABLE_TEILN_OLD2 = "teilnehmer_old2";

			if (TableExists(TABLE_TEILN_OLD2))
			{
				_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "teilnehmer_old2 exists");
				return;
			}

			if (!DoSqlNoQuery("BEGIN TRANSACTION")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"transaction started");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");

			if (!DoSqlNoQuery($"UPDATE {TABLE_TEILNEHMER} SET Type=0 WHERE Type IS NULL")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "set Type=0 if NULL");
			if (!DoSqlNoQuery($"UPDATE {TABLE_TEILNEHMER} SET Disabled=0 WHERE Disabled IS NULL")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "set Disabled=0 if NULL");
			if (!DoSqlNoQuery($"UPDATE {TABLE_TEILNEHMER} SET Remove=0 WHERE Remove IS NULL")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "set Remove=0 if NULL");
			if (!DoSqlNoQuery($"UPDATE {TABLE_TEILNEHMER} SET LeadingEntry=0 WHERE LeadingEntry IS NULL")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "set LeadingEntry=0 if NULL");
			if (!DoSqlNoQuery($"UPDATE {TABLE_TEILNEHMER} SET ChangedBy=0 WHERE ChangedBy IS NULL")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "set ChangedBy=0 if NULL");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");

			if (!CreateNewTable($"{TABLE_TEILN_TEMP}",
				"'Uid' int NOT NULL UNIQUE," +
				"'Number' int unsigned NOT NULL UNIQUE," +
				"'Name' varchar(40)," +
				"'Type' tinyint unsigned NOT NULL DEFAULT 0," +
				"'Hostname' varchar(40)," +
				"'IpAddress' varchar(15)," +
				"'Port' smallint unsigned," +
				"'Extension' varchar(2)," +
				"'Pin' smallint unsigned," +
				"'Answerback' varchar(30) DEFAULT NULL," +
				"'Disabled' tinyint NOT NULL DEFAULT 1," +
				"'Remove' tinyint NOT NULL DEFAULT 0," +
				"'LeadingEntry' tinyint NOT NULL DEFAULT 0," +
				"'ChangedBy' tinyint NOT NULL DEFAULT 0," +
				"'UserId' smallint DEFAULT NULL," +
				"'DeviceId' smallint DEFAULT NULL," +
				"'MainNumber' tinyint DEFAULT NULL," +
				"'TimestampUtc' int unsigned NOT NULL DEFAULT 0," +
				"'Changed' tinyint NOT NULL DEFAULT 1," +
				"PRIMARY KEY('Uid')")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"{TABLE_TEILN_TEMP} created");

			if (!CopyTable(TABLE_TEILNEHMER, TABLE_TEILN_TEMP, null)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"{TABLE_TEILNEHMER} copyied to {TABLE_TEILN_TEMP}");

			if (!RenameTable(TABLE_TEILNEHMER, TABLE_TEILN_OLD2)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"{TABLE_TEILNEHMER} renamed to {TABLE_TEILN_OLD2}");

			if (!RenameTable(TABLE_TEILN_TEMP, TABLE_TEILNEHMER)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", $"{TABLE_TEILN_TEMP} renamed to {TABLE_TEILNEHMER}");

			DoSqlNoQuery($"DROP TABLE servers");

			if (!DoSqlNoQuery("COMMIT")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250512), "SQL-Update", "committed");
		}

		/// <summary>
		/// Änderung der Datenbank durchgeführt am 12.05.2025
		/// - Feldnamen umbenannt (Camelcase)
		/// - neuer Felder für Remove, Benutzer- und Anlagenverwaltung und zusätzliche Infos
		/// </summary>
		private void UpdateTables_20250511()
		{
			const string TABLE_TEILN_TEMP = "teilnehmer_temp";
			const string TABLE_TEILN_OLD = "teilnehmer_old";

			if (TableExists(TABLE_TEILN_OLD))
			{
				_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", "teilnehmer_old exists");
				return;
			}

			if (!DoSqlNoQuery("BEGIN TRANSACTION")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", $"transaction started");

			DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_TEMP}");
			//DoSqlNoQuery($"DROP TABLE {TABLE_TEILN_OLD}");
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", $"{TABLE_TEILN_TEMP} dropped");

			if (!CreateNewTable($"{TABLE_TEILN_TEMP}",
				"'Uid' int NOT NULL UNIQUE," +
				"'Number' int unsigned NOT NULL UNIQUE," +
				"'Name' varchar(40)," +
				"'Type' tinyint unsigned DEFAULT 0," +
				"'Hostname' varchar(40)," +
				"'IpAddress' varchar(15)," +
				"'Port' smallint unsigned," +
				"'Extension' varchar(2)," +
				"'Pin' smallint unsigned," +
				"'Answerback' varchar(30) DEFAULT NULL," +
				"'Disabled' tinyint DEFAULT 1," +
				"'Remove' tinyint DEFAULT NULL," +
				"'LeadingEntry' tinyint DEFAULT NULL," +
				"'ChangedBy' tinyint NOT NULL DEFAULT 0," +
				"'UserId' smallint DEFAULT NULL," +
				"'DeviceId' smallint DEFAULT NULL," +
				"'MainNumber' tinyint DEFAULT NULL," +
				"'Timestamp' int unsigned NOT NULL DEFAULT 0," +
				"'Changed' tinyint NOT NULL DEFAULT 1," +
				"PRIMARY KEY('Uid')")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", $"{TABLE_TEILN_TEMP} created");

			if (!CopyTable(TABLE_TEILNEHMER, TABLE_TEILN_TEMP, null)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", $"{TABLE_TEILNEHMER} copyied to {TABLE_TEILN_TEMP}");

			if (!RenameTableColumn(TABLE_TEILN_TEMP, "Timestamp", "TimestampUtc")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", "'Timestamp' renamed to 'TimestampUtc'");

			if (!RenameTable(TABLE_TEILNEHMER, TABLE_TEILN_OLD)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", $"{TABLE_TEILNEHMER} renamed to {TABLE_TEILN_OLD}");

			if (!RenameTable(TABLE_TEILN_TEMP, TABLE_TEILNEHMER)) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", $"{TABLE_TEILN_TEMP} renamed to {TABLE_TEILNEHMER}");

			//DoSqlNoQuery($"DROP TABLE servers");

			if (!DoSqlNoQuery("COMMIT")) return;
			_logger.Info(TAG, nameof(UpdateTables_20250511), "SQL-Update", "committed");

			return;


			/*
			AddTableColumn(TABLE_TEILNEHMER, "answerback", "TEXT DEFAULT null");
			AddTableColumn(TABLE_TEILNEHMER, "answerback_kind", "tinyint");
			//AddTableColumn(TABLE_TEILNEHMER, "delete", "tinyint NOT NULL DEFAULT 0");
			AddTableColumn(TABLE_TEILNEHMER, "delete", "tinyint");
			AddTableColumn(TABLE_TEILNEHMER, "changedby", "tinyint");

			RenameTableColumn(TABLE_TEILNEHMER, "changedby", "ChangedBy");
			DropTableColumn(TABLE_TEILNEHMER, "ChangedBy");
			*/

			//ModifyTableColumn(TABLE_TEILNEHMER, "delete", "TEXT");
		}

		public bool Backup()
		{
			DateTime dt = DateTime.Now;
			string name = Path.GetFileNameWithoutExtension(Constants.DATABASE_NAME) + 
					$"_{dt:yyyyMMdd}" + Path.GetExtension(Constants.DATABASE_NAME);

			return BackupDatabase(Constants.BACKUP_PATH, name);
		}

		private List<TeilnehmerItem> _teilnehmerList = null;
		private List<TeilnehmerItem> _teilnehmerListAdmin = null;

		public List<TeilnehmerItem> TeilnehmerLoadAllAdmin()
        {
			if (_teilnehmerListAdmin != null) return _teilnehmerListAdmin;

            List<TeilnehmerItem> tlnItems = new List<TeilnehmerItem>();
            lock (Locker)
            {
                try
                {
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} ORDER BY name";
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
						while (sqlReader.Read())
						{
							TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
							tlnItems.Add(tlnItem);
						}
					}
                }
                catch (Exception ex)
                {
                    _logger.Error(TAG, nameof(TeilnehmerLoadAll), TAG2, "error", ex);
                    return null;
                }
            }

			_teilnehmerListAdmin = tlnItems;
			return tlnItems;
		}

		public List<TeilnehmerItem> TeilnehmerLoadAllChanged()
		{
			List<TeilnehmerItem> tlnItems = new List<TeilnehmerItem>();
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_TEILNEHMER} WHERE changed=1 ORDER BY name";
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
						while (sqlReader.Read())
						{
							TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
							tlnItems.Add(tlnItem);
						}
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadAllChanged), TAG2, "error", ex);
					return null;
				}
			}

			return tlnItems;
		}

		public List<TeilnehmerItem> TeilnehmerLoadAll()
		{
			if (_teilnehmerList != null) return _teilnehmerList;

			List<TeilnehmerItem> tlnItems = new List<TeilnehmerItem>();
			lock (Locker)
			{
				try
				{
					string sqlStr = 
							$"SELECT * FROM {TABLE_TEILNEHMER} " +
							$"WHERE Disabled<>1 AND (Remove=0 OR Remove IS NULL) AND Type<>0 ORDER BY name";
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
						while (sqlReader.Read())
						{
							TeilnehmerItem tlnItem = ItemGetQuery<TeilnehmerItem>(sqlReader);
							tlnItems.Add(tlnItem);
						}
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadAll), TAG2, "error", ex);
					return null;
				}
			}

			tlnItems = (from t in tlnItems where !t.Remove select t).ToList();

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
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
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
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadByUid), TAG2, $"uid={uid}", ex);
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
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
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
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerLoadByNumber), TAG2, $"number={number}", ex);
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
					tlnItem.Uid = (Int64)DoSqlScalar($"SELECT last_insert_rowid() FROM {TABLE_TEILNEHMER}");
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerInsert), TAG2, "error", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerUpdateByUid(TeilnehmerItem tlnItem)
		{
			lock (Locker)
			{
				try
				{
					string updateString = GetItemUpdateString(tlnItem, TABLE_TEILNEHMER, $"uid={tlnItem.Uid}");
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerUpdateByUid), TAG2, $"uid={tlnItem.Uid} number={tlnItem.Number}", ex);
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
					string updateString = GetItemUpdateString(tlnItem, TABLE_TEILNEHMER, $"number={tlnItem.Number}");
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerUpdateByNumber), TAG2, $"number={tlnItem.Number}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerSetPin(ulong uid, int? pin)
		{
			lock (Locker)
			{
				try
				{
					int timestamp = CommonHelper.DateTimeToTimestampUtc(DateTime.UtcNow);
					string pinValue = pin.HasValue ? pin.ToString() : "NULL";
					string updateString =
							$"UPDATE {TABLE_TEILNEHMER} SET pin={pinValue}, changed=1, timestamp={timestamp} WHERE uid={uid}";
					DoSqlNoQuery(updateString);
					TeilnehmerCacheClear();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(TeilnehmerUpdateByNumber), TAG2, $"uid={uid} pin={pin}", ex);
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
					_logger.Error(TAG, nameof(TeilnehmerSetType), TAG2, $"uid={uid}", ex);
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
					_logger.Error(TAG, nameof(TeilnehmerSetChanged), TAG2, $"uid={uid} changed={changed}", ex);
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
					_logger.Error(TAG, nameof(TeilnehmerDelete), TAG2, $"uid={uid}", ex);
					return false;
				}
			}
		}

		public bool TeilnehmerBackupCsv()
		{
			string tag2 = "backup";
			List<TeilnehmerItem> tlns = TeilnehmerLoadAllAdmin();
			if (tlns == null)
			{
				_logger.Error(TAG, nameof(TeilnehmerBackupCsv), tag2, "error loading teilnehmer from database");
				return false;
			}

			List<string> tlnLines = new List<string>();
			foreach(TeilnehmerItem tln in tlns)
			{
				string line = tln.GetCsvLine();
				tlnLines.Add(line);
			}

			string dbName = Path.GetFileNameWithoutExtension(Constants.DATABASE_NAME);
			string backupFullname = Path.Combine(Constants.BACKUP_PATH, $"{dbName}_{DateTime.Now:yyyyMMdd}.csv");

			try
			{
				Directory.CreateDirectory(Constants.BACKUP_PATH);
				File.WriteAllLines(backupFullname, tlnLines);
				return true;
			}
			catch(Exception)
			{
				_logger.Error(TAG, nameof(TeilnehmerBackupCsv), tag2, $"error writing {backupFullname}");
				return false;
			}
		}

		public List<QueueItem> QueueLoadAll()
		{
			List<QueueItem> srvItems = new List<QueueItem>();
			lock (Locker)
			{
				try
				{
					string sqlStr = $"SELECT * FROM {TABLE_QUEUE}";
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
						while (sqlReader.Read())
						{
							QueueItem queItem = ItemGetQuery<QueueItem>(sqlReader);
							srvItems.Add(queItem);
						}
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueLoadAll), TAG2, "error", ex);
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
					SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					using (SqliteDataReader sqlReader = sqlCmd.ExecuteReader())
					{
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
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(QueueLoadByServerAndMsg), TAG2, $"server={server}, message={message}", ex);
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
					_logger.Error(TAG, nameof(QueueInsert), TAG2, "error", ex);
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
					_logger.Error(TAG, nameof(QueueUpdateByUid), TAG2, $"number={queItem.uid}", ex);
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
					_logger.Error(TAG, nameof(QueueDeleteByUid), TAG2, $"uid={uid}", ex);
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
					_logger.Error(TAG, nameof(QueueDeleteByServerAndMsg), TAG2, $"server={server} message={message}", ex);
					return false;
				}
			}
		}

		public bool UpdateAnswerback()
		{
			string tag2 = "update answerback";
			const string KG_FILENAME = "kg.csv";

			try
			{
				if (!File.Exists(KG_FILENAME)) return true;

				_logger.Info(TAG, nameof(UpdateAnswerback), tag2, "start");

				// read file
				List<AnswerbackItem> abs = new List<AnswerbackItem>();
				string[] lines = File.ReadAllLines(KG_FILENAME);

				_logger.Info(TAG, nameof(UpdateAnswerback), tag2, "start2");

				foreach (string l in lines)
				{
					string line = l.Trim();
					string errStr = $"invalid answerback line {line} in file {KG_FILENAME}";

					if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
					string[] parts = line.Split(new char[] { ',' });
					if (parts.Length == 3)
					{
						AnswerbackItem abItem = new AnswerbackItem();
						if (int.TryParse(parts[0], out int number))
						{
							abItem.Number = number;
							if (int.TryParse(parts[2], out int status))
							{
								parts[1] = parts[1].Trim('\"');
								abItem.Answerback = parts[1].Trim();
								abItem.Status = (AnswerbackItem.AnswerbackStatus)status;
								if (abItem.Status == AnswerbackItem.AnswerbackStatus.Forward) continue;
								abs.Add(abItem);
								continue;
							}
						}
					}
					_logger.Warn(TAG, nameof(UpdateAnswerback), tag2, errStr);
				}

				List<TeilnehmerItem> tlns = TeilnehmerLoadAllAdmin();

				foreach (AnswerbackItem ab in abs)
				{
					TeilnehmerItem tln = (from t in tlns where t.Number == ab.Number select t).FirstOrDefault();
					if (tln == null) continue;

					if (tln.Answerback == ab.Answerback) continue;

					tln.Answerback = ab.Answerback;
					DateTime utc = DateTime.UtcNow;
					tln.TimestampUtc = utc;
					tln.UpdateTimeUtc = utc;
					tln.ChangedBy = GlobalData.Config.ServerId;
					tln.Changed = true;

					if (!TeilnehmerUpdateByUid(tln))
					{
						_logger.Error(TAG, nameof(UpdateAnswerback), tag2,
								$"error updating teilnehmer {tln.Number} in database");
						return false;
					}

					_logger.Info(TAG, nameof(UpdateAnswerback), tag2, $"Update {tln.Number}, ab='{tln.Answerback}'");
				}
				return true;
			}
			catch(Exception ex)
			{
				_logger.Error(TAG, nameof(UpdateAnswerback), tag2, "error", ex);
				return false;
			}
		}
	}
}
