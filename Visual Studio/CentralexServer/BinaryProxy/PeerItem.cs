using ItelexCommon;

namespace ItelexTlnServer.Data
{
	internal enum PeerTypes
	{
		Deleted = 0,
		Baudot_Hostname = 1,
		Baudot_Fixed = 2,
		Ascii_Hostname = 3,
		Ascii_Fixed = 4,
		Baudot_DynIp = 5,
		Email = 6
	};

	internal class PeerItem
	{
		[SqlInt]
		public int Number { get; set; }

		public string Name { get; set; }

		public byte[] SpecialAttribute { get; set; }

		public int Type { get; set; }

		public string Hostname { get; set; }

		public string IpAddress { get; set; }

		public int Port { get; set; }

		public string Extension { get; set; }

		public int Pin { get; set; }

		public DateTime Timestamp { get; set; }

		public override string ToString()
		{
			return $"{Number} '{Name}' {Type} {Hostname} {IpAddress} {Port} {Extension} {Timestamp}";
		}
	}
}
