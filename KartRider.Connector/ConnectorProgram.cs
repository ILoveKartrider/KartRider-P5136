using KartRider.Compatibility;
using Profile;
using System;
using System.IO;
using System.Windows.Forms;

namespace KartRider.Connector
{
    internal static class ConnectorProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                string gameDirectory = Path.GetFullPath(AppContext.BaseDirectory);
                ClientBuildProfile profile = ClientBuildDetector.DetectAndActivate(gameDirectory);

                if (!File.Exists(Path.Combine(gameDirectory, "KartRider.exe")))
                {
                    MessageBox.Show(
                        "접속기를 KartRider.exe가 있는 P5136 게임 폴더에 놓으세요.",
                        "게임 파일 없음",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (profile.Build != ClientBuild.Korean5136)
                {
                    MessageBox.Show(
                        $"P5136 클라이언트를 확인할 수 없습니다.\n감지 결과: {profile.DisplayName}",
                        "지원하지 않는 클라이언트",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                ProfileService.LoadSettings();
                Application.Run(new ConnectorForm(gameDirectory, profile));
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"접속기를 시작하지 못했습니다.\n{exception.Message}",
                    "접속기 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
