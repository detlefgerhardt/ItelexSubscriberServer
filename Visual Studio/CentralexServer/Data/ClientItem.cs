using CentralexServer.CentralexConnections;
using ItelexCommon;

namespace ItelexTlnServer.Data
{
	internal enum States { Nc, Ready, Call };


	[Serializable]
	internal class ClientItem
	{
		[SqlId]
		public Int64 ClientId { get; set; }

		[SqlInt]
		public int Number { get; set; }

		[SqlString(Length = 40)]
		public string Name { get; set; }

		[SqlInt]
		public int Port { get; set; }

		[SqlInt]
		public int? Pin { get; set; }

		[SqlTinyInt]
		public int State { get; set; }

		[SqlDateStr]
		public DateTime CreatedUtc { get; set; }

		[SqlDateStr]
		public DateTime LastChangedUtc { get; set; }

		public string DisconnectReason { get; set; }

		public ClientStates StateEnum => (ClientStates)State;

		public void SetClientState(ClientStates state)
		{
			State = (int)state;
			LastChangedUtc = DateTime.UtcNow;
		}

		public void SetUpdateLastChangedUtc()
		{
			LastChangedUtc = DateTime.UtcNow;
		}

		public override string ToString()
		{
			return $"{ClientId} {Number} '{Name}' {Port} {State} {CreatedUtc} {LastChangedUtc}";
		}
	}
}
