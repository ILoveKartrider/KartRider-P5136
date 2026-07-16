using System;
using System.IO;

namespace Set_Data;

internal static class SetRiderItem
{
	public static short Set_Character = 3;

	public static short Set_Paint = 1;

	public static short Set_Kart = 0;

	public static short Set_Plate = 0;

	public static short Set_Goggle = 0;

	public static short Set_Balloon = 0;

	public static short Set_HeadBand = 0;

	public static void Save_SetRiderItem()
	{
		using (StreamWriter streamWriter = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Character + FileName.Extension, append: false))
		{
			streamWriter.Write(Set_Character);
		}
		using (StreamWriter streamWriter2 = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Paint + FileName.Extension, append: false))
		{
			streamWriter2.Write(Set_Paint);
		}
		using (StreamWriter streamWriter3 = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Kart + FileName.Extension, append: false))
		{
			streamWriter3.Write(Set_Kart);
		}
		using (StreamWriter streamWriter4 = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Plate + FileName.Extension, append: false))
		{
			streamWriter4.Write(Set_Plate);
		}
		using (StreamWriter streamWriter5 = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Goggle + FileName.Extension, append: false))
		{
			streamWriter5.Write(Set_Goggle);
		}
		using (StreamWriter streamWriter6 = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Balloon + FileName.Extension, append: false))
		{
			streamWriter6.Write(Set_Balloon);
		}
		using (StreamWriter streamWriter7 = new StreamWriter(FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_HeadBand + FileName.Extension, append: false))
		{
			streamWriter7.Write(Set_HeadBand);
		}
		Console.WriteLine("SetRiderItem-------------------------------------------------");
		Console.WriteLine("Character: {0}", Set_Character);
		Console.WriteLine("Paint: {0}", Set_Paint);
		Console.WriteLine("Kart: {0}", Set_Kart);
		Console.WriteLine("Plate: {0}", Set_Plate);
		Console.WriteLine("Goggle: {0}", Set_Goggle);
		Console.WriteLine("Balloon: {0}", Set_Balloon);
		Console.WriteLine("HeadBand: {0}", Set_HeadBand);
	}

	public static void Load_SetRiderItem()
	{
		string path = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Character + FileName.Extension;
		if (File.Exists(path))
		{
			Set_Character = short.Parse(File.ReadAllText(path));
		}
		else
		{
			using StreamWriter streamWriter = new StreamWriter(path, append: false);
			streamWriter.Write(Set_Character);
		}
		string path2 = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Paint + FileName.Extension;
		if (File.Exists(path2))
		{
			Set_Paint = short.Parse(File.ReadAllText(path2));
		}
		else
		{
			using StreamWriter streamWriter2 = new StreamWriter(path2, append: false);
			streamWriter2.Write(Set_Paint);
		}
		string path3 = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Kart + FileName.Extension;
		if (File.Exists(path3))
		{
			Set_Kart = short.Parse(File.ReadAllText(path3));
		}
		else
		{
			using StreamWriter streamWriter3 = new StreamWriter(path3, append: false);
			streamWriter3.Write(Set_Kart);
		}
		string path4 = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Plate + FileName.Extension;
		if (File.Exists(path4))
		{
			Set_Plate = short.Parse(File.ReadAllText(path4));
		}
		else
		{
			using StreamWriter streamWriter4 = new StreamWriter(path4, append: false);
			streamWriter4.Write(Set_Plate);
		}
		string path5 = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Goggle + FileName.Extension;
		if (File.Exists(path5))
		{
			Set_Goggle = short.Parse(File.ReadAllText(path5));
		}
		else
		{
			using StreamWriter streamWriter5 = new StreamWriter(path5, append: false);
			streamWriter5.Write(Set_Goggle);
		}
		string path6 = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_Balloon + FileName.Extension;
		if (File.Exists(path6))
		{
			Set_Balloon = short.Parse(File.ReadAllText(path6));
		}
		else
		{
			using StreamWriter streamWriter6 = new StreamWriter(path6, append: false);
			streamWriter6.Write(Set_Balloon);
		}
		string path7 = FileName.SetRiderItem_LoadFile + FileName.SetRiderItem_HeadBand + FileName.Extension;
		if (File.Exists(path7))
		{
			Set_HeadBand = short.Parse(File.ReadAllText(path7));
			return;
		}
		using StreamWriter streamWriter7 = new StreamWriter(path7, append: false);
		streamWriter7.Write(Set_HeadBand);
	}
}
