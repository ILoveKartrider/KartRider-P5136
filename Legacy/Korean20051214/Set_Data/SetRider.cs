using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Set_Data;

internal static class SetRider
{
	public static string UserID = "WIZONE";

	public static uint UserNO = 201810291u;

	public static string Nickname = "카트라이더";

	public static string RiderIntro = "";

	public static uint Lucci = 1000000u;

	public static int RP = 10000000;

	public static short SlotChanger = 30000;

	public static uint pmap = 0u;

	private static readonly ushort[] DefaultLicenseCompletionMasks = { 31, 7, 31, 63, 0, 0 };

	private const byte MaximumLicenseLevel = 4;

	private const byte DefaultLicenseLevel = 3;

	private static readonly ushort[] LicenseCompletionMasks = (ushort[])DefaultLicenseCompletionMasks.Clone();

	private static byte LicenseLevel = DefaultLicenseLevel;

	private static readonly object LicenseProgressionLock = new object();

	public static ushort[] GetLicenseCompletionMasks()
	{
		lock (LicenseProgressionLock)
		{
			return (ushort[])LicenseCompletionMasks.Clone();
		}
	}

	public static byte GetLicenseLevel()
	{
		lock (LicenseProgressionLock)
		{
			return LicenseLevel;
		}
	}

	public static bool IsSupportedLicenseLevel(byte level)
	{
		return level <= MaximumLicenseLevel;
	}

	public static void SetLicenseCompletionMasks(ushort[] masks)
	{
		if (masks == null || masks.Length != LicenseCompletionMasks.Length)
		{
			throw new ArgumentException("Six license completion masks are required.", nameof(masks));
		}

		lock (LicenseProgressionLock)
		{
			Array.Copy(masks, LicenseCompletionMasks, LicenseCompletionMasks.Length);
		}
	}

	public static void SetAndSaveLicenseCompletionMasks(ushort[] masks)
	{
		if (masks == null || masks.Length != LicenseCompletionMasks.Length)
		{
			throw new ArgumentException("Six license completion masks are required.", nameof(masks));
		}

		lock (LicenseProgressionLock)
		{
			SaveLicenseCompletionNoLock(masks);
			Array.Copy(masks, LicenseCompletionMasks, LicenseCompletionMasks.Length);
		}
	}

	public static void SetAndSaveLicenseProgression(byte level, ushort[] masks)
	{
		ValidateLicenseLevel(level);
		if (masks == null || masks.Length != LicenseCompletionMasks.Length)
		{
			throw new ArgumentException("Six license completion masks are required.", nameof(masks));
		}

		lock (LicenseProgressionLock)
		{
			SaveLicenseCompletionNoLock(masks);
			SaveLicenseLevelNoLock(level);
			Array.Copy(masks, LicenseCompletionMasks, LicenseCompletionMasks.Length);
			LicenseLevel = level;
		}
	}

	public static void ResetLicenseCompletionMasks()
	{
		lock (LicenseProgressionLock)
		{
			Array.Copy(DefaultLicenseCompletionMasks, LicenseCompletionMasks, LicenseCompletionMasks.Length);
		}
	}

	public static void ResetLicenseProgression()
	{
		lock (LicenseProgressionLock)
		{
			Array.Copy(DefaultLicenseCompletionMasks, LicenseCompletionMasks, LicenseCompletionMasks.Length);
			LicenseLevel = DefaultLicenseLevel;
		}
	}

	public static void Save_LicenseCompletion()
	{
		lock (LicenseProgressionLock)
		{
			SaveLicenseCompletionNoLock(LicenseCompletionMasks);
		}
	}

	private static void SaveLicenseCompletionNoLock(ushort[] masks)
	{
		Directory.CreateDirectory(FileName.SetRider_LoadFile);
		string path = FileName.SetRider_LoadFile + FileName.SetRider_LicenseCompletion + FileName.Extension;
		string temporaryPath = path + ".tmp";
		File.WriteAllText(temporaryPath, string.Join(",", masks.Select(value => value.ToString(CultureInfo.InvariantCulture))));
		File.Move(temporaryPath, path, overwrite: true);
	}

