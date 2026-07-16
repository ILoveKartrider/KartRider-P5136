using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using ExcData;
using Profile;
using System;
using System.Collections.Generic;
using System.Net;

namespace KartRider.Compatibility;

/// <summary>
/// Wire compatibility for the Korean P5136 client.
///
/// These serializers implement the observed P5136 wire format. Keep them
/// isolated from the modern protocol: several packets share names while
/// having different payloads.
/// </summary>
public static class Korean5136Protocol
{
    public const ushort ClientVersion = 5136;
    public const ushort LocaleId = 1002;
    public const byte ClientLocation = 118;

    private const uint FirstKey = 2919676295u;
    private const uint SecondKey = 263300380u;
    private const string FirstKeyText = "QyvKvO60jogWDupzJ7gm0kRQdooFjWRjSjlq0gu/x2k=";
    private const string SecondKeyText = "GXQstj1A95XiHvjrOGuPkzdyL+7qxETl/cPlUZk2KA4=";
    private const string PatchUrl = "http://kart.dn.nexoncdn.co.kr/patch";
    private const string AgreementUrl = "https://www.tiancity.com/agreement";
    private const string LegacyLoginToken = "lppicekedkgjdqmncddpddecdogjppqhrghqifqjmjhcfiorecpmockdlngloorhqmekhrpdpejlgnclklrmddhoprcqknrfjolidjhndejiokfjoogqrgldgigqlhpp";

    private static readonly byte[] FirstMessageCompatibilityBlock =
    {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x2E, 0x31, 0x2E, 0x31, 0x37, 0x2E, 0x36,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    // Exact encoded ChRequestChStaticReplyPacket body from the P5136 launcher.
    private static readonly byte[] ChannelStaticReply = Convert.FromBase64String(
        "AU8DAABTAR2z0myODwAAeNqllvlTGjEUx1+9L1hgBcVaau+7Vav2PnZBHH9op6P93VFB64jgANbav77flzR1s2SzS8WZuEk+78jLS/KqROQ5aI6oS3U6oU1qUg29MrXQ28NXE+PjKSAdOsVnHdPf0e5iuoq2A8GBEUyf0AVtQYrlBoMqv6I9h2LZU7JD0YjyYbgUq0V5UBftMq1DdJ9+YLSJ/kgp1oZdwWg+1gM5yoGq01g+1l4Qn9CjGnZpMmMNuvR4KmPVIaGUY9QU9CXtGPUEEce1+hNEM67VqyCanQF6SGfoNAQk46Xvgx7LXFYTubSjr9pNA5M6ToAcITObmOAcvcBYg6Y5bffw2cVfA1L5MQwc4HMX6tX6CsFBtZIZc9j5/4E4M7yOWfMG6lBxKpAzX/4OK31zpkml5/qscL9Fx2jP4KD83rTm33w/QsrSjZxRaLtn/aU4UGm8WUyoUQ/WQjGhfl3sVsm66B1kSRtiv5EX5gDcdkTGtUU61mCtDb2/IHi5u3cKxozYFvlXg73zkFN3C8bsiBa4l49wQl+Nsny/YMV7Q/3AjV2kQh8WI1D75feoPzEVksdziR3TI/ZkIbFgdNifxls3Z90z3oCuuOTaOLx8zfBtxNOHRh+eJxVQ9l7MWwXsm7H4P8LK8pJduCEwvqLrhkRb5ov5pxDtAOlAQQsT9X/Ay2wE8A09OcburQyJC76B7h6tpgOdLVjfBx8M2lrKCKh1vjI/xhtiu0/pNd8AbbjBSeJDSwvyx5qBN+kIRJl4y0BNhKaLYF2GTUX1nXyEu6LbEFB4195nepDeAH/IBZ6NYHLyiEzlrhD4mDOUfibwkxv5lIXRz27k0xhGfcfgp155ls0PrQ5VzA+tDq1PxxrbEeMc3Wo6AFeQMq0QuqHXSyaEX9prNBqqMTxiP2ic4qtuj8EBGg7krUcrPDhIaWsye7TG2JAwYjsUHq0yOEz9Fbq8KBqh/spdrqMgGV+L8kkEHV+RchBoguKzyGdwkrIJcslDH7+pRLAv4VQErL8HHnGpC6eTwL6EnURwWcKZRHBFwtkQbA6cBxy/XCLYl7AbAYejwecDhyEJ7Es4nwguS7iQCK5IeIau8qAtsYpZusqDusgq/gB8YLMe");

    // Concrete channel records decoded from ChannelStaticReply. The switch
    // request carries a game type plus an optional preferred record ID, while
    // PrChannelSwitch must return a concrete record ID from this second table.
    private static readonly IReadOnlyDictionary<byte, ushort[]> ChannelIdsByGameType =
        new Dictionary<byte, ushort[]>
        {
            [20] = new ushort[] { 1 },
            [52] = new ushort[] { 2 },
            [54] = new ushort[] { 3 },
            [53] = new ushort[] { 4 },
            [7] = new ushort[] { 5 },
            [8] = new ushort[] { 6 },
            [13] = new ushort[] { 7 },
            [14] = new ushort[] { 8 },
            [65] = new ushort[] { 9 },
            [66] = new ushort[] { 10 },
            [67] = new ushort[] { 11, 12 },
            [23] = new ushort[] { 13, 14, 15, 16 },
            [68] = new ushort[] { 17, 18 },
            [24] = new ushort[] { 19, 20, 21, 22 },
            [49] = new ushort[] { 23 },
            [48] = new ushort[] { 24 }
        };

    private static readonly HashSet<uint> NoOpPackets = CreateNoOpPackets();

    public static void BindSession(SessionGroup session, string clientId)
    {
        string nickname = ProfileService.SettingConfig.Name;
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return;
        }

        session.Client.Nickname = nickname;
        ClientManager.GetUserNO(nickname);

        if (!FileName.FileNames.ContainsKey(nickname))
        {
            FileName.Load(nickname);
        }

        var config = ProfileService.GetProfileConfig(nickname);
        config.Rider.ClientId = clientId;
        ProfileService.Save(nickname, config);
    }

