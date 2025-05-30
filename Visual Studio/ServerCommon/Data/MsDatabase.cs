using Microsoft.Data.Sqlite;
using ServerCommon.Logging;
using System.Reflection;
using System.Text;

namespace ItelexCommon
{
	public class MsDatabase : IDisposable
	{
		private const string TAG = nameof(MsDatabase);

		protected static Logger _logger;

		protected object Locker { get; set; } = new object();

		protected string _databaseName;
		protected string _sqlConnStr;
		protected SqliteConnection _sqlConn;
		protected bool databaseOpen = false;

		private bool disposed;

		public MsDatabase(string databaseName)
		{
			_databaseName = databaseName;
			_sqlConnStr = $"Data Source={databaseName};";

			//CreateDatabase();
			//ConnectDatabase();
			//DisconnectDatabase();
		}

		~MsDatabase()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			if (!this.disposed)
			{
				this.Dispose(true);
				GC.SuppressFinalize(this);
				this.disposed = true;
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				// clean up managed resources
				_sqlConn.Close();
				_sqlConn.Dispose();
			}

			// clean up unmanaged resources
		}

		public bool ConnectDatabase()
		{
			try
			{
				_sqlConn = new SqliteConnection(_sqlConnStr);
				_sqlConn.Open();
				databaseOpen = true;
				return true;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(ConnectDatabase), "", ex);
				//string messageBoxText = $"Konnte Datenbank {_databaseName} nicht öffnen";
				//string caption = "Datenbankfehler";
				//MessageBoxButtons button = MessageBoxButtons.OK;
				//MessageBoxIcon icon = MessageBoxIcon.Warning;
				//MessageBox.Show(messageBoxText, caption, button, icon);
				return false;
			}
		}

		public void DisconnectDatabase()
		{
			try
			{
				_sqlConn.Close();
				databaseOpen = false;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(DisconnectDatabase), "Error closing database", ex);
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0020:Use pattern matching", Justification = "<Pending>")]
		private static string GetItemCreateString<T>(string table)
		{
			StringBuilder sqlCreate = new StringBuilder();

			Type itemType = typeof(T);
			PropertyInfo[] propertyInfos = itemType.GetProperties();

			bool first = true;
			foreach (PropertyInfo propertyInfo in propertyInfos)
			{
				object[] attributes = propertyInfo.GetCustomAttributes(true);
				if (attributes.Length == 0) continue;
				object attrib = attributes[0];

				string sqlType = null;
				if (attrib is SqlIdAttribute)
				{
					sqlType = $"INTEGER PRIMARY KEY AUTOINCREMENT";
				}
				if (attrib is SqlStringAttribute)
				{
					SqlStringAttribute attr = ((SqlStringAttribute)attributes[0]);
					sqlType = $"varchar({attr.Length})";
				}
				else if (attrib is SqlUInt64StrAttribute)
				{
					// UInt64.MaxValue = 18446744073709551615 
					sqlType = "varchar(20)";
				}
				else if (attrib is SqlIntAttribute)
				{
					sqlType = "int";
				}
				else if (attrib is SqlSmallIntAttribute)
				{
					sqlType = "smallint";
				}
				else if (attrib is SqlTinyIntAttribute)
				{
					sqlType = "tinyint";
				}
				else if (attrib is SqlBoolAttribute)
				{
					sqlType = "bool";
				}
				else if (attrib is SqlMemoAttribute)
				{
					sqlType = "text";
				}
				else if (attrib is SqlDateAttribute)
				{
					sqlType = "int";
				}
				else if (attrib is SqlDateStrAttribute)
				{
					sqlType = "text";
				}
				if (first)
				{
					first = false;
				}
				else
				{
					sqlCreate.Append(",");
				}
				sqlCreate.Append($"{propertyInfo.Name} {sqlType}");
			}

			return $"CREATE TABLE {table} ({sqlCreate})";
		}

		protected string GetItemInsertString(object item, string table)
		{
			if (!databaseOpen) return null;
			if (item == null) return null;

			try
			{
				Type itemType = item.GetType();
				PropertyInfo[] propertyInfos = itemType.GetProperties();

				StringBuilder sqlIntos = new StringBuilder();
				StringBuilder sqlValues = new StringBuilder();

				bool first = true;
				foreach (PropertyInfo propertyInfo in propertyInfos)
				{
					object[] attributes = propertyInfo.GetCustomAttributes(true);
					if (attributes.Length == 0) continue;
					if (attributes[0] is SqlIdAttribute) continue; // skip Id, wird automatisch von sqlite erzeugt

					string key = propertyInfo.Name;
					object value = propertyInfo.GetValue(item, new object[] { });
					string valueString = SqlPropertyValueToString(value, attributes[0]);

					if (first)
					{
						first = false;
					}
					else
					{
						sqlIntos.Append(",");
						sqlValues.Append(",");
					}
					sqlIntos.Append(key);
					sqlValues.Append(valueString);
				}
				return $"INSERT INTO {table} ({sqlIntos}) VALUES ({sqlValues})";
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(GetItemInsertString), $"{nameof(GetItemInsertString)}", ex);
				return null;
			}
		}

		protected string GetItemUpdateString(object item, string table, string where)
		{
			if (!databaseOpen) return null;
			if (item == null) return null;

			try
			{
				Type itemType = item.GetType();
				PropertyInfo[] propertyInfos = itemType.GetProperties();
				StringBuilder sqlUpdate = new StringBuilder();

				bool first = true;
				foreach (PropertyInfo propertyInfo in propertyInfos)
				{
					object[] attributes = propertyInfo.GetCustomAttributes(true);
					if (attributes.Length == 0) continue;
					if (attributes[0] is SqlIdAttribute) continue; // skip Id, wird automatisch von sqlite erzeugt

					string key = propertyInfo.Name;
					object value = propertyInfo.GetValue(item, new object[] { });

					string valueString = SqlPropertyValueToString(value, attributes[0]);

					if (first)
					{
						first = false;
					}
					else
					{
						sqlUpdate.Append(",");
					}
					sqlUpdate.Append($"{key}={valueString}");
				}

				return $"UPDATE {table} SET {sqlUpdate} WHERE {where}";
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(GetItemUpdateString), $"{nameof(GetItemUpdateString)}", ex);
				return null;
			}
		}

		private string SqlPropertyValueToString(object value, object attrib)
		{
			string valueString = "NULL";
			if (value == null) return valueString;

			if (attrib is SqlStringAttribute)
			{
				// strings abschneiden entsprechend der Attribute-Definition ???
				string str = (string)value;
				//int len = ((SqlStringAttribute)attributes[0]).Length;
				//if (str.Length > len)
				//	str = str.Substring(0, len);
				valueString = "'" + EscapeSql(str) + "'";
			}
			else if (attrib is SqlMemoAttribute)
			{
				string str = (string)value;
				valueString = "'" + EscapeSql(str) + "'";
			}
			else if (attrib is SqlUInt64StrAttribute)
			{
				valueString = value.ToString();
			}
			else if (attrib is SqlIntAttribute)
			{
				valueString = value.ToString();
			}
			else if (attrib is SqlTinyIntAttribute)
			{
				valueString = value.ToString();
			}
			else if (attrib is SqlSmallIntAttribute)
			{
				valueString = value.ToString();
			}
			else if (attrib is SqlBoolAttribute)
			{
				valueString = (bool)value ? "1" : "0";
			}
			else if (attrib is SqlDateAttribute)
			{
				valueString = DateTimeToTimestamp((DateTime)value).ToString();
			}
			else if (attrib is SqlDateStrAttribute)
			{
				valueString = "'" + DateTimeStrToTimestamp((DateTime)value).ToString() + "'";
			}
			return valueString;
		}

		protected static T ItemGetQuery<T>(SqliteDataReader sqlReader) where T : new()
		{
			//Stopwatch watch = new Stopwatch();
			//watch.Start();

			T item = new T();

			PropertyInfo[] propertyInfos;
			propertyInfos = typeof(T).GetProperties();

			foreach (PropertyInfo propertyInfo in propertyInfos)
			{
				try
				{
					object[] attributes = propertyInfo.GetCustomAttributes(true);
					if (attributes.Length == 0) continue;
					object attrib = attributes[0];
					var prop = item.GetType().GetProperty(propertyInfo.Name);

					if (attrib is SqlIdAttribute)
					{
						int? value = GetQueryInt32(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlStringAttribute)
					{
						SqlStringAttribute attr = ((SqlStringAttribute)attributes[0]);
						string value = GetQueryString(sqlReader, propertyInfo.Name);
						if (attr.Length > 0 && value?.Length > attr.Length)
						{
							value = value.Substring(0, attr.Length);
						}
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlUInt64StrAttribute)
					{
						string valueStr = GetQueryString(sqlReader, propertyInfo.Name);
						if (UInt64.TryParse(valueStr, out UInt64 value))
						{
							prop.SetValue(item, value, null);
						}
						else
						{
							prop.SetValue(item, null, null);
						}
					}
					else if (attrib is SqlIntAttribute)
					{
						int? value = GetQueryInt32(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlSmallIntAttribute)
					{
						int? value = GetQueryInt16(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlTinyIntAttribute)
					{
						//int? value = GetQueryInt16(sqlReader, propertyInfo.Name);
						int? value = GetQueryUInt8(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlBoolAttribute)
					{
						bool? value = GetQueryBool(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlMemoAttribute)
					{
						string value = GetQueryString(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlDateAttribute)
					{
						DateTime? value = GetQueryDateTime(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
					else if (attrib is SqlDateStrAttribute)
					{
						DateTime? value = GetQueryDateTimeStr(sqlReader, propertyInfo.Name);
						prop.SetValue(item, value, null);
					}
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, nameof(ItemGetQuery), $"Error {propertyInfo.Name} {item.GetType()}", ex);
					item = default(T);
					throw new Exception($"{propertyInfo.Name} {item?.GetType()}", ex);
				}
			}

			return item;
		}

		protected object DoSqlScalar(string sqlStr)
		{
			lock (Locker)
			{
				SqliteCommand sqlCmd = null;
				try
				{
					sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					object result = sqlCmd.ExecuteScalar();
					sqlCmd.Dispose();
					return result;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, $"{nameof(DoSqlScalar)}", $"'{sqlStr}'", ex);
					sqlCmd.Dispose();
					return null;
				}
			}
		}

		protected bool DoSqlNoQuery(string sqlStr)
		{
			lock (Locker)
			{
				SqliteCommand sqlCmd = null;
				try
				{
					sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
					sqlCmd.ExecuteNonQuery();
					sqlCmd.Dispose();
					return true;
				}
				catch (Exception ex)
				{
					_logger.Error(TAG, $"{nameof(DoSqlNoQuery)}", $"'{sqlStr}'", ex);
					sqlCmd.Dispose();
					return false;
				}
			}
		}

		private static string GetQueryString(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(string))
					return (string)reader[fieldName];
				else
					return null;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryString)}", "", ex);
				throw;
			}
		}

		private static byte? GetQueryUInt8(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(byte) || value.GetType() == typeof(Int16) || value.GetType() == typeof(Int32) || value.GetType() == typeof(Int64))
					return Convert.ToByte(value);
				else
					return null;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryUInt8)}", "{fieldName}", ex);
				throw;
			}
		}

		private static Int16? GetQueryInt16(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(Int16) || value.GetType() == typeof(Int32) || value.GetType() == typeof(Int64))
					return Convert.ToInt16(value);
				else
					return null;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryInt16)}", "{fieldName}", ex);
				throw;
			}
		}

		private static Int32? GetQueryInt32(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(Int32) || value.GetType() == typeof(Int64))
					return Convert.ToInt32(value);
				else
					return null;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryInt32)}", "{fieldName}", ex);
				throw;
			}
		}

		private static Int64? GetQueryInt64(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(Int32) || value.GetType() == typeof(Int64))
					return Convert.ToInt64(value);
				else
					return null;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryInt64)}", "{fieldName}", ex);
				throw;
			}
		}

		private static bool? GetQueryBool(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(DBNull)) return null;
				return Convert.ToInt32(reader[fieldName]) != 0;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryBool)}", "{fieldName}", ex);
				throw;
			}
		}

		private static DateTime? GetQueryDateTime(SqliteDataReader reader, string fieldName)
		{
			try
			{
				var value = reader[fieldName];
				if (value.GetType() == typeof(DBNull)) return null;
				int d = Convert.ToInt32(reader[fieldName]);
				return TimestampToDateTime(d);
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryDateTime)}", "{fieldName}", ex);
				throw;
			}
		}

		private static DateTime? GetQueryDateTimeStr(SqliteDataReader reader, string fieldName)
		{
			try
			{
				//if (fieldName == "LastPinChangeTime")
				//{
				//	Debug.WriteLine(fieldName);
				//}

				var value = reader[fieldName];
				//Debug.WriteLine($"{value.GetType()}");
				if (value.GetType() == typeof(DBNull)) return null;
				if (string.IsNullOrEmpty((string)value)) return null;
				DateTime dt = Convert.ToDateTime(value);
				return dt;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(GetQueryDateTimeStr)}", $"{fieldName}", ex);
				throw;
			}
		}

		protected string BuildWhereClause(List<string> whereList)
		{
			return string.Join(" AND ", whereList.ToArray());
		}

		protected int GetAffectedRowsCount()
		{
			try
			{
				string sqlStr = $"SELECT CHANGES()";
				SqliteCommand sqlCmd = new SqliteCommand(sqlStr, _sqlConn);
				return (int)Convert.ToInt32(sqlCmd.ExecuteScalar());
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(GetAffectedRowsCount), "", ex);
				return 0;
			}

		}

		private static string Bool2Str(bool b)
		{
			return (b ? 1 : 0).ToString();
		}

		private static int? DateTimeToTimestamp(DateTime? dateTime)
		{
			if (dateTime == null) return null;

			try
			{
				DateTime dt1 = new DateTime(1970, 1, 1);
				DateTime.SpecifyKind(dt1, DateTimeKind.Utc);
				DateTime dt2 = dateTime.Value;
				DateTime.SpecifyKind(dt2, DateTimeKind.Utc);
				//dt1 = date1.ToLocalTime();
				//dt1 = date1.ToUniversalTime();
				//DateTime dt2 = dateTime.Value.ToLocalTime();
				//DateTime date2 = dateTime.ToUniversalTime();
				TimeSpan ts = new TimeSpan(dt2.Ticks - dt1.Ticks);  // das Delta ermitteln
				long diff = Convert.ToInt64(ts.TotalSeconds);
				if (diff > int.MaxValue) return null;    // invalid
				return (int)diff;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, $"{nameof(DateTimeToTimestamp)}", "", ex);
				return 0;
			}
		}

		protected static string DateTimeStrToTimestamp(DateTime? dateTime)
		{
			if (dateTime == null) return "NULL";
			return dateTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
		}

		static public DateTime TimestampToDateTime(int timestamp)
		{
			DateTime dt = new DateTime(1970, 1, 1);
			DateTime.SpecifyKind(dt, DateTimeKind.Utc);
			//dt = dateTime.ToLocalTime();
			//dt = dt.ToUniversalTime();
			dt = dt.AddSeconds(timestamp);
			return dt;
		}

		protected string EscapeSql(string str)
		{
			if (str == null) return null;
			string[] arr = str.Split('\'');
			str = string.Join("''", arr);
			return str;
		}
	}
}

