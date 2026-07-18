using ExcData;
using KartRider.Common.Network;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using KartRider.Compatibility;
using KartRider_PacketName;
using Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using System.Security;
using System.Data.Common;
using System.Windows.Forms;

namespace KartRider;

internal enum RaceSettlementPacketKind
{
    GameControl,
    GameNextStage,
    GameResult
}

public static class MultyPlayer
{
    public static List<short> itemProb_indi = new List<short>();
    public static List<short> itemProb_team = new List<short>();
    public static Dictionary<short, AICharacter> aiCharacterDict = new Dictionary<short, AICharacter>();
    public static Dictionary<short, AIKart> aiKartDict = new Dictionary<short, AIKart>();
    public static Dictionary<string, byte> StartTimeAttack = new Dictionary<string, byte>();
    public static int[] teamPoints = { 10, 8, 6, 5, 4, 3, 2, 1 };

    public static IPEndPoint GetServerEndPoint(SessionGroup Parent)
    {
        int serverPort = ClientBuildProfiles.Active.Ports.ResolveLoginTcpPort(
            ClientServerRuntime.ConfiguredPort);
        return new IPEndPoint(ClientServerRuntime.AdvertisedAddress, serverPort);
    }

    public static uint ConvertTick()
    {
        // 1. 先处理负数（TickCount64理论上不会为负，但做防御性判断）
        if (Environment.TickCount64 < 0)
        {
            return 0; // 或根据需求返回uint.MaxValue，TickCount64实际不会为负
        }

        // 2. 判断是否超出uint范围（uint.MaxValue是4294967295）
        if (Environment.TickCount64 > uint.MaxValue)
        {
            return (uint)(Environment.TickCount64 % uint.MaxValue); // 溢出时返回余数
        }

        // 3. 未溢出则直接转换
        return (uint)Environment.TickCount64;
    }

    private static void WriteGameControlBody(OutPacket packet, int controlType, uint value0)
    {
        if (controlType == 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(controlType),
                "GameControl type 2 has a versioned race-result block and must be serialized separately.");
        }