    public static uint SendFirstMessage(SessionGroup parent)
    {
        using (OutPacket outPacket = new OutPacket("PcFirstMessage"))
        {
            outPacket.WriteUShort(LocaleId);
            outPacket.WriteUShort(1);
            outPacket.WriteUShort(ClientVersion);
            outPacket.WriteString(PatchUrl);
            outPacket.WriteUInt(FirstKey);
            outPacket.WriteUInt(SecondKey);
            outPacket.WriteByte(ClientLocation);
            outPacket.WriteString(FirstKeyText);
            outPacket.WriteBytes(FirstMessageCompatibilityBlock);
            outPacket.WriteString(SecondKeyText);
            parent.Client.Send(outPacket);
        }

        return FirstKey ^ SecondKey;
    }

    public static bool TryHandle(SessionGroup parent, uint hash, InPacket packet)
    {
        if (Is(hash, "PqAddTimeEventInitPacket"))
        {
            // P5136 creates the AddTimeEvent/menu pending request immediately
            // before transmitting this packet. Replying earlier from
            // PqGetRider races that registration and leaves the real request
            // pending, so the paired reply must be emitted here.
            SendAddTimeEventInit(parent);
            return true;
        }

        if (NoOpPackets.Contains(hash))
        {
            return true;
        }

        if (Is(hash, "LoRqAddRacingTimePacket"))
        {
            using OutPacket response = new OutPacket("LoRpAddRacingTimePacket");
            response.WriteHexString("FF FF FF FF 00 00 00 00 00 00 00 00 00 00");
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqCnAuthenLogin"))
        {
            SendAuthentication(parent);
            return true;
        }

        if (Is(hash, "PqLogin"))
        {
            SendLogin(parent);
            return true;
        }

        if (Is(hash, "PqLoginVipInfo"))
        {
            SendLoginVipInfo(parent);
            return true;
        }

        if (Is(hash, "PqEquipTuningPacket") || Is(hash, "PqEquipTuningExPacket"))
        {
            using OutPacket response = new OutPacket("PrEquipTuningPacket");
            response.WriteInt(0);
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqGetRider"))
        {
            SendRider(parent);
            return true;
        }

        if (Is(hash, "LoRqSetRiderItemOnPacket"))
        {
            ReadRiderItems(parent, packet);
            return true;
        }

        if (Is(hash, "PqGetRiderInfo"))
        {
            SendRiderInfo(parent, packet);
            return true;
        }

        if (Is(hash, "PqUpdateGameOption"))
        {
            ReadGameOptions(parent, packet);
            return true;
        }

        if (Is(hash, "PqGetGameOption"))
        {
            SendGameOptions(parent);
            return true;
        }

        if (Is(hash, "PqSetPlaytimeEventTick"))
        {
            using OutPacket response = new OutPacket("PrSetPlaytimeEventTick");
            response.WriteByte(0);
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqChapterInfoPacket"))
        {
            using OutPacket response = new OutPacket("PrChapterInfoPacket");
            response.WriteInt(0);
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqGetDuelMissionBulk"))
        {
            SendDuelMissions(parent);
            return true;
        }

        if (Is(hash, "PqRiderSchoolDataPacket"))
        {
            SendRiderSchoolData(parent);
            return true;
        }

        if (Is(hash, "PqRiderSchoolProPacket"))
        {
            SendRiderSchoolProgress(parent);
            return true;
        }

        if (Is(hash, "PqStartRiderSchool"))
        {
            SendRiderSchoolStart(parent);
            return true;
        }

        if (Is(hash, "ChRequestChStaticRequestPacket"))
        {
            SendChannelStatic(parent);
            return true;
        }

        if (Is(hash, "PqDynamicCommand"))
        {
            SendDynamicCommand(parent);
            return true;
        }

        if (Is(hash, "PqPubCommandPacket"))
        {
            SendPublicCommand(parent);
            return true;
        }

        if (Is(hash, "PqGetFavoriteChannel"))
        {
            SendFavoriteChannel(parent);
            return true;
        }

        if (Is(hash, "PqKartPassInitPacket"))
        {
            SendKartPassInit(parent);
            return true;
        }

        if (Is(hash, "PqKartPassRewardPacket"))
        {
            using OutPacket response = new OutPacket("PrKartPassRewardPacket");
            response.WriteHexString("00 00 00 00 00 00 00 00 01 00 00 00 01 00 00 00");
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqQuestUX2ndPacket"))
        {
            SendQuestUx2nd(parent);
            return true;
        }

        if (Is(hash, "SpReqNormalShopBuyItemPacket") ||
            Is(hash, "SpReqItemPresetShopBuyItemPacket"))
        {
            SendShopBuyFailure(parent);
            return true;
        }

        if (Is(hash, "PqGetCurrentRid"))
        {
            SendCurrentRider(parent);
            return true;
        }

        if (Is(hash, "PqDisassembleFeeInfo"))
        {
            SendDisassembleFeeInfo(parent);
            return true;
        }

        if (Is(hash, "PqSyncDictionaryInfoPacket"))
        {
            packet.ReadInt();
            SendDictionaryInfo(parent);
            return true;
        }

        if (Is(hash, "LoRqDeleteItemPacket"))
        {
            using OutPacket response = new OutPacket("LoRpDeleteItemPacket");
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqUnLockedItem"))
        {
            HandleUnlockedItem(parent, packet);
            return true;
        }

        if (Is(hash, "PqFavoriteItemUpdate"))
        {
            HandleFavoriteItemUpdate(parent, packet);
            return true;
        }

        if (Is(hash, "PqLockedItemGet"))
        {
            using OutPacket response = new OutPacket("PrLockedItemGet");
            response.WriteInt(0);
            parent.Client.Send(response);
            return true;
        }

        // P5136 uses the same wire layouts and packet identities for
        // PqChannelSwitch/PqClubChannelSwitch -> PrChannelSwitch. Let the
        // modern handlers preserve channel state and choose the real reply.
        if (Is(hash, "PqCheckMyClubStatePacket"))
        {
            SendClubState(parent);
            return true;
        }

        if (Is(hash, "PqGetUserWaitingJoinClubPacket"))
        {
            using OutPacket response = new OutPacket("PrGetUserWaitingJoinClubPacket");
            response.WriteInt(1);
            response.WriteInt(0);
            response.WriteInt(0);
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqCheckCreateClubConditionPacket"))
        {
            using OutPacket response = new OutPacket("PrCheckCreateClubConditionPacket");
            response.WriteInt(3);
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqGetClubListCountPacket"))
        {
            using OutPacket response = new OutPacket("PrGetClubListCountPacket");
            response.WriteHexString("7F F7 00 00 01 00 00 00");
            parent.Client.Send(response);
            return true;
        }

        if (Is(hash, "PqGetClubWaitingCrewCountPacket"))
        {
            using OutPacket response = new OutPacket("PrGetClubWaitingCrewCountPacket");
            response.WriteHexString("32 00 00 00 32 00 00 00");
            parent.Client.Send(response);
            return true;
        }

        return false;
    }

    public static void SendGameOptions(SessionGroup parent)
    {
        string nickname = EnsureNickname(parent);
        var option = ProfileService.GetProfileConfig(nickname).GameOption;
        using OutPacket response = new OutPacket("PrGetGameOption");
        response.WriteFloat(option.Set_BGM);
        response.WriteFloat(option.Set_Sound);
        response.WriteByte(option.Main_BGM);
        response.WriteByte(option.Sound_effect);
        response.WriteByte(option.Full_screen);
        response.WriteByte(option.ShowMirror);
        response.WriteByte(option.ShowOtherPlayerNames);
        response.WriteByte(option.ShowOutlines);
        response.WriteByte(option.ShowShadows);
        response.WriteByte(option.HighLevelEffect);
        response.WriteByte(option.MotionBlurEffect);
        response.WriteByte(option.MotionDistortionEffect);
        response.WriteByte(option.HighEndOptimization);
        response.WriteByte(option.AutoReady);
        response.WriteByte(option.PropDescription);
        response.WriteByte(option.VideoQuality);
        response.WriteByte(option.BGM_Check);
        response.WriteByte(option.Sound_Check);
        response.WriteByte(option.ShowHitInfo);
        response.WriteByte(option.AutoBoost);
        response.WriteByte(option.GameType);
        response.WriteByte(option.SetGhost);
        response.WriteByte(option.SpeedType);
        response.WriteByte(option.RoomChat);
        response.WriteByte(option.DrivingChat);
        response.WriteByte(option.ShowAllPlayerHitInfo);
        response.WriteByte(option.ShowTeamColor);
        response.WriteByte(option.Set_screen);
        response.WriteByte(option.HideCompetitiveRank);
        response.WriteBytes(new byte[80]);
        parent.Client.Send(response);
    }

    public static void SendChannelStatic(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("ChRequestChStaticReplyPacket");
        response.WriteBytes(ChannelStaticReply);
        parent.Client.Send(response);
    }

    public static bool TryResolveChannelId(
        byte requestedGameType,
        ushort preferredChannelId,
        out ushort selectedChannelId)
    {
        selectedChannelId = 0;
        if (!ChannelIdsByGameType.TryGetValue(requestedGameType, out ushort[] channelIds)
            || channelIds.Length == 0)
        {
            return false;
        }

        if (preferredChannelId != 0)
        {
            foreach (ushort channelId in channelIds)
            {
                if (channelId == preferredChannelId)
                {
                    selectedChannelId = preferredChannelId;
                    return true;
                }
            }
        }

        selectedChannelId = channelIds[0];
        return true;
    }

    public static void SendDynamicCommand(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrDynamicCommand");
        response.WriteByte(0);
        response.WriteInt(0);
        parent.Client.Send(response);
    }

    public static void SendClubState(SessionGroup parent)
    {
        string nickname = EnsureNickname(parent);
        var rider = ProfileService.GetProfileConfig(nickname).Rider;
        using OutPacket response = new OutPacket("PrCheckMyClubStatePacket");
        if (rider.ClubMark_LOGO == 0)
        {
            response.WriteInt(0);
            response.WriteString("");
            response.WriteInt(0);
            response.WriteInt(0);
        }
        else
        {
            response.WriteInt(rider.ClubCode);
            response.WriteString(rider.ClubName);
            response.WriteInt(rider.ClubMark_LOGO);
            response.WriteInt(rider.ClubMark_LINE);
        }

        response.WriteShort(5);
        response.WriteString(nickname);
        response.WriteInt(1);
        response.WriteByte(5);
        parent.Client.Send(response);
    }

    private static void SendAuthentication(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrCnAuthenLogin");
        response.WriteInt(1);
        response.WriteString(LegacyLoginToken);
        response.WriteByte(0);
        response.WriteString(AgreementUrl);
        parent.Client.Send(response);
    }

    private static void SendLoginVipInfo(SessionGroup parent)
    {
        string nickname = EnsureNickname(parent);
        int premium = ProfileService.GetProfileConfig(nickname).Rider.Premium;
        using (OutPacket response = new OutPacket("PrLoginVipInfo"))
        {
            response.WriteInt(premium);
            response.WriteByte(1);
            response.WriteInt(0);
            parent.Client.Send(response);
        }

        using OutPacket reward = new OutPacket("LoRpEventRewardPacket");
        reward.WriteInt(0);
        reward.WriteInt(0);
        parent.Client.Send(reward);
    }

    private static void SendRiderSchoolData(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrRiderSchoolDataPacket");
        response.WriteByte(6);
        response.WriteByte(34);
        WriteLegacyTime(response);
        response.WriteInt(0);
        response.WriteByte(0);
        parent.Client.Send(response);
    }

    private static void SendDuelMissions(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrGetDuelMissionBulk");
        response.WriteInt(0);
        response.WriteInt(0);
        WriteLegacyTime(response);
        response.WriteHexString("0F 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
        parent.Client.Send(response);
    }

    private static void SendRiderSchoolProgress(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrRiderSchoolProPacket");
        response.WriteByte(1);
        response.WriteByte(33);
        response.WriteByte(6);
        response.WriteByte(34);
        response.WriteInt(0);
        response.WriteInt(0);
        response.WriteInt(0);
        parent.Client.Send(response);
    }

    private static void SendRiderSchoolStart(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrStartRiderSchool");
        response.WriteByte(1);
        response.WriteEncFloat(1.1f);
        response.WriteEncInt(2000);
        response.WriteEncFloat(1.4f);
        response.WriteEncInt(500);
        response.WriteEncFloat(350f);
        response.WriteEncByte(2);
        response.WriteEncByte(2);
        response.WriteEncByte(1);
        response.WriteEncByte(1);
        response.WriteEncByte(0);
        response.WriteEncByte(1);
        response.WriteEncFloat(100f);
        response.WriteEncFloat(3f);
        response.WriteEncFloat(0.667f + SpeedPatch.DragFactor);
        response.WriteEncFloat(2304f + SpeedPatch.ForwardAccelForce);
        response.WriteEncFloat(1825f);
        response.WriteEncFloat(2070f);
        response.WriteEncFloat(1415f);
        response.WriteEncFloat(10f);
        response.WriteEncFloat(24.61f);
        response.WriteEncFloat(5f);
        response.WriteEncFloat(5f);
        response.WriteEncFloat(0.2f);
        response.WriteEncFloat(0.2f);
        response.WriteEncFloat(0.2f);
        response.WriteEncFloat(4200f + SpeedPatch.DriftEscapeForce);
        response.WriteEncFloat(0.254f + SpeedPatch.CornerDrawFactor);
        response.WriteEncFloat(0.06f);
        response.WriteEncFloat(0.01f);
        response.WriteEncFloat(3860f + SpeedPatch.DriftMaxGauge);
        response.WriteEncFloat(2900f);
        response.WriteEncFloat(3000f);
        response.WriteEncFloat(4350f);
        response.WriteEncFloat(4000f);
        response.WriteEncFloat(3500f);
        response.WriteEncFloat(1.8495f + SpeedPatch.TransAccelFactor);
        response.WriteEncFloat(1.494f + SpeedPatch.BoostAccelFactor);
        response.WriteEncFloat(1000f);
        response.WriteEncFloat(1500f);
        response.WriteEncFloat(2305f + SpeedPatch.StartForwardAccelForceItem);
        response.WriteEncFloat(3745f + SpeedPatch.StartForwardAccelForceSpeed);
        response.WriteEncFloat(0.5f);
        response.WriteEncByte(0);
        response.WriteEncFloat(1.5f);
        response.WriteEncFloat(0.91f);
        response.WriteEncByte(0);
        response.WriteEncInt(20);
        response.WriteEncInt(30);
        response.WriteEncFloat(1.04f);
        response.WriteEncFloat(100f);
        response.WriteEncByte(1);
        response.WriteEncByte(1);
        response.WriteEncByte(1);
        response.WriteEncByte(1);
        response.WriteEncByte(1);
        response.WriteEncByte(1);
        response.WriteEncFloat(0.02f);
        response.WriteEncFloat(0.06f);
        response.WriteEncFloat(0.15f);
        response.WriteEncFloat(1.11f);
        response.WriteEncInt(3000);
        response.WriteEncFloat(2500f);
        response.WriteEncFloat(750f);
        response.WriteEncFloat(0f);
        response.WriteEncFloat(50f);
        response.WriteEncByte(0);
        response.WriteEncInt(3000);
        response.WriteEncFloat(200f);
        response.WriteEncFloat(200f);
        response.WriteEncFloat(50f);
        parent.Client.Send(response);
    }

    private static void SendPublicCommand(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrPubCommandPacket");
        response.WriteInt(0);
        response.WriteInt(0);
        parent.Client.Send(response);
    }

    private static void SendFavoriteChannel(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrGetFavoriteChannel");
        response.WriteHexString("02 00 00 00 00 00 00 00 00 00 01 00");
        parent.Client.Send(response);
    }

    private static void SendKartPassInit(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrKartPassInitPacket");
        response.WriteInt(3);
        response.WriteInt(0);
        parent.Client.Send(response);
    }

    private static void SendQuestUx2nd(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrQuestUX2ndPacket");
        WriteQuestUx2ndBody(response);
        parent.Client.Send(response);
    }

    private static void WriteQuestUx2ndBody(OutPacket response)
    {
        const int firstQuestId = 1211;
        const int lastQuestId = 1217;

        response.WriteInt(1);
        response.WriteInt(1);
        response.WriteInt(lastQuestId - firstQuestId + 1);
        for (int questId = firstQuestId; questId <= lastQuestId; questId++)
        {
            response.WriteInt(questId);
            response.WriteInt(questId);
            response.WriteInt(0);
            response.WriteInt(0);
            response.WriteInt(0);
            response.WriteInt(0);
            response.WriteInt(2);
            response.WriteInt(0);
            response.WriteByte(0);
        }
    }

    private static void SendShopBuyFailure(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("SpRepBuyItemPacket");
        response.WriteByte(1);
        response.WriteBytes(new byte[24]);
        parent.Client.Send(response);
    }

    private static void SendCurrentRider(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrGetCurrentRid");
        response.WriteInt(0);
        parent.Client.Send(response);
    }

    private static void SendDisassembleFeeInfo(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrDisassembleFeeInfo");
        response.WriteHexString("00 00 00 00 06 00 00 00 00 00 E8 03 01 00 F4 01 00 00 E8 03 01 00 F4 01 00 00 E8 03 01 00 F4 01");
        parent.Client.Send(response);
    }

    private static void SendDictionaryInfo(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrSyncDictionaryInfoPacket");
        response.WriteInt(1);
        response.WriteInt(1);
        for (int i = 0; i < 6; i++)
        {
            response.WriteInt(0);
        }
        parent.Client.Send(response);
    }

    private static void HandleUnlockedItem(SessionGroup parent, InPacket packet)
    {
        int stringCount = packet.ReadInt();
        packet.ReadInt();
        for (int i = 0; i < stringCount; i++)
        {
            packet.ReadString(false);
        }
        byte type = packet.ReadByte();

        using OutPacket response = new OutPacket("PrUnLockedItem");
        response.WriteByte(type);
        parent.Client.Send(response);
    }

    private static void HandleFavoriteItemUpdate(SessionGroup parent, InPacket packet)
    {
        string nickname = EnsureNickname(parent);
        packet.ReadByte();
        int count = packet.ReadShort();
        packet.ReadShort();
        for (int i = 0; i < count; i++)
        {
            ushort item = packet.ReadUShort();
            ushort id = packet.ReadUShort();
            ushort serialNumber = packet.ReadUShort();
            byte operation = packet.ReadByte();
            if (operation == 1)
            {
                RiderData.FavoriteItem.Favorite_Item_Add(nickname, item, id, serialNumber);
            }
            else if (operation == 2)
            {
                RiderData.FavoriteItem.Favorite_Item_Del(nickname, item, id, serialNumber);
            }
        }
    }

    private static void SendLogin(SessionGroup parent)
    {
        string nickname = EnsureNickname(parent);
        var config = ProfileService.GetProfileConfig(nickname);
        uint userNo = ClientManager.GetUserNO(nickname);
        IPAddress serverAddress = GetLegacyServerAddress();
        ClientPortTopology ports = ClientBuildProfiles.Active.Ports;
        ushort configuredPort = ProfileService.SettingConfig.ServerPort;

        GameDataReset.DataReset(nickname);
        using OutPacket response = new OutPacket("PrLogin");
        response.WriteInt(0);
        WriteLegacyTime(response);
        response.WriteUInt(userNo);
        response.WriteString(nickname);
        response.WriteByte(2);
        response.WriteByte(1);
        response.WriteByte(0);
        response.WriteInt(0);
        response.WriteByte(0);
        response.WriteInt(1415577599);
        response.WriteUInt(config.Rider.pmap);
        for (int i = 0; i < 11; i++)
        {
            response.WriteInt(0);
        }

        response.WriteByte(0);
        response.WriteEndPoint(serverAddress, ports.ResolveUdpPort(configuredPort));
        response.WriteEndPoint(serverAddress, ports.ResolveP2pPort(configuredPort));
        response.WriteInt(0);
        response.WriteString("");
        response.WriteInt(0);
        response.WriteByte(1);
        response.WriteString("content");
        response.WriteInt(0);
        response.WriteInt(1);
        response.WriteString("cc");
        response.WriteString("kr");
        response.WriteInt(0);
        response.WriteByte(0);
        response.WriteByte(config.GameOption.Set_screen);
        parent.Client.Send(response);
    }

    private static void SendRider(SessionGroup parent)
    {
        string nickname = EnsureNickname(parent);
        var config = ProfileService.GetProfileConfig(nickname);

        // The P5136 startup sequence preloads the complete legacy inventory
        // before publishing the rider snapshot. The later
        // LoRqGetRiderItemPacket is therefore an intentional no-op.
        Korean5136Inventory.Send(parent, nickname);

        using OutPacket response = new OutPacket("PrGetRider");
        response.WriteByte(1);
        response.WriteByte(0);
        response.WriteString(nickname);
        response.WriteShort(0);
        response.WriteShort(0);
        response.WriteShort(config.Rider.Emblem1);
        response.WriteShort(config.Rider.Emblem2);
        response.WriteShort(0);
        WriteLegacyRiderItems(response, config.RiderItem);
        response.WriteString("");
        response.WriteUInt(config.Rider.Lucci);
        response.WriteInt(unchecked((int)config.Rider.RP));
        // Exact all-default P5136 typed suffix. Twenty-five dwords would
        // leave seven trailing bytes beyond the registered packet schema.
        response.WriteBytes(new byte[93]);
        parent.Client.Send(response);
    }

    private static void SendAddTimeEventInit(SessionGroup parent)
    {
        using OutPacket response = new OutPacket("PrAddTimeEventInitPacket");
        response.WriteHexString("6D F8 03 00 E8 B3 18 15 EF B3 17 15");
        response.WriteInt(6);
        response.WriteHexString("2F 7D A1 8B 28 57 BF 7E 3B 6D A8 52");
        response.WriteInt(0);
        response.WriteInt(0);
        response.WriteHexString("6D F8 03 00");
        response.WriteInt(0);
        response.WriteInt(0);
        response.WriteInt(0);
        WriteLegacyTime(response);
        response.WriteInt(0);
        parent.Client.Send(response);
    }

    private static void ReadRiderItems(SessionGroup parent, InPacket packet)
    {
        string nickname = EnsureNickname(parent);
        var config = ProfileService.GetProfileConfig(nickname);
        var item = config.RiderItem;
        item.Set_Character = packet.ReadUShort();
        item.Set_Paint = packet.ReadUShort();
        item.Set_Kart = packet.ReadUShort();
        item.Set_Plate = packet.ReadUShort();
        item.Set_Goggle = packet.ReadUShort();
        item.Set_Balloon = packet.ReadUShort();
        item.Set_Unknown1 = packet.ReadUShort();
        item.Set_HeadBand = packet.ReadUShort();
        item.Set_HeadPhone = packet.ReadUShort();
        item.Set_HandGearL = packet.ReadUShort();
        item.Set_Unknown2 = packet.ReadUShort();
        item.Set_Uniform = packet.ReadUShort();
        item.Set_Decal = packet.ReadUShort();
        item.Set_Pet = packet.ReadUShort();
        item.Set_FlyingPet = packet.ReadUShort();
        item.Set_Aura = packet.ReadUShort();
        item.Set_SkidMark = packet.ReadUShort();
        item.Set_SpecialKit = packet.ReadUShort();
        item.Set_RidColor = packet.ReadUShort();
        item.Set_BonusCard = packet.ReadUShort();
        item.Set_BossModeCard = packet.ReadUShort();
        item.Set_KartPlant1 = packet.ReadUShort();
        item.Set_KartPlant2 = packet.ReadUShort();
        item.Set_KartPlant3 = packet.ReadUShort();
        item.Set_KartPlant4 = packet.ReadUShort();
        item.Set_Unknown3 = packet.ReadUShort();
        item.Set_FishingPole = packet.ReadUShort();
        item.Set_Tachometer = packet.ReadUShort();
        item.Set_Dye = packet.ReadUShort();
        item.Set_KartSN = packet.ReadUShort();
        item.Set_Unknown4 = packet.ReadByte();
        item.Set_KartCoating = packet.ReadUShort();
        item.Set_KartTailLamp = packet.ReadUShort();
        ProfileService.Save(nickname, config);
    }

    private static void SendRiderInfo(SessionGroup parent, InPacket packet)
    {
        string nickname = EnsureNickname(parent);
        packet.ReadInt();
        packet.ReadInt();
        string requestedNickname = packet.ReadString(false);
        if (!string.Equals(requestedNickname, nickname, StringComparison.Ordinal))
        {
            using OutPacket failureResponse = new OutPacket("PrGetRiderInfo");
            failureResponse.WriteByte(0);
            parent.Client.Send(failureResponse);
            return;
        }

        var rider = ProfileService.GetProfileConfig(nickname).Rider;
        using OutPacket response = new OutPacket("PrGetRiderInfo");
        response.WriteByte(1);
        response.WriteUInt(ClientManager.GetUserNO(nickname));
        response.WriteString(nickname);
        response.WriteString(nickname);
        WriteLegacyTime(response);
        for (int i = 0; i < 32; i++)
        {
            response.WriteShort(0);
        }
        response.WriteByte(0);
        response.WriteString("");
        response.WriteInt(unchecked((int)rider.RP));
        response.WriteInt(0);
        response.WriteByte(6);
        WriteLegacyTime(response);
        response.WriteBytes(new byte[17]);
        response.WriteShort(rider.Emblem1);
        response.WriteShort(rider.Emblem2);
        response.WriteShort(0);
        response.WriteString(rider.RiderIntro);
        response.WriteInt(rider.Premium);
        response.WriteByte(1);
        response.WriteInt(GetPremiumPoints(rider.Premium));
        if (rider.ClubMark_LOGO == 0)
        {
            response.WriteInt(0);
            response.WriteInt(0);
            response.WriteInt(0);
            response.WriteString("");
        }
        else
        {
            response.WriteInt(rider.ClubCode);
            response.WriteInt(rider.ClubMark_LOGO);
            response.WriteInt(rider.ClubMark_LINE);
            response.WriteString(rider.ClubName);
        }
        response.WriteInt(0);
        response.WriteByte(rider.Ranker);
        for (int i = 0; i < 5; i++)
        {
            response.WriteInt(0);
        }
        response.WriteByte(0);
        response.WriteByte(0);
        response.WriteByte(0);
        parent.Client.Send(response);
    }

    private static void ReadGameOptions(SessionGroup parent, InPacket packet)
    {
        string nickname = EnsureNickname(parent);
        var config = ProfileService.GetProfileConfig(nickname);
        var option = config.GameOption;
        option.Set_BGM = packet.ReadFloat();
        option.Set_Sound = packet.ReadFloat();
        option.Main_BGM = packet.ReadByte();
        option.Sound_effect = packet.ReadByte();
        option.Full_screen = packet.ReadByte();
        option.ShowMirror = packet.ReadByte();
        option.ShowOtherPlayerNames = packet.ReadByte();
        option.ShowOutlines = packet.ReadByte();
        option.ShowShadows = packet.ReadByte();
        option.HighLevelEffect = packet.ReadByte();
        option.MotionBlurEffect = packet.ReadByte();
        option.MotionDistortionEffect = packet.ReadByte();
        option.HighEndOptimization = packet.ReadByte();
        option.AutoReady = packet.ReadByte();
        option.PropDescription = packet.ReadByte();
        option.VideoQuality = packet.ReadByte();
        option.BGM_Check = packet.ReadByte();
        option.Sound_Check = packet.ReadByte();
        option.ShowHitInfo = packet.ReadByte();
        option.AutoBoost = packet.ReadByte();
        option.GameType = packet.ReadByte();
        option.SetGhost = packet.ReadByte();
        option.SpeedType = packet.ReadByte();
        option.RoomChat = packet.ReadByte();
        option.DrivingChat = packet.ReadByte();
        option.ShowAllPlayerHitInfo = packet.ReadByte();
        option.ShowTeamColor = packet.ReadByte();
        option.Set_screen = packet.ReadByte();
        option.HideCompetitiveRank = packet.ReadByte();
        ProfileService.Save(nickname, config);
    }

    private static void WriteLegacyRiderItems(OutPacket packet, RiderItemData item)
    {
        packet.WriteUShort(item.Set_Character);
        packet.WriteUShort(item.Set_Paint);
        packet.WriteUShort(item.Set_Kart);
        packet.WriteUShort(item.Set_Plate);
        packet.WriteUShort(item.Set_Goggle);
        packet.WriteUShort(item.Set_Balloon);
        packet.WriteUShort(item.Set_Unknown1);
        packet.WriteUShort(item.Set_HeadBand);
        packet.WriteUShort(item.Set_HeadPhone);
        packet.WriteUShort(item.Set_HandGearL);
        packet.WriteUShort(item.Set_Unknown2);
        packet.WriteUShort(item.Set_Uniform);
        packet.WriteUShort(item.Set_Decal);
        packet.WriteUShort(item.Set_Pet);
        packet.WriteUShort(item.Set_FlyingPet);
        packet.WriteUShort(item.Set_Aura);
        packet.WriteUShort(item.Set_SkidMark);
        packet.WriteUShort(item.Set_SpecialKit);
        packet.WriteUShort(item.Set_RidColor);
        packet.WriteUShort(item.Set_BonusCard);
        packet.WriteUShort(item.Set_BossModeCard);
        packet.WriteUShort(item.Set_KartPlant1);
        packet.WriteUShort(item.Set_KartPlant2);
        packet.WriteUShort(item.Set_KartPlant3);
        packet.WriteUShort(item.Set_KartPlant4);
        packet.WriteUShort(item.Set_Unknown3);
        packet.WriteUShort(item.Set_FishingPole);
        packet.WriteUShort(item.Set_Tachometer);
        packet.WriteUShort(item.Set_Dye);
        packet.WriteUShort(item.Set_KartSN);
        packet.WriteByte(item.Set_Unknown4);
        packet.WriteUShort(item.Set_KartCoating);
        packet.WriteUShort(item.Set_KartTailLamp);
    }

    private static void WriteLegacyTime(OutPacket packet)
    {
        DateTime now = DateTime.Now;
        TimeSpan elapsed = now - new DateTime(1900, 1, 1);
        packet.WriteUShort(unchecked((ushort)elapsed.Days));
        packet.WriteUShort(unchecked((ushort)(now.TimeOfDay.TotalSeconds / 4.0)));
    }

    private static string EnsureNickname(SessionGroup parent)
    {
        if (string.IsNullOrWhiteSpace(parent.Client.Nickname))
        {
            parent.Client.Nickname = ProfileService.SettingConfig.Name;
        }
        string nickname = parent.Client.Nickname;
        if (!FileName.FileNames.ContainsKey(nickname))
        {
            FileName.Load(nickname);
        }
        ClientManager.GetUserNO(nickname);
        return nickname;
    }

    private static IPAddress GetLegacyServerAddress()
    {
        if (IPAddress.TryParse(ProfileService.SettingConfig.ServerIP, out IPAddress address) &&
            address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return address;
        }
        return IPAddress.Loopback;
    }

    private static int GetPremiumPoints(int premium) => premium switch
    {
        1 => 10000,
        2 => 30000,
        3 => 60000,
        4 => 120000,
        5 => 200000,
        _ => 0
    };

    private static bool Is(uint hash, string packetName) =>
        hash == Adler32Helper.GenerateAdler32_ASCII(packetName, 0);

    private static HashSet<uint> CreateNoOpPackets()
    {
        // Requests that the original P5136 server deliberately consumes
        // without sending a reply.
        string[] names =
        {
            "PcReportRaidOccur",
            "PqGameReportMyBadUdp",
            "PqEnterMagicHatPacket",
            "LoPingRequestPacket",
            "PqCountdownBoxPeriodPacket",
            "PqServerSideUdpBindCheck",
            "PqVipGradeCheck",
            "LoRqUpdateRiderSchoolDataPacket",
            "RmRiderTalkPacket",
            "PcReportStateInGame",
            "PqNeedTimerGiftEvent",
            "GameBoosterAddPacket",
            "LoRqCheckReplayItemPacket",
            "PqGetRecommandChatServerInfo",
            "LoCheckLoginEvent",
            "PqBlockWordLogPacket",
            "PqWriteActionLogPacket",
            "PqMissionAttendPacket",
            "PqEnterShopPacket",
            "PqAddTimeEventTimerPacket",
            "PqTimeShopOpenTimePacket",
            "PqItemPresetSlotDataList",
            "VipPlaytimeCheck",
            "LoRqEventRewardPacket",
            "LoRqGetRiderItemPacket",
            "LoRqUploadFilePacket",
            "PqGetRiderQuestUX2ndData"
        };

        var hashes = new HashSet<uint>();
        foreach (string name in names)
        {
            hashes.Add(Adler32Helper.GenerateAdler32_ASCII(name, 0));
        }
        return hashes;
    }
}
