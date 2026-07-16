using System;
using Set_Data;

namespace KartRider;

internal sealed class LegacyEquipment
{
	public ushort Character { get; set; }

	public ushort Paint { get; set; }

	public ushort Kart { get; set; }

	public ushort Plate { get; set; }

	public ushort Goggle { get; set; }

	public ushort Balloon { get; set; }

	public ushort Reserved0 { get; set; }

	public ushort HeadBand { get; set; }

	public ushort Reserved1 { get; set; }

	public LegacyEquipment Clone()
	{
		return new LegacyEquipment
		{
			Character = Character,
			Paint = Paint,
			Kart = Kart,
			Plate = Plate,
			Goggle = Goggle,
			Balloon = Balloon,
			Reserved0 = Reserved0,
			HeadBand = HeadBand,
			Reserved1 = Reserved1
		};
	}

	public static LegacyEquipment CreateFromStaticTemplate()
	{
		return new LegacyEquipment
		{
			Character = unchecked((ushort)SetRiderItem.Set_Character),
			Paint = unchecked((ushort)SetRiderItem.Set_Paint),
			Kart = unchecked((ushort)SetRiderItem.Set_Kart),
			Plate = unchecked((ushort)SetRiderItem.Set_Plate),
			Goggle = unchecked((ushort)SetRiderItem.Set_Goggle),
			Balloon = unchecked((ushort)SetRiderItem.Set_Balloon),
			Reserved0 = 0,
			HeadBand = unchecked((ushort)SetRiderItem.Set_HeadBand),
			Reserved1 = 0
		};
	}
}

internal sealed class LegacySessionProfile
{
	private ushort[] _licenseCompletionMasks;

	public uint UserNo { get; set; }

	public string UserId { get; set; }

	public string Nickname { get; set; }

	public string RiderIntro { get; set; }

	public int RP { get; set; }

	public uint PMap { get; set; }

	public uint Lucci { get; set; }

	public short SlotChanger { get; set; }

	public byte LicenseLevel { get; set; }

	public LegacyEquipment Equipment { get; }

	internal string SourceUsername { get; set; }

	private LegacySessionProfile(
		uint userNo,
		string userId,
		string nickname,
		string riderIntro,
		int rp,
		uint pMap,
		uint lucci,
		short slotChanger,
		byte licenseLevel,
		ushort[] licenseCompletionMasks,
		LegacyEquipment equipment)
	{
		UserNo = userNo;
		UserId = userId ?? string.Empty;
		Nickname = nickname ?? string.Empty;
		RiderIntro = riderIntro ?? string.Empty;
		RP = rp;
		PMap = pMap;
		Lucci = lucci;
		SlotChanger = slotChanger;
		LicenseLevel = licenseLevel;
		_licenseCompletionMasks = licenseCompletionMasks ?? Array.Empty<ushort>();
		Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
	}

	public ushort[] GetLicenseCompletionMasks()
	{
		return (ushort[])_licenseCompletionMasks.Clone();
	}

	public void SetLicenseCompletionMasks(ushort[] masks)
	{
		ArgumentNullException.ThrowIfNull(masks);
		_licenseCompletionMasks = (ushort[])masks.Clone();
	}

	public static LegacySessionProfile CreateFromStaticTemplate(uint userNo)
	{
		return new LegacySessionProfile(
			userNo,
			SetRider.UserID,
			SetRider.Nickname,
			SetRider.RiderIntro,
			SetRider.RP,
			SetRider.pmap,
			SetRider.Lucci,
			SetRider.SlotChanger,
			SetRider.GetLicenseLevel(),
			SetRider.GetLicenseCompletionMasks(),
			LegacyEquipment.CreateFromStaticTemplate());
	}

	public static LegacySessionProfile CreatePersisted(
		string sourceUsername,
		uint userNo,
		string userId,
		string nickname,
		string riderIntro,
		int rp,
		uint pMap,
		uint lucci,
		short slotChanger,
		byte licenseLevel,
		ushort[] licenseCompletionMasks,
		LegacyEquipment equipment)
	{
		LegacySessionProfile profile = new LegacySessionProfile(
			userNo,
			userId,
			nickname,
			riderIntro,
			rp,
			pMap,
			lucci,
			slotChanger,
			licenseLevel,
			licenseCompletionMasks,
			equipment);
		profile.SourceUsername = sourceUsername;
		return profile;
	}
}
