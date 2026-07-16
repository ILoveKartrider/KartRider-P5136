using System;
using System.Net.Sockets;
using System.Text;
using KartRider.Common.Network;
using KartRider.Common.Utilities;
using KartRider.IO;
using RiderData;

namespace KartRider;

internal class ClientSession : Session
{
	public SessionGroup Parent { get; set; }

	public ClientSession(SessionGroup parent, Socket socket)
		: base(socket)
	{
		Parent = parent;
	}

	public override void OnDisconnect()
	{
		LegacyPacketTrace.LogEvent(
			$"[2005 SESSION] Disconnect userNo={Parent.Profile.UserNo}, " +
			$"userId='{Parent.Profile.UserId}', roomId={Parent.Multiplayer.RoomId}, " +
			$"remote={GetRemoteEndPoint()}.");
		LegacyMultiplayerHandlers.HandleDisconnect(Parent);
		RouterListener.RemoveSession(Parent);
	}

	public override void OnPacket(InPacket iPacket)
	{
		lock (Parent.m_lock)
		{
			iPacket.Position = 0;
			uint num = iPacket.ReadUInt();
			LegacyPacketTrace.LogReceive(num, iPacket);
			if (LegacyMultiplayerHandlers.TryHandle(Parent, num, iPacket))
			{
				return;
			}
			if (num == Adler32Helper.GenerateAdler32_ASCII("LoPingRequestPacket"))
			{
				using OutPacket pingReply = new OutPacket("LoPingReplyPacket");
				Parent.Client.Send(pingReply);
				return;
			}
			if (num == Adler32Helper.GenerateAdler32_ASCII("PqReportMachineInfo"))
			{
				using OutPacket machineInfoReply = new OutPacket("PrReportMachineInfo");
				Parent.Client.Send(machineInfoReply);
				return;
			}
			if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqAddRacingTimePacket"))
			{
				LegacyPacketHandlers.HandleAddRacingTime(Parent, iPacket);
				return;
			}
			if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqUpdateCbPacket"))
			{
				LegacyPacketHandlers.HandleUpdateCompletion(Parent, iPacket);
				return;
			}
			if (num == Adler32Helper.GenerateAdler32(Encoding.ASCII.GetBytes("PcReportRaidOccur")) || num == 1340475309 || num == Adler32Helper.GenerateAdler32_ASCII("GrRiderTalkPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqEnterMagicHatPacket") || num == Adler32Helper.GenerateAdler32_ASCII("LoPingRequestPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqGetRiderQuestUX2ndData") || num == Adler32Helper.GenerateAdler32_ASCII("PqAddTimeEventInitPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqCountdownBoxPeriodPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqServerSideUdpBindCheck") || num == Adler32Helper.GenerateAdler32_ASCII("PqVipGradeCheck") || num == Adler32Helper.GenerateAdler32_ASCII("SpRqGetMaxGiftIdPacket") || num == Adler32Helper.GenerateAdler32_ASCII("LoRqUpdateRiderSchoolDataPacket") || num == Adler32Helper.GenerateAdler32_ASCII("RmRiderTalkPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqNeedTimerGiftEvent") || num == Adler32Helper.GenerateAdler32_ASCII("PcReportStateInGame") || num == Adler32Helper.GenerateAdler32_ASCII("GameBoosterAddPacket") || num == Adler32Helper.GenerateAdler32_ASCII("LoRqCheckReplayItemPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqGetRecommandChatServerInfo") || num == Adler32Helper.GenerateAdler32_ASCII("LoCheckLoginEvent") || num == Adler32Helper.GenerateAdler32_ASCII("PqBlockWordLogPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqWriteActionLogPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqMissionAttendPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqEnterShopPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqAddTimeEventTimerPacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqTimeShopOpenTimePacket") || num == Adler32Helper.GenerateAdler32_ASCII("PqItemPresetSlotDataList") || num == Adler32Helper.GenerateAdler32_ASCII("VipPlaytimeCheck") || num == Adler32Helper.GenerateAdler32_ASCII("LoRqEventRewardPacket") || num == Adler32Helper.GenerateAdler32_ASCII("LoRqAddRacingTimePacket") || num == Adler32Helper.GenerateAdler32_ASCII("LoRqUploadFilePacket"))
			{
				return;
			}
			if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqStartSinglePacket"))
			{
				LegacyPacketHandlers.HandleStartSingle(Parent, iPacket);
			}
			else
			{
				if (num == Adler32Helper.GenerateAdler32_ASCII("GameSlotPacket"))
				{
					LegacyPacketHandlers.HandleGameSlot(Parent, iPacket);
					return;
				}
				if (num == Adler32Helper.GenerateAdler32_ASCII("GameReportPacket"))
				{
					LegacyPacketHandlers.HandleGameReport(Parent, iPacket);
					return;
				}
				if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqGetRiderPacket"))
				{
					NewRider.NewRiderData(Parent);
				}
				else
				{
					if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqGetRiderItemPacket"))
					{
						NewRider.LoadItemData(Parent);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetRiderInfo"))
					{
						if (string.Equals(
							iPacket.ReadString(),
							Parent.Profile.Nickname,
							StringComparison.Ordinal))
						{
							GameSupport.PrGetRiderInfo(Parent);
							return;
						}
						using OutPacket outPacket = new OutPacket("PrGetRiderInfo");
						outPacket.WriteByte(0);
						Parent.Client.Send(outPacket);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSetPlaytimeEventTick"))
					{
						using (OutPacket outPacket2 = new OutPacket("PrSetPlaytimeEventTick"))
						{
							outPacket2.WriteByte(0);
							Parent.Client.Send(outPacket2);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqStartScenario"))
					{
						GameType.ScenarioType = iPacket.ReadInt();
						using OutPacket outPacket3 = new OutPacket("PrStartScenario");
						outPacket3.WriteInt(GameType.ScenarioType);
						outPacket3.WriteByte(0);
						Parent.Client.Send(outPacket3);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqCompleteScenarioSingle"))
					{
						using (OutPacket outPacket4 = new OutPacket("PrCompleteScenarioSingle"))
						{
							outPacket4.WriteInt(GameType.ScenarioType);
							outPacket4.WriteByte(0);
							Parent.Client.Send(outPacket4);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqChapterInfoPacket"))
					{
						using (OutPacket outPacket5 = new OutPacket("PrChapterInfoPacket"))
						{
							outPacket5.WriteInt();
							Parent.Client.Send(outPacket5);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqChallengerInfoPacket"))
					{
						using (OutPacket outPacket6 = new OutPacket("PrChallengerInfoPacket"))
						{
							int num2 = 40;
							outPacket6.WriteInt(num2);
							for (int i = 0; i < num2; i++)
							{
								outPacket6.WriteShort(55);
							}
							outPacket6.WriteInt();
							outPacket6.WriteByte(1);
							Parent.Client.Send(outPacket6);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqStartChallenger"))
					{
						int value = iPacket.ReadInt();
						int value2 = iPacket.ReadInt();
						iPacket.ReadShort();
						iPacket.ReadByte();
						iPacket.ReadByte();
						iPacket.ReadByte();
						using OutPacket outPacket7 = new OutPacket("PrStartChallenger");
						outPacket7.WriteInt(value);
						outPacket7.WriteInt(value2);
						outPacket7.WriteByte(0);
						outPacket7.WriteByte(1);
						Parent.Client.Send(outPacket7);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqGetTrackRankPacket"))
					{
						uint value3 = iPacket.ReadUInt();
						byte value4 = iPacket.ReadByte();
						byte value5 = iPacket.ReadByte();
						using OutPacket outPacket8 = new OutPacket("LoRpGetTrackRankPacket");
						outPacket8.WriteUInt(value3);
						outPacket8.WriteByte(value4);
						outPacket8.WriteByte(value5);
						outPacket8.WriteInt();
						Parent.Client.Send(outPacket8);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqFavoriteTrackUpdate"))
					{
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqDecLucciPacket"))
					{
						iPacket.ReadByte();
						uint num3 = iPacket.ReadUInt();
						Parent.Profile.Lucci -= num3;
						RouterListener.SaveProfile(Parent);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqUseItemPacket"))
					{
						iPacket.ReadShort();
						iPacket.ReadShort();
						Parent.Profile.SlotChanger = iPacket.ReadShort();
						RouterListener.SaveProfile(Parent);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGameOutRunUX2ndClearPacket"))
					{
						using (OutPacket outPacket9 = new OutPacket("PrGameOutRunUX2ndClearPacket"))
						{
							outPacket9.WriteInt();
							Parent.Client.Send(outPacket9);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetTrainingMissionReward"))
					{
						Console.WriteLine("PqGetTrainingMissionReward: {0}", iPacket);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqBingoGachaPacket"))
					{
						int num5 = iPacket.ReadInt();
						switch (num5)
						{
						case 0:
						{
							using OutPacket outPacket11 = new OutPacket("SpRpBingoGachaPacket");
							outPacket11.WriteInt(num5);
							for (int k = 0; k < 5; k++)
							{
								outPacket11.WriteInt();
							}
							outPacket11.WriteByte(0);
							outPacket11.WriteByte(0);
							outPacket11.WriteByte(0);
							Parent.Client.Send(outPacket11);
							break;
						}
						case 4:
						{
							using OutPacket outPacket10 = new OutPacket("SpRpBingoGachaPacket");
							outPacket10.WriteInt(num5);
							for (int j = 0; j < 5; j++)
							{
								outPacket10.WriteInt();
							}
							outPacket10.WriteByte(0);
							outPacket10.WriteByte(0);
							outPacket10.WriteByte(0);
							Parent.Client.Send(outPacket10);
							break;
						}
						}
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqCheckMyLeaveDatePacket"))
					{
						using (OutPacket outPacket12 = new OutPacket("PrCheckMyLeaveDatePacket"))
						{
							outPacket12.WriteInt();
							Parent.Client.Send(outPacket12);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetUserWaitingJoinClubPacket"))
					{
						using (OutPacket outPacket13 = new OutPacket("PrGetUserWaitingJoinClubPacket"))
						{
							outPacket13.WriteInt(1);
							outPacket13.WriteInt();
							outPacket13.WriteInt();
							Parent.Client.Send(outPacket13);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqCheckCreateClubConditionPacket"))
					{
						using (OutPacket outPacket14 = new OutPacket("PrCheckCreateClubConditionPacket"))
						{
							outPacket14.WriteInt(3);
							Parent.Client.Send(outPacket14);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqClubChannelSwitch"))
					{
						using (OutPacket outPacket15 = new OutPacket("PrInitClubPacket"))
						{
							outPacket15.WriteInt();
							Parent.Client.Send(outPacket15);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqInitClubInfoPacket"))
					{
						using (OutPacket outPacket16 = new OutPacket("PrInitClubInfoPacket"))
						{
							outPacket16.WriteHexString("00 00 00 00 00 00 00 00 00 00 E3 C3 78 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 FF FF 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 80 02 B8 16 00 00 00 00");
							Parent.Client.Send(outPacket16);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSearchClubListPacket"))
					{
						using (OutPacket outPacket17 = new OutPacket("PrSearchClubListPacket"))
						{
							outPacket17.WriteHexString("0C 00 00 00 28 00 00 00 09 00 00 00 4C 00 61 00 73 00 74 00 44 00 61 00 6E 00 63 00 65 00 05 96 00 00 00 FF FF FF FF 00 00 00 00 00 09 3D 00 9E 01 00 00 09 00 00 00 07 00 00 00 73 00 4C C7 45 C5 00 B3 A5 C7 E0 AD 73 00 5E 00 00 00 D0 A9 B1 4E 0F 00 00 00 94 CD B5 C5 8D C1 3C C7 5C B8 2E 00 2E 00 20 00 54 00 68 00 65 00 20 00 45 00 6E 00 64 00 01 05 00 00 00 00 64 01 00 00 03 00 00 00 6C C3 E4 C0 78 C7 05 96 00 00 00 FF FF FF FF 00 00 00 00 00 09 3D 00 8B 01 00 00 00 00 00 00 0A 00 00 00 53 00 75 00 6E 00 53 00 68 00 69 00 6E 00 65 00 FC BB 74 C7 58 00 00 00 D0 A9 B1 4E 47 00 00 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 22 00 44 C5 74 C7 5C D1 20 00 4E 00 4F 00 2E 00 31 00 22 00 20 00 38 AE DC B4 00 AC 20 00 18 B4 94 B2 A0 B0 4C AE C0 C9 20 00 38 AE D0 C6 20 00 A8 BA D1 C9 69 D5 C8 B2 E4 B2 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 00 AC 85 C7 38 BB 58 C7 20 00 3A 00 20 00 5C D1 04 C8 28 00 1C C2 29 BC 29 00 00 05 00 00 00 00 A9 02 00 00 06 00 00 00 6D AD 00 AC 00 B3 5C D4 38 AE DC B4 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 F6 00 00 00 09 00 00 00 06 00 00 00 6D AD 00 AC 00 B3 5C D4 04 C8 24 C1 2D 00 00 00 D0 A9 B1 4E 46 00 00 00 6D AD 00 AC 00 B3 5C D4 38 AE DC B4 20 00 B5 D1 69 D5 20 00 00 D3 04 C8 20 C7 00 C8 20 00 A8 BA D1 C9 69 D5 C8 B2 E4 B2 2E 00 20 00 31 00 36 00 38 C1 20 00 74 C7 C1 C0 20 00 1B BC B5 C2 C8 B2 E4 B2 20 00 20 00 00 AD EC C2 88 C7 94 B2 20 00 84 BD E4 B4 40 C7 20 00 6D AD 00 AC 00 B3 5C D4 38 AE DC B4 20 00 4C D1 A4 C2 B8 D2 20 00 24 C6 08 D5 A1 D1 29 BC 20 00 38 BB 58 C7 FC C8 DC C2 74 BA 20 00 29 B4 C8 B2 E4 B2 2E 00 00 04 00 00 00 00 4C 03 00 00 07 00 00 00 54 00 43 00 43 00 73 00 74 00 61 00 72 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 8F 01 00 00 09 00 00 00 08 00 00 00 54 00 63 00 63 00 6C 00 6C 00 A4 C2 A4 C2 D8 B2 71 00 00 00 D0 A9 B1 4E 95 00 00 00 00 AC 85 C7 20 00 70 C8 74 AC 3A 00 75 D5 58 00 20 00 28 00 31 00 29 00 44 BE E4 B9 20 00 14 BC 5C B8 20 00 F7 CE 20 00 69 D5 C8 B2 E4 B2 2E 00 28 00 32 00 29 00 E4 B9 FC C8 31 00 30 00 30 00 30 00 43 00 53 00 74 C7 C1 C0 20 00 A8 BA 3C C7 E4 C2 18 C2 88 C7 94 B2 84 BD 28 00 51 00 51 00 20 00 67 00 72 00 6F 00 75 00 70 00 20 00 69 00 73 00 20 00 38 00 34 00 33 00 33 00 38 00 36 00 31 00 31 00 29 00 2E 00 5C D5 6D AD 20 00 2D 00 20 00 11 C9 6D AD 20 00 F0 C5 69 D5 38 AE DC B4 20 00 53 00 49 00 4E 00 43 00 45 00 20 00 32 00 30 00 31 00 39 00 2F 00 30 00 31 00 2F 00 30 00 39 00 2E 00 74 CE 74 CE 24 C6 A1 D1 20 00 00 AC 85 C7 38 BB 58 C7 20 00 74 D0 D0 C6 A8 BA D1 C9 78 00 20 00 3A 00 20 00 68 00 74 00 74 00 70 00 73 00 3A 00 2F 00 2F 00 6F 00 70 00 65 00 6E 00 2E 00 6B 00 61 00 6B 00 61 00 6F 00 2E 00 63 00 6F 00 6D 00 2F 00 6F 00 2F 00 67 00 49 00 6C 00 52 00 62 00 31 00 52 00 63 00 00 04 00 00 00 00 B4 03 00 00 09 00 00 00 52 00 65 00 43 00 69 00 70 00 65 00 46 00 61 00 6D 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 42 01 00 00 09 00 00 00 06 00 00 00 08 B8 DC C2 3C D5 E5 B0 74 C7 79 00 1C 00 00 00 D0 A9 B1 4E 8C 00 00 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 53 00 69 00 6E 00 63 00 65 00 20 00 32 00 30 00 31 00 30 00 2E 00 30 00 37 00 2E 00 33 00 30 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 00 AC 85 C7 38 BB 58 C7 20 00 3A 00 20 00 68 00 74 00 74 00 70 00 73 00 3A 00 2F 00 2F 00 6F 00 70 00 65 00 6E 00 2E 00 6B 00 61 00 6B 00 61 00 6F 00 2E 00 63 00 6F 00 6D 00 2F 00 6F 00 2F 00 67 00 71 00 30 00 52 00 34 00 47 00 63 00 62 00 00 04 00 00 00 00 CB 03 00 00 10 00 00 00 50 00 72 00 6F 00 46 00 65 00 73 00 73 00 69 00 6F 00 6E 00 61 00 6C 00 54 00 65 00 61 00 6D 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 4C 01 00 00 09 00 00 00 0C 00 00 00 50 00 6C 00 61 00 79 00 54 00 68 00 65 00 42 00 61 00 7A 00 7A 00 69 00 53 00 00 00 D0 A9 B1 4E 8B 00 00 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 4B 00 61 00 72 00 74 00 52 00 69 00 64 00 65 00 72 00 20 00 4E 00 6F 00 2E 00 31 00 20 00 50 00 72 00 6F 00 46 00 65 00 73 00 73 00 69 00 6F 00 6E 00 61 00 6C 00 20 00 50 00 6C 00 61 00 79 00 65 00 72 00 20 00 54 00 65 00 61 00 6D 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 22 00 20 00 78 C7 DD C0 40 C7 20 00 78 B1 25 B8 FC AC 20 00 B4 C6 2C 00 20 00 F8 AD AC B9 E0 AC 20 00 FF BB 4C C7 74 C7 E4 B2 2E 00 20 00 F8 AD EC B7 C0 BB 5C B8 20 00 98 B0 94 B2 20 00 FF BB 94 B2 E4 B2 2E 00 20 00 22 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 2D 00 20 00 42 00 4A 00 40 AE DD D0 58 D6 20 00 2D 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 28 00 74 D0 FD B7 20 00 A8 BA D1 C9 20 00 11 C9 E8 B2 29 00 00 04 00 00 00 00 06 04 00 00 04 00 00 00 6F 00 6E 00 6C 00 79 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 45 01 00 00 00 00 00 00 05 00 00 00 AC B9 84 BC A4 C2 04 D6 38 D6 41 00 00 00 D0 A9 B1 4E 13 00 00 00 43 00 79 00 61 00 20 00 66 00 65 00 6C 00 6C 00 61 00 73 00 20 00 67 00 74 00 67 00 20 00 73 00 6F 00 6F 00 6E 00 00 04 00 00 00 00 25 04 00 00 05 00 00 00 46 00 72 00 61 00 6E 00 7A 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 10 01 00 00 00 00 00 00 02 00 00 00 2C C8 38 D6 27 00 00 00 D0 A9 B1 4E 30 00 00 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 47 00 6F 00 6F 00 64 00 20 00 62 00 79 00 65 00 20 00 4B 00 61 00 72 00 74 00 00 04 00 00 00 00 95 05 00 00 0B 00 00 00 43 00 6F 00 6E 00 71 00 75 00 65 00 72 00 54 00 65 00 61 00 6D 00 05 96 00 00 00 FF FF FF FF 00 00 00 00 00 09 3D 00 19 01 00 00 00 00 00 00 0C 00 00 00 43 00 6F 00 6E 00 71 00 75 00 65 00 72 00 42 00 4C 00 61 00 63 00 6B 00 2E 00 00 00 D0 A9 B1 4E 0D 00 00 00 47 00 6F 00 6F 00 64 00 20 00 62 00 79 00 65 00 20 00 4B 00 61 00 72 00 74 00 00 05 00 00 00 00 B0 05 00 00 02 00 00 00 EC B2 C1 B9 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 8F 01 00 00 00 00 00 00 04 00 00 00 A4 D0 E8 C3 30 B5 D5 D0 04 00 00 00 D0 A9 B1 4E 01 00 00 00 20 00 00 04 00 00 00 00 E9 05 00 00 09 00 00 00 41 00 70 00 70 00 6C 00 65 00 54 00 65 00 61 00 6D 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 75 00 00 00 00 00 00 00 0B 00 00 00 41 00 70 00 70 00 6C 00 65 00 4C 00 65 00 61 00 64 00 65 00 72 00 46 00 00 00 D0 A9 B1 4E 8F 00 00 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 41 00 70 00 70 00 6C 00 65 00 54 00 65 00 61 00 6D 00 20 00 32 00 30 00 31 00 32 00 2D 00 30 00 32 00 2D 00 30 00 35 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 5E 00 20 00 32 00 30 00 2D 00 31 00 32 00 2D 00 31 00 33 00 20 00 5E 00 20 00 20 00 20 00 20 00 00 AC 85 C7 20 00 3A 00 20 00 60 C5 0C D5 4C D1 A4 C2 B8 D2 29 BC 20 00 26 00 20 00 74 CE 74 CE 24 C6 44 CC 10 B1 20 00 41 00 70 00 70 00 6C 00 65 00 54 00 65 00 61 00 6D 00 20 00 20 00 20 00 20 00 5E 00 20 00 35 00 19 B8 20 00 EC B2 31 C1 21 00 20 00 5E 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 21 00 21 00 21 00 21 00 20 00 20 00 34 00 DC C2 8C C9 20 00 31 00 04 C7 20 00 10 AC AC C0 69 D5 C8 B2 E4 B2 20 00 20 00 21 00 21 00 21 00 21 00 20 00 00 04 00 00 00 00 30 06 00 00 06 00 00 00 52 00 55 00 4E 00 6E 00 47 00 4F 00 05 78 00 00 00 FF FF FF FF 00 00 00 00 C0 C6 2D 00 66 01 00 00 09 00 00 00 06 00 00 00 52 00 6E 00 47 00 C4 C9 4F 00 6C 00 39 00 00 00 D0 A9 B1 4E 68 00 00 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 68 00 74 00 74 00 70 00 73 00 3A 00 2F 00 2F 00 63 00 61 00 66 00 65 00 2E 00 6E 00 61 00 76 00 65 00 72 00 2E 00 63 00 6F 00 6D 00 2F 00 73 00 70 00 65 00 65 00 64 00 72 00 6E 00 67 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 32 00 30 00 30 00 35 00 2E 00 30 00 35 00 2E 00 32 00 32 00 00 04 00 00 00 00 01");
							Parent.Client.Send(outPacket17);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetClubListCountPacket"))
					{
						using (OutPacket outPacket18 = new OutPacket("PrGetClubListCountPacket"))
						{
							outPacket18.WriteHexString("7F F7 00 00 01 00 00 00");
							Parent.Client.Send(outPacket18);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetClubWaitingCrewCountPacket"))
					{
						using (OutPacket outPacket19 = new OutPacket("PrGetClubWaitingCrewCountPacket"))
						{
							outPacket19.WriteHexString("32 00 00 00 32 00 00 00");
							Parent.Client.Send(outPacket19);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqLotteryPacket"))
					{
						iPacket.ReadShort();
						iPacket.ReadByte();
						iPacket.ReadInt();
						GameSupport.SpRpLotteryPacket(Parent);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqRiderSchoolExpiredCheck"))
					{
						using (OutPacket outPacket20 = new OutPacket("PrRiderSchoolExpiredCheck"))
						{
							outPacket20.WriteBytes(default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte));
							Parent.Client.Send(outPacket20);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqRequestExtradata"))
					{
						using (OutPacket outPacket21 = new OutPacket("PrRequestExtradata"))
						{
							outPacket21.WriteShort(0);
							Parent.Client.Send(outPacket21);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("ChRequestChStaticRequestPacket"))
					{
						GameSupport.ChRequestChStaticReplyPacket(Parent);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("ChRequestChDynamicRequestPacket"))
					{
						using OutPacket dynamicReply = new OutPacket("ChRequestChDynamicReplyPacket");
						Parent.Client.Send(dynamicReply);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqDynamicCommand"))
					{
						GameSupport.PrDynamicCommand(Parent);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqPubCommandPacket"))
					{
						using (OutPacket outPacket22 = new OutPacket("PrPubCommandPacket"))
						{
							outPacket22.WriteInt();
							outPacket22.WriteInt();
							Parent.Client.Send(outPacket22);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqWebEventCompleteCheckPacket"))
					{
						using (OutPacket pPacket = new OutPacket("PrWebEventCompleteCheckPacket"))
						{
							Parent.Client.Send(pPacket);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqFavoriteTrackMapGet"))
					{
						using (OutPacket outPacket23 = new OutPacket("PrFavoriteTrackMapGet"))
						{
							outPacket23.WriteInt();
							Parent.Client.Send(outPacket23);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetFavoriteChannel"))
					{
						using (OutPacket outPacket24 = new OutPacket("PrGetFavoriteChannel"))
						{
							outPacket24.WriteHexString("02 00 00 00 00 00 00 00 00 00 01 00");
							Parent.Client.Send(outPacket24);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqKartPassInitPacket"))
					{
						using (OutPacket outPacket25 = new OutPacket("PrKartPassInitPacket"))
						{
							outPacket25.WriteInt(3);
							outPacket25.WriteInt();
							Parent.Client.Send(outPacket25);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqGetCashInventoryPacket"))
					{
						using (OutPacket outPacket26 = new OutPacket("SpRpGetCashInventoryPacket"))
						{
							outPacket26.WriteInt();
							outPacket26.WriteByte(0);
							Parent.Client.Send(outPacket26);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqRemainCashPacket"))
					{
						using (OutPacket outPacket27 = new OutPacket("SpRpRemainCashPacket"))
						{
							outPacket27.WriteUInt();
							outPacket27.WriteUInt();
							Parent.Client.Send(outPacket27);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqRemainTcCashPacket"))
					{
						using (OutPacket outPacket28 = new OutPacket("SpRpRemainTcCashPacket"))
						{
							outPacket28.WriteUInt(99u);
							outPacket28.WriteUInt();
							Parent.Client.Send(outPacket28);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpReqNormalShopBuyItemPacket") || num == Adler32Helper.GenerateAdler32_ASCII("SpReqItemPresetShopBuyItemPacket"))
					{
						using (OutPacket outPacket29 = new OutPacket("SpRepBuyItemPacket"))
						{
							outPacket29.WriteHexString("01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
							Parent.Client.Send(outPacket29);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetCurrentRid"))
					{
						using (OutPacket outPacket30 = new OutPacket("PrGetCurrentRid"))
						{
							outPacket30.WriteInt();
							Parent.Client.Send(outPacket30);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetMyCouponList"))
					{
						using (OutPacket outPacket31 = new OutPacket("PrGetMyCouponList"))
						{
							outPacket31.WriteInt();
							Parent.Client.Send(outPacket31);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqDisassembleFeeInfo"))
					{
						using (OutPacket outPacket32 = new OutPacket("PrDisassembleFeeInfo"))
						{
							outPacket32.WriteHexString("00 00 00 00 06 00 00 00 00 00 E8 03 01 00 F4 01 00 00 E8 03 01 00 F4 01 00 00 E8 03 01 00 F4 01");
							Parent.Client.Send(outPacket32);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqRequestExchangeInitPacket"))
					{
						using (OutPacket outPacket33 = new OutPacket("PrRequestExchangeInitPacket"))
						{
							outPacket33.WriteHexString("01 03 00 00 00 F4 01 00 00 01 00 00 00 02 00 00 00 03 00 00 00");
							Parent.Client.Send(outPacket33);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqRequestPeriodExchangeInitPacket"))
					{
						using (OutPacket outPacket34 = new OutPacket("PrRequestPeriodExchangeInitPacket"))
						{
							outPacket34.WriteBytes(default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte), default(byte));
							Parent.Client.Send(outPacket34);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqEnterRewardBoxStage"))
					{
						using (OutPacket outPacket35 = new OutPacket("SpRpEnterRewardBoxStage"))
						{
							outPacket35.WriteInt();
							outPacket35.WriteInt();
							outPacket35.WriteByte(1);
							Parent.Client.Send(outPacket35);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqExitRewardBoxStage"))
					{
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqGetGiftListIncomingPacket"))
					{
						using (OutPacket outPacket36 = new OutPacket("SpRpGetGiftListIncomingPacket"))
						{
							outPacket36.WriteInt();
							outPacket36.WriteInt();
							outPacket36.WriteInt();
							Parent.Client.Send(outPacket36);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqGetGiftListReceivedPacket"))
					{
						using (OutPacket outPacket37 = new OutPacket("SpRpGetGiftListReceivedPacket"))
						{
							outPacket37.WriteInt();
							Parent.Client.Send(outPacket37);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetCompetitiveRankInfo"))
					{
						using (OutPacket outPacket38 = new OutPacket("PrGetCompetitiveRankInfo"))
						{
							outPacket38.WriteHexString("01 00 00 00 00 FF 00 00 00 00 00 00 00 00 00 00 00 00");
							Parent.Client.Send(outPacket38);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetCompetitiveSlotInfo"))
					{
						using (OutPacket outPacket39 = new OutPacket("PrGetCompetitiveSlotInfo"))
						{
							outPacket39.WriteInt();
							Parent.Client.Send(outPacket39);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetCompetitiveCount"))
					{
						using (OutPacket outPacket40 = new OutPacket("PrGetCompetitiveCount"))
						{
							outPacket40.WriteHexString("B3 02 52 1B 00 00 B4 02 54 1B 00 00 B9 02 82 1B 00 00");
							Parent.Client.Send(outPacket40);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSearchCompetitiveRankPacket"))
					{
						using (OutPacket outPacket41 = new OutPacket("PrSearchCompetitiveRankPacket"))
						{
							outPacket41.WriteInt();
							outPacket41.WriteInt();
							Parent.Client.Send(outPacket41);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetCompetitivePreRankPacket"))
					{
						using (OutPacket outPacket42 = new OutPacket("PrGetCompetitivePreRankPacket"))
						{
							outPacket42.WriteInt();
							outPacket42.WriteInt();
							Parent.Client.Send(outPacket42);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("LoRqDeleteItemPacket"))
					{
						using (OutPacket pPacket2 = new OutPacket("LoRpDeleteItemPacket"))
						{
							Parent.Client.Send(pPacket2);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqQueryCoupon"))
					{
						using (OutPacket outPacket43 = new OutPacket("SpRpQueryCoupon"))
						{
							outPacket43.WriteInt(1);
							outPacket43.WriteInt();
							Parent.Client.Send(outPacket43);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqShopCashPage"))
					{
						using (OutPacket outPacket44 = new OutPacket("PrShopCashPage"))
						{
							outPacket44.WriteString("https://ripay.nexon.com/Payment/Index");
							Parent.Client.Send(outPacket44);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqBingoSync"))
					{
						using (OutPacket outPacket45 = new OutPacket("PrBingoSync"))
						{
							outPacket45.WriteByte(0);
							outPacket45.WriteUShort(0);
							outPacket45.WriteUShort(0);
							for (int l = 0; l < 15; l++)
							{
								outPacket45.WriteByte(0);
							}
							Parent.Client.Send(outPacket45);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqEnterKartPassPacket"))
					{
						using (OutPacket outPacket46 = new OutPacket("PrEnterKartPassPacket"))
						{
							outPacket46.WriteHexString("00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00");
							Parent.Client.Send(outPacket46);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqKartPassPlayTimePacket"))
					{
						using (OutPacket outPacket47 = new OutPacket("PrKartPassPlayTimePacket"))
						{
							outPacket47.WriteInt();
							Parent.Client.Send(outPacket47);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqKartPassRewardPacket"))
					{
						using (OutPacket outPacket48 = new OutPacket("PrKartPassRewardPacket"))
						{
							outPacket48.WriteHexString("00 00 00 00 00 00 00 00 01 00 00 00 01 00 00 00");
							Parent.Client.Send(outPacket48);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqEnterSeasonPassPacket"))
					{
						byte value6 = iPacket.ReadByte();
						using OutPacket outPacket49 = new OutPacket("PrEnterSeasonPassPacket");
						outPacket49.WriteInt();
						outPacket49.WriteByte(value6);
						outPacket49.WriteInt();
						Parent.Client.Send(outPacket49);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSeasonPassRewardPacket"))
					{
						using (OutPacket outPacket50 = new OutPacket("PrSeasonPassRewardPacket"))
						{
							outPacket50.WriteHexString("00 00 00 00 00 00 00 00 01 00 00 00 01 00 00 00");
							Parent.Client.Send(outPacket50);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqCheckPassword"))
					{
						using (OutPacket outPacket51 = new OutPacket("PrCheckPassword"))
						{
							outPacket51.WriteInt();
							Parent.Client.Send(outPacket51);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqUnLockedItem"))
					{
						int num6 = iPacket.ReadInt();
						iPacket.ReadInt();
						for (int m = 0; m < num6; m++)
						{
							iPacket.ReadString();
						}
						byte value7 = iPacket.ReadByte();
						using OutPacket outPacket52 = new OutPacket("PrUnLockedItem");
						outPacket52.WriteByte(value7);
						Parent.Client.Send(outPacket52);
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqFavoriteItemUpdate"))
					{
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqLockedItemGet"))
					{
						using (OutPacket outPacket53 = new OutPacket("PrLockedItemGet"))
						{
							outPacket53.WriteInt();
							Parent.Client.Send(outPacket53);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqLockedItemUpdate"))
					{
						return;
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSimGameSimpleInfoAndRankPacket"))
					{
						using (OutPacket outPacket54 = new OutPacket("PrSimGameSimpleInfoAndRankPacket"))
						{
							outPacket54.WriteInt();
							outPacket54.WriteInt();
							outPacket54.WriteInt();
							outPacket54.WriteInt();
							Parent.Client.Send(outPacket54);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSimGameEnterPacket"))
					{
						using (OutPacket outPacket55 = new OutPacket("PrSimGameEnterPacket"))
						{
							outPacket55.WriteInt();
							Parent.Client.Send(outPacket55);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("SpRqTimeShopPacket"))
					{
						using (OutPacket outPacket58 = new OutPacket("SpRpTimeShopPacket"))
						{
							outPacket58.WriteHexString("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 FF FF FF FF FF FF FF FF FF FF FF FF FF 47 00 00 00 00 00 47 00 00 00 00 00 00 00 02 00 00 00");
							Parent.Client.Send(outPacket58);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqSecretShopEnterPacket"))
					{
						using (OutPacket outPacket59 = new OutPacket("PrSecretShopEnterPacket"))
						{
							outPacket59.WriteHexString("00 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
							Parent.Client.Send(outPacket59);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqEnterUpgradeGearPacket"))
					{
						using (OutPacket outPacket60 = new OutPacket("PrEnterUpgradeGearPacket"))
						{
							outPacket60.WriteHexString("05 00 00 00 03 00 00 00 05 00 00 00 00 00 00 00 00 00 00 00");
							Parent.Client.Send(outPacket60);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqBlockCatchEnterPacket"))
					{
						using (OutPacket outPacket61 = new OutPacket("PrBlockCatchEnterPacket"))
						{
							outPacket61.WriteInt();
							outPacket61.WriteInt();
							outPacket61.WriteInt();
							outPacket61.WriteInt(3);
							outPacket61.WriteInt(3);
							outPacket61.WriteInt();
							outPacket61.WriteInt(5);
							outPacket61.WriteInt(1);
							outPacket61.WriteInt(7);
							outPacket61.WriteInt(2);
							outPacket61.WriteInt(600);
							outPacket61.WriteInt(300);
							outPacket61.WriteInt(200);
							outPacket61.WriteInt(100);
							outPacket61.WriteInt(-100);
							Parent.Client.Send(outPacket61);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("RqEnterFishingStagePacket"))
					{
						using (OutPacket outPacket62 = new OutPacket("RpEnterFishingStagePacket"))
						{
							outPacket62.WriteByte(0);
							Parent.Client.Send(outPacket62);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqPcCafeShowcaseCoupon"))
					{
						using (OutPacket outPacket63 = new OutPacket("PrPcCafeShowcaseCoupon"))
						{
							outPacket63.WriteInt();
							outPacket63.WriteInt();
							Parent.Client.Send(outPacket63);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqGetRiderCareerSummary"))
					{
						using (OutPacket outPacket64 = new OutPacket("PrGetRiderCareerSummary"))
						{
							outPacket64.WriteInt();
							outPacket64.WriteInt();
							Parent.Client.Send(outPacket64);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("checkSecondAuthenPacket"))
					{
						using (OutPacket outPacket65 = new OutPacket("checkSecondAuthenPacket"))
						{
							outPacket65.WriteInt(2);
							outPacket65.WriteInt();
							Parent.Client.Send(outPacket65);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqServerTime"))
					{
						using (OutPacket outPacket66 = new OutPacket("PrServerTime"))
						{
							outPacket66.WriteHexString(Program.DataTime);
							Parent.Client.Send(outPacket66);
							return;
						}
					}
					if (num == Adler32Helper.GenerateAdler32_ASCII("PqLogin"))
					{
						try
						{
							string sourceUsername = LegacyLoginProfileReader.ReadUsername(iPacket);
							string assignedIdentity = RouterListener.AssignLoginUsername(Parent, sourceUsername);
							LegacyPacketTrace.LogEvent(
								$"[2005 LOGIN] AccountDataProfile username='{sourceUsername}', " +
								$"identity='{assignedIdentity}', userNo={Parent.Profile.UserNo}.");
						}
						catch (Exception ex) when (ex is FormatException || ex is PacketReadException)
						{
							LegacyPacketTrace.LogEvent(
								$"[2005 LOGIN] Could not parse AccountDataProfile; " +
								$"using identity='{Parent.Profile.UserId}', userNo={Parent.Profile.UserNo}: {ex.Message}");
						}

						GameDataReset.DataReset(Parent);
						return;
					}
				}
			}
		}
	}
}
