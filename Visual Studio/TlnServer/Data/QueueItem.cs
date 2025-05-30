using ItelexCommon;

namespace ItelexTlnServer.Data
{
	[Serializable]
	internal class QueueItem
	{
		[SqlId]
		public Int64 uid { get; set; }

		[SqlInt]
		public long server { get; set; }

		[SqlInt]
		public long message { get; set; }

		[SqlDate]
		public DateTime? timestamp { get; set; }

		public override string ToString()
		{
			return $"{uid} {server} {message} {timestamp}";
		}
	}
}
