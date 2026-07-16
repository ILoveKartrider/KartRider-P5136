using System;
using KartRider.Common.Security;
using KartRider.IO;

namespace KartRider;

internal static class GameSupport
{
	public static void PcFirstMessage(SessionGroup session)
	{
		uint num = 2919676295u;
		uint num2 = 263300380u;
		using (OutPacket outPacket = new OutPacket("PcFirstMessage"))
		{
			outPacket.WriteUShort(SessionGroup.usLocale);
			outPacket.WriteUShort(1);
			outPacket.WriteUShort(Program.Version);
			outPacket.WriteString("http://kart.dn.nexoncdn.co.kr/patch");
			outPacket.WriteUInt(num);
			outPacket.WriteUInt(num2);
			outPacket.WriteByte(SessionGroup.nClientLoc);
			outPacket.WriteHexString("00 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 00 2E 31 2E 31 37 2E 36 00 00 00 00 00 00 00");
			session.Client.Send(outPacket);
		}
		session.Client._RIV = num ^ num2;
		session.Client._SIV = num ^ num2;
	}

	public static void OnDisconnect(SessionGroup session)
	{
		session.Client.Disconnect();
	}

	public static void SpRpLotteryPacket(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("SpRpLotteryPacket");
		outPacket.WriteHexString("05 00 00 00 00 00 00 00 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00");
		session.Client.Send(outPacket);
	}

	public static void PrGetRiderInfo(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("PrGetRiderInfo");
		outPacket.WriteByte(1);
		outPacket.WriteString(session.Profile.Nickname);
		outPacket.WriteString(session.Profile.Nickname);
		outPacket.WriteHexString("2A 97 00 00");
		outPacket.WriteByte(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteString("");
		outPacket.WriteInt(session.Profile.RP);
		outPacket.WriteByte(0);
		outPacket.WriteByte(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteByte(0);
		outPacket.WriteByte(0);
		session.Client.Send(outPacket);
	}

	public static void ChRequestChStaticReplyPacket(SessionGroup session)
	{
		// The 2005 client expects the original two-table channel catalog.  In
		// particular, id 1 must resolve to "novice" and each lobby tab name must
		// map to a byte id before it can send PqChannelSwitch.
		(byte Id, string Name)[] groups =
		{
			(1, "novice"),
			(2, "rookieIntro"),
			(3, "rookie"),
			(4, "lv3"),
			(5, "lv2"),
			(6, "arena"),
			(7, "event")
		};

		using OutPacket channelData = new OutPacket();
		channelData.WriteInt(groups.Length);
		foreach ((byte id, string name) in groups)
		{
			channelData.WriteByte(id);
			channelData.WriteString(name);
		}

		channelData.WriteInt(groups.Length);
		for (ushort index = 0; index < groups.Length; index++)
		{
			(byte groupId, string groupName) = groups[index];
			channelData.WriteUShort((ushort)(index + 1));
			channelData.WriteString($"{groupName}_4");
			channelData.WriteByte(groupId);
			channelData.WriteUInt(0);
		}

		byte[] encoded = KREncodedBlock.Encode(
			channelData.ToArray(),
			KREncodedBlock.EncodeFlag.ZLib,
			null);

		using OutPacket outPacket = new OutPacket("ChRequestChStaticReplyPacket");
		outPacket.WriteBool(true);
		outPacket.WriteInt(encoded.Length);
		outPacket.WriteBytes(encoded);
		session.Client.Send(outPacket);
		LegacyPacketTrace.LogEvent(
			$"[2005 MP] Sent 2005 channel static catalog " +
			$"({groups.Length} groups, {encoded.Length} encoded bytes).");
	}

	public static void PrDynamicCommand(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("PrDynamicCommand");
		outPacket.WriteByte(0);
		outPacket.WriteInt();
		session.Client.Send(outPacket);
	}

	public static void PrLogin(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("PrLogin");
		outPacket.WriteInt();
		outPacket.WriteHexString(Program.DataTime);
		outPacket.WriteUInt(session.Profile.UserNo);
		outPacket.WriteString(session.Profile.UserId);
		outPacket.WriteByte(byte.MaxValue);
		outPacket.WriteByte(0);
		outPacket.WriteByte(0);
		outPacket.WriteByte(0);
		outPacket.WriteInt();
		outPacket.WriteByte(0);
		uint effectivePMap = session.Profile.PMap;
		if (LegacyObserverPolicy.IsObserver(session.Profile))
		{
			effectivePMap |= 0x40u;
			LegacyPacketTrace.LogEvent(
				$"[2005 OBSERVER] Enabled login capability username=" +
				$"'{session.Profile.SourceUsername}', pmap=0x{effectivePMap:X8}.");
		}
		outPacket.WriteUInt(effectivePMap);
		outPacket.WriteShort(-1);
		outPacket.WriteShort(0);
		outPacket.WriteByte(0);
		outPacket.WriteInt();
		outPacket.WriteInt();
		outPacket.WriteInt();
		outPacket.WriteByte(0);
		outPacket.WriteByte(1);
		outPacket.WriteString("content");
		outPacket.WriteInt(0);
		outPacket.WriteInt(1);
		outPacket.WriteString("cc");
		outPacket.WriteString("kr");
		outPacket.WriteInt(1);
		outPacket.WriteString("content");
		outPacket.WriteInt(0);
		outPacket.WriteInt(3);
		outPacket.WriteString("name");
		outPacket.WriteString("multiplay");
		outPacket.WriteString("enable");
		outPacket.WriteString("true");
		outPacket.WriteString("visible");
		outPacket.WriteString("true");
		outPacket.WriteInt(0);
		session.Client.Send(outPacket);
	}
}
