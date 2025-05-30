using ItelexTlnServer.Data;
using ServerCommon.Utility;
using System.Net;
using System.Text;

namespace Centralex.BinaryProxy
{
	internal enum BinaryCommands
	{
		ClientUpdate = 1,
		AddressConfirm = 2,
		PeerQuery = 3,
		PeerNotFound = 4,
		PeerReplyV1 = 5,
		SyncFullQuery = 6,
		SyncLogin = 7,
		Acknowledge = 8,
		EndOfList = 9,
		PeerSearch = 10,
		Error = 255
	}

	internal class BinaryPacket
	{
		public const int PACKETLEN_PEERREPLY_V1 = 100;

		public int Command { get; set; }

		public BinaryCommands CommandType => (BinaryCommands)Command;

		public byte[] PacketBuffer { get; set; }

		public byte[] Data
		{
			get
			{
				if (PacketBuffer == null) return null;
				byte[] data = new byte[PacketBuffer.Length - 2];
				Buffer.BlockCopy(PacketBuffer, 2, data, 0, PacketBuffer.Length - 2);
				return data;
			}
		}

		public int DataLen => PacketBuffer == null ? 0 : PacketBuffer.Length - 2;

		public BinaryPacket() { }

		public BinaryPacket(byte[] buffer)
		{
			if (buffer.Length >= 2)
			{
				Command = buffer[0];
				int len = buffer[1];
				PacketBuffer = buffer;
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
			for (int i = 0; i < DataLen; i++)
			{
				debStr += $" {Data[i]:X2}";
			}
			return debStr.Trim();
		}

		public string GetDebugPacketStr()
		{
			return $"{Command:X02} {DataLen:X02} " + GetDebugData();
		}

		public static BinaryPacket GetAddressConfirm(byte[] ipaddress)
		{
			byte[] sendData = new byte[6];
			sendData[0] = (int)BinaryCommands.AddressConfirm;
			sendData[1] = 4; // len
			for (int i=0; i<4; i++)
			{
				sendData[i + 2] = ipaddress[i];
			}
			return new BinaryPacket(sendData);
		}

		public static BinaryPacket GetPeerNotFound()
		{
			byte[] sendData = new byte[2];
			sendData[0] = (int)BinaryCommands.PeerNotFound;
			sendData[1] = 0;
			return new BinaryPacket(sendData);
		}

		public static BinaryPacket GetPeerReplyV1(PeerItem tlnItem)
		{
			try
			{
				byte[] sendData = new byte[PACKETLEN_PEERREPLY_V1 + 2];
				int pos = 0;

				sendData[0] = (int)BinaryCommands.PeerReplyV1;
				pos++;

				// length
				sendData[1] = PACKETLEN_PEERREPLY_V1;
				pos++;

				// number
				byte[] numData = BitConverter.GetBytes(tlnItem.Number);
				Buffer.BlockCopy(numData, 0, sendData, pos, 4);
				pos += 4;

				// name
				numData = Encoding.ASCII.GetBytes(tlnItem.Name);
				Buffer.BlockCopy(numData, 0, sendData, pos, numData.Length);
				pos += 40;

				// special attributes
				pos += 2;

				// type
				sendData[pos] = (byte)tlnItem.Type;
				pos++;

				// hostname
				numData = Encoding.ASCII.GetBytes(tlnItem.Hostname);
				Buffer.BlockCopy(numData, 0, sendData, pos, numData.Length);
				pos += 40;

				// ip-address
				if (!string.IsNullOrEmpty(tlnItem.IpAddress))
				{
					IPAddress ip = IPAddress.Parse(tlnItem.IpAddress);
					numData = ip.GetAddressBytes();
					sendData[pos++] = numData[0];
					sendData[pos++] = numData[1];
					sendData[pos++] = numData[2];
					sendData[pos++] = numData[3];
				}

				// port
				numData = BitConverter.GetBytes((UInt16)tlnItem.Number);
				Buffer.BlockCopy(numData, 0, sendData, pos, 2);
				pos += 2;

				// extension
				if (byte.TryParse(tlnItem.Extension, out byte ext))
				{
					sendData[pos] = ext;
				}
				pos++;

				// pin
				// numData = BitConverter.GetBytes((UInt16)tlnItem.pin);
				// Buffer.BlockCopy(numData, 0, sendData, pos, 2);
				pos += 2;

				// timestamp
				uint ts = CommonHelper.DateTimeToTimestampUtc1900(tlnItem.Timestamp);
				numData = BitConverter.GetBytes((UInt32)ts);
				Buffer.BlockCopy(numData, 0, sendData, pos, 4);
				pos += 4;

				return new BinaryPacket(sendData);
			}
			catch(Exception)
			{
				return null;
			}
		}

		public static BinaryPacket GetSyncFullQuery(int serverPin)
		{
			byte[] sendData = new byte[7];
			sendData[0] = (int)BinaryCommands.SyncFullQuery;
			sendData[1] = 5; // len
			sendData[2] = 1; // version

			// server pin
			byte[] numData = BitConverter.GetBytes((UInt32)serverPin);
			Buffer.BlockCopy(numData, 0, sendData, 3, 4);
			return new BinaryPacket(sendData);
		}

		public static BinaryPacket GetAcknowledge()
		{
			return new BinaryPacket(new byte[2] { 0x08, 0x00 });
		}

		public static BinaryPacket GetEndOfList()
		{
			return new BinaryPacket(new byte[2] { 0x09, 0x00 });
		}

		public static BinaryPacket GetError(string msg = "")
		{
			byte[] msgData = Encoding.ASCII.GetBytes(msg);
			byte[] data = new byte[msgData.Length + 2];
			data[0] = (int)BinaryCommands.Error;
			data[1] = (byte)msgData.Length;
			Buffer.BlockCopy(msgData, 0, data, 2, msgData.Length);
			return new BinaryPacket(data);
		}


		public override string ToString()
		{
			return $"{CommandType} {DataLen}: {GetDebugData()}";
		}
	}
}
