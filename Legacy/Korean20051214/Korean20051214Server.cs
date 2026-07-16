using System;
using System.IO;
using Set_Data;

namespace KartRider.Legacy.Korean20051214;

/// <summary>
/// Configuration for the Korean 2005-12-14 (protocol version 236) server.
/// Defaults expose TCP and UDP on the endpoint expected by the 2005 client
/// after a channel switch.
/// </summary>
public sealed class Korean20051214Options
{
	public string ServerIP { get; set; } = "127.0.0.1";

	public int TcpPort { get; set; } = 39312;

	public int UdpPort { get; set; } = 39312;

	public string Username { get; set; } = "WIZONE";

	public string Nickname { get; set; } = "카트라이더";

	public uint UserNo { get; set; } = 201810291u;

	public uint Lucci { get; set; } = 1000000u;

	public int Rp { get; set; } = 10000000;

	public short SlotChanger { get; set; } = 30000;

	public bool PreventItem { get; set; }

	public short Character { get; set; } = 3;

	public short Paint { get; set; } = 1;

	public short Kart { get; set; }

	public short Plate { get; set; }

	public short Goggle { get; set; }

	public short Balloon { get; set; }

	public short HeadBand { get; set; }

	/// <summary>
	/// Directory used for the legacy SetRider text files. The default preserves
	/// the original Profile/Launcher layout relative to the process directory.
	/// </summary>
	public string ProfileRootDirectory { get; set; } = Path.Combine("Profile", "Launcher");

	/// <summary>
	/// Loads (or creates) the original text profile files before accepting clients.
	/// </summary>
	public bool LoadProfileFiles { get; set; } = true;
}

/// <summary>
/// Process-local facade for the recovered 2005-12-14 login/game server.
/// Only one instance can run because the original implementation uses static state.
/// </summary>
public static class Korean20051214Server
{
	private static readonly object SyncRoot = new object();

	public const ushort ProtocolVersion = 236;

	public const string DataTime = "2A 97 00 00";

	public static bool IsRunning
	{
		get
		{
			lock (SyncRoot)
			{
				return RouterListener.IsRunning;
			}
		}
	}

	public static void Start(
		string serverIP = "127.0.0.1",
		int tcpPort = 39312,
		int udpPort = 39312,
		string username = "WIZONE",
		string nickname = "카트라이더")
	{
		Start(new Korean20051214Options
		{
			ServerIP = serverIP,
			TcpPort = tcpPort,
			UdpPort = udpPort,
			Username = username,
			Nickname = nickname
		});
	}

	public static void Start(Korean20051214Options options)
	{
		ArgumentNullException.ThrowIfNull(options);

		lock (SyncRoot)
		{
			if (RouterListener.IsRunning)
			{
				throw new InvalidOperationException("The Korean 2005-12-14 server is already running.");
			}

			Validate(options);
			ApplyProfile(options);
			RouterListener.Start(options.ServerIP, options.TcpPort, options.UdpPort);
		}
	}

	public static void Stop()
	{
		lock (SyncRoot)
		{
			RouterListener.Stop();
		}
	}

	private static void ApplyProfile(Korean20051214Options options)
	{
		string profileRoot = Path.GetFullPath(options.ProfileRootDirectory);
		FileName.SetRider_LoadFile = EnsureTrailingSeparator(Path.Combine(profileRoot, "SetRider"));
		FileName.SetRiderItem_LoadFile = EnsureTrailingSeparator(Path.Combine(profileRoot, "SetRider", "SetRiderItem"));

		SetRider.UserID = options.Username;
		SetRider.UserNO = options.UserNo;
		SetRider.Nickname = options.Nickname;
		SetRider.Lucci = options.Lucci;
		SetRider.RP = options.Rp;
		SetRider.SlotChanger = options.SlotChanger;
		SetRider.ResetLicenseProgression();

		SetRiderItem.Set_Character = options.Character;
		SetRiderItem.Set_Paint = options.Paint;
		SetRiderItem.Set_Kart = options.Kart;
		SetRiderItem.Set_Plate = options.Plate;
		SetRiderItem.Set_Goggle = options.Goggle;
		SetRiderItem.Set_Balloon = options.Balloon;
		SetRiderItem.Set_HeadBand = options.HeadBand;
		Program.PreventItem = options.PreventItem;

		if (options.LoadProfileFiles)
		{
			StartingLoad_ALL.StartingLoad();
		}
		else
		{
			SetRider.Load_LicenseProgression();
		}

		Console.WriteLine(
			$"2005 license progression: level={SetRider.GetLicenseLevel()}, " +
			$"masks={string.Join(",", SetRider.GetLicenseCompletionMasks())}, RP={SetRider.RP}.");
	}

	private static string EnsureTrailingSeparator(string path)
	{
		return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
			? path
			: path + Path.DirectorySeparatorChar;
	}

	private static void Validate(Korean20051214Options options)
	{
		if (string.IsNullOrWhiteSpace(options.ServerIP))
		{
			throw new ArgumentException("A server IP address is required.", nameof(options));
		}

		if (options.TcpPort is < 1 or > 65535)
		{
			throw new ArgumentOutOfRangeException(nameof(options), "TCP port must be between 1 and 65535.");
		}

		if (options.UdpPort is < 1 or > 65535)
		{
			throw new ArgumentOutOfRangeException(nameof(options), "UDP port must be between 1 and 65535.");
		}

		if (string.IsNullOrEmpty(options.Username))
		{
			throw new ArgumentException("A username is required.", nameof(options));
		}

		if (string.IsNullOrEmpty(options.Nickname))
		{
			throw new ArgumentException("A nickname is required.", nameof(options));
		}

		if (string.IsNullOrWhiteSpace(options.ProfileRootDirectory))
		{
			throw new ArgumentException("A profile root directory is required.", nameof(options));
		}
	}
}
