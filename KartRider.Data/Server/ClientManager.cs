using KartRider.Common.Network;
using KartRider.Compatibility;
using Profile;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KartRider;

public static class ClientManager
{
    private static readonly TimeSpan FirstMessageResponseTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan MigrationLifetime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan OperationDrainTimeout = TimeSpan.FromSeconds(5);
    private const int MaximumPendingMigrationsPerSession = 1;

    private static readonly ConcurrentDictionary<string, SessionGroup> ClientSessions =
        new ConcurrentDictionary<string, SessionGroup>(StringComparer.Ordinal);
    private static readonly Dictionary<string, IdentityLease> IdentityLeases =
        new Dictionary<string, IdentityLease>(StringComparer.OrdinalIgnoreCase);
    private static readonly object LifecycleLock = new object();

    public static ConcurrentDictionary<string, uint> NicknameToUserNO =
        new ConcurrentDictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    public static ConcurrentDictionary<uint, string> UserNOToNickname =
        new ConcurrentDictionary<uint, string>();

    private static long nextGeneration;
    private static long nextMigrationId;
    private static uint nextUserNo = 1;

    public static void AddClient(SessionGroup session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        IPEndPoint clientEndPoint = session.Client.Socket.RemoteEndPoint as IPEndPoint;
        if (clientEndPoint == null)
        {
            session.Client.Disconnect();
            return;
        }

        string clientId = GetClientId(clientEndPoint);
        session.ClientId = clientId;
        if (!ClientSessions.TryAdd(clientId, session))
        {
            PacketTrace.LogEvent(
                "LOGIN-TCP",
                "REGISTER-REJECT",
                session.Client.Socket.LocalEndPoint,
                session.Client.Socket.RemoteEndPoint,
                "",
                $"endpoint already registered: {clientId}");
            session.Client.Disconnect();
            return;
        }

        Console.WriteLine($"클라이언트 {clientId} 연결, 현재 접속자: {ClientSessions.Count}명");
        _ = SendFirstMessageAsync(session);
    }

