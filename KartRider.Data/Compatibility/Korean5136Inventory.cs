using KartRider.Common.Network;
using KartRider.IO.Packet;
using Profile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace KartRider.Compatibility;

/// <summary>
/// P5136 rider inventory preload.
///
/// The original HF_5136 server pushes the complete legacy inventory while
/// handling PqGetRider and sends PrGetRider only after the stream is queued.
/// Its item chunks always carry the scalar header 1/1, even when a category
/// needs more than one physical packet.
/// </summary>
internal static class Korean5136Inventory
{
    private const int ItemChunkSize = 100;
    private const int ExcDataChunkSize = 100;

    internal static void Send(SessionGroup parent, string nickname)
    {
        var config = ProfileService.GetProfileConfig(nickname);
        InventoryPlan plan;
        string status = "full";

        try
        {
            plan = BuildPlan(
                FileName.ProfileDir,
                config.Rider.SlotChanger,
                Program.PreventItem);
        }
        catch (Exception exception)
        {
            status = $"fallback:{exception.GetType().Name}";
            PacketTrace.LogEvent(
                "TCP",
                "P5136-INVENTORY-BUILD-ERROR",
                parent.Client.GetLocalEndPoint(),
                parent.Client.GetRemoteEndPoint(),
                nickname,
                exception.Message);
            plan = BuildEquippedFallback(config.RiderItem, Program.PreventItem);
        }

        foreach (List<PartsExcRecord> chunk in plan.PartsExcPackets)
        {
            using OutPacket packet = new OutPacket("LoRpGetRiderExcDataPacket");
            WritePartsExcPacketBody(packet, chunk, ReferenceEquals(chunk, plan.PartsExcPackets[0]));
            parent.Client.Send(packet);
        }

        foreach (List<RiderItemRecord> chunk in plan.ItemPackets)
        {
            using OutPacket packet = new OutPacket("LoRpGetRiderItemPacket");
            WriteRiderItemPacketBody(packet, chunk);
            parent.Client.Send(packet);
        }

        PacketTrace.LogEvent(
            "TCP",
            "P5136-INVENTORY-PRELOAD",
            parent.Client.GetLocalEndPoint(),
            parent.Client.GetRemoteEndPoint(),
            nickname,
            $"status={status}; excPackets={plan.PartsExcPackets.Count}; excRecords={plan.PartsExcRecordCount}; " +
            $"itemPackets={plan.ItemPackets.Count}; itemRecords={plan.ItemRecordCount}");
    }

