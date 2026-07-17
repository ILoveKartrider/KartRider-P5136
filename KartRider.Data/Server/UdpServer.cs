using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KartRider.Common.Network;
using KartRider.Common.Security;
using KartRider.Compatibility;
using KartRider.IO.Packet;
using KartRider_PacketName;
using Profile;

namespace KartRider
{
    /// <summary>
    /// UDP服务端封装类
    /// </summary>
    public class UdpServer
    {
        // UDP核心通信对象
        private UdpClient _udpClient;
        private readonly IPAddress _bindAddress;
        // 监听端口
        private readonly int _listenPort;
        // 服务端名称（日志区分多实例）
        private readonly string _serverName;
        // 线程取消标识（替代CancellationToken）
        private volatile bool _isRunning;
        // 同步锁（防止重复启动/停止）
        private readonly object _lockObj = new object();
        private static readonly GenerationEndpointBindingTable UdpClientBindings =
            new GenerationEndpointBindingTable();
        private static readonly GenerationEndpointBindingTable P2pClientBindings =
            new GenerationEndpointBindingTable();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverName">服务端名称（日志区分）</param>
        /// <param name="listenPort">监听端口（唯一）</param>
        public UdpServer(string serverName, IPAddress bindAddress, int listenPort)
        {
            _serverName = serverName;
            _bindAddress = bindAddress ?? throw new ArgumentNullException(nameof(bindAddress));
            _listenPort = listenPort;
            _isRunning = false;
        }

        public bool IsRunning => _isRunning;

        public IPEndPoint LocalEndPoint => _udpClient?.Client?.LocalEndPoint as IPEndPoint;