	private static void SaveLicenseLevelNoLock(byte level)
	{
		Directory.CreateDirectory(FileName.SetRider_LoadFile);
		string path = FileName.SetRider_LoadFile + FileName.SetRider_LicenseLevel + FileName.Extension;
		string temporaryPath = path + ".tmp";
		File.WriteAllText(temporaryPath, level.ToString(CultureInfo.InvariantCulture));
		File.Move(temporaryPath, path, overwrite: true);
	}

	private static void ValidateLicenseLevel(byte level)
	{
		if (!IsSupportedLicenseLevel(level))
		{
			throw new ArgumentOutOfRangeException(nameof(level), level, "License level must be between 0 and 4 (L1).");
		}
	}

	public static void Save_SetRider()
	{
		using (StreamWriter streamWriter = new StreamWriter(FileName.SetRider_LoadFile + FileName.SetRider_Nickname + FileName.Extension, append: false))
		{
			streamWriter.Write(Nickname);
		}
		using StreamWriter streamWriter2 = new StreamWriter(FileName.SetRider_LoadFile + FileName.SetRider_SlotChanger + FileName.Extension, append: false);
		streamWriter2.Write(SlotChanger);
	}

	public static void Load_SetRider()
	{
		string path = FileName.SetRider_LoadFile + FileName.SetRider_Nickname + FileName.Extension;
		if (File.Exists(path))
		{
			Nickname = File.ReadAllText(path);
		}
		else
		{
			using StreamWriter streamWriter = new StreamWriter(path, append: false);
			streamWriter.Write(Nickname);
		}
		string path2 = FileName.SetRider_LoadFile + FileName.SetRider_SlotChanger + FileName.Extension;
		if (File.Exists(path2))
		{
			SlotChanger = short.Parse(File.ReadAllText(path2));
		}
		else
		{
			using StreamWriter streamWriter2 = new StreamWriter(path2, append: false);
			streamWriter2.Write(SlotChanger);
		}

		Load_LicenseProgression();
	}

	public static void Load_LicenseProgression()
	{
		Directory.CreateDirectory(FileName.SetRider_LoadFile);
		string licensePath = FileName.SetRider_LoadFile + FileName.SetRider_LicenseCompletion + FileName.Extension;
		if (File.Exists(licensePath))
		{
			string[] values = File.ReadAllText(licensePath).Split(',');
			ushort[] loadedMasks = new ushort[LicenseCompletionMasks.Length];
			bool valid = values.Length == loadedMasks.Length;
			for (int i = 0; valid && i < values.Length; i++)
			{
				valid = ushort.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out loadedMasks[i]);
			}

			if (valid)
			{
				SetLicenseCompletionMasks(loadedMasks);
			}
			else
			{
				ResetLicenseCompletionMasks();
				Save_LicenseCompletion();
			}
		}
		else
		{
			Save_LicenseCompletion();
		}

		string levelPath = FileName.SetRider_LoadFile + FileName.SetRider_LicenseLevel + FileName.Extension;
		if (File.Exists(levelPath) &&
			byte.TryParse(File.ReadAllText(levelPath), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte loadedLevel) &&
			loadedLevel <= 5)
		{
			byte supportedLevel = IsSupportedLicenseLevel(loadedLevel) ? loadedLevel : MaximumLicenseLevel;
			lock (LicenseProgressionLock)
			{
				LicenseLevel = supportedLevel;
				if (supportedLevel != loadedLevel)
				{
					SaveLicenseLevelNoLock(supportedLevel);
				}
			}
		}
		else
		{
			lock (LicenseProgressionLock)
			{
				LicenseLevel = DefaultLicenseLevel;
				SaveLicenseLevelNoLock(LicenseLevel);
			}
		}
	}
}