    private static InventoryPlan BuildPlan(string profileDirectory, ushort slotChanger, bool preventItem)
    {
        string itemPath = Path.Combine(profileDirectory, "Item.xml");
        string kartPath = Path.Combine(profileDirectory, "NewKart.xml");
        if (!File.Exists(itemPath))
        {
            throw new FileNotFoundException("P5136 Item.xml was not found.", itemPath);
        }
        if (!File.Exists(kartPath))
        {
            throw new FileNotFoundException("P5136 NewKart.xml was not found.", kartPath);
        }

        XmlDocument itemDocument = LoadXml(itemPath);
        XmlDocument kartDocument = LoadXml(kartPath);
        var plan = new InventoryPlan();

        // HF_5136 sends Tune, Plant, Level, and Parts exception streams first.
        // The bundled P5136 profile has only PartsData.xml, so the other three
        // streams contain no records and produce no packets.
        AddPartsExcPackets(plan, Path.Combine(profileDirectory, "PartsData.xml"));

        AddXmlCategory(plan, itemDocument, "Pet", 21, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "FlyingPet", 52, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Character", 1, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "BonusCard", 32, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "HandGearL", 16, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "HeadBand", 11, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Goggle", 8, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Balloon", 9, AmountMode.SlotChanger, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Tachometer", 61, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "SlotItem", 7, AmountMode.SlotChangerExceptThreeAndFour, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "MyRoom", 28, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "InitialCard", 22, AmountMode.SlotChanger, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Card", 23, AmountMode.SlotChanger, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "HeadPhone", 12, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "ReplayTicket", 13, AmountMode.SlotChanger, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Uniform", 18, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Decal", 20, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Plate", 4, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "RidColor", 31, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "SkidMark", 27, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Aura", 26, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Dye", 70, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "Paint", 2, AmountMode.One, slotChanger, preventItem);

        AddNormalRangePacket(plan, 43, 1, 23, slotChanger);
        AddNormalRangePacket(plan, 44, 1, 15, slotChanger);
        AddNormalRangePacket(plan, 45, 1, 23, slotChanger);
        AddNormalRangePacket(plan, 46, 1, 30, slotChanger);
        AddNormalRangePacket(plan, 37, 1, 1, slotChanger);
        AddNormalRangePacket(plan, 38, 3, 6, slotChanger);
        AddNormalRangePacket(plan, 49, 1, 1, slotChanger);
        AddNormalRangePacket(plan, 53, 1, 1, slotChanger);
        AddNormalRangePacket(plan, 39, 1, 1, slotChanger);

        AddPartsPacket(plan, 1, 1, 1053, 1053, 1053, 1053, 1080, 3, slotChanger);
        AddPartsPacket(plan, 1, 2, 1005, 1005, 1005, 1005, 1050, 5, slotChanger);
        AddPartsPacket(plan, 1, 3, 910, 910, 910, 910, 1000, 10, slotChanger);
        AddPartsPacket(plan, 1, 4, 810, 810, 810, 810, 900, 10, slotChanger);
        AddPartsPacket(plan, 2, 1, 1153, 1053, 1153, 1053, 1180, 3, slotChanger);
        AddPartsPacket(plan, 2, 2, 1105, 1005, 1105, 1005, 1150, 5, slotChanger);
        AddPartsPacket(plan, 2, 3, 1010, 910, 1010, 910, 1100, 10, slotChanger);
        AddPartsPacket(plan, 2, 4, 910, 810, 910, 810, 1000, 10, slotChanger);

        AddXmlCategory(plan, itemDocument, "V1EffectData", 68, AmountMode.SlotChanger, slotChanger, preventItem);
        AddXmlCategory(plan, itemDocument, "V1BoosterEffectData", 69, AmountMode.SlotChanger, slotChanger, preventItem);
        AddNormalRangePacket(plan, 67, 1, 2, slotChanger);
        AddXmlCategory(plan, itemDocument, "upgradeKit", 14, AmountMode.One, slotChanger, preventItem);
        AddXmlCategory(plan, kartDocument, "Kart", 3, AmountMode.One, slotChanger, preventItem, readSerial: true);

        return plan;
    }

    private static InventoryPlan BuildEquippedFallback(RiderItemData item, bool preventItem)
    {
        var plan = new InventoryPlan();
        AddEquipped(plan, 1, item.Set_Character, 0, preventItem);
        AddEquipped(plan, 2, item.Set_Paint, 0, preventItem);
        AddEquipped(plan, 3, item.Set_Kart, item.Set_KartSN, preventItem);
        AddEquipped(plan, 4, item.Set_Plate, 0, preventItem);
        AddEquipped(plan, 8, item.Set_Goggle, 0, preventItem);
        AddEquipped(plan, 9, item.Set_Balloon, 0, preventItem);
        AddEquipped(plan, 11, item.Set_HeadBand, 0, preventItem);
        AddEquipped(plan, 12, item.Set_HeadPhone, 0, preventItem);
        AddEquipped(plan, 16, item.Set_HandGearL, 0, preventItem);
        AddEquipped(plan, 18, item.Set_Uniform, 0, preventItem);
        AddEquipped(plan, 20, item.Set_Decal, 0, preventItem);
        AddEquipped(plan, 21, item.Set_Pet, 0, preventItem);
        AddEquipped(plan, 26, item.Set_Aura, 0, preventItem);
        AddEquipped(plan, 27, item.Set_SkidMark, 0, preventItem);
        AddEquipped(plan, 31, item.Set_RidColor, 0, preventItem);
        AddEquipped(plan, 32, item.Set_BonusCard, 0, preventItem);
        AddEquipped(plan, 52, item.Set_FlyingPet, 0, preventItem);
        AddEquipped(plan, 61, item.Set_Tachometer, 0, preventItem);
        AddEquipped(plan, 70, item.Set_Dye, 0, preventItem);
        return plan;
    }

