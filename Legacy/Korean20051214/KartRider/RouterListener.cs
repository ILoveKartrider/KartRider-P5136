using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Set_Data;

namespace KartRider;

internal class RouterListener
{
	private static readonly object SyncRoot = new object();

	private static readonly HashSet<SessionGroup> Sessions = new HashSet<SessionGroup>();
	private static readonly Dictionary<uint, LegacySessionProfile> StoredProfilesByUserNo =
		new Dictionary<uint, LegacySessionProfile>();
	private static readonly Dictionary<string, LegacySessionProfile> StoredProfilesByUsername =
		new Dictionary<string, LegacySessionProfile>(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<uint, LegacySessionProfile> LoginProfiles =
		new Dictionary<uint, LegacySessionProfile>();
	private static readonly Dictionary<uint, IPEndPoint> ObservedUdpEndPoints =
		new Dictionary<uint, IPEndPoint>();

	private static uint _nextSessionOrdinal;
	private static ILegacyProfileStore _profileStore;

	private static readonly string ProfileStorePath = Path.Combine(
		AppContext.BaseDirectory,
		"data",
		"korean2005",
		"profiles.json");

	public static string sIP;

	public static string forceConnect;

	public static IPEndPoint CurrentUDPServer { get; set; }

	public static string ForceConnect { get; set; }

	public static TcpListener Listener { get; private set; }

	public static LegacyUdpServer UdpServer { get; private set; }

	public static SessionGroup MySession { get; set; }

	public static int TcpPort { get; private set; } = 39312;

	public static int UdpPort { get; private set; } = 39312;

	public static bool IsRunning => Listener != null;

	static RouterListener()
	{
		sIP = "127.0.0.1";
	}

	public static void OnAcceptSocket(IAsyncResult ar)
	{
		TcpListener listener = ar.AsyncState as TcpListener;
		if (listener == null)
		{
			return;
		}

		try
		{
			Socket clientSocket = listener.EndAcceptSocket(ar);
			SessionGroup session;
			lock (SyncRoot)
			{
				if (!ReferenceEquals(Listener, listener))
				{
					clientSocket.Dispose();
					return;
				}

				forceConnect = sIP;
				if (!(ForceConnect == "") && ForceConnect != "0.0.0.0")
				{
					forceConnect = ForceConnect;
				}
				LegacySessionProfile profile = LegacySessionProfile.CreateFromStaticTemplate(0);
				session = new SessionGroup(clientSocket, null, profile);
				Sessions.Add(session);
				MySession = session;
			}
			GameSupport.PcFirstMessage(session);
		}
		catch (ObjectDisposedException)
		{
		}
		catch (SocketException)
		{
		}
		catch (ArgumentException)
		{
		}
		finally
		{
			if (ReferenceEquals(Listener, listener))
			{
				try
				{
					listener.BeginAcceptSocket(OnAcceptSocket, listener);
				}
				catch (ObjectDisposedException)
				{
				}
				catch (SocketException)
				{
				}
			}
		}
	}

	public static void Start()
	{
		Start(sIP, 39312, 39312);
	}

	public static void Start(string serverIP, int tcpPort, int udpPort)
	{
		lock (SyncRoot)
		{
			if (Listener != null)
			{
				throw new InvalidOperationException("The legacy listener is already running.");
			}

			IPAddress serverAddress = IPAddress.Parse(serverIP);
			_nextSessionOrdinal = 0;
			StoredProfilesByUserNo.Clear();
			StoredProfilesByUsername.Clear();
			LoginProfiles.Clear();
			ObservedUdpEndPoints.Clear();
			InitializeProfileStoreLocked();
			LegacyObserverPolicy.Reload();
			sIP = serverAddress.ToString();
			TcpPort = tcpPort;
			UdpPort = udpPort;
			Console.WriteLine("Load server IP : {0}:{1}", sIP, TcpPort);
			ForceConnect = "";
			TcpListener listener = new TcpListener(IPAddress.Any, TcpPort);
			LegacyUdpServer udpServer = new LegacyUdpServer(UdpPort);
			Listener = listener;
			try
			{
				listener.Start();
				udpServer.Start();
				UdpServer = udpServer;
				listener.BeginAcceptSocket(OnAcceptSocket, listener);
				CurrentUDPServer = new IPEndPoint(serverAddress, UdpPort);
			}
			catch
			{
				udpServer.Dispose();
				listener.Stop();
				Listener = null;
				UdpServer = null;
				_nextSessionOrdinal = 0;
				_profileStore = null;
				StoredProfilesByUserNo.Clear();
				StoredProfilesByUsername.Clear();
				LoginProfiles.Clear();
				ObservedUdpEndPoints.Clear();
				throw;
			}
		}
	}

	public static void Stop()
	{
		SessionGroup[] sessions;
		LegacyUdpServer udpServer;
		lock (SyncRoot)
		{
			TcpListener listener = Listener;
			Listener = null;
			if (listener != null)
			{
				listener.Stop();
			}

			sessions = new SessionGroup[Sessions.Count];
			Sessions.CopyTo(sessions);
			SaveAllProfilesLocked();
			Sessions.Clear();
			_nextSessionOrdinal = 0;
			_profileStore = null;
			StoredProfilesByUserNo.Clear();
			StoredProfilesByUsername.Clear();
			LoginProfiles.Clear();
			ObservedUdpEndPoints.Clear();
			MySession = null;
			CurrentUDPServer = null;
			udpServer = UdpServer;
			UdpServer = null;
		}

		udpServer?.Dispose();

		foreach (SessionGroup session in sessions)
		{
			try
			{
				session.Client.Disconnect();
			}
			catch
			{
			}
		}
	}

	public static void RemoveSession(SessionGroup session)
	{
		lock (SyncRoot)
		{
			if (!Sessions.Remove(session))
			{
				return;
			}

			TrySaveProfileLocked(session.Profile);
			if (!ReferenceEquals(MySession, session))
			{
				return;
			}

			MySession = null;
			foreach (SessionGroup remainingSession in Sessions)
			{
				MySession = remainingSession;
				break;
			}
		}
	}

	public static string AssignLoginUsername(SessionGroup session, string username)
	{
		ArgumentNullException.ThrowIfNull(session);
		if (string.IsNullOrWhiteSpace(username))
		{
			throw new ArgumentException("A login username is required.", nameof(username));
		}

		string normalizedUsername = username.Trim();
		lock (SyncRoot)
		{
			if (StoredProfilesByUsername.TryGetValue(
				normalizedUsername,
				out LegacySessionProfile storedProfile))
			{
				session.RebindProfile(storedProfile);
				LoginProfiles[storedProfile.UserNo] = storedProfile;
				return storedProfile.UserId;
			}

			uint userNo = AllocateUserNoLocked();
			string uniqueIdentity = normalizedUsername;
			int suffix = 2;
			while (HasStoredIdentityLocked(uniqueIdentity))
			{
				uniqueIdentity = $"{normalizedUsername}_{suffix}";
				suffix++;
			}

			LegacySessionProfile profile = LegacySessionProfile.CreateFromStaticTemplate(userNo);
			profile.SourceUsername = normalizedUsername;
			profile.UserId = uniqueIdentity;
			profile.Nickname = uniqueIdentity;
			_profileStore.Save(LegacyProfileRecord.FromProfile(profile));
			StoredProfilesByUserNo.Add(profile.UserNo, profile);
			StoredProfilesByUsername.Add(profile.SourceUsername, profile);
			LoginProfiles[profile.UserNo] = profile;
			session.RebindProfile(profile);
			LegacyPacketTrace.LogEvent(
				$"[2005 PROFILE] Created username='{profile.SourceUsername}', userNo={profile.UserNo}.");
			return uniqueIdentity;
		}
	}

	public static void SaveProfile(SessionGroup session)
	{
		ArgumentNullException.ThrowIfNull(session);
		lock (SyncRoot)
		{
			TrySaveProfileLocked(session.Profile);
		}
	}

	public static bool TryBindChannelProfile(SessionGroup currentSession, uint claimedUserNo)
	{
		ArgumentNullException.ThrowIfNull(currentSession);
		if (claimedUserNo == 0)
		{
			return false;
		}

		lock (SyncRoot)
		{
			if (!Sessions.Contains(currentSession))
			{
				return false;
			}

			LegacySessionProfile currentProfile = currentSession.Profile;
			LegacySessionProfile loginProfile;
			if (currentProfile.UserNo == claimedUserNo &&
				currentProfile.SourceUsername != null)
			{
				loginProfile = currentProfile;
				LoginProfiles[claimedUserNo] = loginProfile;
			}
			else if (!LoginProfiles.TryGetValue(claimedUserNo, out loginProfile) &&
				!TryFindLoginProfileLocked(currentSession, claimedUserNo, out loginProfile))
			{
				return false;
			}

			CopyChannelStateLocked(currentSession, loginProfile, claimedUserNo);
			currentSession.RebindProfile(loginProfile);
			currentSession.Multiplayer.UserNo = claimedUserNo;
			return true;
		}
	}

	private static bool TryFindLoginProfileLocked(
		SessionGroup currentSession,
		uint userNo,
		out LegacySessionProfile profile)
	{
		foreach (SessionGroup session in Sessions)
		{
			LegacySessionProfile candidate = session.Profile;
			if (!ReferenceEquals(session, currentSession) &&
				candidate.UserNo == userNo &&
				candidate.SourceUsername != null)
			{
				profile = candidate;
				LoginProfiles[userNo] = candidate;
				return true;
			}
		}

		profile = null;
		return false;
	}

	public static void ObserveUdpEndPoint(uint userNo, IPEndPoint endPoint)
	{
		if (endPoint == null || endPoint.Port == 0)
		{
			return;
		}

		lock (SyncRoot)
		{
			IPEndPoint observedEndPoint = CloneEndPoint(endPoint);
			ObservedUdpEndPoints[userNo] = observedEndPoint;
			foreach (SessionGroup session in Sessions)
			{
				if (session.Profile.UserNo == userNo)
				{
					session.Multiplayer.ObservedUdpEndPoint = CloneEndPoint(observedEndPoint);
				}
			}
		}
	}

	private static void CopyChannelStateLocked(
		SessionGroup target,
		LegacySessionProfile profile,
		uint userNo)
	{
		bool copiedChannel = false;
		foreach (SessionGroup source in Sessions)
		{
			if (ReferenceEquals(source, target) || !ReferenceEquals(source.Profile, profile))
			{
				continue;
			}

			if (!copiedChannel || source.Multiplayer.ChannelToken != 0)
			{
				target.Multiplayer.Channel = source.Multiplayer.Channel;
				target.Multiplayer.ChannelToken = source.Multiplayer.ChannelToken;
				copiedChannel = true;
			}

			if (target.Multiplayer.ReportedUdpEndPoint == null &&
				source.Multiplayer.ReportedUdpEndPoint != null)
			{
				target.Multiplayer.ReportedUdpEndPoint =
					CloneEndPoint(source.Multiplayer.ReportedUdpEndPoint);
			}

			if (target.Multiplayer.ObservedUdpEndPoint == null &&
				source.Multiplayer.ObservedUdpEndPoint != null)
			{
				target.Multiplayer.ObservedUdpEndPoint =
					CloneEndPoint(source.Multiplayer.ObservedUdpEndPoint);
			}
		}

		if (ObservedUdpEndPoints.TryGetValue(userNo, out IPEndPoint observedEndPoint))
		{
			target.Multiplayer.ObservedUdpEndPoint = CloneEndPoint(observedEndPoint);
		}
	}

	private static IPEndPoint CloneEndPoint(IPEndPoint endPoint)
	{
		return new IPEndPoint(endPoint.Address, endPoint.Port);
	}

	private static void InitializeProfileStoreLocked()
	{
		ILegacyProfileStore profileStore = new JsonLegacyProfileStore(ProfileStorePath);
		IReadOnlyList<LegacyProfileRecord> records = profileStore.LoadAll();
		foreach (LegacyProfileRecord record in records)
		{
			LegacySessionProfile profile = record.ToProfile();
			StoredProfilesByUserNo.Add(profile.UserNo, profile);
			StoredProfilesByUsername.Add(profile.SourceUsername, profile);
		}

		_profileStore = profileStore;
		LegacyPacketTrace.LogEvent(
			$"[2005 PROFILE] Loaded {records.Count} profile(s) from '{ProfileStorePath}'.");
	}

	private static void TrySaveProfileLocked(LegacySessionProfile profile)
	{
		if (_profileStore == null ||
			profile == null ||
			string.IsNullOrWhiteSpace(profile.SourceUsername) ||
			!StoredProfilesByUserNo.TryGetValue(profile.UserNo, out LegacySessionProfile storedProfile) ||
			!ReferenceEquals(profile, storedProfile))
		{
			return;
		}

		try
		{
			_profileStore.Save(LegacyProfileRecord.FromProfile(profile));
		}
		catch (Exception ex)
		{
			LegacyPacketTrace.LogEvent(
				$"[2005 PROFILE] Failed to save username='{profile.SourceUsername}', " +
				$"userNo={profile.UserNo}: {ex.Message}");
		}
	}

	private static void SaveAllProfilesLocked()
	{
		if (_profileStore == null || StoredProfilesByUserNo.Count == 0)
		{
			return;
		}

		try
		{
			List<LegacyProfileRecord> profiles = new List<LegacyProfileRecord>();
			foreach (LegacySessionProfile profile in StoredProfilesByUserNo.Values)
			{
				profiles.Add(LegacyProfileRecord.FromProfile(profile));
			}

			_profileStore.SaveAll(profiles);
		}
		catch (Exception ex)
		{
			LegacyPacketTrace.LogEvent($"[2005 PROFILE] Failed to flush profiles: {ex.Message}");
		}
	}

	private static uint AllocateUserNoLocked()
	{
		int maximumAttempts = StoredProfilesByUserNo.Count + Sessions.Count + 2;
		for (int attempts = 0; attempts < maximumAttempts; attempts++)
		{
			uint userNo = unchecked(SetRider.UserNO + _nextSessionOrdinal);
			_nextSessionOrdinal++;
			if (userNo != 0 && !HasAllocatedUserNoLocked(userNo))
			{
				return userNo;
			}
		}

		throw new InvalidOperationException("Unable to allocate a unique 2005 user number.");
	}

	private static bool HasAllocatedUserNoLocked(uint userNo)
	{
		if (StoredProfilesByUserNo.ContainsKey(userNo))
		{
			return true;
		}

		foreach (SessionGroup session in Sessions)
		{
			if (session.Profile.UserNo == userNo)
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasStoredIdentityLocked(string identity)
	{
		foreach (LegacySessionProfile profile in StoredProfilesByUserNo.Values)
		{
			if (string.Equals(profile.UserId, identity, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(profile.Nickname, identity, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
