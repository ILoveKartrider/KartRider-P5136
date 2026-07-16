using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using KartRider.Compatibility;
using LoggerLibrary;
using Profile;

namespace KartRider
{
    public partial class Launcher : Form
    {
        private bool serverTransitionInProgress;

        public string kartRiderDirectory;

        public Launcher()
        {
            InitializeComponent();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            ClientServerRuntime.Stop();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            LauncherVersionValue.Text = CompileTime.Time;
            Console.WriteLine($"서버 런처 빌드: {CompileTime.Time}");
            Console.WriteLine($"서버 데이터 경로: {kartRiderDirectory}");

            if (Directory.Exists(FileName.ProfileDir))
            {
                foreach (string userPath in Directory.GetDirectories(FileName.ProfileDir))
                {
                    ClientManager.GetUserNO(Path.GetFileName(userPath));
                }
            }

            RefreshServerStatus();
        }

        public void RefreshServerStatus()
        {
            bool isRunning = ClientServerRuntime.IsRunning;
            ServerStatusValue.Text = isRunning ? "실행 중" : "중지됨";
            ServerStatusValue.ForeColor = isRunning ? Color.ForestGreen : Color.Firebrick;
            ServerToggleButton.Text = isRunning ? "서버 중지" : "서버 시작";
            ServerToggleButton.Enabled = !serverTransitionInProgress;
            ServerBuildValue.Text = GetBuildDisplayName(ClientBuildProfiles.Active.Build);
            ServerEndpointValue.Text = GetConfiguredEndpoint();
            ConsoleToggleButton.Text = Program.isVisible ? "콘솔 숨기기" : "콘솔 열기";
            RefreshWindowTitle();
        }

        public void RefreshWindowTitle()
        {
            Text = Program.SpeedPatch
                ? "카트라이더 서버 런처 (속도 패치)"
                : "카트라이더 서버 런처";
        }

        private async void ServerToggleButton_Click(object sender, EventArgs e)
        {
            if (serverTransitionInProgress)
            {
                return;
            }

            bool stopServer = ClientServerRuntime.IsRunning;
            serverTransitionInProgress = true;
            ServerToggleButton.Enabled = false;
            ServerToggleButton.Text = stopServer ? "서버 중지 중..." : "서버 시작 중...";

            try
            {
                await Task.Run(() =>
                {
                    if (stopServer)
                    {
                        ClientServerRuntime.Stop();
                    }
                    else
                    {
                        ClientServerRuntime.Start(kartRiderDirectory);
                    }
                });

                Console.WriteLine(stopServer ? "서버를 중지했습니다." : "서버를 시작했습니다.");
            }
            catch (Exception ex)
            {
                string action = stopServer ? "중지" : "시작";
                Console.WriteLine($"서버 {action} 실패: {ex.Message}");
                MessageBox.Show(
                    $"서버를 {action}하지 못했습니다.\n{ex.Message}",
                    "서버 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                serverTransitionInProgress = false;
                RefreshServerStatus();
            }
        }

        private void SettingButton_Click(object sender, EventArgs e)
        {
            Program.SettingDlg = new Setting();
            Program.SettingDlg.ShowDialog(this);
            RefreshServerStatus();
        }

        private void ConsoleToggleButton_Click(object sender, EventArgs e)
        {
            Program.isVisible = !Program.isVisible;
            Program.ShowWindow(
                Program.consoleHandle,
                Program.isVisible ? Program.SW_SHOW : Program.SW_HIDE);
            ProfileService.SettingConfig.Console = Program.isVisible;
            ProfileService.SaveSettings();
            RefreshServerStatus();
        }

        private void SaveLogButton_Click(object sender, EventArgs e)
        {
            CachedConsoleWriter.SaveToFile();
            CachedConsoleWriter.cachedWriter.ClearCache();
            Console.WriteLine("콘솔 로그를 저장했습니다.");
        }

        private static string GetBuildDisplayName(ClientBuild build)
        {
            return build switch
            {
                ClientBuild.Korean20051214 => "한국 2005-12-14 (P236)",
                ClientBuild.Korean5136 => "한국 5136",
                _ => "현대 클라이언트"
            };
        }

        private static string GetConfiguredEndpoint()
        {
            try
            {
                ushort port = ClientBuildProfiles.Active.Ports.ResolveLoginTcpPort(
                    ProfileService.SettingConfig.ServerPort);
                return $"{ProfileService.SettingConfig.ServerIP}:{port}";
            }
            catch
            {
                return $"{ProfileService.SettingConfig.ServerIP}:{ProfileService.SettingConfig.ServerPort}";
            }
        }
    }
}