    private static void AddEquipped(
        InventoryPlan plan,
        ushort category,
        ushort id,
        ushort serial,
        bool preventItem)
    {
        if (id == 0)
        {
            return;
        }

        plan.AddItemPacket(new List<RiderItemRecord>
        {
            RiderItemRecord.Normal(category, id, serial, 1, preventItem)
        });
    }

    private static void AddXmlCategory(
        InventoryPlan plan,
        XmlDocument document,
        string tagName,
        ushort category,
        AmountMode amountMode,
        ushort slotChanger,
        bool preventItem,
        bool readSerial = false)
    {
        XmlNodeList nodes = document.GetElementsByTagName(tagName);
        var records = new List<RiderItemRecord>(nodes.Count);
        foreach (XmlNode node in nodes)
        {
            if (node is not XmlElement element)
            {
                continue;
            }

            ushort id = ParseUShort(element, "id");
            ushort serial = readSerial && element.HasAttribute("sn")
                ? ParseUShort(element, "sn")
                : (ushort)0;
            ushort amount = amountMode switch
            {
                AmountMode.SlotChanger => slotChanger,
                AmountMode.SlotChangerExceptThreeAndFour when id != 3 && id != 4 => slotChanger,
                _ => 1
            };
            records.Add(RiderItemRecord.Normal(category, id, serial, amount, preventItem));
        }

        AddChunkedItemPackets(plan, records);
    }

    private static void AddNormalRangePacket(
        InventoryPlan plan,
        ushort category,
        ushort firstId,
        ushort lastId,
        ushort amount)
    {
        var records = new List<RiderItemRecord>(lastId - firstId + 1);
        for (ushort id = firstId; id <= lastId; id++)
        {
            records.Add(RiderItemRecord.Normal(category, id, 0, amount, false));
        }
        plan.AddItemPacket(records);
    }

    private static void AddPartsPacket(
        InventoryPlan plan,
        ushort itemId,
        byte grade,
        short start63,
        short start64,
        short start65,
        short start66,
        short end,
        short step,
        ushort amount)
    {
        var records = new List<RiderItemRecord>(40);
        AddPartsRange(records, 63, itemId, grade, start63, end, step, amount);
        AddPartsRange(records, 64, itemId, grade, start64, (short)(end - start63 + start64), step, amount);
        AddPartsRange(records, 65, itemId, grade, start65, (short)(end - start63 + start65), step, amount);
        AddPartsRange(records, 66, itemId, grade, start66, (short)(end - start63 + start66), step, amount);
        plan.AddItemPacket(records);
    }

    private static void AddPartsRange(
        List<RiderItemRecord> records,
        ushort category,
        ushort itemId,
        byte grade,
        short firstValue,
        short lastValue,
        short step,
        ushort amount)
    {
        for (short value = firstValue; value <= lastValue; value += step)
        {
            records.Add(RiderItemRecord.Part(category, itemId, amount, grade, value));
        }
    }

    private static void AddChunkedItemPackets(InventoryPlan plan, List<RiderItemRecord> records)
    {
        for (int offset = 0; offset < records.Count; offset += ItemChunkSize)
        {
            int count = Math.Min(ItemChunkSize, records.Count - offset);
            plan.AddItemPacket(records.GetRange(offset, count));
        }
    }