        /// <summary>
        /// 启动UDP服务端
        /// </summary>
        public void Start()
        {
            lock (_lockObj)
            {
                if (_isRunning)
                {
                    Console.WriteLine($"[{_serverName}] 서버가 이미 실행 중입니다.");
                    return;
                }

                try
                {
                    // 初始化UDP客户端并绑定端口
                    _udpClient = new UdpClient(new IPEndPoint(_bindAddress, _listenPort));

                    // 禁用 UDP Socket 的 ConnectionReset 错误（Windows 特有）
                    // 当向未监听端口发送 UDP 时，远程返回 ICMP Port Unreachable，
                    // 系统将其映射为 ConnectionReset 并在 EndReceive 时抛出，
                    // 同时丢弃所有已缓冲的正常数据包。此设置彻底关闭该行为。
                    const int SIO_UDP_CONNRESET = -1744830452;
                    _udpClient.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);

                    _isRunning = true;

                    Console.WriteLine($"[{_serverName}] 서버 시작 완료, 수신 포트: {_listenPort}");
                    Console.WriteLine($"[{_serverName}] 클라이언트 데이터 대기 중...\n");

                    // 开始异步接收数据（APM模式）
                    BeginReceive();
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[{_serverName}] 서버 시작 실패: {ex.Message} (포트가 사용 중일 수 있습니다.)");
                    _isRunning = false;
                    _udpClient?.Dispose();
                    _udpClient = null;
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_serverName}] 서버 시작 오류: {ex.Message}");
                    _isRunning = false;
                    _udpClient?.Dispose();
                    _udpClient = null;
                    throw;
                }
            }
        }

        /// <summary>
        /// 停止UDP服务端
        /// </summary>
        public void Stop()
        {
            lock (_lockObj)
            {
                if (!_isRunning)
                {
                    Console.WriteLine($"[{_serverName}] 서버가 실행 중이 아닙니다.");
                    return;
                }

                _isRunning = false;

                try
                {
                    // 关闭UDP客户端（终止异步接收）
                    _udpClient?.Close();
                    Console.WriteLine($"[{_serverName}] 서버 중지 완료");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_serverName}] 서버 중지 오류: {ex.Message}");
                }
                finally
                {
                    // 释放资源
                    _udpClient?.Dispose();
                    _udpClient = null;
                    UdpClientBindings.Clear();
                    P2pClientBindings.Clear();
                }
            }
        }

        /// <summary>
        /// 异步接收数据（APM模式：BeginReceive）
        /// </summary>
        private void BeginReceive()
        {
            if (!_isRunning || _udpClient == null) return;

            try
            {
                // 启动异步接收，完成后回调EndReceive
                _udpClient.BeginReceive(EndReceive, null);
            }
            catch (Exception ex)
            {
                if (_isRunning) // 仅在运行中时打印异常（停止时的异常忽略）
                {
                    Console.WriteLine($"[{_serverName}] 데이터 수신 오류: {ex.Message}");
                    // 延迟重试接收（避免异常循环）
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(1000);
                        BeginReceive();
                    });
                }
            }
        }

        /// <summary>
        /// 异步接收完成回调（APM模式：EndReceive）
        /// </summary>
        /// <param name="ar">异步操作结果</param>
        private void EndReceive(IAsyncResult ar)
        {
            if (!_isRunning || _udpClient == null) return;

            IPEndPoint clientEP = null;
            byte[] receiveBuffer = null;

            try
            {
                // 结束异步接收，获取数据和客户端地址
                receiveBuffer = _udpClient.EndReceive(ar, ref clientEP);

                try
                {
                    // 解析数据
                    if (clientEP != null)
                    {
                        if (receiveBuffer == null || receiveBuffer.Length < 8)
                        {
                            PacketTrace.LogPacket(
                                _serverName,
                                "RX",
                                GetLocalEndPoint(),
                                clientEP,
                                null,
                                receiveBuffer,
                                -1,
                                $"result=MALFORMED; wireLength={receiveBuffer?.Length ?? 0}",
                                receiveBuffer);
                            return;
                        }

                        uint iv = BitConverter.ToUInt32(receiveBuffer, 0);
                        uint otherChecksum = BitConverter.ToUInt32(receiveBuffer, receiveBuffer.Length - 4);
                        byte[] packetData = new byte[receiveBuffer.Length - (4 + 4)];
                        Buffer.BlockCopy(receiveBuffer, 4, packetData, 0, packetData.Length);
                        if (packetData.Length < 12)
                        {
                            PacketTrace.LogPacket(
                                _serverName,
                                "RX",
                                GetLocalEndPoint(),
                                clientEP,
                                null,
                                packetData,
                                -1,
                                $"result=MALFORMED; iv=0x{iv:X8}; wireLength={receiveBuffer.Length}",
                                receiveBuffer);
                            return;
                        }

                        uint calculatedChecksum = KRPacketCrypto.HashDecrypt(packetData, (uint)packetData.Length, iv);
                        bool checksumValid =
                            (iv ^ otherChecksum ^ 1329075907U) == calculatedChecksum;
                        InPacket p = new InPacket(packetData);
                        uint accountID = p.ReadUInt();
                        uint hash = p.ReadUInt();
                        uint packetName = p.ReadUInt();
                        var packetValue = (PacketName)packetName;

                        string nickname = ClientManager.GetNickname(accountID) ?? string.Empty;
                        PacketTrace.LogPacket(
                            _serverName,
                            "RX",
                            GetLocalEndPoint(),
                            clientEP,
                            nickname,
                            packetData,
                            8,
                            $"account={accountID}; route=0x{hash:X8}; iv=0x{iv:X8}; checksum={(checksumValid ? "OK" : "BAD")}; wireLength={receiveBuffer.Length}",
                            receiveBuffer);
                        if (!checksumValid)
                        {
                            PacketTrace.LogEvent(
                                _serverName,
                                "RX-DROP",
                                GetLocalEndPoint(),
                                clientEP,
                                nickname,
                                "invalid UDP checksum");
                            return;
                        }

                        using IDisposable udpIdentityOperation =
                            ClientManager.TryAcquireIdentityOperation(
                            accountID,
                            clientEP.Address,
                            out nickname,
                            out long identityGeneration);
                        if (udpIdentityOperation == null)
                        {
                            PacketTrace.LogEvent(
                                _serverName,
                                "RX-DROP",
                                GetLocalEndPoint(),
                                clientEP,
                                nickname,
                                "account has no active identity owner for this source address");
                            return;
                        }

                        bool p2p = false;
                        bool isP2pTransport = string.Equals(
                            _serverName,
                            "P2P",
                            StringComparison.OrdinalIgnoreCase);
                        var playerConfig = ProfileService.GetProfileConfig(nickname);
                        IPEndPoint client = ClientManager.ClientToIPEndPoint(playerConfig.Rider.ClientId);
                        if (client != null)
                        {
                            var clientudp = new IPEndPoint(client.Address, playerConfig.Rider.UdpPort);
                            p2p = clientEP.Equals(clientudp);
                        }

                        GenerationEndpointBindingTable bindingTable = isP2pTransport
                            ? P2pClientBindings
                            : UdpClientBindings;
                        EndpointBindResult bindResult = bindingTable.TryBind(
                            nickname,
                            clientEP,
                            hash,
                            p2p,
                            identityGeneration,
                            out GenerationEndpointBinding routeBinding);
                        if (bindResult != EndpointBindResult.Bound &&
                            bindResult != EndpointBindResult.Refreshed &&
                            bindResult != EndpointBindResult.AdvancedGeneration)
                        {
                            PacketTrace.LogEvent(
                                _serverName,
                                "RX-DROP",
                                GetLocalEndPoint(),
                                clientEP,
                                nickname,
                                $"route bind rejected: transport={(isP2pTransport ? "P2P" : "UDP")}; " +
                                $"result={bindResult}; generation={identityGeneration}; " +
                                $"boundGeneration={routeBinding?.Generation ?? 0}; " +
                                $"boundEndpoint={routeBinding?.Endpoint}");
                            return;
                        }

                        if (PacketDispatcher.Dispatch(typeof(UdpServer), packetValue, p, receiveBuffer, clientEP, this))
                            return;

                        if (packetValue == PacketName.PqUdpEcho)
                        {
                            OutPacket outPacket = new OutPacket();
                            outPacket.WriteUInt(accountID);
                            outPacket.WriteUInt(hash);
                            outPacket.WriteInt((int)PacketName.PrUdpEcho);

                            outPacket.WriteInt(p.ReadInt());
                            outPacket.WriteInt(p.ReadInt());
                            BeginSend(outPacket, clientEP);
                        }
                        else if (packetValue == PacketName.PqUdpTimeSync)
                        {
                            OutPacket outPacket = new OutPacket();
                            outPacket.WriteUInt(accountID);
                            outPacket.WriteUInt(hash);
                            outPacket.WriteInt((int)PacketName.PrUdpTimeSync);

                            outPacket.WriteInt(p.ReadInt());
                            outPacket.WriteUInt(MultyPlayer.ConvertTick());
                            bool success = BeginSend(outPacket, clientEP);

                            if (!string.IsNullOrEmpty(nickname))
                            {
                                int roomId = RoomManager.TryGetRoomId(nickname);
                                var room = RoomManager.GetRoom(roomId);
                                if (room != null)
                                {
                                    lock (room)
                                    {
                                        room.Ready?.TryUpdate(nickname, success, false);
                                    }
                                }
                            }
                        }
                        else if (packetValue == PacketName.GameSlotPacket)
                        {
                            int roomId = RoomManager.TryGetRoomId(nickname);
                            var room = RoomManager.GetRoom(roomId);
                            byte[] data = p.ReadBytes(p.Available);
                            if (room != null)
                            {
                                foreach (RoomMember member in room._slots)
                                {
                                    if (member is Player player && player.Nickname != nickname)
                                    {
                                        var targetUdp = GetUdp(player.Nickname);
                                        // 发送方和目标方都是 P2P 直连时跳过，避免重复发送
                                        if (p2p && targetUdp.Item3)
                                            continue;

                                        var udp = targetUdp.Item1;
                                        var pHash = targetUdp.Item2 == 0 ? hash : targetUdp.Item2;

                                        OutPacket outPacket = new OutPacket();
                                        outPacket.WriteUInt(ClientManager.GetUserNO(player.Nickname));
                                        outPacket.WriteUInt(pHash);
                                        outPacket.WriteUInt(packetName);
                                        outPacket.WriteBytes(data);

                                        UdpBoardCast(player, udp, outPacket);
                                    }
                                }
                                foreach (RoomMember member in room.ObIDs)
                                {
                                    if (member is Player player)
                                    {
                                        var targetUdp = GetUdp(player.Nickname);
                                        // 发送方和目标方都是 P2P 直连时跳过，避免重复发送
                                        if (p2p && targetUdp.Item3)
                                            continue;

                                        var udp = targetUdp.Item1;
                                        var pHash = targetUdp.Item2 == 0 ? hash : targetUdp.Item2;

                                        OutPacket outPacket = new OutPacket();
                                        outPacket.WriteUInt(ClientManager.GetUserNO(player.Nickname));
                                        outPacket.WriteUInt(pHash);
                                        outPacket.WriteUInt(packetName);
                                        outPacket.WriteBytes(data);

                                        UdpBoardCast(player, udp, outPacket);
                                    }
                                }
                            }
                        }
                        else if (packetValue == PacketName.RoomSlotPacket)
                        {
                            string owner = MyRoomData.GetRoomOwnerByNickname(nickname);
                            byte[] data = p.ReadBytes(p.Available);
                            if (!string.IsNullOrEmpty(owner))
                            {
                                var members = MyRoomData.GetRoomPlayers(owner);
                                foreach (var member in members)
                                {
                                    if (string.IsNullOrEmpty(member) || string.Equals(member, nickname, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    var targetUdp = GetUdp(member);
                                    // 发送方和目标方都是 P2P 直连时跳过，避免重复发送
                                    if (p2p && targetUdp.Item3)
                                        continue;

                                    var udp = targetUdp.Item1;
                                    var pHash = targetUdp.Item2 == 0 ? hash : targetUdp.Item2;

                                    OutPacket outPacket = new OutPacket();
                                    outPacket.WriteUInt(ClientManager.GetUserNO(member));
                                    outPacket.WriteUInt(pHash);
                                    outPacket.WriteUInt(packetName);
                                    outPacket.WriteBytes(data);

                                    bool success = BeginSend(outPacket, udp);
                                    if (success)
                                    {
                                        // Console.WriteLine($"[{udp}][{currentTime}][{nickname}] {packetValue}: {BitConverter.ToString(outPacket.ToArray()).Replace("-", " ")}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"알 수 없는 UDP 패킷: {packetValue}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    PacketTrace.LogEvent(
                        _serverName,
                        "RX-ERROR",
                        GetLocalEndPoint(),
                        clientEP,
                        null,
                        ex.ToString());
                    Console.WriteLine($"[{_serverName}] 데이터 처리 오류: {ex.Message}");
                }
            }
            catch (ObjectDisposedException)
            {
                // 服务端停止时触发，忽略
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // UDP Socket 收到 ICMP Port Unreachable，非致命错误，静默忽略并继续接收
            }
            catch (SocketException ex)
            {
                PacketTrace.LogEvent(
                    _serverName,
                    "RX-SOCKET-ERROR",
                    GetLocalEndPoint(),
                    clientEP,
                    null,
                    ex.ToString());
                Console.WriteLine($"[{_serverName}] 데이터 처리 오류: {ex.Message}, 소켓 오류 코드: {ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                PacketTrace.LogEvent(
                    _serverName,
                    "RX-ERROR",
                    GetLocalEndPoint(),
                    clientEP,
                    null,
                    ex.ToString());
                Console.WriteLine($"[{_serverName}] 데이터 처리 오류: {ex.Message}");
            }
            finally
            {
                // 持续接收下一个数据包（核心：循环异步接收）
                if (_isRunning)
                {
                    BeginReceive();
                }
            }
        }

        public bool BeginSend(OutPacket outPacket, IPEndPoint endPoint)
        {
            byte[] buffer = outPacket.ToArray();
            byte[] plaintext = (byte[])buffer.Clone();
            byte[] data = null;
            uint siv = 0;
            try
            {
                if (endPoint == null || IPAddress.Any.Equals(endPoint.Address) && endPoint.Port == 0)
                {
                    PacketTrace.LogPacket(
                        _serverName,
                        "TX",
                        GetLocalEndPoint(),
                        endPoint,
                        null,
                        plaintext,
                        8,
                        "result=DROP; reason=invalid endpoint");
                    // 无效端点（0.0.0.0:0），跳过发送，避免触发 ICMP Port Unreachable
                    return false;
                }

                data = new byte[buffer.Length + 8];

                siv = (uint)(new Random((int)DateTime.Now.Ticks).Next());
                uint newHash = KRPacketCrypto.HashEncrypt(buffer, (uint)buffer.Length, siv);
                Buffer.BlockCopy(BitConverter.GetBytes(siv), 0, data, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((uint)(siv ^ newHash ^ 1329075907U)), 0, data, data.Length - 4, 4);
                Buffer.BlockCopy(buffer, 0, data, 4, buffer.Length);

                int sentBytes = _udpClient.Send(data, data.Length, endPoint);
                PacketTrace.LogPacket(
                    _serverName,
                    "TX",
                    GetLocalEndPoint(),
                    endPoint,
                    null,
                    plaintext,
                    8,
                    $"result={(sentBytes == data.Length ? "SENT" : "PARTIAL")}; sent={sentBytes}; wireLength={data.Length}; iv=0x{siv:X8}",
                    data);
                if (sentBytes == data.Length)
                {
                    return true;
                }
                else
                {
                    Console.WriteLine($"데이터 일부만 전송됨: {sentBytes} / {data.Length}바이트");
                    return false;
                }
            }
            catch (SocketException ex)
            {
                PacketTrace.LogPacket(
                    _serverName,
                    "TX",
                    GetLocalEndPoint(),
                    endPoint,
                    null,
                    plaintext,
                    8,
                    $"result=ERROR; {ex.GetType().Name}: {ex.Message}; iv=0x{siv:X8}",
                    data);
                Console.WriteLine($"전송 실패 (네트워크 오류): {ex.Message}, 소켓 오류 코드: {ex.SocketErrorCode}");
                return false;
            }
            catch (Exception ex)
            {
                PacketTrace.LogPacket(
                    _serverName,
                    "TX",
                    GetLocalEndPoint(),
                    endPoint,
                    null,
                    plaintext,
                    8,
                    $"result=ERROR; {ex.GetType().Name}: {ex.Message}; iv=0x{siv:X8}",
                    data);
                Console.WriteLine($"전송 실패: {ex.Message}");
                return false;
            }
        }

        private IPEndPoint GetLocalEndPoint()
        {
            try
            {
                return _udpClient?.Client?.LocalEndPoint as IPEndPoint;
            }
            catch
            {
                return null;
            }
        }

        public static (IPEndPoint, uint, bool) GetUdp(string nickname)
        {
            if (UdpClientBindings.TryGet(nickname, out GenerationEndpointBinding value))
            {
                if (ClientManager.IsIdentityGenerationCurrent(nickname, value.Generation))
                    return (value.Endpoint, value.Hash, value.DirectP2p);

                UdpClientBindings.TryRemove(nickname, value);
            }

            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136 &&
                ClientManager.GetParent(nickname) == null)
            {
                return (null, 0, false);
            }

            var profile = ProfileService.GetProfileConfig(nickname);
            IPEndPoint client = ClientManager.ClientToIPEndPoint(profile.Rider.ClientId);
            if (client == null)
                return (null, 0, false);
            var udpIP = new IPEndPoint(client.Address, profile.Rider.UdpPort);
            return (udpIP, 0, false);
        }

        public static void RemoveNickname(string nickname)
        {
            if (!string.IsNullOrWhiteSpace(nickname))
            {
                UdpClientBindings.Remove(nickname);
                P2pClientBindings.Remove(nickname);
            }
        }

        internal static EndpointBindResult BindEndpointForTesting(
            string nickname,
            bool p2pTransport,
            IPEndPoint endpoint,
            uint hash,
            bool directP2p,
            long generation,
            out GenerationEndpointBinding binding)
        {
            return (p2pTransport ? P2pClientBindings : UdpClientBindings).TryBind(
                nickname,
                endpoint,
                hash,
                directP2p,
                generation,
                out binding);
        }

        internal static bool TryGetEndpointForTesting(
            string nickname,
            bool p2pTransport,
            out GenerationEndpointBinding binding)
        {
            return (p2pTransport ? P2pClientBindings : UdpClientBindings).TryGet(
                nickname,
                out binding);
        }

        internal static void ClearEndpointBindingsForTesting()
        {
            UdpClientBindings.Clear();
            P2pClientBindings.Clear();
        }

        public void UdpBoardCast(Player player, IPEndPoint udp, OutPacket outPacket)
        {
            InPacket iPacket = new InPacket(outPacket.ToArray());
            var data1 = iPacket.ReadBytes(32);
            var packetName = iPacket.ReadUInt();
            var packetValue = (PacketName)packetName;
            var tick = iPacket.ReadUInt();
            // var data2 = iPacket.ReadBytes(iPacket.Available);
            if (packetValue == PacketName.GameKartQuadPacket || packetValue == PacketName.GameKartPacket)
            {
                if (tick < player.LastPacketReceived && tick > MultyPlayer.ConvertTick())
                {
                    Console.WriteLine($"[{player.Nickname}] 패킷 손실률이 너무 높아 패킷을 폐기합니다.");
                    return;
                }
                player.LastPacketReceived = tick;
            }
            bool success = BeginSend(outPacket, udp);
            if (success)
            {
                // Console.WriteLine($"[{udp}][{currentTime}][{nickname}] {packetValue}: {BitConverter.ToString(outPacket.ToArray()).Replace("-", " ")}");
            }
        }
    }

    internal enum EndpointBindResult
    {
        Bound,
        Refreshed,
        AdvancedGeneration,
        EndpointMismatch,
        StaleGeneration,
        InvalidInput
    }

    internal sealed class GenerationEndpointBinding
    {
        public GenerationEndpointBinding(
            IPEndPoint endpoint,
            uint hash,
            bool directP2p,
            long generation)
        {
            Endpoint = new IPEndPoint(endpoint.Address, endpoint.Port);
            Hash = hash;
            DirectP2p = directP2p;
            Generation = generation;
        }

        public IPEndPoint Endpoint { get; }
        public uint Hash { get; }
        public bool DirectP2p { get; }
        public long Generation { get; }
    }

    internal sealed class GenerationEndpointBindingTable
    {
        private readonly ConcurrentDictionary<string, GenerationEndpointBinding> bindings =
            new ConcurrentDictionary<string, GenerationEndpointBinding>(
                StringComparer.OrdinalIgnoreCase);

        public EndpointBindResult TryBind(
            string nickname,
            IPEndPoint endpoint,
            uint hash,
            bool directP2p,
            long generation,
            out GenerationEndpointBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(nickname) ||
                endpoint == null ||
                endpoint.Port == 0 ||
                generation <= 0)
            {
                return EndpointBindResult.InvalidInput;
            }

            while (true)
            {
                if (!bindings.TryGetValue(nickname, out GenerationEndpointBinding current))
                {
                    var first = new GenerationEndpointBinding(
                        endpoint,
                        hash,
                        directP2p,
                        generation);
                    if (bindings.TryAdd(nickname, first))
                    {
                        binding = first;
                        return EndpointBindResult.Bound;
                    }

                    continue;
                }

                if (current.Generation > generation)
                {
                    binding = current;
                    return EndpointBindResult.StaleGeneration;
                }

                if (current.Generation == generation &&
                    !current.Endpoint.Equals(endpoint))
                {
                    binding = current;
                    return EndpointBindResult.EndpointMismatch;
                }

                var replacement = new GenerationEndpointBinding(
                    endpoint,
                    hash,
                    directP2p,
                    generation);
                if (!bindings.TryUpdate(nickname, replacement, current))
                    continue;

                binding = replacement;
                return current.Generation == generation
                    ? EndpointBindResult.Refreshed
                    : EndpointBindResult.AdvancedGeneration;
            }
        }

        public bool TryGet(string nickname, out GenerationEndpointBinding binding)
        {
            binding = null;
            return !string.IsNullOrWhiteSpace(nickname) &&
                   bindings.TryGetValue(nickname, out binding);
        }

        public bool TryRemove(string nickname, GenerationEndpointBinding expected)
        {
            if (string.IsNullOrWhiteSpace(nickname) || expected == null)
                return false;

            return ((ICollection<KeyValuePair<string, GenerationEndpointBinding>>)bindings)
                .Remove(new KeyValuePair<string, GenerationEndpointBinding>(nickname, expected));
        }

        public void Remove(string nickname)
        {
            if (!string.IsNullOrWhiteSpace(nickname))
                bindings.TryRemove(nickname, out _);
        }

        public void Clear() => bindings.Clear();
    }
}
