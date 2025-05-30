using ItelexTlnServer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TlnServer.BinaryServer
{
	internal class ResetCentralexClientsItem
	{
		public string IpAddress { get; set; }

		public int FromPort { get; set; }

		public int ToPort { get; set; }
	}
}