    private static void AddPartsExcPackets(InventoryPlan plan, string partsPath)
    {
        if (!File.Exists(partsPath))
        {
            return;
        }

        XmlDocument document = LoadXml(partsPath);
        XmlNodeList nodes = document.GetElementsByTagName("Kart");
        var records = new List<PartsExcRecord>(nodes.Count);
        foreach (XmlNode node in nodes)
        {
            if (node is not XmlElement element)
            {
                continue;
            }

            records.Add(new PartsExcRecord(
                ParseShort(element, "id"),
                ParseShort(element, "sn"),
                ParseShort(element, "Item_Id1"),
                ParseByte(element, "Grade1"),
                ParseShort(element, "PartsValue1"),
                ParseShort(element, "Item_Id2"),
                ParseByte(element, "Grade2"),
                ParseShort(element, "PartsValue2"),
                ParseShort(element, "Item_Id3"),
                ParseByte(element, "Grade3"),
                ParseShort(element, "PartsValue3"),
                ParseShort(element, "Item_Id4"),
                ParseByte(element, "Grade4"),
                ParseShort(element, "PartsValue4"),
                ParseShort(element, "partsCoating"),
                ParseShort(element, "partsTailLamp")));
        }

        for (int offset = 0; offset < records.Count; offset += ExcDataChunkSize)
        {
            int count = Math.Min(ExcDataChunkSize, records.Count - offset);
            plan.AddPartsExcPacket(records.GetRange(offset, count));
        }
    }

    private static void WriteRiderItemPacketBody(
        OutPacket packet,
        IReadOnlyList<RiderItemRecord> records)
    {
        packet.WriteInt(1);
        packet.WriteInt(1);
        packet.WriteInt(records.Count);
        foreach (RiderItemRecord record in records)
        {
            packet.WriteUShort(record.Category);
            packet.WriteUShort(record.Id);
            packet.WriteUShort(record.Serial);
            packet.WriteUShort(record.Amount);
            packet.WriteByte(record.Prevent);
            packet.WriteByte(record.Reserved);
            packet.WriteShort(record.ExpirationLow);
            packet.WriteShort(record.ExpirationHigh);
            packet.WriteByte(record.PartFlag);
            packet.WriteByte(record.Grade);
            packet.WriteShort(record.Value);
        }
    }

    private static void WritePartsExcPacketBody(
        OutPacket packet,
        IReadOnlyList<PartsExcRecord> records,
        bool firstChunk)
    {
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteByte(firstChunk ? (byte)1 : (byte)0);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteInt(0);
        packet.WriteInt(0);
        packet.WriteInt(0);
        packet.WriteInt(records.Count);

        foreach (PartsExcRecord record in records)
        {
            packet.WriteShort(record.Id);
            packet.WriteShort(record.Serial);
            packet.WriteShort(0);
            packet.WriteShort(-1);
            packet.WriteShort(0);
            packet.WriteShort(record.Engine);
            packet.WriteByte(record.EngineGrade);
            packet.WriteShort(record.EngineValue);
            packet.WriteShort(record.Handle);
            packet.WriteByte(record.HandleGrade);
            packet.WriteShort(record.HandleValue);
            packet.WriteShort(record.Wheel);
            packet.WriteByte(record.WheelGrade);
            packet.WriteShort(record.WheelValue);
            packet.WriteShort(record.Booster);
            packet.WriteByte(record.BoosterGrade);
            packet.WriteShort(record.BoosterValue);
            packet.WriteShort(record.Coating);
            packet.WriteByte(0);
            packet.WriteShort(0);
            packet.WriteShort(record.TailLamp);
            packet.WriteByte(0);
            packet.WriteShort(0);
        }

        packet.WriteInt(0);
        packet.WriteInt(0);
    }

    private static XmlDocument LoadXml(string path)
    {
        var document = new XmlDocument();
        document.Load(path);
        return document;
    }

