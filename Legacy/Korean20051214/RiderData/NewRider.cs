using KartRider;
using KartRider.IO;

namespace RiderData;

internal static class NewRider
{
	public static short Value1 = 30000;

	public static short Value2 = 1;

	public static void LoadItemData(SessionGroup session)
	{
		Kart(session);
		Character(session);
		HeadBand(session);
		Goggle(session);
		Balloon(session);
		SlotItem(session);
		Plate(session);
		Paint(session);
	}

	public static void NewRiderData(SessionGroup session)
	{
		LegacySessionProfile profile = session.Profile;
		LegacyEquipment equipment = profile.Equipment;
		using OutPacket outPacket = new OutPacket("LoRpGetRiderPacket");
		outPacket.WriteByte(1);
		outPacket.WriteString(profile.Nickname);
		outPacket.WriteInt();
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteByte(profile.LicenseLevel);
		foreach (ushort completionMask in profile.GetLicenseCompletionMasks())
		{
			outPacket.WriteUShort(completionMask);
		}
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteShort(0);
		outPacket.WriteUShort(equipment.Character);
		outPacket.WriteUShort(equipment.Paint);
		outPacket.WriteUShort(equipment.Kart);
		outPacket.WriteUShort(equipment.Plate);
		outPacket.WriteUShort(equipment.Goggle);
		outPacket.WriteUShort(equipment.Balloon);
		outPacket.WriteUShort(equipment.Reserved0);
		outPacket.WriteUShort(equipment.HeadBand);
		outPacket.WriteUShort(equipment.Reserved1);
		outPacket.WriteString(profile.RiderIntro);
		outPacket.WriteUInt(profile.Lucci);
		outPacket.WriteInt(profile.RP);
		for (int i = 0; i < 4; i++)
		{
			outPacket.WriteInt();
		}
		session.Client.Send(outPacket);
	}

	public static void Kart(SessionGroup session)
	{
		int num = 72;
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		outPacket.WriteInt(num);
		for (short num2 = 1; num2 <= num; num2++)
		{
			outPacket.WriteShort(3);
			outPacket.WriteShort(num2);
			outPacket.WriteShort(Value2);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void Paint(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		int num = 11;
		outPacket.WriteInt(num);
		for (short num2 = 1; num2 <= num; num2++)
		{
			outPacket.WriteShort(2);
			outPacket.WriteShort(num2);
			outPacket.WriteShort(Value2);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void Character(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		int num = 16;
		outPacket.WriteInt(num);
		for (short num2 = 1; num2 <= num; num2++)
		{
			outPacket.WriteShort(1);
			outPacket.WriteShort(num2);
			outPacket.WriteShort(Value2);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void Plate(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		int num = 43;
		outPacket.WriteInt(num);
		for (short num2 = 1; num2 <= num; num2++)
		{
			outPacket.WriteShort(4);
			outPacket.WriteShort(num2);
			outPacket.WriteShort(Value2);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void Balloon(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		outPacket.WriteInt(25);
		for (short num = 1; num <= 25; num++)
		{
			outPacket.WriteShort(9);
			outPacket.WriteShort(num);
			outPacket.WriteShort(Value1);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void Goggle(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		int num = 5;
		outPacket.WriteInt(num);
		for (short num2 = 1; num2 <= num; num2++)
		{
			outPacket.WriteShort(8);
			outPacket.WriteShort(num2);
			outPacket.WriteShort(Value2);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void HeadBand(SessionGroup session)
	{
		using OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket");
		int num = 6;
		outPacket.WriteInt(num);
		for (short num2 = 1; num2 <= num; num2++)
		{
			outPacket.WriteShort(11);
			outPacket.WriteShort(num2);
			outPacket.WriteShort(Value1);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
		}
		session.Client.Send(outPacket);
	}

	public static void SlotItem(SessionGroup session)
	{
		using (OutPacket outPacket = new OutPacket("LoRpGetRiderItemPacket"))
		{
			outPacket.WriteInt(1);
			outPacket.WriteShort(7);
			outPacket.WriteShort(1);
			outPacket.WriteShort(session.Profile.SlotChanger);
			outPacket.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket.WriteByte(0);
			outPacket.WriteShort(-1);
			outPacket.WriteShort(0);
			session.Client.Send(outPacket);
		}
		using (OutPacket outPacket2 = new OutPacket("LoRpGetRiderItemPacket"))
		{
			outPacket2.WriteInt(1);
			outPacket2.WriteShort(13);
			outPacket2.WriteShort(1);
			outPacket2.WriteShort(1);
			outPacket2.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket2.WriteByte(0);
			outPacket2.WriteShort(-1);
			outPacket2.WriteShort(0);
			session.Client.Send(outPacket2);
		}
		using (OutPacket outPacket3 = new OutPacket("LoRpGetRiderItemPacket"))
		{
			int num = 2;
			outPacket3.WriteInt(num);
			for (short num2 = 1; num2 <= num; num2++)
			{
				outPacket3.WriteShort(6);
				outPacket3.WriteShort(num2);
				outPacket3.WriteShort(1);
				outPacket3.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
				outPacket3.WriteByte(0);
				outPacket3.WriteShort(-1);
				outPacket3.WriteShort(0);
			}
			session.Client.Send(outPacket3);
		}
		using (OutPacket outPacket4 = new OutPacket("LoRpGetRiderItemPacket"))
		{
			int num3 = 2;
			outPacket4.WriteInt(num3);
			for (short num4 = 1; num4 <= num3; num4++)
			{
				outPacket4.WriteShort(10);
				outPacket4.WriteShort(num4);
				outPacket4.WriteShort(1);
				outPacket4.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
				outPacket4.WriteByte(0);
				outPacket4.WriteShort(-1);
				outPacket4.WriteShort(0);
			}
			session.Client.Send(outPacket4);
		}
		using OutPacket outPacket5 = new OutPacket("LoRpGetRiderItemPacket");
		int num5 = 6;
		outPacket5.WriteInt(num5);
		for (short num6 = 1; num6 <= num5; num6++)
		{
			outPacket5.WriteShort(14);
			outPacket5.WriteShort(num6);
			outPacket5.WriteShort(1);
			outPacket5.WriteByte(Program.PreventItem ? ((byte)1) : ((byte)0));
			outPacket5.WriteByte(0);
			outPacket5.WriteShort(-1);
			outPacket5.WriteShort(0);
		}
		session.Client.Send(outPacket5);
	}
}
