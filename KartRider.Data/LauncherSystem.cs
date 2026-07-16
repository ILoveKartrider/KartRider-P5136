using KartLibrary.File;
using KartRider.Common.Data;
using KartRider.IO.Packet;
using Profile;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KartRider
{
	public static class LauncherSystem
	{
		public static void MessageBoxType1()
		{
			MessageBox.Show("카트라이더가 이미 실행 중입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		public static void MessageBoxType2()
		{
			MessageBox.Show(
				"서버 포트를 열 수 없습니다.\n다른 서버가 실행 중인지 확인하거나 서버 설정에서 포트를 변경하세요.",
				"서버 시작 실패",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
		}

		public static void MessageBoxType3(string RootDirectory)
		{
			DialogResult result = MessageBox.Show(
				"게임 파일을 찾을 수 없습니다.\n확인을 누르면 현재 폴더에 게임 파일을 내려받습니다. 취소를 누르면 종료합니다.",
				"작업 확인",
				MessageBoxButtons.OKCancel,
				MessageBoxIcon.Question);
			
			if (result == DialogResult.OK)
			{
				// 使用本程序目录作为游戏目录进行下载
				string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(RootDirectory))
				{
                    CheckGame(currentDirectory);
                }
				else
				{
                    CheckGame(RootDirectory);
                }
            }
			else
			{
				Environment.Exit(1);
			}
		}

		public static async Task CheckGameAsync(string kartRiderDirectory, string updateUrl = "")
		{
			// 强制显示终端窗口
            bool wasVisible = Program.isVisible;
            if (!Program.isVisible)
            {
                Program.isVisible = true;
                Program.ShowWindow(Program.consoleHandle, Program.SW_SHOW);
            }

			try
			{
				string filePath = JsonHelper.GetFilePath();
				if (string.IsNullOrEmpty(updateUrl))
				{
					var data = await Update.GetUpdateAsync().ConfigureAwait(false);
					if (data != null)
					{
						updateUrl = data.update_prefix;
					}
					else
					{
						MessageBox.Show("게임 버전 정보를 가져오지 못했습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
				await new PatchManager().StartPatchAsync(updateUrl, kartRiderDirectory).ConfigureAwait(false);

				PINFile val = new PINFile(Path.GetFullPath(Path.Combine(kartRiderDirectory, @"KartRider.pin")));
				ProfileService.SettingConfig.ClientVersion = val.Header.MinorVersion;
				ProfileService.SettingConfig.LocaleID = val.Header.LocaleID;
				ProfileService.SettingConfig.nClientLoc = val.Header.Unk2;
				ProfileService.SaveSettings();
				// 更新完成后，根据设置恢复终端显示状态
				if (!wasVisible && !ProfileService.SettingConfig.Console)
				{
					Program.isVisible = false;
					Program.ShowWindow(Program.consoleHandle, Program.SW_HIDE);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"업데이트 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		public static void CheckGame(string kartRiderDirectory, string updateUrl = "", bool single = true)
		{
			Exception capturedException = null;

			// 在新线程中运行异步操作，避免阻塞 UI 线程
			var thread = new Thread(() =>
			{
				try
				{
					Console.WriteLine("[CheckGame] 업데이트 스레드 시작");
					CheckGameAsync(kartRiderDirectory, updateUrl).GetAwaiter().GetResult();
					Console.WriteLine("[CheckGame] 업데이트 스레드 완료");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[CheckGame] 업데이트 스레드 예외: {ex.GetType().Name}: {ex.Message}");
					Console.WriteLine($"[CheckGame] 스택 추적: {ex.StackTrace}");
					capturedException = ex;
				}
			})
			{
				IsBackground = false, // 改为前台线程，确保异常能被捕获
				Name = "GameUpdateThread"
			};

			Console.WriteLine("[CheckGame] 스레드 시작");
			thread.Start();
			Console.WriteLine("[CheckGame] 스레드 완료 대기 중...");
			thread.Join(); // 等待线程完成
			Console.WriteLine("[CheckGame] 스레드 종료");

			if (capturedException != null)
			{
				MessageBox.Show($"업데이트 중 오류가 발생했습니다: {capturedException.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else
			{
				if (single)
				{
					PatchManager.RhoDump(kartRiderDirectory);
				}
			}
		}
	}
}
