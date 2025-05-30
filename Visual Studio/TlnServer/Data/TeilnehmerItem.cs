using ItelexCommon;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ItelexTlnServer.Data
{
	internal enum ClientAddressTypes
	{
		Deleted = 0,
		Baudot_Hostname = 1,
		Baudot_Fixed = 2,
		Ascii_Hostname = 3,
		Ascii_Fixed = 4,
		Baudot_DynIp = 5,
		Email = 6
	};

	[Serializable]
	internal class TeilnehmerItem
	{
		[SqlId]
		public Int64 Uid { get; set; }

		[SqlInt]
		public int Number { get; set; }

		[SqlString(Length = 40)]
		public string Name { get; set; }

		[SqlTinyInt]
		public int Type { get; set; }

		[SqlString(Length = 40)]
		public string Hostname { get; set; }

		[SqlString(Length = 15)]
		public string IpAddress { get; set; }

		[SqlInt]
		public int? Port { get; set; }

		[SqlString(Length = 2)]
		public string Extension { get; set; }

		[SqlInt]
		public int? Pin { get; set; }

		[SqlBool]
		public bool Disabled { get; set; }

		[SqlBool]
		public bool LeadingEntry { get; set; }

		[SqlBool]
		public bool Remove { get; set; }

		[SqlString(Length = 30)]
		public string Answerback { get; set; }

		[SqlInt]
		public int UserId { get; set; }

		[SqlInt]
		public int DeviceId { get; set; }

		[SqlBool]
		public bool MainNumber { get; set; }

		[SqlInt]
		public int UpdatedBy { get; set; }

		[SqlInt]
		public int ChangedBy { get; set; }

		[SqlDate]
		public DateTime TimestampUtc { get; set; }

		[SqlDateStr]
		public DateTime UpdateTimeUtc { get; set; }

		[SqlDateStr]
		public DateTime CreateTimeUtc { get; set; }

		[SqlBool]
		public bool Changed { get; set; }

		public int? PinOrNull => Pin == 0 ? null : Pin;

		public bool Processed { get; set; }

		public bool Equals(TeilnehmerItem other)
		{
			return Number == other.Number &&
				Name == other.Name &&
				Type == other.Type &&
				Hostname == other.Hostname &&
				IpAddress == other.IpAddress &&
				Port == other.Port &&
				Extension == other.Extension &&
				PinOrNull == other.PinOrNull &&
				Disabled == other.Disabled &&
				Remove == other.Remove &&
				Answerback == other.Answerback &&
				LeadingEntry == other.LeadingEntry &&
				UpdatedBy == other.UpdatedBy &&
				ChangedBy == other.ChangedBy &&
				UserId == other.UserId &&
				DeviceId == other.DeviceId &&
				MainNumber == other.MainNumber &&
				UpdateTimeUtc == other.UpdateTimeUtc &&
				CreateTimeUtc == other.CreateTimeUtc;
		}

		public Dictionary<string, Tuple<object, object>> Compare(TeilnehmerItem other)
		{
			Dictionary<string, Tuple<object, object>> list = new Dictionary<string, Tuple<object, object>>();
			if (Name != other.Name) list["Name"] = new Tuple<object, object>(Name, other.Name);
			if (Type != other.Type) list["Type"] = new Tuple<object, object>(Type, other.Type);
			if (Hostname != other.Hostname) list["Hostname"] = new Tuple<object, object>(Hostname, other.Hostname);
			if (IpAddress != other.IpAddress) list["IpAddress"] = new Tuple<object, object>(IpAddress, other.IpAddress);
			if (Port.GetValueOrDefault() != other.Port.GetValueOrDefault())
				list["Port"] = new Tuple<object, object>(Port, other.Port);
			if (Extension != other.Extension) list["Extension"] = new Tuple<object, object>(Extension, other.Extension);
			if (PinOrNull != other.PinOrNull) list["Pin"] = new Tuple<object, object>(Pin, other.Pin);
			if (Disabled != other.Disabled)
				list["Disabled"] = new Tuple<object, object>(Disabled, other.Disabled);
			if (Remove != other.Remove)
				list["Remove"] = new Tuple<object, object>(Remove, other.Remove);
			if (Answerback != other.Answerback) list["Answerback"] = new Tuple<object, object>(Answerback, other.Answerback);
			if (LeadingEntry != other.LeadingEntry)
				list["LeadingEntry"] = new Tuple<object, object>(LeadingEntry, other.LeadingEntry);
			if (UpdatedBy != other.UpdatedBy) list["UpdatedBy"] = new Tuple<object, object>(UpdatedBy, other.UpdatedBy);
			if (ChangedBy != other.ChangedBy) list["ChangedBy"] = new Tuple<object, object>(ChangedBy, other.ChangedBy);
			if (UserId != other.UserId)
				list["UserId"] = new Tuple<object, object>(UserId, other.UserId);
			if (DeviceId != other.DeviceId)
				list["DeviceId"] = new Tuple<object, object>(DeviceId, other.DeviceId);
			if (MainNumber != other.MainNumber)
				list["MainNumber"] = new Tuple<object, object>(MainNumber, other.MainNumber);
			if (UpdateTimeUtc != other.UpdateTimeUtc)
				list["UpdateTimeUtc"] = new Tuple<object, object>(UpdateTimeUtc, other.UpdateTimeUtc);
			if (CreateTimeUtc != other.CreateTimeUtc)
				list["CreateTimeUtc"] = new Tuple<object, object>(CreateTimeUtc, other.CreateTimeUtc);
			return list;
		}

		/// <summary>
		/// show values of other
		/// </summary>
		/// <param name="other"></param>
		/// <param name="error"></param>
		/// <returns></returns>
		public string CompareToString(TeilnehmerItem other, out string error)
		{
			error = null;
			try
			{
				Dictionary<string, Tuple<object, object>> list = Compare(other);

				string str = "";
				if (list.ContainsKey("Name"))
				{
					str += $"Name '{(string)list["Name"].Item2}', ";
				}
				if (list.ContainsKey("Number"))
				{
					str += $"Number {(string)list["Number"].Item2}, ";
				}
				if (list.ContainsKey("Type"))
				{
					str += $"Type {(int?)list["Type"].Item2}, ";
				}
				if (list.ContainsKey("Hostname"))
				{
					str += $"Hostname '{(string)list["Hostname"].Item2}', ";
				}
				if (list.ContainsKey("IpAddress"))
				{
					str += $"IpAddress {(string)list["IpAddress"].Item2}, ";
				}
				if (list.ContainsKey("Port"))
				{
					str += $"Port {(int?)list["Port"].Item2}, ";
				}
				if (list.ContainsKey("Extension"))
				{
					str += $"Extension '{(string)list["Extension"].Item2}', ";
				}
				if (list.ContainsKey("Pin"))
				{
					//str += $"pin {(int?)list["pin"].Item2}, ";
					str += $"Pin hidden, "; // do not show pin in logfiles
				}
				if (list.ContainsKey("Disabled"))
				{
					str += $"Disabled {(bool?)list["Disabled"].Item2}, ";
				}
				if (list.ContainsKey("Remove"))
				{
					str += $"Remove {(bool?)list["Remove"].Item2}, ";
				}
				if (list.ContainsKey("Answerback"))
				{
					str += $"Answerback '{(string)list["Answerback"].Item2}', ";
				}
				if (list.ContainsKey("LeadingEntry"))
				{
					str += $"LeadingEntry {(bool?)list["LeadingEntry"].Item2}, ";
				}
				if (list.ContainsKey("UpdatedBy"))
				{
					str += $"UpdatedBy {(int)list["UpdatedBy"].Item2}, ";
				}
				if (list.ContainsKey("ChangedBy"))
				{
					str += $"ChangedBy {(int)list["ChangedBy"].Item2}, ";
				}
				if (list.ContainsKey("UserId"))
				{
					str += $"UserId {(int?)list["UserId"].Item2}, ";
				}
				if (list.ContainsKey("DeviceId"))
				{
					str += $"DeviceId {(int?)list["DeviceId"].Item2}, ";
				}
				if (list.ContainsKey("MainNumber"))
				{
					str += $"MainNumber {(bool?)list["MainNumber"].Item2}, ";
				}
				if (list.ContainsKey("UpdateTimeUtc"))
				{
					str += $"UpdateTimeUtc {(DateTime?)list["UpdateTimeUtc"].Item2}, ";
				}
				if (list.ContainsKey("CreateTimeUtc"))
				{
					str += $"CreateTimeUtc {(DateTime?)list["CreateTimeUtc"].Item2}, ";
				}

				if (str.Length >= 2) str = str.Substring(0, str.Length - 2);
				return str;
			}
			catch(Exception ex)
			{
				error = ex.Message;
				return "";
			}
		}

		public string GetCsvLine()
		{
			if (!string.IsNullOrEmpty(Answerback))
			{
				Debug.Write("");
			}

			StringBuilder sb = new StringBuilder();
			sb.Append($"{Number},");
			sb.Append($"\"{Name}\",");
			sb.Append($"{Type},");
			sb.Append($"\"{Hostname}\",");
			sb.Append($"\"{IpAddress}\",");
			sb.Append($"{Port},");
			sb.Append($"{Extension},");
			sb.Append($"{BoolToInt(Disabled)},");
			sb.Append($"{BoolToInt(Remove)},");
			sb.Append($"{BoolToInt(LeadingEntry)},");
			sb.Append($"\"{Answerback}\",");
			sb.Append($"{UpdatedBy},");
			sb.Append($"{ChangedBy},");
			sb.Append($"{UserId},");
			sb.Append($"{DeviceId},");
			sb.Append($"{BoolToInt(MainNumber)},");
			sb.Append($"{UpdateTimeUtc:dd.MM.yyyy HH:mm:ss},");
			sb.Append($"{CreateTimeUtc:dd.MM.yyyy HH:mm:ss},");
			return sb.ToString();
		}

		private int BoolToInt(bool value)
		{
			return value ? 1 : 0;
		}

		public override string ToString()
		{
			return $"{Uid} {Number} '{Name}' {Type} {Hostname} {IpAddress} {Port} {Extension} {Disabled} {Remove} {TimestampUtc} {Changed}";
		}

		public static bool AddressTypeIsHostname(ClientAddressTypes addrType)
		{
			switch(addrType)
			{
				case ClientAddressTypes.Deleted:
					return false; // IP
				case ClientAddressTypes.Baudot_Hostname:
					return true; // Hostname
				case ClientAddressTypes.Baudot_Fixed:
					return false;
				case ClientAddressTypes.Ascii_Hostname:
					return true; // Hostname
				case ClientAddressTypes.Ascii_Fixed:
					return false; // IP
				case ClientAddressTypes.Baudot_DynIp:
					return false; // IP
				case ClientAddressTypes.Email:
					return true; // Hostname
				default:
					return true; // Hostname
			}
		}
	}
}
