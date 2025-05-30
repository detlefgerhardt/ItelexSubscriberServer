using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TlnServer.Config
{
	internal class SyncServerItem
	{
		public int Id { get; set; }

		public string Address { get; set; }

		public int Port { get; set; }

		public int Version { get; set; }

		public override string ToString()
		{
			return $"{Id} {Address} {Port} {Version}";
		}
	}
}