    private static async Task SendFirstMessageAsync(SessionGroup session)
    {
        try
        {
            // P5136 is server-first and exposes no client-ready signal. This
            // delay is a mitigation for its receive-queue reset, not a protocol
            // guarantee. It is asynchronous so accepting one client never
            // prevents the listener from accepting the next one.
            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean5136)
            {
                int delayMilliseconds = ClientServerRuntime.FirstMessageDelayMilliseconds;
                if (delayMilliseconds > 0)
                    await Task.Delay(delayMilliseconds).ConfigureAwait(false);
            }

            if (!IsRegistered(session) || session.Client.mDisconnected != 0)
                return;

            GameSupport.PcFirstMessageAsync(session);
            _ = ObserveFirstMessageResponseAsync(session);
        }
        catch (Exception exception)
        {
            PacketTrace.LogEvent(
                "LOGIN-TCP",
                "FIRST-MESSAGE-ERROR",
                session.Client.GetLocalEndPoint(),
                session.Client.GetRemoteEndPoint(),
                session.Client.Nickname,
                exception.ToString());
            session.Client.Disconnect();
        }
    }

    private static async Task ObserveFirstMessageResponseAsync(SessionGroup session)
    {
        await Task.Delay(FirstMessageResponseTimeout).ConfigureAwait(false);
        if (!IsRegistered(session) || session.Client.mDisconnected != 0 ||
            session.IdentityGeneration != 0)
        {
            return;
        }

        PacketTrace.LogEvent(
            "LOGIN-TCP",
            "FIRST-MESSAGE-TIMEOUT",
            session.Client.GetLocalEndPoint(),
            session.Client.GetRemoteEndPoint(),
            "",
            $"no PqLogin/PqChannelMovein within {FirstMessageResponseTimeout.TotalSeconds:0}s");
        session.Client.Disconnect();
    }

    public static bool TryClaimLoginIdentity(
        SessionGroup session,
        string requestedNickname,
        out string nickname,
        out uint userNo,
        out string rejectionReason)
    {
        nickname = string.Empty;
        userNo = 0;
        rejectionReason = string.Empty;

        if (session == null || !IsRegistered(session))
        {
            rejectionReason = "등록되지 않은 로그인 세션입니다.";
            return false;
        }
        if (!ClientIdentityValidator.TryNormalize(
            requestedNickname,
            FileName.ProfileDir,
            out nickname,
            out rejectionReason))
        {
            return false;
        }

        long claimedGeneration;
        lock (LifecycleLock)
        {
            if (!IsRegistered(session) || session.Client.mDisconnected != 0)
            {
                rejectionReason = "로그인 세션이 이미 종료되었습니다.";
                return false;
            }

            if (!string.IsNullOrEmpty(session.Client.Nickname))
            {
                if (string.Equals(session.Client.Nickname, nickname, StringComparison.OrdinalIgnoreCase) &&
                    IsCurrentOwnerLocked(session))
                {
                    userNo = GetUserNOLocked(nickname);
                    return true;
                }

                rejectionReason = "이미 다른 계정으로 인증된 세션입니다.";
                return false;
            }

            if (IdentityLeases.TryGetValue(nickname, out IdentityLease existingLease))
            {
                string owner = existingLease.Owner?.ClientId ?? "채널 이동 대기 중";
                rejectionReason = $"'{nickname}' 계정이 이미 접속 중입니다 ({owner}).";
                return false;
            }

            userNo = GetUserNOLocked(nickname);
            claimedGeneration = Interlocked.Increment(ref nextGeneration);
            try
            {
                // Prepare persistent endpoint state before publishing the
                // lease. A disconnect cannot let a replacement generation
                // claim this nickname while an older profile write is pending.
                UpdateClientProfileEndpoint(nickname, session.ClientId);
            }
            catch (Exception exception)
            {
                rejectionReason = $"Could not prepare the account profile: {exception.Message}";
                return false;
            }

            if (!IsRegistered(session) || session.Client.mDisconnected != 0)
            {
                rejectionReason = "The login session disconnected while preparing its profile.";
                return false;
            }

            session.Client.Nickname = nickname;
            session.IdentityGeneration = claimedGeneration;
            session.P5136ChannelGameType = 0;
            session.P5136ChannelId = 0;
            IdentityLeases.Add(
                nickname,
                new IdentityLease(nickname, userNo, session, claimedGeneration));
        }

        try
        {
            PacketTrace.LogEvent(
                "LOGIN-TCP",
                "IDENTITY-CLAIM",
                session.Client.GetLocalEndPoint(),
                session.Client.GetRemoteEndPoint(),
                nickname,
                $"generation={claimedGeneration}; userNo={userNo}");
            return true;
        }
        catch (Exception exception)
        {
            lock (LifecycleLock)
            {
                if (IdentityLeases.TryGetValue(nickname, out IdentityLease lease) &&
                    ReferenceEquals(lease.Owner, session) &&
                    lease.Generation == claimedGeneration)
                {
                    IdentityLeases.Remove(nickname);
                }
                session.Client.Nickname = string.Empty;
                session.IdentityGeneration = 0;
                session.P5136ChannelGameType = 0;
                session.P5136ChannelId = 0;
            }

            rejectionReason = $"계정 프로필을 준비하지 못했습니다: {exception.Message}";
            return false;
        }
    }

    public static bool TryBeginChannelMigration(
        SessionGroup source,
        ushort channelId,
        byte channelGameType,
        out ushort token,
        out string rejectionReason)
    {
        token = 0;
        rejectionReason = string.Empty;
        if (source == null || !IsRegistered(source))
        {
            rejectionReason = "등록되지 않은 채널 세션입니다.";
            return false;
        }

        MigrationPermit permit;
        lock (LifecycleLock)
        {
            if (!IsCurrentOwnerLocked(source) ||
                !IdentityLeases.TryGetValue(source.Client.Nickname, out IdentityLease lease))
            {
                rejectionReason = "현재 계정 세션만 채널을 이동할 수 있습니다.";
                return false;
            }

            if (lease.Cleaning || lease.TransferInProgress)
            {
                rejectionReason = "The identity is already being transferred or cleaned up.";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            lease.Permits.RemoveAll(candidate => candidate.ExpiresUtc <= now);
            // Latest request wins: a generation may expose only one migration
            // permit, so a late socket from an earlier UI click is stale.
            lease.Permits.RemoveAll(candidate =>
                candidate.SourceGeneration == lease.Generation);
            if (lease.Permits.Count >= MaximumPendingMigrationsPerSession)
            {
                rejectionReason = "처리되지 않은 채널 이동 요청이 너무 많습니다.";
                return false;
            }

            ushort generatedToken;
            do
            {
                generatedToken = checked((ushort)RandomNumberGenerator.GetInt32(1, ushort.MaxValue + 1));
            }
            while (lease.Permits.Any(candidate => candidate.Token == generatedToken));
            token = generatedToken;

            permit = new MigrationPermit(
                Interlocked.Increment(ref nextMigrationId),
                lease.UserNo,
                lease.Generation,
                source,
                source.Client.GetRemoteEndPoint().Address,
                channelId,
                channelGameType,
                token,
                now.Add(MigrationLifetime));
            lease.Permits.Add(permit);
        }

        PacketTrace.LogEvent(
            "LOGIN-TCP",
            "MIGRATION-BEGIN",
            source.Client.GetLocalEndPoint(),
            source.Client.GetRemoteEndPoint(),
            source.Client.Nickname,
            $"generation={source.IdentityGeneration}; channel={channelId}; gameType={channelGameType}; token={token}; ttlMs={MigrationLifetime.TotalMilliseconds:0}");
        _ = ExpireMigrationAsync(source.Client.Nickname, permit.Id);
        return true;
    }

    public static bool TryCompleteChannelMigration(
        SessionGroup destination,
        uint userNo,
        ushort channelId,
        ushort token,
        out string nickname,
        out string rejectionReason)
    {
        nickname = string.Empty;
        rejectionReason = string.Empty;
        if (destination == null || !IsRegistered(destination))
        {
            rejectionReason = "등록되지 않은 채널 이동 세션입니다.";
            return false;
        }

        SessionGroup previousOwner;
        long newGeneration;
        lock (LifecycleLock)
        {
            if (!IsRegistered(destination) || destination.Client.mDisconnected != 0)
            {
                rejectionReason = "채널 이동 대상 세션이 이미 종료되었습니다.";
                return false;
            }

            if (!string.IsNullOrEmpty(destination.Client.Nickname))
            {
                rejectionReason = "채널 이동 대상 세션이 이미 인증되어 있습니다.";
                return false;
            }
            if (!UserNOToNickname.TryGetValue(userNo, out nickname) ||
                !IdentityLeases.TryGetValue(nickname, out IdentityLease lease))
            {
                rejectionReason = $"알 수 없는 사용자 번호입니다: {userNo}.";
                return false;
            }

            if (lease.Cleaning || lease.TransferInProgress)
            {
                rejectionReason = "The identity is already being transferred or cleaned up.";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            MigrationPermit permit = lease.Permits.FirstOrDefault(candidate =>
                candidate.UserNo == userNo &&
                candidate.ChannelId == channelId &&
                candidate.Token == token &&
                candidate.ExpiresUtc > now);
            if (permit == null)
            {
                rejectionReason = $"유효한 채널 이동 허가가 없습니다 (channel={channelId}, token={token}).";
                return false;
            }
            if (permit.SourceGeneration != lease.Generation)
            {
                rejectionReason = "이미 소비되었거나 오래된 채널 이동 허가입니다.";
                return false;
            }

            IPAddress destinationAddress = destination.Client.GetRemoteEndPoint().Address;
            if (!Equals(permit.SourceAddress, destinationAddress))
            {
                rejectionReason = $"채널 이동 원격 주소가 다릅니다: {permit.SourceAddress} -> {destinationAddress}.";
                return false;
            }
            if (lease.Owner != null && !ReferenceEquals(lease.Owner, permit.SourceSession))
            {
                rejectionReason = "채널 이동 중 계정 소유 세션이 변경되었습니다.";
                return false;
            }

            previousOwner = permit.SourceSession;
            lease.TransferInProgress = true;

            DateTime drainDeadline = DateTime.UtcNow.Add(OperationDrainTimeout);
            while (lease.ActiveOperations != 0)
            {
                TimeSpan remaining = drainDeadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero ||
                    !Monitor.Wait(LifecycleLock, remaining))
                {
                    lease.TransferInProgress = false;
                    Monitor.PulseAll(LifecycleLock);
                    rejectionReason = "Timed out while draining packets from the previous session.";
                    return false;
                }
            }

            if (!IdentityLeases.TryGetValue(lease.Nickname, out IdentityLease currentLease) ||
                !ReferenceEquals(currentLease, lease) ||
                lease.Cleaning ||
                !lease.Permits.Contains(permit) ||
                permit.ExpiresUtc <= DateTime.UtcNow ||
                permit.SourceGeneration != lease.Generation ||
                !IsRegistered(destination) ||
                destination.Client.mDisconnected != 0)
            {
                lease.TransferInProgress = false;
                Monitor.PulseAll(LifecycleLock);
                rejectionReason = "The migration permit expired or changed while draining the old session.";
                return false;
            }

            if (!TryPrepareMigrationBindings(
                lease.Nickname,
                previousOwner,
                destination,
                out Exception prepareError))
            {
                lease.TransferInProgress = false;
                Monitor.PulseAll(LifecycleLock);
                rejectionReason = $"Could not transfer session state: {prepareError.Message}";
                return false;
            }

            // ClientSessions can change without LifecycleLock during socket
            // teardown, so verify the destination again after file/room prep.
            if (!IsRegistered(destination) || destination.Client.mDisconnected != 0)
            {
                RestoreMigrationBindings(lease.Nickname, previousOwner, destination);
                lease.TransferInProgress = false;
                Monitor.PulseAll(LifecycleLock);
                rejectionReason = "The destination session disconnected during migration.";
                return false;
            }

            lease.Permits.Clear();
            newGeneration = Interlocked.Increment(ref nextGeneration);
            lease.Owner = destination;
            lease.Generation = newGeneration;
            lease.TransferInProgress = false;
            destination.Client.Nickname = lease.Nickname;
            destination.IdentityGeneration = newGeneration;
            destination.P5136ChannelGameType = permit.ChannelGameType;
            destination.P5136ChannelId = permit.ChannelId;
            nickname = lease.Nickname;
            Monitor.PulseAll(LifecycleLock);
        }

        PacketTrace.LogEvent(
            "LOGIN-TCP",
            "MIGRATION-COMPLETE",
            destination.Client.GetLocalEndPoint(),
            destination.Client.GetRemoteEndPoint(),
            nickname,
            $"generation={newGeneration}; channel={channelId}; gameType={destination.P5136ChannelGameType}; token={token}");

        if (previousOwner != null && !ReferenceEquals(previousOwner, destination) &&
            previousOwner.Client.mDisconnected == 0)
        {
            previousOwner.Client.Disconnect();
        }
        return true;
    }

    private static bool TryPrepareMigrationBindings(
        string nickname,
        SessionGroup previousOwner,
        SessionGroup destination,
        out Exception error)
    {
        try
        {
            UpdateClientProfileEndpoint(nickname, destination.ClientId);
            RoomManager.RebindPlayerSession(nickname, destination);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            RestoreMigrationBindings(nickname, previousOwner, destination);
            PacketTrace.LogEvent(
                "LOGIN-TCP",
                "MIGRATION-REBIND-ERROR",
                destination.Client.GetLocalEndPoint(),
                destination.Client.GetRemoteEndPoint(),
                nickname,
                exception.ToString());
            return false;
        }
    }

    private static void RestoreMigrationBindings(
        string nickname,
        SessionGroup previousOwner,
        SessionGroup failedDestination)
    {
        try
        {
            if (previousOwner != null)
            {
                UpdateClientProfileEndpoint(nickname, previousOwner.ClientId);
                RoomManager.RebindPlayerSession(nickname, previousOwner);
            }
        }
        catch (Exception exception)
        {
            PacketTrace.LogEvent(
                "LOGIN-TCP",
                "MIGRATION-ROLLBACK-ERROR",
                failedDestination?.Client.GetLocalEndPoint(),
                failedDestination?.Client.GetRemoteEndPoint(),
                nickname,
                exception.ToString());
        }
    }

    private static async Task ExpireMigrationAsync(string nickname, long permitId)
    {
        await Task.Delay(MigrationLifetime).ConfigureAwait(false);

        IdentityLease cleanupLease = null;
        lock (LifecycleLock)
        {
            if (!IdentityLeases.TryGetValue(nickname, out IdentityLease lease))
                return;

            MigrationPermit permit = lease.Permits.FirstOrDefault(candidate => candidate.Id == permitId);
            if (permit == null)
                return;

            lease.Permits.Remove(permit);
            if (lease.Generation != permit.SourceGeneration ||
                lease.Permits.Any(candidate => candidate.SourceGeneration == lease.Generation))
            {
                return;
            }

            if (lease.Owner == null || lease.Owner.Client.mDisconnected != 0 ||
                !IsRegistered(lease.Owner))
            {
                lease.Owner = null;
                lease.Cleaning = true;
                lease.TransferInProgress = false;
                cleanupLease = lease;
            }
        }

        if (cleanupLease != null)
        {
            PacketTrace.LogEvent(
                "LOGIN-TCP",
                "MIGRATION-EXPIRED",
                null,
                null,
                nickname,
                $"permit={permitId}; shared state cleanup");
            ScheduleIdentityCleanup(cleanupLease, "migration expired");
        }
    }

    public static void RemoveClient(SessionGroup session)
    {
        if (session == null || string.IsNullOrEmpty(session.ClientId))
            return;

        bool removed = ((ICollection<KeyValuePair<string, SessionGroup>>)ClientSessions).Remove(
            new KeyValuePair<string, SessionGroup>(session.ClientId, session));
        if (!removed)
            return;

        string nickname = session.Client.Nickname;
        IdentityLease cleanupLease = null;
        bool deferred = false;
        lock (LifecycleLock)
        {
            if (!string.IsNullOrEmpty(nickname) &&
                IdentityLeases.TryGetValue(nickname, out IdentityLease lease) &&
                ReferenceEquals(lease.Owner, session) &&
                lease.Generation == session.IdentityGeneration)
            {
                DateTime now = DateTime.UtcNow;
                lease.Permits.RemoveAll(candidate => candidate.ExpiresUtc <= now);
                if (lease.Permits.Any(candidate => candidate.SourceGeneration == lease.Generation))
                {
                    lease.Owner = null;
                    deferred = true;
                }
                else
                {
                    lease.Owner = null;
                    lease.Cleaning = true;
                    lease.TransferInProgress = false;
                    cleanupLease = lease;
                }
            }
        }

        if (cleanupLease != null)
            ScheduleIdentityCleanup(cleanupLease, "owner disconnected");

        Console.WriteLine(
            $"클라이언트 {session.ClientId} 연결 종료, 현재 접속자: {ClientSessions.Count}명" +
            (deferred ? " (채널 이동 완료까지 상태 유지)" : string.Empty));
    }

    private static void ScheduleIdentityCleanup(IdentityLease lease, string reason)
    {
        _ = CleanupIdentityStateAsync(lease, reason);
    }

    private static async Task CleanupIdentityStateAsync(
        IdentityLease lease,
        string reason)
    {
        int attempt = 0;
        while (true)
        {
            lock (LifecycleLock)
            {
                if (!IdentityLeases.TryGetValue(lease.Nickname, out IdentityLease current) ||
                    !ReferenceEquals(current, lease) ||
                    current.Generation != lease.Generation ||
                    !current.Cleaning)
                {
                    return;
                }

                if (current.ActiveOperations != 0)
                {
                    // Disconnect can be initiated inside a packet handler. Do
                    // not clean shared state until that handler releases its
                    // generation fence.
                    attempt++;
                }
            }

            bool canClean;
            lock (LifecycleLock)
            {
                canClean = IdentityLeases.TryGetValue(
                        lease.Nickname,
                        out IdentityLease current) &&
                    ReferenceEquals(current, lease) &&
                    current.Generation == lease.Generation &&
                    current.Cleaning &&
                    current.ActiveOperations == 0;
            }

            if (canClean && CleanupIdentityState(lease.Nickname))
            {
                lock (LifecycleLock)
                {
                    if (IdentityLeases.TryGetValue(
                            lease.Nickname,
                            out IdentityLease current) &&
                        ReferenceEquals(current, lease) &&
                        current.Generation == lease.Generation &&
                        current.Cleaning &&
                        current.Owner == null)
                    {
                        IdentityLeases.Remove(lease.Nickname);
                    }
                }

                PacketTrace.LogEvent(
                    "LOGIN-TCP",
                    "IDENTITY-CLEANUP",
                    null,
                    null,
                    lease.Nickname,
                    $"generation={lease.Generation}; reason={reason}; attempts={attempt + 1}");
                return;
            }

            attempt++;
            int retryDelayMs = Math.Min(2000, 100 * (1 << Math.Min(attempt, 4)));
            await Task.Delay(retryDelayMs).ConfigureAwait(false);
        }
    }

    private static bool CleanupIdentityState(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return true;

        bool success = true;
        void RunCleanup(string component, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                success = false;
                PacketTrace.LogEvent(
                    "LOGIN-TCP",
                    "IDENTITY-CLEANUP-ERROR",
                    null,
                    null,
                    nickname,
                    $"component={component}; {exception}");
            }
        }

        RunCleanup("random-track", () => RandomTrack.ClearUsedTracks(nickname));
        RunCleanup("room", () =>
        {
            if (!RoomManager.RemovePlayerByNickname(nickname))
                throw new InvalidOperationException("Room membership remained after cleanup.");
        });
        RunCleanup("my-room", () => MyRoomData.TryLeaveMyRoom(nickname));
        RunCleanup("udp", () => UdpServer.RemoveNickname(nickname));
        return success;
    }

    public static bool IsRegistered(Session session)
    {
        if (session == null)
            return false;
        IPEndPoint endpoint = session.Socket.RemoteEndPoint as IPEndPoint;
        if (endpoint == null)
            return false;
        return ClientSessions.TryGetValue(GetClientId(endpoint), out SessionGroup parent) &&
               ReferenceEquals(parent.Client, session);
    }

    public static bool IsRegistered(SessionGroup session)
    {
        return session != null && !string.IsNullOrEmpty(session.ClientId) &&
               ClientSessions.TryGetValue(session.ClientId, out SessionGroup current) &&
               ReferenceEquals(current, session);
    }

    public static bool IsCurrentOwner(SessionGroup session)
    {
        if (session == null)
            return false;
        lock (LifecycleLock)
        {
            return IsCurrentOwnerLocked(session);
        }
    }

    private static bool IsCurrentOwnerLocked(SessionGroup session)
    {
        return !string.IsNullOrEmpty(session.Client.Nickname) &&
               IdentityLeases.TryGetValue(session.Client.Nickname, out IdentityLease lease) &&
               ReferenceEquals(lease.Owner, session) &&
               lease.Generation == session.IdentityGeneration;
    }

    public static IDisposable TryAcquireIdentityOperation(SessionGroup session)
    {
        if (session == null)
            return null;

        lock (LifecycleLock)
        {
            if (!IsCurrentOwnerLocked(session) ||
                !IdentityLeases.TryGetValue(session.Client.Nickname, out IdentityLease lease) ||
                lease.TransferInProgress ||
                lease.Cleaning)
            {
                return null;
            }

            lease.ActiveOperations++;
            return new IdentityOperationLease(lease, session, lease.Generation);
        }
    }

    private static void ReleaseIdentityOperation(
        IdentityLease lease,
        SessionGroup session,
        long generation)
    {
        lock (LifecycleLock)
        {
            if (lease.Generation == generation && lease.ActiveOperations > 0)
            {
                lease.ActiveOperations--;
                if (lease.ActiveOperations == 0)
                    Monitor.PulseAll(LifecycleLock);
            }
        }
    }

    public static List<string> GetOnlinePlayers()
    {
        lock (LifecycleLock)
        {
            return IdentityLeases.Values
                .Where(lease => lease.Owner != null && lease.Owner.Client.mDisconnected == 0)
                .Select(lease => lease.Nickname)
                .ToList();
        }
    }

    public static ICollection<SessionGroup> GetClients()
    {
        if (ClientBuildProfiles.Active.Build != ClientBuild.Korean5136)
            return ClientSessions.Values.ToArray();

        lock (LifecycleLock)
        {
            return IdentityLeases.Values
                .Where(lease =>
                    !lease.Cleaning &&
                    !lease.TransferInProgress &&
                    lease.Owner != null &&
                    lease.Owner.Client.mDisconnected == 0 &&
                    IsRegistered(lease.Owner))
                .Select(lease => lease.Owner)
                .ToArray();
        }
    }

    public static void DisconnectAll()
    {
        SessionGroup[] sessions = ClientSessions.Values.ToArray();
        foreach (SessionGroup session in sessions)
        {
            try
            {
                session.Client.Disconnect();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"서버 종료 중 클라이언트 연결 해제 실패: {exception.Message}");
            }
        }

        ClientSessions.Clear();
        List<string> orphanedIdentities;
        lock (LifecycleLock)
        {
            orphanedIdentities = IdentityLeases.Keys.ToList();
            IdentityLeases.Clear();
        }
        foreach (string nickname in orphanedIdentities)
            CleanupIdentityState(nickname);
    }

    public static SessionGroup GetParent(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return null;
        lock (LifecycleLock)
        {
            return IdentityLeases.TryGetValue(nickname, out IdentityLease lease) &&
                   lease.Owner != null && lease.Owner.Client.mDisconnected == 0 &&
                   IsRegistered(lease.Owner)
                ? lease.Owner
                : null;
        }
    }

    public static string GetClientId(IPEndPoint endPoint) =>
        $"{endPoint.Address}:{endPoint.Port}";

    public static IPEndPoint ClientToIPEndPoint(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        int colonIndex = input.LastIndexOf(':');
        if (colonIndex == -1)
            return null;
        string ipPart = input.Substring(0, colonIndex);
        string portPart = input.Substring(colonIndex + 1);
        if (!IPAddress.TryParse(ipPart, out IPAddress ipAddress))
            return null;
        if (!int.TryParse(portPart, out int port) || port < 0 || port > 65535)
            return null;
        return new IPEndPoint(ipAddress, port);
    }

    public static bool HasClientWithNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return false;
        lock (LifecycleLock)
        {
            return IdentityLeases.ContainsKey(nickname);
        }
    }

    public static uint GetUserNO(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return 0;
        lock (LifecycleLock)
        {
            return GetUserNOLocked(nickname);
        }
    }

    private static uint GetUserNOLocked(string nickname)
    {
        if (NicknameToUserNO.TryGetValue(nickname, out uint existingUserNo))
            return existingUserNo;

        uint newUserNo = nextUserNo++;
        while (newUserNo == 0 || UserNOToNickname.ContainsKey(newUserNo))
            newUserNo = nextUserNo++;
        NicknameToUserNO[nickname] = newUserNo;
        UserNOToNickname[newUserNo] = nickname;
        return newUserNo;
    }

    public static string GetNickname(uint userNo) =>
        UserNOToNickname.TryGetValue(userNo, out string nickname) ? nickname : null;

    public static bool TryGetActiveIdentity(
        uint userNo,
        IPAddress remoteAddress,
        out string nickname,
        out long generation)
    {
        nickname = string.Empty;
        generation = 0;
        if (remoteAddress == null ||
            !UserNOToNickname.TryGetValue(userNo, out string mappedNickname))
        {
            return false;
        }

        lock (LifecycleLock)
        {
            if (!IdentityLeases.TryGetValue(mappedNickname, out IdentityLease lease) ||
                lease.Cleaning ||
                lease.TransferInProgress ||
                lease.Owner == null ||
                lease.Owner.Client.mDisconnected != 0 ||
                !IsRegistered(lease.Owner) ||
                lease.Owner.IdentityGeneration != lease.Generation ||
                !Equals(lease.Owner.Client.GetRemoteEndPoint().Address, remoteAddress))
            {
                return false;
            }

            nickname = lease.Nickname;
            generation = lease.Generation;
            return true;
        }
    }

    public static IDisposable TryAcquireIdentityOperation(
        uint userNo,
        IPAddress remoteAddress,
        out string nickname,
        out long generation)
    {
        nickname = string.Empty;
        generation = 0;
        if (remoteAddress == null ||
            !UserNOToNickname.TryGetValue(userNo, out string mappedNickname))
        {
            return null;
        }

        lock (LifecycleLock)
        {
            if (!IdentityLeases.TryGetValue(mappedNickname, out IdentityLease lease) ||
                lease.Cleaning ||
                lease.TransferInProgress ||
                lease.Owner == null ||
                lease.Owner.Client.mDisconnected != 0 ||
                !IsRegistered(lease.Owner) ||
                lease.Owner.IdentityGeneration != lease.Generation ||
                !Equals(lease.Owner.Client.GetRemoteEndPoint().Address, remoteAddress))
            {
                return null;
            }

            nickname = lease.Nickname;
            generation = lease.Generation;
            lease.ActiveOperations++;
            return new IdentityOperationLease(lease, lease.Owner, lease.Generation);
        }
    }

    public static bool IsIdentityGenerationCurrent(string nickname, long generation)
    {
        if (string.IsNullOrWhiteSpace(nickname) || generation == 0)
            return false;
        lock (LifecycleLock)
        {
            return IdentityLeases.TryGetValue(nickname, out IdentityLease lease) &&
                   !lease.Cleaning &&
                   !lease.TransferInProgress &&
                   lease.Owner != null &&
                   lease.Owner.Client.mDisconnected == 0 &&
                   lease.Generation == generation &&
                   lease.Owner.IdentityGeneration == generation &&
                   IsRegistered(lease.Owner);
        }
    }

    public static void UpdateNickname(string oldNickname, string newNickname)
    {
        if (!ClientIdentityValidator.TryNormalize(
            newNickname,
            FileName.ProfileDir,
            out string normalizedNickname,
            out _))
        {
            return;
        }

        lock (LifecycleLock)
        {
            if (!NicknameToUserNO.TryGetValue(oldNickname, out uint userNo) ||
                NicknameToUserNO.ContainsKey(normalizedNickname) ||
                IdentityLeases.ContainsKey(normalizedNickname))
            {
                return;
            }

            if (IdentityLeases.TryGetValue(oldNickname, out IdentityLease lease))
            {
                if (lease.Permits.Count != 0)
                    return;
                IdentityLeases.Remove(oldNickname);
                lease.Nickname = normalizedNickname;
                lease.Owner.Client.Nickname = normalizedNickname;
                IdentityLeases.Add(normalizedNickname, lease);
            }

            NicknameToUserNO.TryRemove(oldNickname, out _);
            NicknameToUserNO[normalizedNickname] = userNo;
            UserNOToNickname[userNo] = normalizedNickname;
        }
    }

    private static void UpdateClientProfileEndpoint(string nickname, string clientId)
    {
        ProfileService.Update(
            nickname,
            config => config.Rider.ClientId = clientId);
    }

    private sealed class IdentityLease
    {
        public IdentityLease(string nickname, uint userNo, SessionGroup owner, long generation)
        {
            Nickname = nickname;
            UserNo = userNo;
            Owner = owner;
            Generation = generation;
        }

        public string Nickname { get; set; }
        public uint UserNo { get; }
        public SessionGroup Owner { get; set; }
        public long Generation { get; set; }
        public List<MigrationPermit> Permits { get; } = new List<MigrationPermit>();
        public int ActiveOperations { get; set; }
        public bool TransferInProgress { get; set; }
        public bool Cleaning { get; set; }
    }

    private sealed class IdentityOperationLease : IDisposable
    {
        private IdentityLease lease;
        private SessionGroup session;
        private readonly long generation;

        public IdentityOperationLease(
            IdentityLease lease,
            SessionGroup session,
            long generation)
        {
            this.lease = lease;
            this.session = session;
            this.generation = generation;
        }

        public void Dispose()
        {
            IdentityLease capturedLease = Interlocked.Exchange(ref lease, null);
            SessionGroup capturedSession = Interlocked.Exchange(ref session, null);
            if (capturedLease != null && capturedSession != null)
            {
                ReleaseIdentityOperation(capturedLease, capturedSession, generation);
            }
        }
    }

    private sealed class MigrationPermit
    {
        public MigrationPermit(
            long id,
            uint userNo,
            long sourceGeneration,
            SessionGroup sourceSession,
            IPAddress sourceAddress,
            ushort channelId,
            byte channelGameType,
            ushort token,
            DateTime expiresUtc)
        {
            Id = id;
            UserNo = userNo;
            SourceGeneration = sourceGeneration;
            SourceSession = sourceSession;
            SourceAddress = sourceAddress;
            ChannelId = channelId;
            ChannelGameType = channelGameType;
            Token = token;
            ExpiresUtc = expiresUtc;
        }

        public long Id { get; }
        public uint UserNo { get; }
        public long SourceGeneration { get; }
        public SessionGroup SourceSession { get; }
        public IPAddress SourceAddress { get; }
        public ushort ChannelId { get; }
        public byte ChannelGameType { get; }
        public ushort Token { get; }
        public DateTime ExpiresUtc { get; }
    }
}
