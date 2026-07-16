using System;
using Set_Data;

namespace KartRider;

internal class GameDataReset
{
	public static void GameType_DataReset()
	{
		GameType.TimeAttack_RP = 0;
		GameType.TimeAttack_Lucci = 0u;
		GameType.RewardType = 0;
		GameType.min = 0;
		GameType.sec = 0;
		GameType.mil = 0;
		GameType.S4_DriftMaxGauge = 1f;
		GameType.ScenarioType = 0;
		GameType.StartType = 0;
	}

	public static void DataReset(SessionGroup session)
	{
		if (SetRider.Lucci > SessionGroup.LucciMax)
		{
			SetRider.Lucci = SessionGroup.LucciMax;
		}
		GameType_DataReset();
		GameSupport.PrLogin(session);
		Console.WriteLine("Login...OK");
	}
}
