using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using ExcData;
using KartRider.Common.Network;
using KartRider.Compatibility;
using KartRider.IO.Packet;
using Profile;
using RiderData;

namespace KartRider;

public class MyRoom
{
    public string[] Players = new string[8];
}

public class MyRoomData
{
    public static Dictionary<string, MyRoom> MyRooms = new Dictionary<string, MyRoom>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _memberToOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new object();

    public static MyRoom GetOrCreateRoom(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return null;

        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out var room))
            {
                room = new MyRoom();
                room.Players[0] = owner;
                MyRooms[owner] = room;
            }
            else if (string.IsNullOrEmpty(room.Players[0]))
            {
                room.Players[0] = owner;
            }
            return room;
        }
    }

    public static bool TryEnterMyRoom(string owner, string member)
    {
        return TryEnterMyRoom(owner, member, true);
    }

    private static bool TryEnterMyRoom(string owner, string member, bool broadcast)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(member))
            return false;

        if (!ClientManager.TryResolveKnownRider(owner, out owner) ||
            !ClientManager.TryResolveKnownRider(member, out member))
        {
            return false;
        }

        if (!string.Equals(owner, member, StringComparison.OrdinalIgnoreCase) &&
            ClientManager.GetParent(owner) == null)
            return false;

        string previousOwner = null;

        lock (_lock)
        {
            _memberToOwner.TryGetValue(member, out previousOwner);

            var room = GetOrCreateRoom(owner);
            if (room == null)
                return false;

            int targetSlot = -1;
            if (string.Equals(owner, member, StringComparison.OrdinalIgnoreCase))
            {
                targetSlot = 0;
            }
            else
            {
                for (int i = 1; i < room.Players.Length; i++)
                {
                    if (string.Equals(room.Players[i], member, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSlot = i;
                        break;
                    }
                }

                if (targetSlot < 0)
                {
                    for (int i = 1; i < room.Players.Length; i++)
                    {
                        if (string.IsNullOrEmpty(room.Players[i]))
                        {
                            targetSlot = i;
                            break;
                        }
                    }
                }
            }

            // Do not disturb the current room if the destination is full.
            if (targetSlot < 0)
                return false;

            if (!string.IsNullOrEmpty(previousOwner) &&
                !string.Equals(previousOwner, owner, StringComparison.OrdinalIgnoreCase))
            {
                _memberToOwner.Remove(member);
                RemoveMemberFromRoom(previousOwner, member);
            }

            for (int i = 0; i < room.Players.Length; i++)
            {
                if (i != targetSlot &&
                    string.Equals(room.Players[i], member, StringComparison.OrdinalIgnoreCase))
                {
                    room.Players[i] = null;
                }
            }
            room.Players[targetSlot] = member;
            _memberToOwner[member] = owner;
        }

        if (!broadcast)
            return true;

        if (!string.IsNullOrEmpty(previousOwner) && !string.Equals(previousOwner, owner, StringComparison.OrdinalIgnoreCase))
        {
            BroadcastRoomSlotData(previousOwner);
        }
        BroadcastRoomSlotData(owner);
        return true;
    }

    public static bool TryLeaveMyRoom(string member)
    {
        if (string.IsNullOrEmpty(member))
            return false;

        string owner;
        bool removed;

        lock (_lock)
        {
            if (!_memberToOwner.TryGetValue(member, out owner))
                return false;

            _memberToOwner.Remove(member);
            removed = RemoveMemberFromRoom(owner, member);
        }

        if (removed)
        {
            BroadcastRoomSlotData(owner);
        }
        return removed;
    }

    private static bool RemoveMemberFromRoom(string owner, string member)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(member))
            return false;

        if (!MyRooms.TryGetValue(owner, out var room))
            return false;

        bool removed = false;
        for (int i = 0; i < 8; i++)
        {
            if (string.Equals(room.Players[i], member, StringComparison.OrdinalIgnoreCase))
            {
                room.Players[i] = i == 0 ? owner : null;
                removed = true;
                break;
            }
        }

        if (removed && !_memberToOwner.Values.Any(value =>
            string.Equals(value, owner, StringComparison.OrdinalIgnoreCase)))
        {
            MyRooms.Remove(owner);
        }

        return removed;
    }

    private static bool IsRoomEmpty(MyRoom room)
    {
        if (room == null)
            return true;

        for (int i = 0; i < 8; i++)
        {
            if (!string.IsNullOrEmpty(room.Players[i]))
                return false;
        }
        return true;
    }

    public static string[] GetRoomPlayers(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return Array.Empty<string>();

        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out var room))
                return Array.Empty<string>();

            var result = new List<string>(8);
            for (int i = 0; i < 8; i++)
            {
                string member = room.Players[i];
                if (!string.IsNullOrEmpty(member) &&
                    _memberToOwner.TryGetValue(member, out string memberOwner) &&
                    string.Equals(memberOwner, owner, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(member);
                }
            }
            return result.ToArray();
        }
    }

    private static void BroadcastRoomSlotData(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return;

        string[] players;
        string[] recipients;
        lock (_lock)
        {
            if (!MyRooms.TryGetValue(owner, out MyRoom room))
                return;

            players = (string[])room.Players.Clone();
            recipients = players
                .Where(member => !string.IsNullOrEmpty(member) &&
                    _memberToOwner.TryGetValue(member, out string memberOwner) &&
                    string.Equals(memberOwner, owner, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        foreach (string member in recipients)
        {
            SessionGroup session = ClientManager.GetParent(member);
            if (session != null)
            {
                SendRoomSlotDataPacket(session, players);
            }
        }
    }

    public static void ChRpEnterMyRoomPacket(SessionGroup Parent, string Nickname, string nickname = null)
    {
        string owner = string.IsNullOrEmpty(nickname) ? Nickname : nickname;

        if (!ClientManager.TryResolveKnownRider(owner, out owner) ||
            (!string.Equals(owner, Nickname, StringComparison.OrdinalIgnoreCase) &&
             ClientManager.GetParent(owner) == null))
        {
            ChRpEnterMyRoomPacket(Parent, 3);
            return;
        }

        EnsureProfileLoaded(owner);

        string previousOwner = GetRoomOwnerByNickname(Nickname);
        if (!TryEnterMyRoom(owner, Nickname, false))
        {
            ChRpEnterMyRoomPacket(Parent, 1);
            return;
        }

        var ownerConfig = ProfileService.GetProfileConfig(owner);
        using (OutPacket outPacket = new OutPacket("ChRpEnterMyRoomPacket"))
        {
            outPacket.WriteString(owner);
            outPacket.WriteByte(0);
            outPacket.WriteShort(ownerConfig.MyRoom.MyRoom);
            outPacket.WriteByte(ownerConfig.MyRoom.MyRoomBGM);
            outPacket.WriteByte(ownerConfig.MyRoom.UseRoomPwd);
            outPacket.WriteByte(0);
            outPacket.WriteByte(ownerConfig.MyRoom.UseItemPwd);
            outPacket.WriteByte(ownerConfig.MyRoom.TalkLock);
            outPacket.WriteString(ownerConfig.MyRoom.RoomPwd);
            outPacket.WriteString("");
            outPacket.WriteString(ownerConfig.MyRoom.ItemPwd);
            outPacket.WriteShort(ownerConfig.MyRoom.MyRoomKart1);
            outPacket.WriteShort(ownerConfig.MyRoom.MyRoomKart2);
            Parent.Client.Send(outPacket);
        }

        if (!string.IsNullOrEmpty(previousOwner) &&
            !string.Equals(previousOwner, owner, StringComparison.OrdinalIgnoreCase))
        {
            BroadcastRoomSlotData(previousOwner);
        }
        BroadcastRoomSlotData(owner);
    }

    public static void ChRpEnterMyRoomPacket(SessionGroup Parent, byte errorCode = 3)
    {
        using (OutPacket outPacket = new OutPacket("ChRpEnterMyRoomPacket"))
        {
            outPacket.WriteString("");
            outPacket.WriteByte(errorCode); // 0：允许进入 3：玩家不存在 5：随机进入失败
            outPacket.WriteShort(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(0);
            outPacket.WriteByte(1);
            outPacket.WriteString("");//RoomPwd
            outPacket.WriteString("");
            outPacket.WriteString("");//ItemPwd 
            outPacket.WriteShort(0);
            outPacket.WriteShort(0);
            Parent.Client.Send(outPacket);
        }
        return;
    }

    public static void RmNotiMyRoomInfoPacket(SessionGroup Parent, string Nickname)
    {
        var myRoomConfig = ProfileService.GetProfileConfig(Nickname);
        using (OutPacket outPacket = new OutPacket("RmNotiMyRoomInfoPacket"))
        {
            outPacket.WriteShort(myRoomConfig.MyRoom.MyRoom);
            outPacket.WriteByte(myRoomConfig.MyRoom.MyRoomBGM);
            outPacket.WriteByte(myRoomConfig.MyRoom.UseRoomPwd);
            outPacket.WriteByte(0);
            outPacket.WriteByte(myRoomConfig.MyRoom.UseItemPwd);
            outPacket.WriteByte(myRoomConfig.MyRoom.TalkLock);
            outPacket.WriteString(myRoomConfig.MyRoom.RoomPwd);
            outPacket.WriteString("");
            outPacket.WriteString(myRoomConfig.MyRoom.ItemPwd);
            outPacket.WriteShort(myRoomConfig.MyRoom.MyRoomKart1);
            outPacket.WriteShort(myRoomConfig.MyRoom.MyRoomKart2);
            Parent.Client.Send(outPacket);
        }
    }

    public static void RmRiderTalkPacket(string Nickname, string Message)
    {
        if (string.IsNullOrEmpty(Nickname) || Message == null)
            return;

        string owner;
        string[] members;
        int slotIndex = -1;
        lock (_lock)
        {
            if (!_memberToOwner.TryGetValue(Nickname, out owner) ||
                !MyRooms.TryGetValue(owner, out MyRoom room))
            {
                return;
            }

            for (int i = 0; i < room.Players.Length; i++)
            {
                if (string.Equals(room.Players[i], Nickname, StringComparison.OrdinalIgnoreCase))
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0)
                return;

            // The sender renders its own input locally. Echo it only to the other
            // sessions that are still mapped to this room.
            members = room.Players
                .Where(member => !string.IsNullOrEmpty(member) &&
                    !string.Equals(member, Nickname, StringComparison.OrdinalIgnoreCase) &&
                    _memberToOwner.TryGetValue(member, out string memberOwner) &&
                    string.Equals(memberOwner, owner, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        using (OutPacket outPacket = new OutPacket("RmRiderEchoPacket"))
        {
            outPacket.WriteInt(slotIndex);
            outPacket.WriteString(Message);
            foreach (string member in members)
            {
                ClientManager.GetParent(member)?.Client.Send(outPacket);
            }
        }
    }

    public static string GetRoomOwnerByNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return null;

        lock (_lock)
        {
            if (_memberToOwner.TryGetValue(nickname, out var owner))
                return owner;
            return null;
        }
    }

    public static void RmCharPosPacket(string nickname, int claimedSlot, float[] transform)
    {
        if (string.IsNullOrEmpty(nickname) || transform == null || transform.Length != 6)
            return;

        string owner;
        string[] members;
        int actualSlot = -1;
        lock (_lock)
        {
            if (!_memberToOwner.TryGetValue(nickname, out owner) ||
                !MyRooms.TryGetValue(owner, out MyRoom room))
            {
                return;
            }

            for (int i = 0; i < room.Players.Length; i++)
            {
                if (string.Equals(room.Players[i], nickname, StringComparison.OrdinalIgnoreCase))
                {
                    actualSlot = i;
                    break;
                }
            }
            if (actualSlot < 0 || actualSlot != claimedSlot)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "MYROOM-POS-DROP",
                    null,
                    null,
                    nickname,
                    $"owner={owner}; claimedSlot={claimedSlot}; actualSlot={actualSlot}");
                return;
            }

            members = room.Players
                .Where(member => !string.IsNullOrEmpty(member) &&
                    !string.Equals(member, nickname, StringComparison.OrdinalIgnoreCase) &&
                    _memberToOwner.TryGetValue(member, out string memberOwner) &&
                    string.Equals(memberOwner, owner, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        using (OutPacket outPacket = new OutPacket("RmCharPosPacket"))
        {
            outPacket.WriteInt(actualSlot);
            foreach (float value in transform)
                outPacket.WriteFloat(value);

            foreach (string member in members)
            {
                ClientManager.GetParent(member)?.Client.Send(outPacket);
            }
        }
    }

    private static void EnsureProfileLoaded(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return;

        if (!Profile.FileName.FileNames.ContainsKey(nickname))
        {
            Profile.FileName.Load(nickname);
        }
    }

    private static IPEndPoint GetPlayerEndPoint(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return new IPEndPoint(System.Net.IPAddress.Any, 0);

        EnsureProfileLoaded(nickname);
        var profile = ProfileService.GetProfileConfig(nickname);
        try
        {
            IPEndPoint client = ClientManager.ClientToIPEndPoint(profile.Rider.ClientId);
            if (client != null)
            {
                return new IPEndPoint(client.Address, profile.Rider.P2pPort);
            }
        }
        catch
        {
        }

        return new IPEndPoint(System.Net.IPAddress.Any, 0);
    }

    private static void WriteEmptySlot(OutPacket outPacket)
    {
        outPacket.WriteBytes(new byte[
            ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 ? 122 : 132]);
        outPacket.WriteByte(0xFF);
    }

    private static void WritePlayerSlot(OutPacket outPacket, string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            WriteEmptySlot(outPacket);
            return;
        }

        EnsureProfileLoaded(nickname);
        if (!ClientManager.NicknameToUserNO.ContainsKey(nickname))
        {
            WriteEmptySlot(outPacket);
            return;
        }
        var profile = ProfileService.GetProfileConfig(nickname);

        outPacket.WriteUInt(ClientManager.GetUserNO(nickname));
        outPacket.WriteEndPoint(GetPlayerEndPoint(nickname));
        outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, 0));
        outPacket.WriteString(nickname);
        GameSupport.GetRider(nickname, outPacket);
        outPacket.WriteUInt(profile.Rider.RP);
        outPacket.WriteBytes(new byte[29]);
        outPacket.WriteString(profile.Rider.ClubName);
        outPacket.WriteByte(0);
    }

    public static void RmSlotDataPacket(SessionGroup Parent, string Nickname)
    {
        string[] players = null;
        string owner = GetRoomOwnerByNickname(Nickname);
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(owner) && MyRooms.TryGetValue(owner, out var foundRoom))
            {
                players = (string[])foundRoom.Players.Clone();
            }
        }

        SendRoomSlotDataPacket(Parent, players);
    }

    private static void SendRoomSlotDataPacket(SessionGroup Parent, string[] players)
    {
        using (OutPacket outPacket = new OutPacket("RmSlotDataPacket"))
        {
            if (players != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    WritePlayerSlot(outPacket, players[i]);
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    WriteEmptySlot(outPacket);
                }
            }

            Parent.Client.Send(outPacket);
        }
    }

    public static void ChRpSecedeMyRoomPacket(SessionGroup Parent, string Nickname)
    {
        TryLeaveMyRoom(Nickname);
        using (OutPacket outPacket = new OutPacket("ChRpSecedeMyRoomPacket"))
        {
            outPacket.WriteByte(1);
            Parent.Client.Send(outPacket);
        }
    }

    public static void RmRequestItemsPacket(SessionGroup Parent, string Nickname)
    {
        string MasterNickname = GetRoomOwnerByNickname(Nickname);
        if (string.IsNullOrEmpty(MasterNickname))
        {
            using (OutPacket outPacket = new OutPacket("RmOwnerItemEnchantPacket"))
            {
                outPacket.WriteInt(0);
                Parent.Client.Send(outPacket);
            }
        }
        else
        {
            if (!FileName.FileNames.ContainsKey(MasterNickname))
            {
                FileName.Load(MasterNickname);
            }
            var filename = FileName.FileNames[MasterNickname];

            RmOwnerItemEnchantPacket(Parent, filename);
            RmOwnerItemPacket(Parent, filename);
        }
    }

    public static void RmOwnerItemEnchantPacket(SessionGroup Parent, fileName filename)
    {
        var TuneList = new List<Tune>();
        if (File.Exists(filename.TuneData_LoadFile))
        {
            TuneList = JsonHelper.DeserializeNoBom<List<Tune>>(filename.TuneData_LoadFile) ?? new List<Tune>();
        }

        int range = 26;//分批次数
        int times = TuneList.Count / range + (TuneList.Count % range > 0 ? 1 : 0);
        for (int i = 0; i < times; i++)
        {
            var tempList = TuneList.GetRange(i * range, (i + 1) * range > TuneList.Count ? (TuneList.Count - i * range) : range);
            int TuneCount = tempList.Count;
            using (OutPacket oPacket = new OutPacket("RmOwnerItemEnchantPacket"))
            {
                oPacket.WriteInt(TuneCount);
                for (var f = 0; f < TuneCount; f++)
                {
                    oPacket.WriteShort(3);
                    oPacket.WriteShort(tempList[f].ID);
                    oPacket.WriteShort(tempList[f].SN);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].Tune1);
                    oPacket.WriteShort(tempList[f].Tune2);
                    oPacket.WriteShort(tempList[f].Tune3);
                    oPacket.WriteShort(tempList[f].Slot1);
                    oPacket.WriteShort(tempList[f].Count1);
                    oPacket.WriteShort(tempList[f].Slot2);
                    oPacket.WriteShort(tempList[f].Count2);
                }
                Parent.Client.Send(oPacket);
            }
        }
    }

    public static void RmOwnerItemPacket(SessionGroup Parent, fileName filename)
    {
        int range = 26;//分批次数
        int count = 1;
        bool isKorean5136 = ClientBuildProfiles.Active.Build == ClientBuild.Korean5136;

        var newkart = new List<NewKart>();
        if (File.Exists(filename.NewKart_LoadFile))
        {
            newkart = JsonHelper.DeserializeNoBom<List<NewKart>>(filename.NewKart_LoadFile) ?? new List<NewKart>();
        }
        int newkartTimes = newkart.Count / range + (newkart.Count % range > 0 ? 1 : 0);

        var PartsList = new List<Parts>();
        if (File.Exists(filename.PartsData_LoadFile))
        {
            PartsList = JsonHelper.DeserializeNoBom<List<Parts>>(filename.PartsData_LoadFile) ?? new List<Parts>();
        }
        int PartsListTimes = PartsList.Count / range + (PartsList.Count % range > 0 ? 1 : 0);

        var Parts12List = new List<Parts12>();
        if (File.Exists(filename.Parts12Data_LoadFile))
        {
            Parts12List = JsonHelper.DeserializeNoBom<List<Parts12>>(filename.Parts12Data_LoadFile) ?? new List<Parts12>();
        }
        int Parts12ListTimes = Parts12List.Count / range + (Parts12List.Count % range > 0 ? 1 : 0);

        int AllCount = newkartTimes + PartsListTimes + (isKorean5136 ? 0 : Parts12ListTimes);

        if (newkart.Count == 0)
        {
            using (OutPacket oPacket = new OutPacket("RmOwnerItemPacket"))
            {
                oPacket.WriteInt(1);
                oPacket.WriteInt(1);
                oPacket.WriteInt(0);
                oPacket.WriteBytes(new byte[isKorean5136 ? 8 : 16]);
                oPacket.WriteInt(1);
                oPacket.WriteInt(1);
                Parent.Client.Send(oPacket);
            }
            return;
        }

        for (int i = 0; i < newkartTimes; i++)
        {
            var tempList = newkart.GetRange(i * range, (i + 1) * range > newkart.Count ? (newkart.Count - i * range) : range);
            using (OutPacket oPacket = new OutPacket("RmOwnerItemPacket"))
            {
                oPacket.WriteInt(newkartTimes);
                oPacket.WriteInt(i + 1);
                oPacket.WriteInt(tempList.Count);
                foreach (var Kart in tempList)
                {
                    oPacket.WriteUShort(3);
                    oPacket.WriteUShort(Kart.KartID);
                    oPacket.WriteUShort(Kart.KartSN);
                    oPacket.WriteUShort(1);
                    oPacket.WriteByte((byte)((Program.PreventItem ? 1 : 0)));
                    oPacket.WriteByte(0);
                    oPacket.WriteShort(-1);
                    oPacket.WriteShort(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteShort(0);
                }
                oPacket.WriteBytes(new byte[isKorean5136 ? 8 : 16]);
                oPacket.WriteInt(AllCount);
                oPacket.WriteInt(count++);
                Parent.Client.Send(oPacket);
            }
        }

        for (int i = 0; i < PartsListTimes; i++)
        {
            var tempList = PartsList.GetRange(i * range, (i + 1) * range > PartsList.Count ? (PartsList.Count - i * range) : range);
            int parts = tempList.Count;
            using (OutPacket oPacket = new OutPacket("RmOwnerItemPacket"))
            {
                oPacket.WriteInt(PartsListTimes);
                oPacket.WriteInt(i + 1);
                oPacket.WriteInt(0);
                oPacket.WriteInt(0);
                oPacket.WriteInt(parts);
                for (var f = 0; f < parts; f++)
                {
                    oPacket.WriteShort(tempList[f].ID);
                    oPacket.WriteShort(tempList[f].SN);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(-1);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].Engine);
                    oPacket.WriteByte(tempList[f].EngineGrade);
                    oPacket.WriteShort(tempList[f].EngineValue);
                    oPacket.WriteShort(tempList[f].Handle);
                    oPacket.WriteByte(tempList[f].HandleGrade);
                    oPacket.WriteShort(tempList[f].HandleValue);
                    oPacket.WriteShort(tempList[f].Wheel);
                    oPacket.WriteByte(tempList[f].WheelGrade);
                    oPacket.WriteShort(tempList[f].WheelValue);
                    oPacket.WriteShort(tempList[f].Booster);
                    oPacket.WriteByte(tempList[f].BoosterGrade);
                    oPacket.WriteShort(tempList[f].BoosterValue);
                    oPacket.WriteShort(tempList[f].Coating);
                    oPacket.WriteByte(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].TailLamp);
                    oPacket.WriteByte(0);
                    oPacket.WriteShort(0);
                }
                if (!isKorean5136)
                {
                    oPacket.WriteBytes(new byte[8]);
                }
                oPacket.WriteInt(AllCount);
                oPacket.WriteInt(count++);
                Parent.Client.Send(oPacket);
            }
        }

        for (int i = 0; !isKorean5136 && i < Parts12ListTimes; i++)
        {
            var tempList = Parts12List.GetRange(i * range, (i + 1) * range > Parts12List.Count ? (Parts12List.Count - i * range) : range);
            int parts12 = tempList.Count;
            using (OutPacket oPacket = new OutPacket("RmOwnerItemPacket"))
            {
                oPacket.WriteInt(Parts12ListTimes);
                oPacket.WriteInt(i + 1);
                oPacket.WriteInt(0);
                oPacket.WriteInt(0);
                oPacket.WriteInt(0);
                oPacket.WriteInt(parts12);
                for (var f = 0; f < parts12; f++)
                {
                    oPacket.WriteShort(tempList[f].ID);
                    oPacket.WriteShort(tempList[f].SN);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(-1);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].Engine);
                    oPacket.WriteShort(1);
                    oPacket.WriteShort(tempList[f].Handle);
                    oPacket.WriteShort(1);
                    oPacket.WriteShort(tempList[f].Wheel);
                    oPacket.WriteShort(1);
                    oPacket.WriteShort(tempList[f].Booster);
                    oPacket.WriteShort(1);
                    oPacket.WriteShort(tempList[f].Coating);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].TailLamp);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].BoosterEffect);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(0);
                    oPacket.WriteShort(tempList[f].ExceedType);
                    oPacket.WriteShort(0);
                }
                oPacket.WriteBytes(new byte[4]);
                oPacket.WriteInt(AllCount);
                oPacket.WriteInt(count++);
                Parent.Client.Send(oPacket);
            }
        }
    }
}