    private static ushort ParseUShort(XmlElement element, string attribute) =>
        ushort.Parse(element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static short ParseShort(XmlElement element, string attribute) =>
        short.Parse(element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static byte ParseByte(XmlElement element, string attribute) =>
        byte.Parse(element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private enum AmountMode
    {
        One,
        SlotChanger,
        SlotChangerExceptThreeAndFour
    }

    private sealed class InventoryPlan
    {
        public List<List<PartsExcRecord>> PartsExcPackets { get; } = new List<List<PartsExcRecord>>();
        public List<List<RiderItemRecord>> ItemPackets { get; } = new List<List<RiderItemRecord>>();
        public int PartsExcRecordCount { get; private set; }
        public int ItemRecordCount { get; private set; }

        public void AddPartsExcPacket(List<PartsExcRecord> packet)
        {
            PartsExcPackets.Add(packet);
            PartsExcRecordCount += packet.Count;
        }

        public void AddItemPacket(List<RiderItemRecord> packet)
        {
            if (packet.Count == 0)
            {
                return;
            }
            ItemPackets.Add(packet);
            ItemRecordCount += packet.Count;
        }
    }

    private readonly struct RiderItemRecord
    {
        private RiderItemRecord(
            ushort category,
            ushort id,
            ushort serial,
            ushort amount,
            byte prevent,
            byte reserved,
            short expirationLow,
            short expirationHigh,
            byte partFlag,
            byte grade,
            short value)
        {
            Category = category;
            Id = id;
            Serial = serial;
            Amount = amount;
            Prevent = prevent;
            Reserved = reserved;
            ExpirationLow = expirationLow;
            ExpirationHigh = expirationHigh;
            PartFlag = partFlag;
            Grade = grade;
            Value = value;
        }

        public ushort Category { get; }
        public ushort Id { get; }
        public ushort Serial { get; }
        public ushort Amount { get; }
        public byte Prevent { get; }
        public byte Reserved { get; }
        public short ExpirationLow { get; }
        public short ExpirationHigh { get; }
        public byte PartFlag { get; }
        public byte Grade { get; }
        public short Value { get; }

        public static RiderItemRecord Normal(
            ushort category,
            ushort id,
            ushort serial,
            ushort amount,
            bool preventItem) =>
            new RiderItemRecord(
                category,
                id,
                serial,
                amount,
                preventItem ? (byte)1 : (byte)0,
                0,
                -1,
                0,
                0,
                0,
                0);

        public static RiderItemRecord Part(
            ushort category,
            ushort id,
            ushort amount,
            byte grade,
            short value) =>
            new RiderItemRecord(category, id, 0, amount, 0, 0, -1, -1, 1, grade, value);
    }

    private readonly struct PartsExcRecord
    {
        public PartsExcRecord(
            short id,
            short serial,
            short engine,
            byte engineGrade,
            short engineValue,
            short handle,
            byte handleGrade,
            short handleValue,
            short wheel,
            byte wheelGrade,
            short wheelValue,
            short booster,
            byte boosterGrade,
            short boosterValue,
            short coating,
            short tailLamp)
        {
            Id = id;
            Serial = serial;
            Engine = engine;
            EngineGrade = engineGrade;
            EngineValue = engineValue;
            Handle = handle;
            HandleGrade = handleGrade;
            HandleValue = handleValue;
            Wheel = wheel;
            WheelGrade = wheelGrade;
            WheelValue = wheelValue;
            Booster = booster;
            BoosterGrade = boosterGrade;
            BoosterValue = boosterValue;
            Coating = coating;
            TailLamp = tailLamp;
        }

        public short Id { get; }
        public short Serial { get; }
        public short Engine { get; }
        public byte EngineGrade { get; }
        public short EngineValue { get; }
        public short Handle { get; }
        public byte HandleGrade { get; }
        public short HandleValue { get; }
        public short Wheel { get; }
        public byte WheelGrade { get; }
        public short WheelValue { get; }
        public short Booster { get; }
        public byte BoosterGrade { get; }
        public short BoosterValue { get; }
        public short Coating { get; }
        public short TailLamp { get; }
    }
}
