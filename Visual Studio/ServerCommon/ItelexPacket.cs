namespace CentralexCommon
{
	public enum ItelexCommands
	{
		Heartbeat = 0x00,
		DirectDial = 0x01,
		BaudotData = 0x02,
		End = 0x03,
		Reject = 0x04,
		Ack = 0x06,
		ProtocolVersion = 0x07,
		SelfTest = 0x08,
		RemoteConfig = 0x09,
		ConnectRemote = 0x81,
		RemoteConfirm = 0x82,
		RemoteCall = 0x83,
		AcceptCallRemote = 0x84,
	}

	public class ItelexPacket
	{
		public int Command { get; set; }

		public ItelexCommands CommandType => (ItelexCommands)Command;

		public byte[] Data { get; set; }

		public int Len => Data == null ? 0 : Data.Length;

		public ItelexPacket() { }

		public ItelexPacket(byte[] buffer)
		{
			Command = buffer[0];
			int len = buffer[1];
			if (len > 0)
			{
				Data = new byte[len];
				Buffer.BlockCopy(buffer, 2, Data, 0, len);
			}
			else
			{
				Data = null;
			}
		}

		/*
		public void Dump(string pre)
		{
			Debug.Write($"{pre}: cmd={CommandType} [{Len}]");
			for (int i = 0; i < Len; i++)
			{
				Debug.Write($" {Data[i]:X2}");
			}
			Debug.WriteLine("");
		}
		*/

		public string GetDebugData()
		{
			string debStr = "";
			for (int i = 0; i < Len; i++)
			{
				debStr += $" {Data[i]:X2}";
			}
			return debStr.Trim();
		}

		public string GetDebugPacket()
		{
			string debStr = "";
			for (int i = 0; i < Len + 2; i++)
			{
				int d = 0;
				if (i == 0)
				{
					d = Command;
				}
				else if (i == 1)
				{
					d = Len;
				}
				else
				{
					d = Data[i - 2];
				}
				debStr += $" {d:X2}";
			}
			return debStr.Trim();
		}

		public string GetDebugPacketStr()
		{
			return $"{Command:X02} {Len:X02} " + GetDebugData();
		}

		public override string ToString()
		{
			return $"{CommandType} {Len}: {GetDebugData()}";
		}
	}
}
