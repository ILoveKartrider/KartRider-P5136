using System;
using System.Net.Sockets;
using System.Threading;

namespace KartRider;

internal class SessionGroup
{
	private LegacySessionProfile _profile;

	public object m_lock = new object();

	public uint TimeAttackStartTicks;

	public int SendPlaneCount = 6;

	public int TotalSendPlaneCount = 6;

	public byte PlaneCheck1;

	public LegacySingleRaceState SingleRace { get; } = new LegacySingleRaceState();

	public LegacyMultiplayerState Multiplayer { get; } = new LegacyMultiplayerState();

	public LegacySessionProfile Profile => Volatile.Read(ref _profile);

	public static uint LucciMax = 9999999u;

	public static ushort usLocale = 1002;

	public static byte nClientLoc = 118;

	public static string Service = "kr";

	public static string Developer = "KartRider 2023 by LAON";

	public ClientSession Client { get; set; }

	public SessionGroup(Socket clientSocket, Socket serverSocket, LegacySessionProfile profile)
	{
		_profile = profile ?? throw new ArgumentNullException(nameof(profile));
		Client = new ClientSession(this, clientSocket);
	}

	internal LegacySessionProfile RebindProfile(LegacySessionProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);
		return Interlocked.Exchange(ref _profile, profile);
	}
}
