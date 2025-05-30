using ItelexCommon;

namespace ItelexTlnServer.Data
{
	[Serializable]
	internal class ServersItem
	{
		[SqlId]
		public Int64 uid { get; set; }

		[SqlString(Length = 40)]
		public string address { get; set; }

		[SqlSmallInt]
		public int port { get; set; }

		[SqlTinyInt]
		public int version { get; set; }

		public override string ToString()
		{
			return $"{uid} {address} {version} {port}";
		}
	}
}
