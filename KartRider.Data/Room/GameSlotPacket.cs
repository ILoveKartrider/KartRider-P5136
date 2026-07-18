using System;
using System.Collections.Generic;
using ExcData;
using KartRider.Common.Network;
using KartRider.Compatibility;
using KartRider.IO.Packet;
using Profile;

namespace KartRider;

public class SlotData
{
    private static readonly Random _random = new Random();

    public static void GameSlotPacket(SessionGroup Parent, InPacket iPacket)
    {
        var kartConfig = SpecialKartConfig.LoadConfigFromFile(FileName.SpecialKartConfig);
        int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        Player player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);
        if (player == null)
        {
            return;
        }
        int id = iPacket.ReadInt();
        uint item = iPacket.ReadUInt();
        byte type = iPacket.ReadByte();

        if (id == player.ID)
        {
            if (type <= 2)
            {
                byte[] data1 = iPacket.ReadBytes(25);
                short liveRank = iPacket.ReadShort();
                iPacket.ReadByte();
                byte[] data2 = iPacket.ReadBytes(4);
                if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
                {
                    // Marker + skill + pad form the modern-only u32.
                    iPacket.ReadByte();
                    iPacket.ReadShort();
                    iPacket.ReadByte();
                }
                uint blobLength = iPacket.ReadUInt();
                if (blobLength > 0x3C0 || blobLength > (uint)iPacket.Available)
                {
                    return;
                }
                byte[] blob = iPacket.ReadBytes((int)blobLength);
                ItemPickupContext pickup = ParseItemPickupContext(data1, liveRank);
                int racerCount;
                lock (room)
                {
                    racerCount = room.GetCount();
                    player.LastItemBoxRank = pickup.LiveRank;
                    player.LastItemBoxX = pickup.X;
                    player.LastItemBoxY = pickup.Y;
                    player.LastItemBoxZ = pickup.Z;
                }
                short skill = RandomItemSkill(
                    Parent.Client.Nickname,
                    room.GameType,
                    pickup.LiveRank,
                    racerCount);
                PacketTrace.LogDetailEvent(
                    "LOGIN-TCP",
                    "ITEM-PICKUP",
                    null,
                    null,
                    Parent.Client.Nickname,
                    $"playerId={id}; clientRank0={pickup.LiveRank}; " +
                    $"displayRank={pickup.LiveRank + 1}; racers={racerCount}; " +
                    $"xyz=({pickup.X:R},{pickup.Y:R},{pickup.Z:R}); item={skill}");
                using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
                {
                    oPacket.WriteInt(id);
                    oPacket.WriteUInt(item);
                    oPacket.WriteByte(type);
                    oPacket.WriteBytes(data1);
                    oPacket.WriteShort(skill);
                    oPacket.WriteByte(1);
                    oPacket.WriteBytes(data2);
                    if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
                    {
                        oPacket.WriteByte(2);
                        oPacket.WriteShort(skill);
                        oPacket.WriteByte(0);
                    }
                    oPacket.WriteUInt(blobLength);
                    oPacket.WriteBytes(blob);
                    MultyPlayer.BroadCast(roomId, oPacket);
                }
                return;
            }
            else if (type is 5 or 7 or 8 or 17)
            {
                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteBytes(iPacket.ToArray());
                    MultyPlayer.BroadCast(roomId, oPacket);
                }
                return;
            }
            else if (type == 9)
            {
                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteBytes(iPacket.ToArray());
                    MultyPlayer.BroadCast(roomId, oPacket, Parent.Client.Nickname);
                }
                return;
            }
            else if (type == 12)
            {
                byte[] packet = iPacket.ToArray();
                BarricadePlacementContext placement = default;
                bool echoToSender = TryResolveSenderInclusivePlacement(
                    type,
                    packet,
                    out placement);
                if (echoToSender)
                {
                    PacketTrace.LogDetailEvent(
                        "LOGIN-TCP",
                        "BARRICADE-PLACEMENT",
                        null,
                        null,
                        Parent.Client.Nickname,
                        $"playerId={placement.PlayerId}; objectId=0x{placement.ObjectId:X8}; " +
                        $"xyz=({placement.X:R},{placement.Y:R},{placement.Z:R}); " +
                        "recipients=room including sender");
                }

                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteBytes(packet);
                    MultyPlayer.BroadCast(
                        roomId,
                        oPacket,
                        echoToSender ? string.Empty : Parent.Client.Nickname);
                }
                return;
            }
            else if (type == 10)
            {
                byte uni = iPacket.ReadByte();
                byte success = iPacket.ReadByte();
                byte unk = iPacket.ReadByte();
                var skill = iPacket.ReadShort();
                if (success == 1 || success == 2)
                {
                    List<short> skills = V2Specs.GetSkills(Parent.Client.Nickname);
                    if (skills.Contains(14) && skill == 5)
                    {
                        AddItemSkill(roomId, id, Parent, 6);
                    }

                    // Ensure profile is loaded before accessing
                    var parentConfig2 = ProfileService.GetProfileConfig(Parent.Client.Nickname);
                    bool catalogHandled = KartCatalogAbilities.TryGetFiringToGain(
                        parentConfig2.RiderItem.Set_Kart,
                        skill,
                        firingStep: 1,
                        ResolveCatalogGameType(room.GameType),
                        out KartCatalogAbilityRule catalogRule);
                    if (catalogHandled)
                    {
                        AddItemSkill(
                            roomId,
                            id,
                            Parent,
                            catalogRule.TargetItemId!.Value,
                            catalogRule.Probability!.Value);
                    }
                    else if (kartConfig.SkillMappings.TryGetValue(parentConfig2.RiderItem.Set_Kart, out var kartSkills2))
                    {
                        if (kartSkills2.TryGetValue(skill, out var skillConfig2))
                        {
                            // 传入概率参数，由 AddItemSkill 内部判断是否触发
                            AddItemSkill(roomId, id, Parent, skillConfig2.TargetItemId, skillConfig2.Probability);
                        }
                    }
                    Console.WriteLine("GameSlotPacket, Mapping. Skill = {0}", skill);
                }
                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteBytes(iPacket.ToArray());
                    MultyPlayer.BroadCast(roomId, oPacket, Parent.Client.Nickname);
                }
                return;
            }
            else if(type == 11)
            {
                var uni = iPacket.ReadByte();
                var skill = iPacket.ReadShort();
                List<short> skills = V2Specs.GetSkills(Parent.Client.Nickname);
                if (skills.Contains(13) && skill == 3)
                {
                    AttackedSkill(roomId, id, Parent, type, uni, 10);
                }

                // Ensure profile is loaded before accessing
                var parentConfig = ProfileService.GetProfileConfig(Parent.Client.Nickname);
                bool catalogHandled = KartCatalogAbilities.TryGetFiredToGain(
                    parentConfig.RiderItem.Set_Kart,
                    skill,
                    ResolveCatalogGameType(room.GameType),
                    out KartCatalogAbilityRule catalogRule);
                if (catalogHandled)
                {
                    AttackedSkill(
                        roomId,
                        id,
                        Parent,
                        type,
                        uni,
                        catalogRule.TargetItemId!.Value,
                        catalogRule.Probability!.Value);
                }
                else if (kartConfig.SkillAttacked.TryGetValue(parentConfig.RiderItem.Set_Kart, out var kartSkills))
                {
                    if (kartSkills.TryGetValue(skill, out var skillConfig))
                    {
                        // 传入概率参数，由 AttackedSkill 内部判断是否触发
                        AttackedSkill(roomId, id, Parent, type, uni, skillConfig.TargetItemId, skillConfig.Probability);
                    }
                }
                Console.WriteLine("GameSlotPacket, Attacked. Skill = {0}", skill);

                // P5136 resolves normal-pet defense locally from Set_Pet and
                // etc_/itemTable@kr.xml, then reports the result as this
                // type-11 frame.  It is a client broadcast (the uint 'item'
                // field carries the recipient mask), not server-only
                // telemetry.  Preserve the original frame and its mask; do
                // not roll the pet probability a second time on the server.
                byte[] reactionPacket = iPacket.ToArray();
                PacketTrace.LogDetailEvent(
                    "LOGIN-TCP",
                    "ITEM-REACTION-RELAY",
                    Parent.Client.GetLocalEndPoint(),
                    Parent.Client.GetRemoteEndPoint(),
                    Parent.Client.Nickname,
                    $"playerId={id}; recipientMask=0x{item:X8}; uni={uni}; " +
                    $"skill={skill}; bytes={reactionPacket.Length}; " +
                    "recipients=player-id-mask except sender");
                using (OutPacket oPacket = new OutPacket())
                {
                    oPacket.WriteBytes(reactionPacket);
                    MultyPlayer.BroadCastPlayerIdMask(
                        roomId,
                        oPacket,
                        item,
                        Parent.Client.Nickname);
                }
                return;
            }
        }
    }

    internal static ItemPickupContext ParseItemPickupContext(
        byte[] data,
        short liveRank)
    {
        if (data == null || data.Length != 25)
        {
            throw new ArgumentException(
                "P5136 item pickup context must be exactly 25 bytes.",
                nameof(data));
        }

        return new ItemPickupContext(
            liveRank,
            BitConverter.ToSingle(data, 13),
            BitConverter.ToSingle(data, 17),
            BitConverter.ToSingle(data, 21));
    }

    internal static bool TryResolveSenderInclusivePlacement(
        byte type,
        byte[] packet,
        out BarricadePlacementContext placement)
    {
        placement = default;
        return ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 &&
               type == 12 &&
               TryParseKorean5136BarricadePlacement(packet, out placement);
    }

    internal static bool TryParseKorean5136BarricadePlacement(
        byte[] packet,
        out BarricadePlacementContext placement)
    {
        const int PacketLength = 93;
        const int PayloadLength = 73;
        const uint GameSlotPacketHash = 0x27C00574;
        const uint GopBarricadeHash = 0x1D8604A3;
        const uint GoItemBarricadeHash = 0x2D0605C2;

        placement = default;
        if (packet == null ||
            packet.Length != PacketLength ||
            BitConverter.ToUInt32(packet, 0) != GameSlotPacketHash ||
            packet[12] != 12 ||
            BitConverter.ToUInt32(packet, 16) != PayloadLength ||
            BitConverter.ToUInt32(packet, 20) != GopBarricadeHash ||
            BitConverter.ToUInt32(packet, 24) != GoItemBarricadeHash ||
            packet[32] != 1)
        {
            return false;
        }

        float x = BitConverter.ToSingle(packet, 45);
        float y = BitConverter.ToSingle(packet, 49);
        float z = BitConverter.ToSingle(packet, 53);
        int playerId = BitConverter.ToInt32(packet, 4);
        int ownerId = BitConverter.ToInt32(packet, 37);
        if (ownerId != playerId || BitConverter.ToUInt32(packet, 41) != 0)
        {
            return false;
        }
        for (int offset = 45; offset < PacketLength; offset += sizeof(float))
        {
            if (!float.IsFinite(BitConverter.ToSingle(packet, offset)))
            {
                return false;
            }
        }

        placement = new BarricadePlacementContext(
            playerId,
            BitConverter.ToUInt32(packet, 28),
            BitConverter.ToUInt32(packet, 33),
            ownerId,
            x,
            y,
            z);
        return true;
    }

    public static short RandomItemSkill(
        string Nickname,
        byte gameType,
        int liveRank = -1,
        int racerCount = 0)
    {
        if (gameType == 2 || gameType == 4)
        {
            short skill = ItemProbabilityService.NextItem(
                teamMode: gameType == 4,
                liveRank,
                racerCount);
            return GetItemSkill(Nickname, skill, gameType);
        }
        return 0;
    }

    public static short GetItemSkill(string Nickname, short skill, byte? gameType = null)
    {
        var kartConfig = SpecialKartConfig.LoadConfigFromFile(FileName.SpecialKartConfig);
        List<short> skills = V2Specs.GetSkills(Nickname);
        for (int i = 0; i < skills.Count; i++)
        {
            if (V2Specs.itemSkill.TryGetValue(skills[i], out var Level) &&
                Level.TryGetValue(skill, out var LevelSkill))
            {
                return LevelSkill;
            }
        }
        var slotConfig = ProfileService.GetProfileConfig(Nickname);
        if (KartCatalogAbilities.TryGetTransform(
                slotConfig.RiderItem.Set_Kart,
                skill,
                ResolveCatalogTransformMode(gameType),
                out KartCatalogAbilityRule catalogRule))
        {
            if (catalogRule.Probability!.Value >= 100 ||
                _random.Next(100) < catalogRule.Probability.Value)
            {
                Console.WriteLine(
                    "[Catalog SkillChange] player={0}; kart={1}; item={2}->{3}; probability={4}%",
                    Nickname,
                    slotConfig.RiderItem.Set_Kart,
                    skill,
                    catalogRule.TargetItemId!.Value,
                    catalogRule.Probability.Value);
                return catalogRule.TargetItemId.Value;
            }
            return skill;
        }
        if (KartCatalogAbilities.HasTransformDefinition(
                slotConfig.RiderItem.Set_Kart,
                skill))
        {
            // The XML has a rule for this kart/item, but not for the current
            // mode (for example, a no_flag-only rule in a flag room).  Do not
            // let the generic JSON fallback override that explicit absence.
            return skill;
        }

        if (kartConfig.SkillChange.TryGetValue(slotConfig.RiderItem.Set_Kart, out var changes) &&
            changes.TryGetValue(skill, out var skillConfig))
        {
            // 触发几率判断
            if (skillConfig.Probability >= 100 || _random.Next(100) < skillConfig.Probability)
            {
                Console.WriteLine("[SkillChange] 玩家 {0} 道具变更 {1} -> {2} (概率: {3}%)", Nickname, skill, skillConfig.TargetItemId, skillConfig.Probability);
                return skillConfig.TargetItemId;
            }
            else
            {
                Console.WriteLine("[SkillChange] 玩家 {0} 道具变更未触发 {1} (概率: {2}%)", Nickname, skill, skillConfig.Probability);
            }
        }
        return skill;
    }

    private static string ResolveCatalogGameType(byte gameType)
    {
        return gameType switch
        {
            14 => "FlagIndi",
            54 => "FlagTeam",
            _ => string.Empty
        };
    }

    private static string ResolveCatalogTransformMode(byte? gameType)
    {
        return gameType switch
        {
            14 => "FlagIndi",
            54 => "FlagTeam",
            _ => "no_flag"
        };
    }

    public static void AddItemSkill(int roomId, int id, SessionGroup Parent, short skill, byte probability = 100)
    {
        // 概率判断：不触发时直接返回，不发送数据包
        if (probability < 100 && _random.Next(100) >= probability)
        {
            Console.WriteLine("[AddItemSkill] 玩家 {0} 技能 {1} 未触发 (概率: {2}%)", Parent.Client.Nickname, skill, probability);
            return;
        }

        skill = GetItemSkill(
            Parent.Client.Nickname,
            skill,
            RoomManager.GetRoom(roomId)?.GameType);
        using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
        {
            oPacket.WriteInt(id);
            oPacket.WriteUInt(uint.MaxValue);
            oPacket.WriteByte(10);
            oPacket.WriteHexString("001000");
            oPacket.WriteShort(skill);
            oPacket.WriteByte(1);
            oPacket.WriteBytes(new byte[3]);
            if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
            {
                oPacket.WriteByte(2);
                oPacket.WriteShort(skill);
                oPacket.WriteByte(0);
            }
            oPacket.WriteInt(0); // blob length
            Parent.Client.Send(oPacket);
            BroadCast(roomId, id, Parent.Client.Nickname, skill);
        }
    }

    public static void AttackedSkill(int roomId, int id, SessionGroup Parent, byte type, byte uni, short skill, byte probability = 100)
    {
        // 概率判断：不触发时直接返回，不发送数据包
        if (probability < 100 && _random.Next(100) >= probability)
        {
            Console.WriteLine("[AttackedSkill] 玩家 {0} 技能 {1} 未触发 (概率: {2}%)", Parent.Client.Nickname, skill, probability);
            return;
        }

        skill = GetItemSkill(
            Parent.Client.Nickname,
            skill,
            RoomManager.GetRoom(roomId)?.GameType);
        using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
        {
            oPacket.WriteInt(id);
            oPacket.WriteUInt();
            oPacket.WriteByte(type);
            oPacket.WriteByte(uni);
            oPacket.WriteShort(skill);
            oPacket.WriteByte(1);
            oPacket.WriteShort();
            if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
            {
                oPacket.WriteByte(2);
                oPacket.WriteShort(skill);
                oPacket.WriteByte(0);
            }
            oPacket.WriteInt(0); // blob length
            Parent.Client.Send(oPacket);
            BroadCast(roomId, id, Parent.Client.Nickname, skill);
        }
    }

    public static void BroadCast(int roomId, int id, string Nickname, short skill, uint ticks = 0)
    {
        using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
        {
            oPacket.WriteInt(id);
            oPacket.WriteUInt(uint.MaxValue);
            oPacket.WriteByte(1);
            oPacket.WriteByte(0);
            oPacket.WriteHexString("00 00 00 F0");
            oPacket.WriteUInt(ticks == 0 ? MultyPlayer.ConvertTick() : ticks);
            oPacket.WriteBytes(new byte[16]);
            oPacket.WriteShort(skill);
            oPacket.WriteByte(1);
            oPacket.WriteHexString("FF FF 00 00");
            if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
            {
                oPacket.WriteByte(2);
                oPacket.WriteShort(skill);
                oPacket.WriteByte(0);
            }
            oPacket.WriteInt(24); // blob length
            oPacket.WriteBytes(new byte[8]);
            oPacket.WriteHexString("00 00 00 F0 01 00 00 00");
            oPacket.WriteInt(id);
            oPacket.WriteUInt(ticks == 0 ? MultyPlayer.ConvertTick() : ticks);
            MultyPlayer.BroadCast(roomId, oPacket, Nickname);
        }
    }
}

internal readonly struct ItemPickupContext
{
    public ItemPickupContext(short liveRank, float x, float y, float z)
    {
        LiveRank = liveRank;
        X = x;
        Y = y;
        Z = z;
    }

    public short LiveRank { get; }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }
}

internal readonly struct BarricadePlacementContext
{
    public BarricadePlacementContext(
        int playerId,
        uint objectId,
        uint tick,
        int ownerId,
        float x,
        float y,
        float z)
    {
        PlayerId = playerId;
        ObjectId = objectId;
        Tick = tick;
        OwnerId = ownerId;
        X = x;
        Y = y;
        Z = z;
    }

    public int PlayerId { get; }

    public uint ObjectId { get; }

    public uint Tick { get; }

    public int OwnerId { get; }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }
}
