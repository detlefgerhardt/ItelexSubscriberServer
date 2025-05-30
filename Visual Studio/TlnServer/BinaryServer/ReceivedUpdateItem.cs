using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TlnServer.BinaryServer
{
	internal class ReceivedUpdateItem
	{
		public long TlnId { get; set; }

		public DateTime Timestamp { get; set; }

		public int ServerId { get; set; } 
	}
}