        // P5136 and the modern client share this canonical non-type-2 body.
        // The old server emitted only the first nine bytes and relied on the
        // client archive treating EOF as zero-valued fields.
        packet.WriteInt(controlType);
        packet.WriteByte(0); // optional pair absent
        packet.WriteUInt(value0);
        packet.WriteInt(0);
        packet.WriteInt(0);
        packet.WriteByte(0); // optional seven-dword block absent
        packet.WriteInt(0);
        packet.WriteBytes(new byte[40]);
        packet.WriteBytes(new byte[10]);
        packet.WriteInt(0);
        packet.WriteInt(0);
        packet.WriteEncByte(0);
    }

    public static Dictionary<int, int> GetAllRanks(Dictionary<int, uint> timeData)
    {
        if (timeData.Count == 0)
            return new Dictionary<int, int>();

        // 按值降序排序（值越大排名越靠前）
        var sortedItems = timeData
            .OrderBy(item => item.Value)
            .ToList();

        var ranks = new Dictionary<int, int>();

        // 排名从0开始，逐个分配（相同值也会依次+1）
        for (int i = 0; i < sortedItems.Count; i++)
        {
            ranks[sortedItems[i].Key] = i; // 直接使用索引作为排名
        }

        return ranks;
    }

    static void Start(SessionGroup Parent, int roomId)
    {
        GameRoom room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine($"Room {roomId} does not exist.");
            return;
        }

        var ready = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (RoomMember member in room._slots.Concat(room.ObIDs))
        {
            if (member is Player player && !string.IsNullOrEmpty(player.Nickname))
                ready[player.Nickname] = false;
        }

        long handshakeGeneration;
        lock (room)
        {
            if (room.StartHandshakePending || room.StartTicks != 0)
                return;

            handshakeGeneration = ++room.StartHandshakeGeneration;
            room.Ready = ready;
            room.StartHandshakePending = true;
        }

        // Never hold ClientSession.Parent.m_lock while waiting for the other
        // clients. Their readiness packets must be allowed to run immediately.
        _ = WaitForRaceReadinessAsync(
            Parent,
            room,
            ready,
            handshakeGeneration);
    }

    private static async Task WaitForRaceReadinessAsync(
        SessionGroup parent,
        GameRoom room,
        ConcurrentDictionary<string, bool> ready,
        long handshakeGeneration)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (!ReferenceEquals(RoomManager.GetRoom(room.RoomId), room))
                return;

            lock (room)
            {
                if (!room.StartHandshakePending ||
                    room.StartTicks != 0 ||
                    room.StartHandshakeGeneration != handshakeGeneration ||
                    !ReferenceEquals(room.Ready, ready))
                    return;
            }

            if (ready.Values.All(isReady => isReady))
            {
                Set_startTrigger(parent, room, handshakeGeneration);
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        lock (room)
        {
            if (!room.StartHandshakePending ||
                room.StartTicks != 0 ||
                room.StartHandshakeGeneration != handshakeGeneration ||
                !ReferenceEquals(room.Ready, ready))
                return;
        }
        Set_startTrigger(parent, room, handshakeGeneration);
    }

    // Retained temporarily as a readable record of the legacy behavior. It is
    // intentionally not called; the asynchronous implementation above avoids
    // blocking the receive callback for up to thirty seconds.
    static void StartBlockingLegacy(SessionGroup Parent, int roomId)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine($"방 {roomId}이(가) 없습니다.");
            return;
        }

        lock (room)
        {
            if (room.StartHandshakePending || room.StartTicks != 0)
            {
                return;
            }
            room.StartHandshakePending = true;
        }

        room.Ready = new ConcurrentDictionary<string, bool>();

        foreach (var player in room._slots)
        {
            if (player is Player p && !string.IsNullOrEmpty(p.Nickname))
            {
                room.Ready[p.Nickname] = false;
            }
        }
        foreach (var player in room.ObIDs)
        {
            if (player is Player p && !string.IsNullOrEmpty(p.Nickname))
            {
                room.Ready[p.Nickname] = false;
            }
        }

        // 标记是否所有值都为true
        bool allReady = true;

        // 第一步：遍历字典值，检查是否有false
        foreach (bool value in room.Ready.Values)
        {
            if (!value) // 只要有一个值为false，标记为未全部就绪
            {
                allReady = false;
                break; // 找到false后提前退出遍历，提升效率
            }
        }

        // 可选：添加退出条件，防止无限循环（比如超时）
        // 示例：累计等待10秒后退出
        int waitCount = 0;

        // 第二步：用while循环判断（根据allReady的值执行逻辑）
        // 场景1：等待所有值变为true（循环直到全部为true）
        while (!allReady)
        {
            Console.WriteLine("준비하지 않은 플레이어가 있어 대기합니다...");

            // 模拟：重新检查字典值（实际场景中可替换为刷新数据的逻辑）
            allReady = true;
            foreach (bool value in room.Ready.Values)
            {
                if (!value)
                {
                    allReady = false;
                    break;
                }
            }

            // 模拟等待（避免死循环，实际场景可替换为业务逻辑）
            System.Threading.Thread.Sleep(1000);

            waitCount++;
            if (waitCount >= 30)
            {
                Set_startTrigger(Parent, room);
                return;
            }
        }

        // 循环结束后输出结果
        if (allReady)
        {
            Set_startTrigger(Parent, room);
            return;
        }
    }

    static void Set_startTrigger(
        SessionGroup Parent,
        GameRoom room,
        long handshakeGeneration = 0)
    {
        if (handshakeGeneration == 0)
            handshakeGeneration = room.StartHandshakeGeneration;
        var onceTimer = new System.Timers.Timer();
        onceTimer.Interval = 1000;
        onceTimer.Elapsed += new System.Timers.ElapsedEventHandler(
            (s, _event) => startTrigger(
                Parent,
                room,
                handshakeGeneration,
                s,
                _event));
        onceTimer.AutoReset = false;
        onceTimer.Start();
    }

    static void startTrigger(
        SessionGroup Parent,
        GameRoom room,
        long handshakeGeneration,
        object sender,
        System.Timers.ElapsedEventArgs e)
    {
        lock (room)
        {
            if (room.StartTicks != 0 ||
                !room.StartHandshakePending ||
                room.StartHandshakeGeneration != handshakeGeneration)
            {
                Console.WriteLine("startTrigger: 방 시작 시간이 이미 설정되어 있어 건너뜁니다.");
                return;
            }
            room.StartTicks = ConvertTick() + 3000;
            room.StartHandshakePending = false;
            room.TimeData = new Dictionary<int, uint>();
            room.Ranking = new Dictionary<int, int>();
            room.EndTicks = 0;
            room.SettlementClosed = false;
            room.Ready = new ConcurrentDictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);
        }
        using (OutPacket oPacket = new OutPacket("GameAiMasterSlotNoticePacket"))
        {
            oPacket.WriteInt();
            BroadCast(room.RoomId, oPacket);
        }
        using (OutPacket oPacket = new OutPacket("GameControlPacket"))
        {
            WriteGameControlBody(oPacket, 1, room.StartTicks);
            BroadCast(room.RoomId, oPacket);
        }
        Console.WriteLine("시작 틱 = {0}", room.StartTicks);
    }

    private static bool TryBeginSettlement(
        GameRoom room,
        out uint endTicks,
        out long raceGeneration)
    {
        lock (room)
        {
            if (room.EndTicks != 0 || room.StartTicks == 0 || room.SettlementClosed)
            {
                endTicks = 0;
                raceGeneration = 0;
                return false;
            }

            endTicks = ConvertTick() + 10000;
            room.EndTicks = endTicks;
            raceGeneration = room.StartHandshakeGeneration;
            return true;
        }
    }

    internal static RaceSettlementPacketKind[] GetSettlementPacketOrder(ClientBuild build)
    {
        if (build == ClientBuild.Korean5136)
        {
            // P5136 builds the ceremony roster from the result snapshot when
            // GameControl type 4 advances to the final stage. Sending type 4
            // first leaves only the locally known rider in that roster.
            return new[]
            {
                RaceSettlementPacketKind.GameNextStage,
                RaceSettlementPacketKind.GameResult,
                RaceSettlementPacketKind.GameControl
            };
        }

        return new[]
        {
            RaceSettlementPacketKind.GameControl,
            RaceSettlementPacketKind.GameNextStage,
            RaceSettlementPacketKind.GameResult
        };
    }

    private static void DispatchSettlementPackets(
        GameRoom room,
        RoomMember[] members,
        Dictionary<int, uint> timeData,
        uint endTicks)
    {
        RaceSettlementPacketKind[] order = GetSettlementPacketOrder(
            ClientBuildProfiles.Active.Build);
        Console.WriteLine(
            "[SETTLEMENT] room={0}, players={1}, order={2}",
            room.RoomId,
            members.Count(member => member is Player),
            string.Join(" -> ", order));

        foreach (RaceSettlementPacketKind packetKind in order)
        {
            switch (packetKind)
            {
                case RaceSettlementPacketKind.GameControl:
                    using (OutPacket outPacket = new OutPacket("GameControlPacket"))
                    {
                        WriteGameControlBody(outPacket, 4, endTicks + 6000);
                        BroadCast(room.RoomId, outPacket);
                    }
                    break;

                case RaceSettlementPacketKind.GameNextStage:
                    GameNextStagePacket(room);
                    break;

                case RaceSettlementPacketKind.GameResult:
                    GameResultPacket(room, members, timeData);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown settlement packet kind: {packetKind}");
            }
        }
    }

    static void Set_settleTrigger(GameRoom room, long raceGeneration)
    {
        var onceTimer = new System.Timers.Timer();
        onceTimer.Interval = 10000;
        onceTimer.Elapsed += new System.Timers.ElapsedEventHandler(
            (s, _event) => settleTrigger(room, raceGeneration, s, _event));
        onceTimer.AutoReset = false;
        onceTimer.Start();
    }

    static void settleTrigger(
        GameRoom room,
        long raceGeneration,
        object sender,
        System.Timers.ElapsedEventArgs e)
    {
        if (!ReferenceEquals(RoomManager.GetRoom(room.RoomId), room))
        {
            return;
        }

        uint endTicks;
        RoomMember[] participantSnapshot;
        Dictionary<int, uint> timeDataSnapshot;
        lock (room)
        {
            if (room.StartHandshakeGeneration != raceGeneration ||
                room.EndTicks == 0 ||
                room.StartTicks == 0 ||
                room.SettlementClosed)
            {
                return;
            }
            endTicks = room.EndTicks;
            room.SettlementClosed = true;
            participantSnapshot = room._IDs.ToArray();
            timeDataSnapshot = new Dictionary<int, uint>(room.TimeData);
        }

        DispatchSettlementPackets(
            room,
            participantSnapshot,
            timeDataSnapshot,
            endTicks);

        int firstID = room.Ranking.FirstOrDefault(x => x.Value == 0).Key;
        if (room.RoomMaster < 8 && RoomManager.TryGetIdDetail(room.RoomId, firstID) is Player p1)
        {
            room.RoomMaster = firstID;
            p1.PlayerType = 2;
        }
        else if (room.GetOBCount() < 1 && RoomManager.TryGetIdDetail(room.RoomId, firstID) is Player p2)
        {
            room.RoomMaster = firstID;
            p2.PlayerType = 2;
        }
        InitRoom(room);
        Console.WriteLine("종료 틱 = {0}", endTicks + 6000);
    }

    public static void Clientsession(SessionGroup Parent, uint hash, InPacket iPacket)
    {
        if (hash == Adler32Helper.GenerateAdler32_ASCII("GameSlotPacket", 0))
        {
            SlotData.GameSlotPacket(Parent, iPacket);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GameControlPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            if (iPacket.Available < 5)
            {
                Console.WriteLine("GameControlPacket: 헤더가 잘렸습니다. (남은 데이터 {0}바이트)", iPacket.Available);
                return;
            }

            int state = iPacket.ReadInt();
            byte hasPair = iPacket.ReadByte();
            if (hasPair != 0)
            {
                if (iPacket.Available < 8)
                {
                    Console.WriteLine("GameControlPacket: 선택 데이터 쌍이 잘렸습니다.");
                    return;
                }
                iPacket.ReadInt();
                iPacket.ReadInt();
            }

            if (iPacket.Available < 4)
            {
                Console.WriteLine("GameControlPacket: 상태 {0}의 value0 값이 없습니다.", state);
                return;
            }
            uint value0 = iPacket.ReadUInt();
            //start
            if (state == 0 && room.StartTicks == 0)
            {
                Start(Parent, roomId);
            }
            //finish
            else if (state == 2)
            {
                uint time = value0;
                var player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);
                if (player != null)
                {
                    using (OutPacket oPacket = new OutPacket("GameRaceTimePacket"))
                    {
                        oPacket.WriteInt(player.ID);
                        oPacket.WriteUInt(time);
                        BroadCast(roomId, oPacket);
                    }
                    lock (room)
                    {
                        if (!room.SettlementClosed)
                            room.TimeData.TryAdd(player.ID, time);
                    }
                    Console.WriteLine("GameControlPacket, ID={0}, 시간={1}", player.ID, time);
                }
                if (TryBeginSettlement(
                    room,
                    out uint endTicks,
                    out long raceGeneration))
                {
                    using (OutPacket oPacket = new OutPacket("GameControlPacket"))
                    {
                        WriteGameControlBody(oPacket, 3, endTicks);
                        BroadCast(roomId, oPacket, Parent.Client.Nickname);
                    }
                    Set_settleTrigger(room, raceGeneration);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChGetRoomListRequestPacket"))
        {
            int page = iPacket.ReadInt();
            byte roomListType = iPacket.ReadByte();
            byte roomListMode = iPacket.ReadByte();
            Console.WriteLine(
                "방 목록 요청: 토큰={0}, 유형={1}, 모드={2}, P5136 게임 유형={3}, 채널={4}",
                page,
                roomListType,
                roomListMode,
                Parent.P5136ChannelGameType,
                Parent.P5136ChannelId);
            bool isKorean5136RoomList =
                ClientBuildProfiles.Active.Build == ClientBuild.Korean5136;
            byte selectedChannelGameType = Parent.P5136ChannelGameType;
            bool hasExpectedRoomGameType = Korean5136Protocol.TryResolveRoomGameType(
                selectedChannelGameType,
                out byte expectedRoomGameType);
            bool requestMatchesSelectedChannel =
                !hasExpectedRoomGameType || roomListType == expectedRoomGameType;
            if (isKorean5136RoomList && !requestMatchesSelectedChannel)
            {
                Console.WriteLine(
                    "방 목록 요청 거부: P5136 채널 게임 유형 {0}은(는) 방 유형 {1}을 요구하지만 {2}을(를) 받았습니다.",
                    selectedChannelGameType,
                    expectedRoomGameType,
                    roomListType);
            }
            var rooms = RoomManager.GetRoomsByPage(
                page,
                isKorean5136RoomList
                    ? room => requestMatchesSelectedChannel &&
                              IsRoomVisibleToSession(Parent, room)
                    : null,
                out int visibleRoomCount);
            using (OutPacket oPacket = new OutPacket("ChGetRoomListReplyPacket"))
            {
                Console.WriteLine(
                    "현재 채널 방 수: {0} (서버 전체: {1})",
                    visibleRoomCount,
                    RoomManager._rooms.Count);
                // Total room count was inserted before the common page field
                // and room vector after P5136.
                if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
                {
                    oPacket.WriteInt(visibleRoomCount); // 房间总数
                }
                // The client stores the returned common/page token back into
                // the same state member used by the request producer.
                oPacket.WriteInt(page);
                oPacket.WriteInt(rooms.Count); // 房间数量
                foreach (var _room in rooms)
                {
                    oPacket.WriteShort((short)_room.Key);
                    oPacket.WriteString(_room.Value.RoomName); // 房间名称
                    oPacket.WriteUInt(_room.Value.track); // 赛道
                    oPacket.WriteBool(_room.Value.Lock); // 是否上锁
                    oPacket.WriteByte(_room.Value.GameType); // 模式
                    oPacket.WriteByte(_room.Value.SpeedType); // 速度模式
                    oPacket.WriteBool(_room.Value.Started); // 房间状态
                    oPacket.WriteByte((byte)(8 - _room.Value.CloseSlotIds.Count)); // 房间最大人数
                    oPacket.WriteByte((byte)_room.Value.GetCount()); // 房间人数
                    // P5136 entries end after two common bytes. Modern adds a
                    // trailing u32 to every room record.
                    oPacket.WriteBytes(new byte[
                        ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 ? 2 : 6]);
                }
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqChannelSwitch", 0))
        {
            int length = iPacket.ReadInt();
            iPacket.ReadBytes(length);
            byte requestedGameType = iPacket.ReadByte();
            ushort preferredChannelId = iPacket.ReadUShort();
            bool isKorean5136 = ClientBuildProfiles.Active.Build == ClientBuild.Korean5136;

            // Modern channel.xml is a 1:1 table, so its request value resolves
            // as gameType - 1. P5136 instead advertises a separate concrete
            // channel table: for example speedIndiCombine (67) owns records
            // 11/12 and speedIndiInfinit (23) owns records 13-16. Returning
            // 66 for game type 67 makes the client fail its exact record lookup
            // and discard the selected mode before opening the create-room UI.
            ushort channel;
            if (isKorean5136)
            {
                if (!Korean5136Protocol.TryResolveChannelId(
                    requestedGameType,
                    preferredChannelId,
                    out channel))
                {
                    Console.WriteLine(
                        "채널 전환 거부 (P5136에서 알 수 없는 채널 그룹): 게임 유형={0}, 요청 채널={1}",
                        requestedGameType,
                        preferredChannelId);
                    return;
                }
            }
            else
            {
                channel = requestedGameType == 0
                    ? (ushort)0
                    : (ushort)(requestedGameType - 1);
            }

            Channel channelData = null;
            if (!isKorean5136 && channel <= byte.MaxValue)
            {
                byte modernChannel = (byte)channel;
                channelData = GameSupport.Channels.ContainsKey(modernChannel)
                    ? GameSupport.Channels[modernChannel]
                    : null;
            }

            if (isKorean5136)
            {
                // The P5136 static catalog describes mode groups and concrete
                // records but has no modern createSpeed field. Keep the server
                // driving preset explicit so room creation does not depend on
                // the old hard-coded fallback.
                StartTimeAttack[Parent.Client.Nickname] =
                    ProfileService.SettingConfig.SpeedType;
                Console.WriteLine(
                    "채널 전환 (P5136 목록): 게임 유형={0}, 요청 채널={1}, 선택 채널={2}, 속도 유형={3}",
                    requestedGameType,
                    preferredChannelId,
                    channel,
                    StartTimeAttack[Parent.Client.Nickname]);
            }
            else
            {
                if (channelData == null)
                {
                    return;
                }
                StartTimeAttack[Parent.Client.Nickname] = channelData.CreateSpeed;
                Console.WriteLine("채널 전환: 채널={0}", channelData.Name);
            }

            // 获取服务器IP地址
            IPEndPoint serverIPEndPoint = GetServerEndPoint(Parent);
            ushort migrationToken = preferredChannelId;
            if (isKorean5136 && !ClientManager.TryBeginChannelMigration(
                Parent,
                channel,
                requestedGameType,
                out migrationToken,
                out string migrationRejection))
            {
                PacketTrace.LogEvent(
                    "LOGIN-TCP",
                    "MIGRATION-REJECT",
                    Parent.Client.GetLocalEndPoint(),
                    Parent.Client.GetRemoteEndPoint(),
                    Parent.Client.Nickname,
                    migrationRejection);
                return;
            }

            using (OutPacket oPacket = new OutPacket("PrChannelSwitch"))
            {
                oPacket.WriteInt(0);
                oPacket.WriteUShort(channel);
                // P5136 copies this opaque token into PqChannelMovein. A
                // per-request value separates an authorized migration from a
                // second connection that only knows the same user number.
                oPacket.WriteUShort(migrationToken);
                oPacket.WriteEndPoint(serverIPEndPoint);
                Parent.Client.Send(oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqChannelMovein", 0))
        {
            uint userNo = iPacket.ReadUInt();
            ushort channelId = iPacket.ReadUShort();
            ushort migrationToken = iPacket.ReadUShort();
            if (!ClientManager.TryCompleteChannelMigration(
                Parent,
                userNo,
                channelId,
                migrationToken,
                out string nickname,
                out string migrationRejection))
            {
                PacketTrace.LogEvent(
                    "LOGIN-TCP",
                    "MIGRATION-REJECT",
                    Parent.Client.GetLocalEndPoint(),
                    Parent.Client.GetRemoteEndPoint(),
                    "",
                    migrationRejection);
                Console.WriteLine($"PqChannelMovein 거부: {migrationRejection}");
                Parent.Client.Disconnect();
                return;
            }
            Console.WriteLine("PqChannelMovein: 닉네임={0}", nickname);
            if (!string.IsNullOrEmpty(nickname))
            {
                using (OutPacket oPacket = new OutPacket("PrChannelMoveIn"))
                {
                    oPacket.WriteByte(1);
                    ClientPortTopology ports = ClientBuildProfiles.Active.Ports;
                    oPacket.WriteEndPoint(IPAddress.Any, ports.ResolveUdpPort(ClientServerRuntime.ConfiguredPort));
                    oPacket.WriteEndPoint(IPAddress.Any, ports.ResolveP2pPort(ClientServerRuntime.ConfiguredPort));
                    Parent.Client.Send(oPacket);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChCreateRoomRequestPacket", 0))
        {
            Console.WriteLine(
                "ChCreateRoomRequestPacket 수신: 본문 크기={0}바이트",
                iPacket.Available);
            string RoomName = iPacket.ReadString();    //room name
            Console.WriteLine("방 이름={0}, 길이={1}", RoomName, RoomName.Length);
            string Password = iPacket.ReadString();
            Console.WriteLine("방 비밀번호 있음={0}", Password.Length > 0);
            byte GameType = iPacket.ReadEncodedByte(); //7c
            iPacket.ReadInt(); // +0x1C
            var AiCount = iPacket.ReadInt();
            Console.WriteLine("AI 수={0}", AiCount);
            if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
            {
                // Added after P5136. All following ChCreateRoomRequestPacket
                // members are shifted by four bytes in the modern client.
                iPacket.ReadInt(); // +0x24 (modern only)
            }
            uint RoomDataHeader = iPacket.ReadUInt(); // first dword of the nested 36-byte room block
            byte[] RoomData = iPacket.ReadBytes(32);
            iPacket.ReadBytes(28); // nested endpoint/context block at +0x48
            iPacket.ReadByte(); // +0x64
            int AiSwitch = iPacket.ReadInt(); // +0x68
            iPacket.ReadByte(); // +0x6C
            iPacket.ReadByte(); // +0x6D
            iPacket.ReadInt(); // +0x70
            iPacket.ReadByte(); // +0x74
            Console.WriteLine("AI 사용={0}", AiSwitch);

            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 &&
                Parent.P5136ChannelGameType == 0)
            {
                Console.WriteLine(
                    "방 생성 거부: 현재 P5136 채널 상태가 없습니다. (세대={0}, 채널={1})",
                    Parent.IdentityGeneration,
                    Parent.P5136ChannelId);
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
                return;
            }

            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 &&
                Korean5136Protocol.TryResolveRoomGameType(
                    Parent.P5136ChannelGameType,
                    out byte expectedCreateGameType) &&
                GameType != expectedCreateGameType)
            {
                Console.WriteLine(
                    "방 생성 거부: P5136 채널 게임 유형 {0}은(는) 방 유형 {1}을 요구하지만 {2}을(를) 받았습니다.",
                    Parent.P5136ChannelGameType,
                    expectedCreateGameType,
                    GameType);
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
                return;
            }

            var udpRoute = UdpServer.GetUdp(Parent.Client.Nickname);
            if (udpRoute.Item2 == 0)
            {
                Console.WriteLine(
                    "방 생성 거부: {0}의 등록된 UDP 경로가 없습니다. (주소={1})",
                    Parent.Client.Nickname,
                    udpRoute.Item1);
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteByte(0);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
                return;
            }

            var RoomId = RoomManager.CreateRoom();
            var Room = RoomManager.GetRoom(RoomId);
            Room.RoomName = RoomName;
            if (Password != "")
            {
                Room.Lock = true;
            }
            Room.LockPwd = Password;
            if (StartTimeAttack.ContainsKey(Parent.Client.Nickname))
            {
                Room.SpeedType = StartTimeAttack[Parent.Client.Nickname];
            }
            else
            {
                Room.SpeedType = 7;
            }
            Room.GameType = GameType;
            Room.P5136ChannelGameType = Parent.P5136ChannelGameType;
            Room.P5136ChannelId = Parent.P5136ChannelId;
            Room.RoomDataHeader = RoomDataHeader;
            Room.RoomData = RoomData;
            Console.WriteLine("방 생성 완료: 방 번호={0}", RoomId);
            byte randomTrackGameType = 0;
            if (GameType == 2 || GameType == 4 || GameType == 14 || GameType == 54)
            {
                randomTrackGameType = 1;
            }
            Room.RandomTrackGameType = randomTrackGameType;

            if (GameType == 3 || GameType == 4)
            {
                byte slot = RoomManager.AddPlayer(RoomId, Parent.Client.Nickname, 2, 2, Parent);
                if (slot == 255)
                {
                    Console.WriteLine("방 생성 실패");
                    return;
                }
                Player player = RoomManager.GetPlayer(RoomId, Parent.Client.Nickname);
                if (player == null)
                {
                    Console.WriteLine("플레이어 조회 실패");
                    return;
                }
                Room.RoomMaster = player.ID;
                uint pmap = ProfileService.GetProfileConfig(Parent.Client.Nickname).Rider.pmap;
                if (pmap == 590)
                {
                    Room.RoomMaster = 0;
                }
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(2);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
            }
            else
            {
                byte slot = RoomManager.AddPlayer(RoomId, Parent.Client.Nickname, 0, 2, Parent);
                if (slot == 255)
                {
                    Console.WriteLine("방 생성 실패");
                    return;
                }
                Player player = RoomManager.GetPlayer(RoomId, Parent.Client.Nickname);
                if (player == null)
                {
                    Console.WriteLine("플레이어 조회 실패");
                    return;
                }
                Room.RoomMaster = player.ID;
                uint pmap = ProfileService.GetProfileConfig(Parent.Client.Nickname).Rider.pmap;
                if (pmap == 590)
                {
                    Room.RoomMaster = 0;
                }
                using (OutPacket oPacket = new OutPacket("ChCreateRoomReplyPacket"))
                {
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(1);
                    oPacket.WriteByte(8);
                    oPacket.WriteEncByte(GameType);
                    Parent.Client.Send(oPacket);
                }
            }
            if (AiCount > 0 && AiSwitch == 6)
            {
                // 新增 AI 数量
                AddAis(Room, AiCount - 1, randomTrackGameType);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrFirstRequestPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            if (roomId == -1)
            {
                return;
            }
            GrSessionDataPacket(Parent, Parent.Client.Nickname);
            //Thread.Sleep(10);
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrChangeTrackPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            if (iPacket.Available < 40)
            {
                Console.WriteLine(
                    "GrChangeTrackPacket: 본문이 잘렸습니다. ({0}바이트, 예상 40바이트)",
                    iPacket.Available);
                return;
            }

            room.track = iPacket.ReadUInt();
            room.RoomDataHeader = iPacket.ReadUInt();
            room.RoomData = iPacket.ReadBytes(32);
            Console.WriteLine(
                "Gr 트랙 변경: 트랙=0x{0:X8}, 이름={1}, 메타데이터 헤더=0x{2:X8}",
                room.track,
                RandomTrack.GetTrackName(room.track),
                room.RoomDataHeader);
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestSetSlotStatePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }

            var player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);
            if (player == null)
            {
                Console.WriteLine("플레이어 조회 실패: 방 번호={0}, 닉네임={1}", roomId, Parent.Client.Nickname);
                return;
            }

            player.PlayerType = iPacket.ReadInt();
            GrSlotStatePacket(roomId);
            using (OutPacket oPacket = new OutPacket("GrReplySetSlotStatePacket"))
            {
                oPacket.WriteUInt(ClientManager.GetUserNO(Parent.Client.Nickname));
                oPacket.WriteByte(1);
                oPacket.WriteInt(player.ID);
                oPacket.WriteInt(player.PlayerType);
                BroadCast(roomId, oPacket);
            }
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestClosePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            if (roomId == -1)
            {
                return;
            }
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            uint unk1 = iPacket.ReadUInt();
            byte type = iPacket.ReadByte();
            uint slotId1 = iPacket.ReadUInt();
            uint unk2 = iPacket.ReadUInt();
            uint slotId2 = iPacket.ReadUInt();
            using (OutPacket oPacket = new OutPacket("GrReplyClosePacket"))
            {
                oPacket.WriteUInt(ClientManager.GetUserNO(Parent.Client.Nickname));
                if (room.GameType == 3 || room.GameType == 4)
                {
                    if (unk1 < 8 && slotId1 < 8 && unk2 < 8 && slotId2 < 8 && type == 1 && !room.CloseSlotIds.Contains((byte)slotId1) && !room.CloseSlotIds.Contains((byte)slotId2))
                    {
                        if (room.AddClose((byte)slotId1, (int)unk1) && room.AddClose((byte)slotId2, (int)unk2))
                        {
                            oPacket.WriteByte(1);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk2);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(1);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                        else
                        {
                            oPacket.WriteByte(0);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk2);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(0);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                    }
                    else if (unk1 < 8 && slotId1 < 8 && unk2 < 8 && slotId2 < 8 && type == 0 && room.CloseSlotIds.Contains((byte)slotId1) && room.CloseSlotIds.Contains((byte)slotId2))
                    {
                        if (room.RemoveClose((byte)slotId1, (int)unk1) && room.RemoveClose((byte)slotId2, (int)unk2))
                        {
                            oPacket.WriteByte(1);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk2);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(0);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                        else
                        {
                            oPacket.WriteByte(0);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk2);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(0);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                    }
                    else
                    {
                        oPacket.WriteByte(0);
                        oPacket.WriteUInt(unk1);
                        oPacket.WriteUInt(unk2);
                        int closeCount = room.CloseSlotIds.Count;
                        oPacket.WriteInt(0);
                        oPacket.WriteInt(closeCount);
                        foreach (byte slotId in room.CloseSlotIds)
                        {
                            oPacket.WriteByte(slotId);
                        }
                    }
                    BroadCast(roomId, oPacket);
                }
                else
                {
                    if (unk1 < 8 && slotId1 < 8 && type == 1 && !room.CloseSlotIds.Contains((byte)slotId1))
                    {
                        if (room.AddClose((byte)slotId1, (int)unk1))
                        {
                            oPacket.WriteByte(1);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk1);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(1);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                        else
                        {
                            oPacket.WriteByte(0);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk1);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(0);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                    }
                    else if (unk1 < 8 && slotId1 < 8 && type == 0 && room.CloseSlotIds.Contains((byte)slotId1))
                    {
                        if (room.RemoveClose((byte)slotId1, (int)unk1))
                        {
                            oPacket.WriteByte(1);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk1);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(0);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                        else
                        {
                            oPacket.WriteByte(0);
                            oPacket.WriteUInt(unk1);
                            oPacket.WriteUInt(unk1);
                            int closeCount = room.CloseSlotIds.Count;
                            oPacket.WriteInt(0);
                            oPacket.WriteInt(closeCount);
                            foreach (byte slotId in room.CloseSlotIds)
                            {
                                oPacket.WriteByte(slotId);
                            }
                        }
                    }
                    else
                    {
                        oPacket.WriteByte(0);
                        oPacket.WriteUInt(unk1);
                        oPacket.WriteUInt(unk1);
                        int closeCount = room.CloseSlotIds.Count;
                        oPacket.WriteInt(0);
                        oPacket.WriteInt(closeCount);
                        foreach (byte slotId in room.CloseSlotIds)
                        {
                            oPacket.WriteByte(slotId);
                        }
                    }
                    BroadCast(roomId, oPacket);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestStartPacket"))
        {
            iPacket.ReadInt();
            GrSessionDataPacket(Parent);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PcReportStateInGame", 0))
        {
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChLeaveRoomRequestPacket"))
        {
            iPacket.ReadByte();
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            int slotId = RoomManager.GetPlayerSlotId(roomId, Parent.Client.Nickname);
            if (slotId != -1)
            {
                Console.WriteLine($"방 나가기: 방 번호={roomId}, 슬롯 번호={slotId}");
                var Leave = RoomManager.RemovePlayer(roomId, (byte)slotId, Parent.Client.Nickname);
                using (OutPacket oPacket = new OutPacket("ChLeaveRoomReplyPacket"))
                {
                    oPacket.WriteBool(Leave);
                    Parent.Client.Send(oPacket);
                }
            }
            else
            {
                Console.WriteLine($"방 나가기 실패: 방 번호={roomId}, 슬롯 번호={slotId}");
                using (OutPacket oPacket = new OutPacket("ChLeaveRoomReplyPacket"))
                {
                    oPacket.WriteBool(false);
                    Parent.Client.Send(oPacket);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestBasicAiPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            int ID = (int)iPacket.ReadUInt();
            iPacket.ReadByte(); // add/remove option
            if (RoomManager.TryGetIdDetail(roomId, ID) is Ai ai)
            {
                room.RemoveMember(ai.SlotId, "");
                using (OutPacket oPacket = new OutPacket("GrSlotDataBasicAi"))
                {
                    oPacket.WriteInt(1);
                    oPacket.WriteByte(1);
                    oPacket.WriteInt(ID);
                    oPacket.WriteHexString("00 00 00 00 00 00 00 00 00 00 00 00 00");
                    Position(roomId, oPacket);
                    BroadCast(roomId, oPacket);
                }
            }
            else
            {
                AddAi(Parent, roomId, ID);
            }
            using (OutPacket oPacket = new OutPacket("GrReplyBasicAiPacket"))
            {
                oPacket.WriteByte(1);
                BroadCast(roomId, oPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GameAiGoalinPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var Id = iPacket.ReadInt();
            var Time = iPacket.ReadUInt();
            using (OutPacket oPacket = new OutPacket("GameRaceTimePacket"))
            {
                oPacket.WriteInt(Id);
                oPacket.WriteUInt(Time);
                BroadCast(roomId, oPacket);
            }
            lock (room)
            {
                if (!room.SettlementClosed)
                    room.TimeData.TryAdd(Id, Time);
            }
            Console.WriteLine("GameAiGoalinPacket: ID={0}, 시간={1}", Id, Time);
            if (TryBeginSettlement(
                room,
                out uint endTicks,
                out long raceGeneration))
            {
                using (OutPacket oPacket = new OutPacket("GameControlPacket"))
                {
                    WriteGameControlBody(oPacket, 3, endTicks);
                    BroadCast(roomId, oPacket);
                }
                Set_settleTrigger(room, raceGeneration);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GameTeamBoosterRequestAddGaugePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var team = iPacket.ReadByte();
            var value = iPacket.ReadFloat();
            Console.WriteLine("GameTeamBoosterRequestAddGaugePacket: 팀={0}, 값={1}", team, value);

            if (team == 1)
            {
                room.redGauge += (value * 0.000125f / room.GetPlayerCount(team));
                if (room.redGauge > 1f) room.redGauge = 1f;
                using (OutPacket oPacket = new OutPacket("GameTeamBoosterSetGaugePacket"))
                {
                    oPacket.WriteByte(team);
                    oPacket.WriteFloat(room.redGauge);
                    BroadCast(roomId, oPacket, "", team);
                }
                if (room.redGauge == 1f) room.redGauge = 0f;
            }
            else if (team == 2)
            {
                room.blueGauge += (value * 0.000125f / room.GetPlayerCount(team));
                if (room.blueGauge > 1f) room.blueGauge = 1f;
                using (OutPacket oPacket = new OutPacket("GameTeamBoosterSetGaugePacket"))
                {
                    oPacket.WriteByte(team);
                    oPacket.WriteFloat(room.blueGauge);
                    BroadCast(roomId, oPacket, "", team);
                }
                if (room.blueGauge == 1f) room.blueGauge = 0f;
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrChangeTeamPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                return;
            }
            var player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);
            if (player == null)
            {
                Console.WriteLine("플레이어 조회 실패: 방 번호={0}, 닉네임={1}", roomId, Parent.Client.Nickname);
                return;
            }
            byte team = iPacket.ReadByte();
            var Bool = RoomManager.ChangeMemberTeam(roomId, player.SlotId, team);
            Console.WriteLine("팀 변경: 방 번호={0}, 슬롯 번호={1}, 팀={2}, 결과={3}", roomId, player.SlotId, team, Bool);
            using (OutPacket oPacket = new OutPacket("GrChangeTeamPacketReply"))
            {
                oPacket.WriteInt(player.ID);
                oPacket.WriteByte(player.Team);
                Position(roomId, oPacket);
                Parent.Client.Send(oPacket);
            }
            GrSlotDataPacket(roomId);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChJoinRoomRequestPacket"))
        {
            // Both audited clients use the same request codec: u16 room ID,
            // password, one byte, and a 28-byte connection context.
            int roomId = iPacket.ReadUShort();
            string pwd = iPacket.ReadString();
            byte unk = iPacket.ReadByte();
            if (iPacket.Available < 28)
            {
                Console.WriteLine(
                    "ChJoinRoomRequestPacket: 내용이 잘렸습니다. ({0}/28바이트)",
                    iPacket.Available);
                return;
            }
            iPacket.ReadBytes(28);
            Console.WriteLine("ChJoinRoomRequestPacket: 방 번호={0}, 미확인 값={1}, 비밀번호={2}", roomId, unk, pwd);

            ChJoinRoomReplyPacket(Parent, roomId, pwd);
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRiderTalkPacket"))
        {
            string value = iPacket.ReadString();
            iPacket.ReadUInt();
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            if (roomId == -1)
            {
                return;
            }
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                Console.WriteLine("방 조회 실패: 방 번호={0}", roomId);
                return;
            }

            var player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);
            if (player == null)
            {
                Console.WriteLine("플레이어 조회 실패: 방 번호={0}, 닉네임={1}", roomId, Parent.Client.Nickname);
                return;
            }

            using (OutPacket outPacket = new OutPacket("GrRiderEchoPacket"))
            {
                outPacket.WriteInt(player.ID);
                outPacket.WriteString(value);
                BroadCast(roomId, outPacket, Parent.Client.Nickname);
            }

            if (value.StartsWith("选图", StringComparison.OrdinalIgnoreCase) || value.StartsWith("换图", StringComparison.OrdinalIgnoreCase) || value.StartsWith("選圖", StringComparison.OrdinalIgnoreCase) || value.StartsWith("換圖", StringComparison.OrdinalIgnoreCase))
            {
                uint track = RandomTrack.GetHash(value);
                if (track != 0)
                {
                    room.track = track;
                    GrSlotDataPacket(roomId);
                }
                return;
            }
            else if (value.StartsWith("开始游戏", StringComparison.OrdinalIgnoreCase) || value.StartsWith("開始遊戲", StringComparison.OrdinalIgnoreCase))
            {
                GrSessionDataPacket(Parent);
                return;
            }
            else if (value.StartsWith("结束游戏", StringComparison.OrdinalIgnoreCase) || value.StartsWith("結束遊戲", StringComparison.OrdinalIgnoreCase))
            {
                StopGame(roomId, Parent);
                return;
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqRoomMasterChangePacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                Console.WriteLine("방 조회 실패: 방 번호={0}", roomId);
                return;
            }
            string Target = iPacket.ReadString();
            var player = RoomManager.GetPlayer(roomId, Target);
            if (player != null)
            {
                room.RoomMaster = player.ID;
                player.PlayerType = 2;
                GrSlotDataPacket(roomId);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PcCancelMatching"))
        {
            // Cancellation has an empty body and must not execute the random
            // room-join path used by PcStartMatching.
            Console.WriteLine("PcCancelMatching: 매칭이 취소되었습니다.");
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PcStartMatching"))
        {
            var roomList = RoomManager._rooms.Values
                .Where(room => !room.Lock && IsRoomVisibleToSession(Parent, room))
                .ToList();
            if (roomList.Count > 0)
            {
                Random random = new Random();
                GameRoom room = roomList[random.Next(roomList.Count)];
                ChJoinRoomReplyPacket(Parent, room.RoomId, "");
            }
            else
            {
                using (OutPacket outPacket = new OutPacket("PcMatchingFound"))
                {
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(0);
                    Parent.Client.Send(outPacket);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChGetCurrentCmpRequestPacket"))
        {
            using (OutPacket outPacket = new OutPacket("ChGetCurrentCmpReplyPacket"))
            {
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteByte(0);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqRotationModeDataPacket"))
        {
            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136)
            {
                // This request/reply class pair does not exist in P5136.
                return;
            }
            using (OutPacket outPacket = new OutPacket("PrRotationModeDataPacket"))
            {
                outPacket.WriteInt(0);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqChangeRoomInfoPacket"))
        {
            string RoomName = iPacket.ReadString();
            string RoomPassword = iPacket.ReadString();

            int LimitTime = iPacket.ReadInt();
            byte RKeyAllowed = iPacket.ReadByte();
            byte modernRoomOption = 0;
            if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
            {
                // This room option was appended after P5136.
                modernRoomOption = iPacket.ReadByte();
            }
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            if (roomId == -1)
            {
                Console.WriteLine("방 번호 조회 실패: 닉네임={0}", Parent.Client.Nickname);
                return;
            }
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
            {
                Console.WriteLine("방 조회 실패: 방 번호={0}", roomId);
                return;
            }
            room.RoomName = RoomName;
            if (RoomPassword.Length > 0)
            {
                room.Lock = true;
            }
            else
            {
                room.Lock = false;
            }
            room.LockPwd = RoomPassword;

            using (OutPacket outPacket = new OutPacket("PrChangeRoomInfoPacket"))
            {
                outPacket.WriteBool(true);
                outPacket.WriteString(RoomName);
                outPacket.WriteString(RoomPassword);
                outPacket.WriteInt(LimitTime);
                outPacket.WriteByte(RKeyAllowed);
                if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
                {
                    outPacket.WriteByte(modernRoomOption);
                }
                BroadCast(roomId, outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("GrRequestKickPacket"))
        {
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            if (roomId == -1)
            {
                Console.WriteLine("방 번호 조회 실패: 닉네임={0}", Parent.Client.Nickname);
                return;
            }
            int ID = iPacket.ReadInt();
            if (RoomManager.TryGetIdDetail(roomId, ID) is Player p)
            {
                var player = RoomManager.RemovePlayer(roomId, p.SlotId, p.Nickname);
                if (player)
                {
                    using (OutPacket outPacket = new OutPacket("ChLeaveRoomReplyPacket"))
                    {
                        outPacket.WriteByte(1);
                        p.Session.Client.Send(outPacket);
                    }
                    using (OutPacket outPacket = new OutPacket("GrKickBroadcastPacket"))
                    {
                        outPacket.WriteString(p.Nickname);
                        BroadCast(roomId, outPacket);
                    }
                    using (OutPacket outPacket = new OutPacket("GrReplyKickPacket"))
                    {
                        outPacket.WriteByte(0);
                        Parent.Client.Send(outPacket);
                    }
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("ChGetCurrentGpRequestPacket"))
        {
            using (OutPacket outPacket = new OutPacket("ChGetCurrentGpReplyPacket"))
            {
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteInt(0);
                outPacket.WriteByte(1);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqWhereIsRider", 0))
        {
            uint UserID = iPacket.ReadUInt();
            string nickname = ClientManager.GetNickname(UserID);
            if (string.IsNullOrEmpty(nickname)) return;
            int roomId = RoomManager.TryGetRoomId(nickname);
            var room = RoomManager.GetRoom(roomId);
            if (roomId == -1)
            {
                using (OutPacket outPacket = new OutPacket("PrWhereIsRider"))
                {
                    outPacket.WriteUInt(UserID);
                    outPacket.WriteBytes(new byte[10]);
                    Parent.Client.Send(outPacket);
                }
                return;
            }
            else
            {
                using (OutPacket outPacket = new OutPacket("PrWhereIsRider"))
                {
                    outPacket.WriteUInt(UserID);
                    outPacket.WriteInt(roomId);
                    var channel = GameSupport.Channels.FirstOrDefault(c => c.Value.GameType == room.GameType).Key;
                    outPacket.WriteInt(channel);
                    outPacket.WriteBool(room.Lock);
                    outPacket.WriteByte(1);
                    Parent.Client.Send(outPacket);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqWhereAmI"))
        {
            uint UserID = ClientBuildProfiles.Active.Build == ClientBuild.Korean5136
                ? ClientManager.GetUserNO(Parent.Client.Nickname)
                : iPacket.ReadUInt();
            int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            byte channel = room == null
                ? (byte)0
                : GameSupport.Channels.FirstOrDefault(c => c.Value.GameType == room.GameType).Key;
            using (OutPacket outPacket = new OutPacket("PrWhereAmI"))
            {
                outPacket.WriteUInt(UserID);
                outPacket.WriteInt(roomId);
                if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136)
                {
                    outPacket.WriteByte(channel);
                }
                else
                {
                    outPacket.WriteInt(channel);
                    outPacket.WriteByte(0);
                }
                outPacket.WriteString("");
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqInviteGamePacket"))
        {
            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136)
            {
                // This request/reply class pair does not exist in P5136.
                return;
            }
            uint UserID = iPacket.ReadUInt();
            string nickname = ClientManager.GetNickname(UserID);
            if (string.IsNullOrEmpty(nickname))
            {
                return; // 无效用户
            }

            var parent = ClientManager.GetParent(nickname);
            if (parent != null)
            {
                using (OutPacket outPacket = new OutPacket("PrInviteGamePacket"))
                {
                    outPacket.WriteBytes(iPacket.ReadBytes(iPacket.Available));
                    parent.Client.Send(outPacket);
                }
            }
            return;
        }
        else if (hash == Adler32Helper.GenerateAdler32_ASCII("PqSendMacroChat"))
        {
            var roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
            var room = RoomManager.GetRoom(roomId);
            if (roomId == -1)
            {
                return;
            }

            int type = iPacket.ReadInt();
            byte id = iPacket.ReadByte();
            iPacket.ReadString();
            Player player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);

            using (OutPacket outPacket = new OutPacket("PcSendMacroChat"))
            {
                outPacket.WriteUInt(ClientManager.GetUserNO(Parent.Client.Nickname));
                outPacket.WriteInt(type);
                outPacket.WriteByte(id);
                if (type == 0)
                {
                    outPacket.WriteString(ProfileService.GetProfileConfig(Parent.Client.Nickname).GameOption.QuickMsg.GetValueOrDefault(id) ?? "");
                    BroadCast(roomId, outPacket, Parent.Client.Nickname);
                }
                else
                {
                    outPacket.WriteString(ProfileService.GetProfileConfig(Parent.Client.Nickname).GameOption.TeamQuickMsg.GetValueOrDefault(id) ?? "");
                    BroadCast(roomId, outPacket, Parent.Client.Nickname, player.Team);
                }
                if (room.GetPlayerCount(0) == 1)
                {
                    if (ProfileService.GetProfileConfig(Parent.Client.Nickname).GameOption.QuickMsg.GetValueOrDefault(id) == "结束游戏" ||
                        ProfileService.GetProfileConfig(Parent.Client.Nickname).GameOption.QuickMsg.GetValueOrDefault(id) == "結束遊戲" ||
                        ProfileService.GetProfileConfig(Parent.Client.Nickname).GameOption.TeamQuickMsg.GetValueOrDefault(id) == "结束游戏" ||
                        ProfileService.GetProfileConfig(Parent.Client.Nickname).GameOption.TeamQuickMsg.GetValueOrDefault(id) == "結束遊戲")
                    {
                        StopGame(roomId, Parent);
                    }
                }
            }
            return;
        }
        else
        {
            return;
        }
    }

    public static void GrSlotDataPacket(int roomId)
    {
        using (OutPacket outPacket = new OutPacket("GrSlotDataPacket"))
        {
            GrSlotDataPacket(roomId, outPacket);
            BroadCast(roomId, outPacket);
        }
    }

    static void StopGame(int roomId, SessionGroup Parent)
    {
        var room = RoomManager.GetRoom(roomId);
        using (OutPacket outPacket = new OutPacket("PcSlaveNotice"))
        {
            outPacket.WriteString("结束游戏");
            BroadCast(roomId, outPacket, Parent.Client.Nickname);
        }
        using (OutPacket outPacket = new OutPacket("GameControlPacket"))
        {
            WriteGameControlBody(outPacket, 4, ConvertTick());
            BroadCast(roomId, outPacket);
        }
        InitRoom(room);
        using (OutPacket outPacket = new OutPacket("GameResultPacket"))
        {
            outPacket.WriteByte(0);
            outPacket.WriteInt(0); // player count
            outPacket.WriteInt(0); // AI count
            outPacket.WriteBytes(new byte[34]);
            WriteGameResultTail(outPacket);
            BroadCast(roomId, outPacket);
        }
    }

    private static void WriteGameResultTail(OutPacket outPacket)
    {
        outPacket.WriteUInt(uint.MaxValue);
        outPacket.WriteByte(0);
        if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
        {
            // GameResultPacket gained this trailing dword after P5136.
            outPacket.WriteInt(0);
        }
    }

    static void InitRoom(GameRoom room)
    {
        foreach (RoomMember member in room._IDs)
        {
            if (member is Player p)
            {
                p.PlayerType = 2;
                p.LastPacketReceived = 0;
            }
        }
        foreach (RoomMember member in room.ObIDs)
        {
            if (member is Player p)
            {
                p.LastPacketReceived = 0;
            }
        }
        lock (room)
        {
            room.Started = false;
            room.StartTicks = 0;
            room.StartHandshakePending = false;
            room.StartHandshakeGeneration++;
            room.SettlementClosed = true;
            room.Ready.Clear();
        }
    }

    static void GrSlotDataPacket(int roomId, OutPacket outPacket, bool enter = false, string nickname = "")
    {
        var room = RoomManager.GetRoom(roomId);
        outPacket.WriteUInt(room.track); // track name hash
        outPacket.WriteUInt(room.RoomDataHeader);
        outPacket.WriteBytes(room.RoomData); // 32
        outPacket.WriteInt(room.RoomMaster); // RoomMaster

        outPacket.WriteBytes(new byte[11]);
        outPacket.WriteInt(room.CloseSlotIds.Count); // 房间格子锁定数量 格子ID byte
        foreach (var slotId in room.CloseSlotIds)
        {
            outPacket.WriteByte(slotId);
        }
        outPacket.WriteBytes(new byte[16]);

        /* ---- Player ---- */
        foreach (RoomMember member in room._IDs)
        {
            if (member is Player p)
            {
                var pConfig = ProfileService.GetProfileConfig(p.Nickname);

                Console.WriteLine("플레이어: 닉네임={0}, ID={1}, 슬롯 번호={2}", p.Nickname, p.ID, p.SlotId);
                if (enter)
                {
                    if (p.ID == room.RoomMaster && p.PlayerType == 2)
                    {
                        outPacket.WriteInt(3);
                    }
                    else
                    {
                        outPacket.WriteInt(p.PlayerType);
                    }
                }
                else
                {
                    outPacket.WriteInt(p.PlayerType); // Player Type, 2 = RoomMaster, 3 = AutoReady, 4 = Observer, 5 = Preparing, 7 = AI
                }
                outPacket.WriteUInt(ClientManager.GetUserNO(p.Nickname));
                IPEndPoint client = ClientManager.ClientToIPEndPoint(pConfig.Rider.ClientId);
                outPacket.WriteEndPoint(new IPEndPoint(client.Address, pConfig.Rider.P2pPort));
                outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, 0));
                if (room.RoomName.Contains("比赛") && nickname != "" && nickname != p.Nickname)
                {
                    outPacket.WriteString("跑跑卡丁车");
                    outPacket.WriteShort(0);
                    outPacket.WriteShort(0);
                    outPacket.WriteShort(0);
                    GameSupport.GetRider(nickname, outPacket);
                    outPacket.WriteString("");
                }
                else
                {
                    outPacket.WriteString(p.Nickname);
                    outPacket.WriteShort(pConfig.Rider.Emblem1);
                    outPacket.WriteShort(pConfig.Rider.Emblem2);
                    outPacket.WriteShort(0);
                    GameSupport.GetRider(p.Nickname, outPacket);
                    outPacket.WriteString(pConfig.Rider.Card);
                }
                outPacket.WriteUInt(pConfig.Rider.RP);
                if (room.GameType == 3 || room.GameType == 4)
                {
                    outPacket.WriteByte(p.Team);
                }
                else
                {
                    outPacket.WriteByte(0);
                }

                if (room.Ranking.ContainsKey(p.ID))
                {
                    outPacket.WriteInt(room.Ranking[p.ID]);
                }
                else
                {
                    int nextValue = room.Ranking.Count;
                    room.Ranking[p.ID] = nextValue;
                    outPacket.WriteInt(nextValue);
                }

                outPacket.WriteBytes(new byte[30]);

                outPacket.WriteInt(1500);
                outPacket.WriteInt(1499);
                outPacket.WriteInt(0);
                outPacket.WriteInt(2000);
                outPacket.WriteInt(5);
                outPacket.WriteHexString("FF 00 00 00");

                outPacket.WriteByte(RiderData.RiderSchool.catLevel); //3
                if (pConfig.Rider.ClubMark_LOGO == 0)
                {
                    outPacket.WriteString("");
                    outPacket.WriteInt(0);
                }
                else
                {
                    outPacket.WriteString(pConfig.Rider.ClubName);
                    outPacket.WriteInt(pConfig.Rider.ClubMark_LOGO);
                }
                // The modern active-player record appends one u16 after the
                // two common u16 values at the end of this zero-filled tail.
                outPacket.WriteBytes(new byte[
                    ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 ? 17 : 19]);
            }
            else if (member is Ai a)
            {
                Console.WriteLine("AI: ID={0}, 슬롯 번호={1}", a.ID, a.SlotId);
                outPacket.WriteInt(7);
                outPacket.WriteShort(a.Character);
                outPacket.WriteShort(a.Rid);
                outPacket.WriteShort(a.Kart);
                outPacket.WriteShort(a.Balloon);
                outPacket.WriteShort(a.HeadBand);
                outPacket.WriteShort(a.Goggle);
                if (room.GameType == 3 || room.GameType == 4)
                {
                    outPacket.WriteByte(a.Team);
                }
                else
                {
                    outPacket.WriteByte(0);
                }
            }
            else if (member is Close close)
            {
                outPacket.WriteInt(close.PlayerType);
            }
            else
            {
                outPacket.WriteInt(0);
            }
        }

        /* ---- Observer ---- */
        foreach (RoomMember member in room.ObIDs)
        {
            if (member is Player p)
            {
                var pConfig = ProfileService.GetProfileConfig(p.Nickname);
                outPacket.WriteInt(p.PlayerType);
                outPacket.WriteUInt(ClientManager.GetUserNO(p.Nickname));
                IPEndPoint client = ClientManager.ClientToIPEndPoint(pConfig.Rider.ClientId);
                outPacket.WriteEndPoint(new IPEndPoint(client.Address, pConfig.Rider.P2pPort));
                outPacket.WriteEndPoint(new IPEndPoint(IPAddress.Any, 0));
                outPacket.WriteString(p.Nickname);
            }
            else
            {
                outPacket.WriteInt(0);
            }
        }

        Position(roomId, outPacket);
    }

    static void GrSessionDataPacket(SessionGroup Parent)
    {
        int roomId = RoomManager.TryGetRoomId(Parent.Client.Nickname);
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        int readyCount = 0;
        foreach (RoomMember member in room._IDs)
        {
            if (member is Player player)
            {
                if (player.PlayerType == 3 || player.ID == room.RoomMaster)
                {
                    readyCount++;
                    continue;
                }
            }
        }

        int playerCount = room.GetPlayerCount();
        if (readyCount < playerCount || playerCount < 1)
        {
            using (OutPacket oPacket = new OutPacket("GrReplyStartPacket"))
            {
                oPacket.WriteInt(2);
                Parent.Client.Send(oPacket);
            }
            return;
        }

        room.Started = true;

        bool ai = false;
        if (room.GetAiCount() > 0)
        {
            ai = true;
        }
        uint track = RandomTrack.GetRandomTrack(Parent, $"[{room.RoomName}][{room.RoomId.ToString()}]", room.RandomTrackGameType, room.track, ai);
        room.trackTemp = track;

        using (OutPacket oPacket = new OutPacket("GrReplyStartPacket"))
        {
            oPacket.WriteInt(0);
            Parent.Client.Send(oPacket);
        }

        foreach (RoomMember member in room.ObIDs)
        {
            if (member is Player p)
            {
                GrCommandStartPacket(roomId, p);
            }
        }

        foreach (RoomMember member in room._IDs)
        {
            if (member is Player p)
            {
                GrCommandStartPacket(roomId, p);
            }
        }
    }

    static void GrCommandStartPacket(int roomId, Player p)
    {
        var room = RoomManager.GetRoom(roomId);
        using (OutPacket oPacket = new OutPacket("GrCommandStartPacket"))
        {
            oPacket.WriteUInt(Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("GrSessionDataPacket")));
            GrSessionDataPacket(p.Nickname, oPacket);

            oPacket.WriteUInt(Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("GrSlotDataPacket")));
            GrSlotDataPacket(roomId, oPacket, true, p.Nickname);
            oPacket.WriteInt();

            //kart data
            ushort KartID = ProfileService.GetProfileConfig(p.Nickname).RiderItem.Set_Kart;
            ushort FlyingPetID = ProfileService.GetProfileConfig(p.Nickname).RiderItem.Set_FlyingPet;
            if (room.RoomName.Contains("原版"))
            {
                StartGameData.GetDefaultSpac(oPacket, p.Nickname, room.SpeedType, KartID, 0);
            }
            else
            {
                StartGameData.GetKartSpac(
                    oPacket,
                    p.Nickname,
                    room.SpeedType,
                    KartID,
                    FlyingPetID,
                    Korean5136PlantPerformance.FromRoomGameType(room.GameType));
            }

            oPacket.WriteInt(room.GetAiCount()); //AI count
            if (room.GetAiCount() > 0)
            {
                for (int j = 0; j < room.GetAiCount(); j++)
                {
                    var AiSpec = AI.GetAISpec(room.RandomTrackGameType);
                    oPacket.WriteEncFloat(AiSpec[0]);
                    oPacket.WriteEncFloat(AiSpec[1]);
                    oPacket.WriteEncFloat(AiSpec[2]);
                    oPacket.WriteEncFloat(AiSpec[3]);
                    oPacket.WriteEncFloat(AiSpec[4]);
                    oPacket.WriteEncFloat(AiSpec[5]);
                }
            }
            oPacket.WriteUInt(room.trackTemp); //track name hash
            oPacket.WriteInt(10000);

            oPacket.WriteInt();
            oPacket.WriteUInt(Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("MissionInfo")));
            oPacket.WriteHexString("00 00 00 00 00 00 00 00 00 00 FF FF FF FF 00 00 00 00 00 00 00 00 00");
            //oPacket.WriteString("[applied param]\r\ntransAccelFactor='1.8555' driftEscapeForce='4720' steerConstraint='24.95' normalBoosterTime='3860' \r\npartsBoosterLock='1' \r\n\r\n[equipped / default parts param]\r\ntransAccelFactor='1.86' driftEscapeForce='2120' steerConstraint='2.7' normalBoosterTime='860' \r\n\r\n\r\n[gamespeed param]\r\ntransAccelFactor='-0.0045' driftEscapeForce='2600' steerConstraint='22.25' normalBoosterTime='3000' \r\n\r\n\r\n[factory enchant param]\r\n");
            Console.WriteLine("트랙: {0}", RandomTrack.GetTrackName(room.trackTemp));
            p.Session.Client.Send(oPacket);
        }
    }

    static void GrSessionDataPacket(SessionGroup Parent, string Nickname)
    {
        using (OutPacket oPacket = new OutPacket("GrSessionDataPacket"))
        {
            GrSessionDataPacket(Nickname, oPacket);
            Parent.Client.Send(oPacket);
        }
    }

    static void GrSessionDataPacket(string Nickname, OutPacket outPacket)
    {
        int roomId = RoomManager.TryGetRoomId(Nickname);
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine("방 조회 실패: 방 번호={0}", roomId);
            return;
        }
        outPacket.WriteString(room.RoomName);
        outPacket.WriteString(room.LockPwd);
        outPacket.WriteByte(room.GameType);
        outPacket.WriteByte(room.SpeedType); //7
        outPacket.WriteInt(0);
        outPacket.WriteByte(0);
        outPacket.WriteInt(0);
        // GrSessionDataPacket gained one trailing byte after P5136.
        outPacket.WriteBytes(new byte[
            ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 ? 6 : 7]);
    }

    private static void WriteJoinRoomReplyTail(OutPacket outPacket)
    {
        outPacket.WriteByte(0); // +0x14 in both builds
        if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
        {
            outPacket.WriteInt(0); // +0x18 was added after P5136
        }
    }

    static void ChJoinRoomReplyPacket(SessionGroup Parent, int roomId, String pwd)
    {
        // 1-无法进入房间；2-房间已满；3-密码错误；4-匹配失败；5-创建新房间
        var room = RoomManager.GetRoom(roomId);
        if (room == null || room.Started || !IsRoomVisibleToSession(Parent, room))
        {
            byte gameType = room?.GameType ?? 0;
            using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
            {
                outPacket.WriteByte(1);
                outPacket.WriteByte(0);
                outPacket.WriteByte(0);
                outPacket.WriteEncByte(gameType);
                WriteJoinRoomReplyTail(outPacket);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (!string.IsNullOrEmpty(room.LockPwd) && pwd != room.LockPwd)
        {
            using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
            {
                outPacket.WriteByte(3);
                outPacket.WriteByte(0);
                outPacket.WriteByte(0);
                outPacket.WriteEncByte(room.GameType);
                WriteJoinRoomReplyTail(outPacket);
                Parent.Client.Send(outPacket);
            }
            return;
        }

        int playerCount = room.GetPlayerCount();
        byte slot = RoomManager.AddPlayer(roomId, Parent.Client.Nickname, 0, 2, Parent);
        Player player = RoomManager.GetPlayer(roomId, Parent.Client.Nickname);
        if (slot == 255 || player == null)
        {
            using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
            {
                outPacket.WriteByte(2);
                outPacket.WriteByte(0);
                outPacket.WriteByte(0);
                outPacket.WriteEncByte(room.GameType);
                WriteJoinRoomReplyTail(outPacket);
                Parent.Client.Send(outPacket);
            }
            return;
        }
        else if (room.GameType == 3 || room.GameType == 4)
        {
            uint pmap = ProfileService.GetProfileConfig(Parent.Client.Nickname).Rider.pmap;
            if (pmap == 718 || (playerCount < 1 && room.RoomMaster < 8))
            {
                room.RoomMaster = player.ID;
            }
            if (slot < 4)
            {
                player.Team = 2;
                using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                {
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(1);
                    outPacket.WriteByte(2);
                    outPacket.WriteEncByte(room.GameType);
                    WriteJoinRoomReplyTail(outPacket);
                    Parent.Client.Send(outPacket);
                }
                return;
            }
            else if (slot > 3 && slot < 8)
            {
                player.Team = 1;
                using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                {
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(1);
                    outPacket.WriteByte(2);
                    outPacket.WriteEncByte(room.GameType);
                    WriteJoinRoomReplyTail(outPacket);
                    Parent.Client.Send(outPacket);
                }
                return;
            }
            else
            {
                using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
                {
                    outPacket.WriteByte(1);
                    outPacket.WriteByte(0);
                    outPacket.WriteByte(0);
                    outPacket.WriteEncByte(room.GameType);
                    WriteJoinRoomReplyTail(outPacket);
                    Parent.Client.Send(outPacket);
                }
                return;
            }
        }
        else
        {
            uint pmap = ProfileService.GetProfileConfig(Parent.Client.Nickname).Rider.pmap;
            if (pmap == 718 || (playerCount < 1 && room.RoomMaster < 8))
            {
                room.RoomMaster = player.ID;
            }
            using (OutPacket outPacket = new OutPacket("ChJoinRoomReplyPacket"))
            {
                outPacket.WriteByte(0);
                outPacket.WriteByte(1);
                outPacket.WriteByte(8);
                outPacket.WriteEncByte(room.GameType);
                WriteJoinRoomReplyTail(outPacket);
                Parent.Client.Send(outPacket);
            }
        }
    }

    private static bool IsRoomVisibleToSession(SessionGroup parent, GameRoom room)
    {
        if (room == null)
        {
            return false;
        }

        if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
        {
            return true;
        }

        byte selectedChannelGameType = parent?.P5136ChannelGameType ?? 0;
        if (selectedChannelGameType == 0 ||
            room.P5136ChannelGameType != selectedChannelGameType)
        {
            return false;
        }

        return !Korean5136Protocol.TryResolveRoomGameType(
                   selectedChannelGameType,
                   out byte expectedRoomGameType) ||
               room.GameType == expectedRoomGameType;
    }

    public static void BroadCast(int roomId, OutPacket outPacket, string Self = "", byte team = 0)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        foreach (RoomMember member in room.ObIDs)
        {
            if (member is Player p)
            {
                if (Self != p.Nickname)
                {
                    p.Session.Client.Send(outPacket);
                }
            }
        }

        foreach (RoomMember member in room._slots)
        {
            if (member is Player p)
            {
                if (Self != p.Nickname)
                {
                    if (team == 0)
                    {
                        p.Session.Client.Send(outPacket);
                    }
                    else if (p.Team == team)
                    {
                        p.Session.Client.Send(outPacket);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Relays a client-authored GameSlot result only to the player IDs selected
    /// by its wire-level recipient mask. P5136 assigns IDs 0-15 across racers
    /// and observers, so SlotId cannot represent the observer half.
    /// </summary>
    public static void BroadCastPlayerIdMask(
        int roomId,
        OutPacket outPacket,
        uint recipientMask,
        string self = "")
    {
        GameRoom room = RoomManager.GetRoom(roomId);
        if (room == null || recipientMask == 0)
        {
            return;
        }

        var recipients = new List<Player>();
        var seenIds = new HashSet<int>();
        lock (room)
        {
            foreach (RoomMember member in room._slots.Concat(room.ObIDs))
            {
                if (member is not Player player ||
                    player.ID < 0 ||
                    player.ID >= 32 ||
                    string.Equals(player.Nickname, self, StringComparison.Ordinal) ||
                    (recipientMask & (1u << player.ID)) == 0 ||
                    !seenIds.Add(player.ID))
                {
                    continue;
                }

                recipients.Add(player);
            }
        }

        foreach (Player player in recipients)
        {
            player.Session.Client.Send(outPacket);
        }
    }

    // 添加指定数量的 Ai
    static void AddAis(GameRoom room, int count, byte randomTrackGameType)
    {
        var selector = new DictionaryRandomSelector();
        List<short> randomCharIds = selector.GetRandomCharacterIds(aiCharacterDict, 8);
        List<short> randomKartIds = null;
        if (randomTrackGameType == 0)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 8, true, false);
        }
        else if (randomTrackGameType == 1)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 8, false, true);
        }
        int aiCount = 0;
        for (int i = 0; i < 8; i++)
        {
            short targetCharId = randomCharIds[i];
            short targetKartId = randomKartIds[i];
            if (aiCharacterDict.TryGetValue(targetCharId, out var targetChar))
            {
                short? ridIndex = selector.GetRandomRidIndex(targetChar);
                short? balloonId = 0;
                short? headbandId = 0;
                short? goggleId = 0;
                if (randomTrackGameType == 1)
                {
                    balloonId = selector.GetRandomAccessoryId(targetChar.Balloons);
                    headbandId = selector.GetRandomAccessoryId(targetChar.Headbands);
                    goggleId = selector.GetRandomAccessoryId(targetChar.Goggles);
                }
                byte team = i < 4 ? (byte)2 : (byte)1;
                Ai ai = new Ai
                {
                    Character = targetCharId,
                    Rid = ridIndex ?? 0,
                    Kart = targetKartId,
                    Balloon = balloonId ?? 0,
                    HeadBand = headbandId ?? 0,
                    Goggle = goggleId ?? 0,
                    Team = team
                };
                if (room.TrySetAi(ai, team) != 255)
                {
                    aiCount++;
                }
                if (aiCount == count)
                {
                    break;
                }
            }
        }
    }

    static void AddAi(SessionGroup Parent, int roomId, int ID)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            Console.WriteLine("방 조회 실패: 방 번호={0}", roomId);
        }
        var selector = new DictionaryRandomSelector();
        List<short> randomCharIds = selector.GetRandomCharacterIds(aiCharacterDict, 2);
        List<short> randomKartIds = new List<short>();
        if (room.RandomTrackGameType == 0)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 2, true, false);
        }
        else if (room.RandomTrackGameType == 1)
        {
            randomKartIds = selector.GetRandomKartIds(aiKartDict, 2, false, true);
        }
        if (room.GameType == 3 || room.GameType == 4)
        {
            var Ais = new List<Ai>();
            for (int i = 0; i < 2; i++)
            {
                short targetCharId = randomCharIds[i];
                short targetKartId = randomKartIds[i];
                if (aiCharacterDict.TryGetValue(targetCharId, out var targetChar))
                {
                    short? ridIndex = selector.GetRandomRidIndex(targetChar);
                    short? balloonId = 0;
                    short? headbandId = 0;
                    short? goggleId = 0;
                    if (room.RandomTrackGameType == 1)
                    {
                        balloonId = selector.GetRandomAccessoryId(targetChar.Balloons);
                        headbandId = selector.GetRandomAccessoryId(targetChar.Headbands);
                        goggleId = selector.GetRandomAccessoryId(targetChar.Goggles);
                    }
                    Ais.Add(new Ai
                    {
                        Character = targetCharId,
                        Rid = ridIndex ?? 0,
                        Kart = targetKartId,
                        Balloon = balloonId ?? 0,
                        HeadBand = headbandId ?? 0,
                        Goggle = goggleId ?? 0
                    });
                }
            }
            byte slot0 = room.TrySetAi(Ais[0], 2);
            byte slot1 = room.TrySetAi(Ais[1], 1);
            if (slot0 != 255 && slot1 != 255)
            {
                var ai0 = RoomManager.TryGetSlotDetail(roomId, slot0) as Ai;
                var ai1 = RoomManager.TryGetSlotDetail(roomId, slot1) as Ai;
                using (OutPacket oPacket = new OutPacket("GrSlotDataBasicAi"))
                {
                    oPacket.WriteInt(0);
                    oPacket.WriteByte(2);
                    oPacket.WriteInt(ai0.ID);
                    oPacket.WriteShort(ai0.Character);
                    oPacket.WriteShort(ai0.Rid);
                    oPacket.WriteShort(ai0.Kart);
                    oPacket.WriteShort(ai0.Balloon);
                    oPacket.WriteShort(ai0.HeadBand);
                    oPacket.WriteShort(ai0.Goggle);
                    oPacket.WriteByte(ai0.Team);
                    oPacket.WriteInt(ai1.ID);
                    oPacket.WriteShort(ai1.Character);
                    oPacket.WriteShort(ai1.Rid);
                    oPacket.WriteShort(ai1.Kart);
                    oPacket.WriteShort(ai1.Balloon);
                    oPacket.WriteShort(ai1.HeadBand);
                    oPacket.WriteShort(ai1.Goggle);
                    oPacket.WriteByte(ai1.Team);
                    Position(roomId, oPacket);
                    BroadCast(roomId, oPacket);
                }
            }
        }
        else
        {
            short targetCharId = randomCharIds[0];
            short targetKartId = randomKartIds[0];
            if (aiCharacterDict.TryGetValue(targetCharId, out var targetChar))
            {
                short? ridIndex = selector.GetRandomRidIndex(targetChar);
                short? balloonId = 0;
                short? headbandId = 0;
                short? goggleId = 0;
                if (room.RandomTrackGameType == 1)
                {
                    balloonId = selector.GetRandomAccessoryId(targetChar.Balloons);
                    headbandId = selector.GetRandomAccessoryId(targetChar.Headbands);
                    goggleId = selector.GetRandomAccessoryId(targetChar.Goggles);
                }
                byte slot2 = room.TrySetAi(new Ai
                {
                    Character = targetCharId,
                    Rid = ridIndex ?? 0,
                    Kart = targetKartId,
                    Balloon = balloonId ?? 0,
                    HeadBand = headbandId ?? 0,
                    Goggle = goggleId ?? 0,
                    Team = 0
                }, 0);
                if (slot2 != 255)
                {
                    var ai2 = RoomManager.TryGetSlotDetail(roomId, slot2) as Ai;
                    using (OutPacket oPacket = new OutPacket("GrSlotDataBasicAi"))
                    {
                        oPacket.WriteInt(0);
                        oPacket.WriteByte(1);
                        oPacket.WriteInt(ai2.ID);
                        oPacket.WriteShort(ai2.Character);
                        oPacket.WriteShort(ai2.Rid);
                        oPacket.WriteShort(ai2.Kart);
                        oPacket.WriteShort(ai2.Balloon);
                        oPacket.WriteShort(ai2.HeadBand);
                        oPacket.WriteShort(ai2.Goggle);
                        oPacket.WriteByte(0);
                        Position(roomId, oPacket);
                        BroadCast(roomId, oPacket);
                    }
                }
            }
        }
    }

    static void Position(int roomId, OutPacket outPacket)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }
        foreach (RoomMember member in room._slots)
        {
            if (member is Player player)
            {
                outPacket.WriteInt(player.ID);
            }
            else if (member is Ai ai)
            {
                outPacket.WriteInt(ai.ID);
            }
            else
            {
                outPacket.WriteHexString("FFFFFFFF");
            }
        }
    }

    static void GrSlotStatePacket(int roomId)
    {
        var room = RoomManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }
        using (OutPacket oPacket = new OutPacket("GrSlotStatePacket"))
        {
            foreach (RoomMember member in room._IDs)
            {
                if (member is Player player)
                {
                    oPacket.WriteInt(player.PlayerType);
                }
                else if (member is Ai ai)
                {
                    oPacket.WriteInt(7);
                }
                else
                {
                    oPacket.WriteInt(0);
                }
            }
            oPacket.WriteBytes(new byte[32]);
            BroadCast(roomId, oPacket);
        }
    }

    private static void GameNextStagePacket(GameRoom room)
    {
        using (OutPacket outPacket = new OutPacket("GameNextStagePacket"))
        {
            outPacket.WriteByte(room.GameType);
            outPacket.WriteInt();
            outPacket.WriteInt();
            BroadCast(room.RoomId, outPacket);
        }
    }

    static void GameResultPacket(GameRoom room, RoomMember[] members, Dictionary<int, uint> timeData)
    {
        int playerCount = 0;
        int aiCount = 0;
        foreach (RoomMember member in members)
        {
            if (member is Player p1)
            {
                playerCount++;
                if (!timeData.ContainsKey(p1.ID))
                {
                    timeData[p1.ID] = uint.MaxValue;
                }
            }
            else if (member is Ai a1)
            {
                aiCount++;
                if (!timeData.ContainsKey(a1.ID))
                {
                    timeData[a1.ID] = uint.MaxValue;
                }
            }
        }

        Dictionary<int, int> ranking = GetAllRanks(timeData);
        room.Ranking = ranking;

        var firstId = ranking.First(kv => kv.Value == 0).Key;
        byte firstTeam = 0;
        if (members[firstId] is Player p2)
        {
            firstTeam = p2.Team;
        }
        else if (members[firstId] is Ai a2)
        {
            firstTeam = a2.Team;
        }
        Console.WriteLine("1위 ID={0}, 팀={1}", firstId, firstTeam);

        int redTeam = 0;
        int blueTeam = 0;
        foreach (RoomMember member in members)
        {
            if (member is Player p3)
            {
                if (p3.Team == 2 && timeData[p3.ID] != uint.MaxValue)
                {
                    blueTeam += teamPoints[ranking[p3.ID]];
                }
                else if (p3.Team == 1 && timeData[p3.ID] != uint.MaxValue)
                {
                    redTeam += teamPoints[ranking[p3.ID]];
                }
            }
            if (member is Ai a3)
            {
                if (a3.Team == 2 && timeData[a3.ID] != uint.MaxValue)
                {
                    blueTeam += teamPoints[ranking[a3.ID]];
                }
                else if (a3.Team == 1 && timeData[a3.ID] != uint.MaxValue)
                {
                    redTeam += teamPoints[ranking[a3.ID]];
                }
            }
        }

        using (OutPacket outPacket = new OutPacket("GameResultPacket"))
        {
            if (room.GameType == 3)
            {
                if (redTeam == blueTeam)
                {
                    outPacket.WriteByte(firstTeam);
                }
                else
                {
                    outPacket.WriteByte((byte)(redTeam > blueTeam ? 1 : 2));
                }
            }
            else if (room.GameType == 4)
            {
                outPacket.WriteByte(firstTeam);
            }
            else
            {
                outPacket.WriteByte(0);
            }

            outPacket.WriteInt(playerCount); // player count
            foreach (RoomMember member in members)
            {
                if (member is Player p4)
                {
                    var p4Config = ProfileService.GetProfileConfig(p4.Nickname);

                    outPacket.WriteInt(p4.ID); // player id
                    outPacket.WriteUInt(timeData[p4.ID]);
                    outPacket.WriteByte();
                    outPacket.WriteUShort(p4Config.RiderItem.Set_Kart);
                    int playerRanking = ranking[p4.ID];
                    int playerPoint = timeData[p4.ID] == uint.MaxValue ? 0 : teamPoints[playerRanking];
                    Console.WriteLine("플레이어 {0}: 순위={1}, 점수={2}", p4.ID, playerRanking, playerPoint);
                    outPacket.WriteInt(playerRanking);
                    if (room.GameType == 3 || room.GameType == 4)
                    {
                        outPacket.WriteShort(2); //2
                    }
                    else
                    {
                        outPacket.WriteShort(0);
                    }
                    outPacket.WriteByte();

                    var reward = TimeReward.Reward(playerRanking);
                    p4Config.Rider.RP += reward.RP;
                    outPacket.WriteUInt(p4Config.Rider.RP);
                    outPacket.WriteUInt(reward.RP); // Earned RP
                    outPacket.WriteUInt(reward.Lucci); // Earned Lucci
                    p4Config.Rider.Lucci += reward.Lucci;
                    outPacket.WriteUInt(p4Config.Rider.Lucci);
                    ProfileService.Save(p4.Nickname, p4Config);
                    outPacket.WriteBytes(new byte[29]);

                    if (room.GameType == 3 || room.GameType == 4)
                    {
                        outPacket.WriteInt(playerPoint);
                        outPacket.WriteByte(p4.Team); // Team
                    }
                    else
                    {
                        outPacket.WriteInt(0);
                        outPacket.WriteByte(0);
                    }
                    outPacket.WriteBytes(new byte[12]);
                    outPacket.WriteInt(1);
                    outPacket.WriteByte(0);
                    outPacket.WriteUShort(p4Config.RiderItem.Set_Character);
                    outPacket.WriteBytes(new byte[49]);
                    outPacket.WriteHexString("FF");
                    outPacket.WriteBytes(new byte[37]);
                    outPacket.WriteInt(p4Config.Rider.ClubMark_LOGO);
                    outPacket.WriteBytes(new byte[39]);
                }
            }

            outPacket.WriteInt(aiCount); // AI count
            foreach (RoomMember member in members)
            {
                if (member is Ai a4)
                {
                    outPacket.WriteInt(a4.ID);
                    outPacket.WriteUInt(timeData[a4.ID]);
                    outPacket.WriteByte();

                    // 获取 kart 属性值
                    outPacket.WriteShort(a4.Kart);
                    int AiRanking = ranking[a4.ID];
                    int AiPoint = timeData[a4.ID] == uint.MaxValue ? 0 : teamPoints[AiRanking];
                    Console.WriteLine("AI {0}: 순위={1}, 점수={2}", a4.ID, AiRanking, AiPoint);
                    outPacket.WriteInt(AiRanking);
                    outPacket.WriteShort(0);
                    if (room.GameType == 3 || room.GameType == 4)
                    {
                        outPacket.WriteByte(a4.Team); // Team
                        outPacket.WriteInt(AiPoint);
                    }
                    else
                    {
                        outPacket.WriteByte(0);
                        outPacket.WriteInt(0);
                    }
                }
            }
            Console.WriteLine("레드 팀 점수={0}, 블루 팀 점수={1}", redTeam, blueTeam);
            outPacket.WriteBytes(new byte[34]);
            WriteGameResultTail(outPacket);
            BroadCast(room.RoomId, outPacket);
        }
    }
}
