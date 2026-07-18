using KartRider.Common.Network;
using KartRider.IO.Packet;
using ExcData;
using Profile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private static readonly ushort[] PrePartsCategoryOrder =
    {
        21, 52, 1, 32, 16, 11, 8, 9, 61, 7, 28, 22, 23, 12, 13, 18,
        20, 4, 31, 27, 26, 70, 2, 30, 36, 55, 59, 43, 44, 45, 46, 37,
        38, 49, 53, 39
    };
    private static readonly ushort[] PostPartsCategoryOrder = { 68, 69, 67, 14, 3 };
    private static readonly HashSet<ushort> UnitAmountCategories = new HashSet<ushort>
    {
        1, 2, 3, 4, 8, 11, 12, 14, 16, 18, 20, 21, 26, 27, 28, 30,
        31, 32, 52, 61, 70
    };

    internal static void Send(SessionGroup parent, string nickname)
    {
        var config = ProfileService.GetProfileConfig(nickname);
        InventoryPlan plan;

        try
        {
            if (!FileName.FileNames.ContainsKey(nickname))
            {
                FileName.Load(nickname);
            }

            plan = BuildPlan(
                FileName.ProfileDir,
                config.Rider.SlotChanger,
                Program.PreventItem,
                FileName.FileNames[nickname].PlantData_LoadFile);
        }
        catch (Exception exception)
        {
            PacketTrace.LogEvent(
                "TCP",
                "P5136-INVENTORY-BUILD-ERROR",
                parent.Client.GetLocalEndPoint(),
                parent.Client.GetRemoteEndPoint(),
                nickname,
                exception.Message);
            throw new InvalidOperationException(
                "P5136 공용 인벤토리를 만들 수 없습니다. 서버를 중지하고 카트 데이터 XML을 다시 추출하세요.",
                exception);
        }

        foreach (List<PlantExcRecord> chunk in plan.PlantExcPackets)
        {
            using OutPacket packet = new OutPacket("LoRpGetRiderExcDataPacket");
            WritePlantExcPacketBody(
                packet,
                chunk,
                ReferenceEquals(chunk, plan.PlantExcPackets[0]));
            parent.Client.Send(packet);
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
            $"status=catalog; plantPackets={plan.PlantExcPackets.Count}; " +
            $"plantRecords={plan.PlantExcRecordCount}; excPackets={plan.PartsExcPackets.Count}; " +
            $"excRecords={plan.PartsExcRecordCount}; " +
            $"itemPackets={plan.ItemPackets.Count}; itemRecords={plan.ItemRecordCount}");
    }

    private static InventoryPlan BuildPlan(
        string profileDirectory,
        ushort slotChanger,
        bool preventItem,
        string plantDataPath = null)
    {
        IReadOnlyList<KartCatalogInventoryItem> catalog =
            KartCatalogInventory.GetItemsSnapshot();
        if (catalog.Count == 0)
        {
            throw new InvalidDataException(
                "KartCatalog.xml has no loaded P5136 inventory data.");
        }

        var plan = new InventoryPlan();

        // HF_5136 sends Tune, Plant, Level, and Parts exception streams first.
        // Plant state becomes user-specific as soon as a part is equipped, so
        // it must be restored before the global X-parts snapshot on reconnect.
        AddPlantExcPackets(plan, plantDataPath);
        AddPartsExcPackets(plan, Path.Combine(profileDirectory, "PartsData.xml"));

        var records = new List<RiderItemRecord>();
        foreach (KartCatalogInventoryItem item in catalog)
        {
            if (!KartCatalogInventory.IsGrantItem(item))
            {
                continue;
            }

            ushort amount = GetCatalogAmount(item.Category, item.Id, slotChanger);
            ushort serial = item.Category == 3
                ? NormalizeKartSerial(item.Id, item.Serial)
                : item.Serial;
            records.Add(RiderItemRecord.Normal(
                item.Category,
                item.Id,
                serial,
                amount,
                preventItem));
        }
        if (records.Count < 5000 || records.Count(record => record.Category == 3) < 1200)
        {
            throw new InvalidDataException(
                $"P5136 grant inventory is incomplete (items={records.Count}, " +
                $"karts={records.Count(record => record.Category == 3)}).");
        }
        Dictionary<ushort, List<RiderItemRecord>> recordsByCategory = records
            .GroupBy(record => record.Category)
            .ToDictionary(group => group.Key, group => group.ToList());
        AddCatalogCategoryPackets(plan, recordsByCategory, PrePartsCategoryOrder);

        AddPartsPacket(plan, 1, 1, 1053, 1053, 1053, 1053, 1080, 3, slotChanger);
        AddPartsPacket(plan, 1, 2, 1005, 1005, 1005, 1005, 1050, 5, slotChanger);
        AddPartsPacket(plan, 1, 3, 910, 910, 910, 910, 1000, 10, slotChanger);
        AddPartsPacket(plan, 1, 4, 810, 810, 810, 810, 900, 10, slotChanger);
        AddPartsPacket(plan, 2, 1, 1153, 1053, 1153, 1053, 1180, 3, slotChanger);
        AddPartsPacket(plan, 2, 2, 1105, 1005, 1105, 1005, 1150, 5, slotChanger);
        AddPartsPacket(plan, 2, 3, 1010, 910, 1010, 910, 1100, 10, slotChanger);
        AddPartsPacket(plan, 2, 4, 910, 810, 910, 810, 1000, 10, slotChanger);

        AddCatalogCategoryPackets(plan, recordsByCategory, PostPartsCategoryOrder);

        return plan;
    }

    private static void AddCatalogCategoryPackets(
        InventoryPlan plan,
        IReadOnlyDictionary<ushort, List<RiderItemRecord>> recordsByCategory,
        IEnumerable<ushort> categoryOrder)
    {
        foreach (ushort category in categoryOrder)
        {
            if (!recordsByCategory.TryGetValue(category, out List<RiderItemRecord> records))
            {
                throw new InvalidDataException(
                    $"P5136 grant inventory category {category} is missing.");
            }
            AddChunkedItemPackets(plan, records);
        }
    }

    internal static IReadOnlyList<(
        ushort Category,
        ushort Id,
        ushort Serial,
        ushort Amount,
        byte PartFlag,
        byte Grade,
        short Value)>
        BuildGrantSnapshotForTesting(
            string profileDirectory,
            ushort slotChanger,
            bool preventItem)
    {
        InventoryPlan plan = BuildPlan(profileDirectory, slotChanger, preventItem);
        return plan.ItemPackets
            .SelectMany(packet => packet)
            .Select(record => (
                record.Category,
                record.Id,
                record.Serial,
                record.Amount,
                record.PartFlag,
                record.Grade,
                record.Value))
            .ToArray();
    }

    internal static IReadOnlyList<int> BuildItemPacketSizesForTesting(
        string profileDirectory,
        ushort slotChanger,
        bool preventItem)
    {
        return BuildPlan(profileDirectory, slotChanger, preventItem)
            .ItemPackets
            .Select(packet => packet.Count)
            .ToArray();
    }

    internal static IReadOnlyList<(short Id, short Serial)>
        BuildPlantExcSnapshotForTesting(string plantDataPath)
    {
        var plan = new InventoryPlan();
        AddPlantExcPackets(plan, plantDataPath);
        return plan.PlantExcPackets
            .SelectMany(packet => packet)
            .Select(record => (record.Id, record.Serial))
            .ToArray();
    }

    internal static IReadOnlyList<(short Id, short Serial)>
        BuildPartsExcSnapshotForTesting(string partsDataPath)
    {
        var plan = new InventoryPlan();
        AddPartsExcPackets(plan, partsDataPath);
        return plan.PartsExcPackets
            .SelectMany(packet => packet)
            .Select(record => (record.Id, record.Serial))
            .ToArray();
    }

    private static ushort GetCatalogAmount(ushort category, ushort id, ushort slotChanger)
    {
        if (UnitAmountCategories.Contains(category) ||
            (category == 7 && (id == 3 || id == 4)))
        {
            return 1;
        }
        return slotChanger;
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

            short kartId = ParseShort(element, "id");
            records.Add(new PartsExcRecord(
                kartId,
                NormalizeKartSerial(kartId, ParseShort(element, "sn")),
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

    private static void AddPlantExcPackets(InventoryPlan plan, string plantDataPath)
    {
        if (string.IsNullOrWhiteSpace(plantDataPath) || !File.Exists(plantDataPath))
        {
            return;
        }

        List<Plant> plants = JsonHelper.DeserializeNoBom<List<Plant>>(plantDataPath)
            ?? new List<Plant>();
        for (int offset = 0; offset < plants.Count; offset += ExcDataChunkSize)
        {
            int count = Math.Min(ExcDataChunkSize, plants.Count - offset);
            var chunk = new List<PlantExcRecord>(count);
            for (int index = 0; index < count; index++)
            {
                Plant plant = plants[offset + index];
                chunk.Add(new PlantExcRecord(
                    plant.ID,
                    NormalizeKartSerial(plant.ID, plant.SN),
                    plant.Engine,
                    plant.EngineID,
                    plant.Handle,
                    plant.HandleID,
                    plant.Wheel,
                    plant.WheelID,
                    plant.Kit,
                    plant.KitID));
            }
            plan.AddPlantExcPacket(chunk);
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

    private static void WritePlantExcPacketBody(
        OutPacket packet,
        IReadOnlyList<PlantExcRecord> records,
        bool firstChunk)
    {
        packet.WriteByte(0);
        packet.WriteByte(firstChunk ? (byte)1 : (byte)0);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteInt(0);
        packet.WriteInt(records.Count);

        foreach (PlantExcRecord record in records)
        {
            packet.WriteShort(record.Id);
            packet.WriteShort(record.Serial);
            packet.WriteInt(4);
            packet.WriteShort(record.EngineCategory);
            packet.WriteShort(record.EngineId);
            packet.WriteShort(record.HandleCategory);
            packet.WriteShort(record.HandleId);
            packet.WriteShort(record.WheelCategory);
            packet.WriteShort(record.WheelId);
            packet.WriteShort(record.KitCategory);
            packet.WriteShort(record.KitId);
        }

        // Exact P5136 plant exception suffix.  Later protocol generations add
        // another byte and dword here; emitting those fields desynchronizes the
        // HF_5136 decoder.
        packet.WriteInt(0);
        packet.WriteInt(0);
        packet.WriteInt(0);
        packet.WriteInt(0);
    }

    private static XmlDocument LoadXml(string path)
    {
        var document = new XmlDocument();
        document.Load(path);
        return document;
    }

    private static short ParseShort(XmlElement element, string attribute) =>
        short.Parse(element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static byte ParseByte(XmlElement element, string attribute) =>
        byte.Parse(element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static ushort NormalizeKartSerial(ushort kartId, ushort serial) =>
        kartId == 0 ? serial : (ushort)1;

    private static short NormalizeKartSerial(short kartId, short serial) =>
        kartId == 0 ? serial : (short)1;

    private sealed class InventoryPlan
    {
        public List<List<PlantExcRecord>> PlantExcPackets { get; } =
            new List<List<PlantExcRecord>>();
        public List<List<PartsExcRecord>> PartsExcPackets { get; } = new List<List<PartsExcRecord>>();
        public List<List<RiderItemRecord>> ItemPackets { get; } = new List<List<RiderItemRecord>>();
        public int PlantExcRecordCount { get; private set; }
        public int PartsExcRecordCount { get; private set; }
        public int ItemRecordCount { get; private set; }

        public void AddPlantExcPacket(List<PlantExcRecord> packet)
        {
            PlantExcPackets.Add(packet);
            PlantExcRecordCount += packet.Count;
        }

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

    private readonly struct PlantExcRecord
    {
        public PlantExcRecord(
            short id,
            short serial,
            short engineCategory,
            short engineId,
            short handleCategory,
            short handleId,
            short wheelCategory,
            short wheelId,
            short kitCategory,
            short kitId)
        {
            Id = id;
            Serial = serial;
            EngineCategory = engineCategory;
            EngineId = engineId;
            HandleCategory = handleCategory;
            HandleId = handleId;
            WheelCategory = wheelCategory;
            WheelId = wheelId;
            KitCategory = kitCategory;
            KitId = kitId;
        }

        public short Id { get; }
        public short Serial { get; }
        public short EngineCategory { get; }
        public short EngineId { get; }
        public short HandleCategory { get; }
        public short HandleId { get; }
        public short WheelCategory { get; }
        public short WheelId { get; }
        public short KitCategory { get; }
        public short KitId { get; }
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
