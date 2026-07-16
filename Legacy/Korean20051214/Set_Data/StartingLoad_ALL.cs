using System.IO;

namespace Set_Data;

internal static class StartingLoad_ALL
{
	public static void StartingLoad()
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(FileName.SetRiderItem_LoadFile);
		if (!directoryInfo.Exists)
		{
			directoryInfo.Create();
		}
		SetRider.Load_SetRider();
		SetRiderItem.Load_SetRiderItem();
	}
}
