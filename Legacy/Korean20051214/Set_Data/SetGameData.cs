using System.IO;

namespace Set_Data;

internal static class SetGameData
{
	public static void Save_Nickname()
	{
		using StreamWriter streamWriter = new StreamWriter(FileName.SetRider_LoadFile + FileName.SetRider_Nickname + FileName.Extension, append: false);
		streamWriter.Write(SetRider.Nickname);
	}

	public static void Save_SlotChanger()
	{
		using StreamWriter streamWriter = new StreamWriter(FileName.SetRider_LoadFile + FileName.SetRider_SlotChanger + FileName.Extension, append: false);
		streamWriter.Write(SetRider.SlotChanger);
	}
}
