using ItelexTlnServer.Data;
using Org.BouncyCastle.Utilities;
using ServerCommon.Logging;
using ServerCommon.Utility;
using SQLitePCL;
using System.Diagnostics;
using System.Net;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TlnServer.BinaryServer
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
		ResetCentralexClients = 11,
		//SyncDataV1 = 11,
		SyncReplyV2 = 12,
		SyncReplyV3 = 13,
		Error = 255
	}

	internal enum SpecialAttr
	{
		DISABLED = 0x0002,
		REMOVE = 0x0004,
		LEADING_ENTRY = 0x0008,
	}

	internal class BinaryPacket
	{
		public const int PACKETLEN_PEERREPLY_V1 = 100;
		//public const int PACKETLEN_SYNCDATA_V1 = 130;
		public const int PACKETLEN_SYNCREPLY_V2 = 131;
		public const int PACKETLEN_SYNCREPLY_V3 = 141;

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

		public static BinaryPacket GetSyncFullQuery(uint serverPin)
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

		public static BinaryPacket GetSyncLogin(uint serverPin)
		{
			byte[] sendData = new byte[7];
			sendData[0] = (int)BinaryCommands.SyncLogin;
			sendData[1] = 5; // len
			sendData[2] = 1; // version

			// server pin
			byte[] numData = BitConverter.GetBytes((UInt32)serverPin);
			Buffer.BlockCopy(numData, 0, sendData, 3, 4);
			return new BinaryPacket(sendData);
		}


		public static BinaryPacket GetAcknowledge()
		{
			return new BinaryPacket(new byte[2] { (byte)BinaryCommands.Acknowledge, 0x00 });
		}

		public static BinaryPacket GetEndOfList()
		{
			return new BinaryPacket(new byte[2] { 0x09, 0x00 });
		}

		public static BinaryPacket GetError(string msg = "")
		{
			byte[] msgData = Encoding.ASCII.GetBytes(msg);
			byte[] data = new byte[msgData.Length + 2];
			data[0] = (byte)BinaryCommands.Error;
			data[1] = (byte)msgData.Length;
			Buffer.BlockCopy(msgData, 0, data, 2, msgData.Length);
			return new BinaryPacket(data);
		}

		public static TeilnehmerItem PeerReplyV1ToTeilnehmer(BinaryPacket peerReply)
		{
			TeilnehmerItem tlnItem = new TeilnehmerItem();
			byte[] data = peerReply.Data;
			if (data.Length != PACKETLEN_PEERREPLY_V1) return null; // invalid packet

			int pos = 0;

			// number
			tlnItem.Number = (int)BitConverter.ToUInt32(data, pos);
			pos += 4;

			// name
			tlnItem.Name = Encoding.ASCII.GetString(data, pos, 40).TrimEnd(new char[] { '\x00' });
			pos += 40;

			int specialAttribute = (int)BitConverter.ToUInt16(data, pos);
			tlnItem.Disabled = ((specialAttribute & (int)SpecialAttr.DISABLED) != 0);
			pos += 2;

			// type
			tlnItem.Type = data[pos];
			pos++;

			// hostname
			tlnItem.Hostname = Encoding.ASCII.GetString(data, pos, 40).Trim(new char[] { '\x00' });
			pos += 40;

			// ipaddress
			if (data[pos] == 0 && data[pos + 1] == 0 && data[pos + 2] == 0 && data[pos + 3] == 0)
			{
				tlnItem.IpAddress = "";
			}
			else
			{
				tlnItem.IpAddress = $"{data[pos]}.{data[pos + 1]}.{data[pos + 2]}.{data[pos + 3]}";
			}
			pos += 4;

			// port
			tlnItem.Port = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// extension
			tlnItem.Extension = ConvertExtToString(data[pos]);
			pos++;

			// pin
			int pin = BitConverter.ToUInt16(data, pos);
			tlnItem.Pin = (pin == 0) ? null : pin;
			pos += 2;

			// timestamp
			uint ts = BitConverter.ToUInt32(data, pos);
			tlnItem.TimestampUtc = CommonHelper.TimestampToDateTimeUtc1900(ts);
			tlnItem.UpdateTimeUtc = CommonHelper.TimestampToDateTimeUtc1900(ts);
			pos += 4;

			return tlnItem;
		}

		public static BinaryPacket TeilnehmerToPeerReplyV1(TeilnehmerItem tlnItem)
		{
			try
			{
				byte[] data = new byte[102];
				data[0] = (int)BinaryCommands.PeerReplyV1;
				data[1] = PACKETLEN_PEERREPLY_V1;
				int pos = 2;

				// number
				byte[] bytes = BitConverter.GetBytes((UInt32)tlnItem.Number);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				// name
				if (!string.IsNullOrEmpty(tlnItem.Name))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Name);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// no special attributes
				pos += 2;

				// type
				data[pos] = (byte)tlnItem.Type;
				pos++;

				// hostname
				if (!string.IsNullOrEmpty(tlnItem.Hostname))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Hostname);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// ipaddress
				bytes = IpAddressToByteArray(tlnItem.IpAddress);
				if (bytes != null)
				{
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 4;

				// port
				bytes = BitConverter.GetBytes((UInt16)tlnItem.Port);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// extension
				data[pos] = ConvertStringToExt(tlnItem.Extension);
				pos++;

				// no pin
				pos += 2;

				// timestamp
				//uint ts = tlnItem.TimestampUtc.HasValue ? CommonHelper.DateTimeToTimestampUtc1900(tlnItem.TimestampUtc.Value) : 0;
				uint ts = CommonHelper.DateTimeToTimestampUtc1900(tlnItem.TimestampUtc);
				bytes = BitConverter.GetBytes((UInt32)ts);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				return new BinaryPacket(data);
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static TeilnehmerItem SyncReplyV2ToTeilnehmer(BinaryPacket peerReply)
		{
			TeilnehmerItem tlnItem = new TeilnehmerItem();

			byte[] data = peerReply.Data;
			if (data.Length != PACKETLEN_SYNCREPLY_V2) return null; // invalid packet

			int pos = 0;

			// number
			tlnItem.Number = (int)BitConverter.ToUInt32(data, pos);
			pos += 4;

			// name
			tlnItem.Name = Encoding.ASCII.GetString(data, pos, 40).TrimEnd(new char[] { '\x00' });
			if (string.IsNullOrEmpty(tlnItem.Name)) tlnItem.Name = null;
			pos += 40;

			int specialAttribute = (int)BitConverter.ToUInt16(data, pos);
			tlnItem.Disabled = ((specialAttribute & (int)SpecialAttr.DISABLED) != 0);
			tlnItem.Remove = ((specialAttribute & (int)SpecialAttr.REMOVE) != 0);
			tlnItem.LeadingEntry = ((specialAttribute & (int)SpecialAttr.LEADING_ENTRY) != 0);
			//if (tlnItem.LeadingEntry == false) tlnItem.LeadingEntry = null;
			pos += 2;

			// type
			tlnItem.Type = data[pos];
			pos++;

			// hostname
			tlnItem.Hostname = Encoding.ASCII.GetString(data, pos, 40).Trim(new char[] { '\x00' });
			if (string.IsNullOrEmpty(tlnItem.Hostname)) tlnItem.Hostname = null;
			pos += 40;

			// ipaddress
			if (data[pos] == 0 && data[pos + 1] == 0 && data[pos + 2] == 0 && data[pos + 3] == 0)
			{
				tlnItem.IpAddress = "";
			}
			else
			{
				tlnItem.IpAddress = $"{data[pos]}.{data[pos + 1]}.{data[pos + 2]}.{data[pos + 3]}";
			}
			pos += 4;

			// port
			tlnItem.Port = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// extension
			tlnItem.Extension = ConvertExtToString(data[pos]);
			if (string.IsNullOrEmpty(tlnItem.Extension)) tlnItem.Extension = null;
			pos++;

			// pin
			int pin = BitConverter.ToUInt16(data, pos);
			tlnItem.Pin = (pin == 0) ? null : pin;
			pos += 2;

			// Answerback
			tlnItem.Answerback = Encoding.ASCII.GetString(data, pos, 30).Trim(new char[] { '\x00' });
			if (string.IsNullOrEmpty(tlnItem.Answerback)) tlnItem.Answerback = null;
			pos += 30;

			// Changedby
			tlnItem.ChangedBy = data[pos];
			pos++;

			// timestamp
			uint ts = BitConverter.ToUInt32(data, pos);
			tlnItem.TimestampUtc = CommonHelper.TimestampToDateTimeUtc1900(ts);
			tlnItem.UpdateTimeUtc = CommonHelper.TimestampToDateTimeUtc1900(ts);
			pos += 4;

			return tlnItem;
		}

		public static BinaryPacket TeilnehmerToSyncReplyV2(TeilnehmerItem tlnItem, bool sync)
		{
			try
			{
				byte[] data = new byte[PACKETLEN_SYNCREPLY_V2 + 2];
				data[0] = (int)BinaryCommands.SyncReplyV2;
				data[1] = PACKETLEN_SYNCREPLY_V2;
				int pos = 2;

				// number
				byte[] bytes = BitConverter.GetBytes((UInt32)tlnItem.Number);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				// name
				if (!string.IsNullOrEmpty(tlnItem.Name))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Name.ExtSubstring(0, 40));
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// special attributes
				if (sync)
				{
					int specialAttributes = 0;
					if (tlnItem.Disabled) specialAttributes |= (int)SpecialAttr.DISABLED;
					if (tlnItem.Remove) specialAttributes |= (int)SpecialAttr.REMOVE;
					if (tlnItem.LeadingEntry) specialAttributes |= (int)SpecialAttr.LEADING_ENTRY;
					bytes = BitConverter.GetBytes((UInt16)specialAttributes);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 2;

				// type
				data[pos] = (byte)tlnItem.Type;
				pos++;

				// hostname
				if (!string.IsNullOrEmpty(tlnItem.Hostname))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Hostname.ExtSubstring(0, 40));
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// ipaddress
				bytes = IpAddressToByteArray(tlnItem.IpAddress);
				if (bytes != null)
				{
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 4;

				// port
				bytes = BitConverter.GetBytes((UInt16)tlnItem.Port);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// extension
				data[pos] = ConvertStringToExt(tlnItem.Extension);
				pos++;

				// pin
				if (sync)
				{
					if (tlnItem.Pin != null)
					{
						bytes = BitConverter.GetBytes((UInt16)tlnItem.Pin.Value);
						Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
					}
				}
				pos += 2;

				// answerback
				if (!string.IsNullOrEmpty(tlnItem.Answerback))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Answerback.ExtSubstring(0, 30));
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 30;

				// changedby
				data[pos] = (byte)tlnItem.ChangedBy;
				pos++;

				// timestamp
				//uint ts = tlnItem.TimestampUtc.HasValue ? CommonHelper.DateTimeToTimestampUtc1900(tlnItem.TimestampUtc.Value) : 0;
				uint ts = CommonHelper.DateTimeToTimestampUtc1900(tlnItem.TimestampUtc);
				bytes = BitConverter.GetBytes((UInt32)ts);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				return new BinaryPacket(data);
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static TeilnehmerItem SyncReplyV3ToTeilnehmer(BinaryPacket peerReply)
		{
			TeilnehmerItem tlnItem = new TeilnehmerItem();
			byte[] data = peerReply.Data;
			if (data.Length != PACKETLEN_SYNCREPLY_V3) return null; // invalid packet

			int pos = 0;

			// number
			tlnItem.Number = (int)BitConverter.ToUInt32(data, pos);
			pos += 4;

			// Name
			tlnItem.Name = Encoding.ASCII.GetString(data, pos, 40).TrimEnd(new char[] { '\x00' });
			if (string.IsNullOrEmpty(tlnItem.Name)) tlnItem.Name = null;
			pos += 40;

			// SpecialAttributes
			int specialAttributes = (int)BitConverter.ToUInt16(data, pos);
			tlnItem.Disabled = ((specialAttributes & (int)SpecialAttr.DISABLED) != 0);
			tlnItem.Remove = ((specialAttributes & (int)SpecialAttr.REMOVE) != 0);
			tlnItem.LeadingEntry = ((specialAttributes & (int)SpecialAttr.LEADING_ENTRY) != 0);
			pos += 2;

			// Type
			tlnItem.Type = data[pos];
			pos++;

			// Hostname
			tlnItem.Hostname = Encoding.ASCII.GetString(data, pos, 40).Trim(new char[] { '\x00' });
			if (string.IsNullOrEmpty(tlnItem.Hostname)) tlnItem.Hostname = null;
			pos += 40;

			// IpAaddress
			if (data[pos] == 0 && data[pos + 1] == 0 && data[pos + 2] == 0 && data[pos + 3] == 0)
			{
				tlnItem.IpAddress = "";
			}
			else
			{
				tlnItem.IpAddress = $"{data[pos]}.{data[pos + 1]}.{data[pos + 2]}.{data[pos + 3]}";
			}
			pos += 4;

			// Port
			tlnItem.Port = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// Extension
			tlnItem.Extension = ConvertExtToString(data[pos]);
			if (string.IsNullOrEmpty(tlnItem.Extension)) tlnItem.Extension = null;
			pos++;

			// Pin
			int pin = BitConverter.ToUInt16(data, pos);
			tlnItem.Pin = (pin == 0) ? null : pin;
			pos += 2;

			// Answerback
			tlnItem.Answerback = Encoding.ASCII.GetString(data, pos, 30).Trim(new char[] { '\x00' });
			if (string.IsNullOrEmpty(tlnItem.Answerback)) tlnItem.Answerback = null;
			pos += 30;

			// UserId
			tlnItem.UserId = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// DeviceId
			tlnItem.DeviceId = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// MainNumber
			tlnItem.MainNumber = data[pos] == 1;
			pos++;

			// UpdatedBy
			tlnItem.UpdatedBy = data[pos];
			pos++;

			// ChangedBy
			tlnItem.ChangedBy = data[pos];
			pos++;

			// TimestampUtc / ChangeTimeUtc
			uint ts1 = BitConverter.ToUInt32(data, pos);
			tlnItem.TimestampUtc = CommonHelper.TimestampToDateTimeUtc1900(ts1);
			tlnItem.UpdateTimeUtc = CommonHelper.TimestampToDateTimeUtc1900(ts1);
			pos += 4;

			// CreateTimeUtc
			uint ts2 = BitConverter.ToUInt32(data, pos);
			if (ts2 != 0)
			{
				tlnItem.CreateTimeUtc = CommonHelper.TimestampToDateTimeUtc1900(ts2);
			}
			pos += 4;

			return tlnItem;
		}

		public static BinaryPacket TeilnehmerToSyncReplyV3(TeilnehmerItem tlnItem)
		{
			try
			{
				byte[] data = new byte[PACKETLEN_SYNCREPLY_V3 + 2];
				data[0] = (int)BinaryCommands.SyncReplyV3;
				data[1] = PACKETLEN_SYNCREPLY_V3;
				int pos = 2;

				// Number
				byte[] bytes = BitConverter.GetBytes((UInt32)tlnItem.Number);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				// Name
				if (!string.IsNullOrEmpty(tlnItem.Name))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Name.ExtSubstring(0, 40));
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// SpecialAttributes
				int specialAttributes = 0;
				if (tlnItem.Disabled) specialAttributes |= (int)SpecialAttr.DISABLED;
				if (tlnItem.Remove) specialAttributes |= (int)SpecialAttr.REMOVE;
				if (tlnItem.LeadingEntry) specialAttributes |= (int)SpecialAttr.LEADING_ENTRY;
				bytes = BitConverter.GetBytes((UInt16)specialAttributes);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// Type
				data[pos] = (byte)tlnItem.Type;
				pos++;

				// Hostname
				if (!string.IsNullOrEmpty(tlnItem.Hostname))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Hostname.ExtSubstring(0, 40));
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// IpAddress
				bytes = IpAddressToByteArray(tlnItem.IpAddress);
				if (bytes != null)
				{
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 4;

				// Port
				bytes = BitConverter.GetBytes((UInt16)tlnItem.Port);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// Extension
				data[pos] = ConvertStringToExt(tlnItem.Extension);
				pos++;

				// Pin
				if (tlnItem.Pin != null)
				{
					bytes = BitConverter.GetBytes((UInt16)tlnItem.Pin.Value);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 2;

				// Answerback
				if (!string.IsNullOrEmpty(tlnItem.Answerback))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.Answerback.ExtSubstring(0, 30));
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 30;

				// UserId
				bytes = BitConverter.GetBytes((UInt16)tlnItem.UserId);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// DeviceId
				bytes = BitConverter.GetBytes((UInt16)tlnItem.DeviceId);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// MainNumber
				data[pos] = (byte)(tlnItem.MainNumber ? 1 : 0);
				pos++;

				// UpdatedBy
				data[pos] = (byte)tlnItem.UpdatedBy;
				pos++;

				// ChangedBy
				data[pos] = (byte)tlnItem.ChangedBy;
				pos++;

				// UpdateTimeUtc
				uint ts1 = CommonHelper.DateTimeToTimestampUtc1900(tlnItem.UpdateTimeUtc);
				bytes = BitConverter.GetBytes((UInt32)ts1);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				// CreateTimeUtc
				uint ts2 = CommonHelper.DateTimeToTimestampUtc1900(tlnItem.CreateTimeUtc);
				bytes = BitConverter.GetBytes((UInt32)ts2);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				return new BinaryPacket(data);
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static ResetCentralexClientsItem PacketToResetCentralexClients(BinaryPacket packet)
		{
			const int LEN = 12;

			ResetCentralexClientsItem item = new ResetCentralexClientsItem();
			byte[] data = packet.Data;
			if (data.Length != LEN) return null; // invalid packet

			int pos = 0;

			// skip server pin
			pos += 4;

			// ipaddress
			if (data[pos] == 0 && data[pos + 1] == 0 && data[pos + 2] == 0 && data[pos + 3] == 0)
			{
				item.IpAddress = "";
			}
			else
			{
				item.IpAddress = $"{data[pos]}.{data[pos + 1]}.{data[pos + 2]}.{data[pos + 3]}";
			}
			pos += 4;

			// from port
			item.FromPort = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// to port
			item.ToPort = BitConverter.ToUInt16(data, pos);
			pos += 2;

			return item;
		}

		public static BinaryPacket ResetCentralexClientsToPacket(uint serverPin, ResetCentralexClientsItem item)
		{
			const int LEN = 12;

			byte[] data = new byte[LEN + 2];
			data[0] = (int)BinaryCommands.ResetCentralexClients;
			data[1] = LEN;

			int pos = 2;

			// server pin
			byte[] bytes = BitConverter.GetBytes((UInt32)serverPin);
			Buffer.BlockCopy(bytes, 0, data, pos, 4);
			pos += 4;

			bytes = IpAddressToByteArray(item.IpAddress);
			if (bytes != null)
			{
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
			}
			pos += 4;

			// fromPort
			bytes = BitConverter.GetBytes((UInt16)item.FromPort);
			Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
			pos += 2;

			// toPort
			bytes = BitConverter.GetBytes((UInt16)item.ToPort);
			Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
			pos += 2;

			return new BinaryPacket(data);
		}



#if false

		public static TeilnehmerItem SyncDataV1ToTeilnehmer(BinaryPacket peerReply)
		{
			TeilnehmerItem tlnItem = new TeilnehmerItem();
			byte[] data = peerReply.Data;
			if (data.Length != PACKETLEN_PEERREPLY_V1) return null; // invalid packet

			int pos = 0;

			// number
			tlnItem.number = (int)BitConverter.ToUInt32(data, pos);
			pos += 4;

			// name
			tlnItem.name = Encoding.ASCII.GetString(data, pos, 40).TrimEnd(new char[] { '\x00' });
			pos += 40;

			int specialAttribute = (int)BitConverter.ToUInt16(data, pos);
			tlnItem.disabled = ((specialAttribute & 0x0002) != 0);
			pos += 2;

			// type
			tlnItem.type = data[pos];
			pos++;

			// hostname
			tlnItem.hostname = Encoding.ASCII.GetString(data, pos, 40).Trim(new char[] { '\x00' });
			pos += 40;

			// ipaddress
			if (data[pos] == 0 && data[pos + 1] == 0 && data[pos + 2] == 0 && data[pos + 3] == 0)
			{
				tlnItem.ipaddress = "";
			}
			else
			{
				tlnItem.ipaddress = $"{data[pos]}.{data[pos + 1]}.{data[pos + 2]}.{data[pos + 3]}";
			}
			pos += 4;

			// port
			tlnItem.port = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// extension
			tlnItem.extension = ConvertExtToString(data[pos]);
			pos++;

			// pin
			tlnItem.pin = BitConverter.ToUInt16(data, pos);
			pos += 2;

			// answerback
			//tlnItem.answerback = Encoding.ASCII.GetString(data, pos, 40).TrimEnd(new char[] { '\x00' });
			pos += 40;

			// timestamp
			uint ts = BitConverter.ToUInt32(data, pos);
			tlnItem.timestamp = CommonHelper.TimestampToDateTimeUtc1900(ts);
			pos += 4;

			return tlnItem;
		}

		public static BinaryPacket TeilnehmerToSyncDataV1(TeilnehmerItem tlnItem, bool sync)
		{
			try
			{
				byte[] data = new byte[PACKETLEN_PEERREPLY_V1+2];
				data[0] = (int)BinaryCommands.SyncDataV1;
				data[1] = PACKETLEN_PEERREPLY_V1;
				int pos = 2;

				// number
				byte[] bytes = BitConverter.GetBytes((UInt32)tlnItem.number);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				// name
				if (!string.IsNullOrEmpty(tlnItem.name))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.name);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// special attributes
				if (sync)
				{
					int specialAttributes = 0;
					if (tlnItem.disabled.GetValueOrDefault()) specialAttributes |= 0x0002;
					bytes = BitConverter.GetBytes((UInt16)specialAttributes);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 2;

				// type
				data[pos] = (byte)tlnItem.type.GetValueOrDefault();
				pos++;

				// hostname
				if (!string.IsNullOrEmpty(tlnItem.hostname))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.hostname);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 40;

				// ipaddress
				bytes = IpAddressToByteArray(tlnItem.ipaddress);
				if (bytes != null)
				{
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				pos += 4;

				// port
				bytes = BitConverter.GetBytes((UInt16)tlnItem.port);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 2;

				// extension
				data[pos] = ConvertStringToExt(tlnItem.extension);
				pos++;

				// pin
				if (sync)
				{
					if (tlnItem.pin != null)
					{
						bytes = BitConverter.GetBytes((UInt16)tlnItem.pin.Value);
						Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
					}
				}
				pos += 2;

				/*
				// answerback
				if (!string.IsNullOrEmpty(tlnItem.answerback))
				{
					bytes = Encoding.ASCII.GetBytes(tlnItem.answerback);
					Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				}
				*/
				pos += 30;

				// timestamp
				uint ts = tlnItem.timestamp.HasValue ? CommonHelper.DateTimeToTimestampUtc1900(tlnItem.timestamp.Value) : 0;
				bytes = BitConverter.GetBytes((UInt32)ts);
				Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
				pos += 4;

				return new BinaryPacket(data);
			}
			catch (Exception)
			{
				return null;
			}
		}
#endif

		private static byte[] IpAddressToByteArray(string ipAddr)
		{
			if (string.IsNullOrEmpty(ipAddr)) return null;

			try
			{
				IPAddress ip = IPAddress.Parse(ipAddr);
				byte[] data = new byte[4];
				return ip.GetAddressBytes();
			}
			catch(Exception)
			{
				return null;
			}
		}

		private static string ConvertExtToString(byte extNum)
		{
			string extStr = "";
			if (extNum == 0)
			{
				extStr = "";
			}
			else if (extNum >= 1 && extNum <= 99)
			{
				extStr = extNum.ToString("D2");
			}
			else if (extNum == 100)
			{
				extStr = "00";
			}
			else if (extNum >= 101 && extNum <= 109)
			{
				extStr = (extNum - 100).ToString();
			}
			else if (extNum == 110)
			{
				extStr = "0";
			}
			else if (extNum > 110)
			{
				extStr = ""; // invalid
			}
			return extStr;
		}

		private static byte ConvertStringToExt(string extStr)
		{
			if (!int.TryParse(extStr, out int extNum))
			{
				extNum = 0; // invalid
			}
			else if (extStr == "00")
			{
				extNum = 100;
			}
			else if (extStr == "0")
			{
				extNum = 110;
			}
			else if (extStr.Length == 1)
			{
				extNum += 100;
			}
			else if (extStr.Length == 2)
			{
				// nothing todo
			}
			else if (extStr.Length >= 3)
			{
				extNum = 0; // invalid
			}
			return (byte)extNum;
		}

		public override string ToString()
		{
			return $"{CommandType} {DataLen}: {GetDebugData()}";
		}
	}
}
