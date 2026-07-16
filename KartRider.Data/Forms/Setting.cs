using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using ExcData;
using Profile;
using KartRider.Compatibility;

namespace KartRider
{
    public partial class Setting : Form
    {
        private static readonly IReadOnlyDictionary<string, string> KoreanDisplayNames = new Dictionary<string, string>
        {
            ["国服"] = "중국 서버",
            ["国服复古"] = "중국 클래식",
            ["韩服复古"] = "한국 클래식",
            ["标准"] = "표준",
            ["慢速"] = "느림",
            ["普通"] = "보통",
            ["快速"] = "빠름",
            ["高速"] = "매우 빠름",
            ["新手"] = "초보",
            ["初级"] = "루키",
            ["Pro"] = "PRO",
            ["简单"] = "쉬움",
            ["困难"] = "어려움",
            ["地狱"] = "지옥"
        };

        public string[] AiSpeed = new string[] { "简单", "困难", "地狱" };

        private bool synchronizingSelections;

        public Setting()
        {
            InitializeComponent();
            Version_comboBox.Format += LocalizeComboBoxItem;
            Speed_comboBox.Format += LocalizeComboBoxItem;
            AiSpeed_comboBox.Format += LocalizeComboBoxItem;
            Version_comboBox.SelectedIndexChanged += Version_comboBox_SelectedIndexChanged;
            Speed_comboBox.SelectedIndexChanged += Speed_comboBox_SelectedIndexChanged;
            AiSpeed_comboBox.SelectedIndexChanged += AiSpeed_comboBox_SelectedIndexChanged;
        }

        private static void LocalizeComboBoxItem(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is string internalName && KoreanDisplayNames.TryGetValue(internalName, out string displayName))
            {
                e.Value = displayName;
            }
        }

        private static string ResolveInternalName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            foreach (KeyValuePair<string, string> pair in KoreanDisplayNames)
            {
                if (string.Equals(pair.Value, value, StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }

            return value;
        }

        private void PopulateSpeedOptions(string version)
        {
            Speed_comboBox.Items.Clear();
            if (!SpeedType.speedNames.TryGetValue(version, out Dictionary<string, byte> speedOptions))
            {
                return;
            }

            foreach (string key in speedOptions.Keys)
            {
                Speed_comboBox.Items.Add(key);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlayerName.Text))
            {
                MessageBox.Show("기본 계정 이름을 입력하세요.", "설정 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(ServerIP.Text))
            {
                MessageBox.Show("서버 IP를 입력하세요.", "설정 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            if (!IPAddress.TryParse(ServerIP.Text.Trim(), out IPAddress serverAddress) ||
                serverAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                MessageBox.Show(
                    "서버 수신 주소는 IPv4 형식이어야 합니다. 예: 127.0.0.1",
                    "설정 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            if (!ushort.TryParse(ServerPort.Text, out ushort serverPort) || serverPort == 0)
            {
                MessageBox.Show("포트는 1~65535 사이의 숫자여야 합니다.", "설정 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            bool serverWasRunning = ClientServerRuntime.IsRunning;
            bool serverConfigurationChanged =
                !string.Equals(serverAddress.ToString(), ProfileService.SettingConfig.ServerIP, StringComparison.Ordinal) ||
                serverPort != ProfileService.SettingConfig.ServerPort ||
                !string.Equals(PlayerName.Text, ProfileService.SettingConfig.Name, StringComparison.Ordinal);

            ProfileService.SettingConfig.Name = PlayerName.Text;
            ProfileService.SettingConfig.ServerIP = serverAddress.ToString();
            ProfileService.SettingConfig.ServerPort = serverPort;
            ProfileService.SettingConfig.SoloRank = SoloRank.Checked;
            ProfileService.SettingConfig.EnableMod = EnableMod.Checked;
            ProfileService.SaveSettings();

            if (serverConfigurationChanged && serverWasRunning)
            {
                try
                {
                    ClientServerRuntime.Stop();
                    ClientServerRuntime.Start(Program.LauncherDlg.kartRiderDirectory);
                    Console.WriteLine("변경된 설정으로 서버를 다시 시작했습니다.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"설정 적용 후 서버 재시작 실패: {ex.Message}");
                    MessageBox.Show(
                        $"설정은 저장했지만 서버를 다시 시작하지 못했습니다.\n{ex.Message}",
                        "서버 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            Program.LauncherDlg?.RefreshServerStatus();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            ProfileService.LoadSettings();
            bool settingsChanged = false;
            string configuredVersion = ResolveInternalName(ProfileService.SettingConfig.Version);
            string defaultVersion = ClientBuildProfiles.Active.IsLegacy ? "韩服复古" : "国服";
            if (!SpeedType.speedNames.ContainsKey(configuredVersion))
            {
                configuredVersion = defaultVersion;
            }
            if (!string.Equals(ProfileService.SettingConfig.Version, configuredVersion, StringComparison.Ordinal))
            {
                ProfileService.SettingConfig.Version = configuredVersion;
                settingsChanged = true;
            }

            Dictionary<string, byte> configuredSpeedOptions = SpeedType.speedNames[configuredVersion];
            string configuredSpeed = configuredSpeedOptions
                .FirstOrDefault(pair => pair.Value == ProfileService.SettingConfig.SpeedType).Key;
            if (configuredSpeed == null)
            {
                configuredSpeed = configuredSpeedOptions.Keys.First();
                ProfileService.SettingConfig.SpeedType = configuredSpeedOptions[configuredSpeed];
                settingsChanged = true;
            }

            string configuredAiSpeed = ResolveInternalName(ProfileService.SettingConfig.AiSpeedType);
            if (!AiSpeed.Contains(configuredAiSpeed))
            {
                configuredAiSpeed = AiSpeed[0];
            }
            if (!string.Equals(ProfileService.SettingConfig.AiSpeedType, configuredAiSpeed, StringComparison.Ordinal))
            {
                ProfileService.SettingConfig.AiSpeedType = configuredAiSpeed;
                settingsChanged = true;
            }

            synchronizingSelections = true;
            try
            {
                PlayerName.Text = ProfileService.SettingConfig.Name;
                ServerIP.Text = ProfileService.SettingConfig.ServerIP;
                ServerPort.Text = ProfileService.SettingConfig.ServerPort.ToString();
                SoloRank.Checked = ProfileService.SettingConfig.SoloRank;
                EnableMod.Checked = ProfileService.SettingConfig.EnableMod;

                Version_comboBox.Items.Clear();
                foreach (string key in SpeedType.speedNames.Keys)
                {
                    Version_comboBox.Items.Add(key);
                }
                Version_comboBox.SelectedItem = configuredVersion;

                PopulateSpeedOptions(configuredVersion);
                Speed_comboBox.SelectedItem = configuredSpeed;

                AiSpeed_comboBox.Items.Clear();
                foreach (string key in AiSpeed)
                {
                    AiSpeed_comboBox.Items.Add(key);
                }
                AiSpeed_comboBox.SelectedItem = configuredAiSpeed;

                PhysicsNote_label.Text = ClientBuildProfiles.Active.Build == ClientBuild.Korean20051214
                    ? "※ 물리 프리셋과 속도 등급은 2005 싱글플레이에 적용되지 않습니다."
                    : "※ 클라이언트 버전이 아니라 서버의 주행 물리 설정입니다.";
            }
            finally
            {
                synchronizingSelections = false;
            }

            if (settingsChanged)
            {
                ProfileService.SaveSettings();
            }
        }

        private void Version_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (synchronizingSelections)
            {
                return;
            }

            if (Version_comboBox.SelectedItem is string selectedVersion)
            {
                if (SpeedType.speedNames.TryGetValue(selectedVersion, out Dictionary<string, byte> speedOptions))
                {
                    synchronizingSelections = true;
                    try
                    {
                        ProfileService.SettingConfig.Version = selectedVersion;
                        PopulateSpeedOptions(selectedVersion);
                        if (Speed_comboBox.Items.Count > 0)
                        {
                            Speed_comboBox.SelectedIndex = 0;
                            string selectedSpeed = (string)Speed_comboBox.SelectedItem;
                            ProfileService.SettingConfig.SpeedType = speedOptions[selectedSpeed];
                        }
                    }
                    finally
                    {
                        synchronizingSelections = false;
                    }
                    ProfileService.SaveSettings();
                }
                else
                {
                    Console.WriteLine("잘못된 버전 유형을 선택했습니다.");
                }
            }
        }

        private void Speed_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (synchronizingSelections)
            {
                return;
            }

            if (Speed_comboBox.SelectedItem is string selectedSpeed)
            {
                if (SpeedType.speedNames.TryGetValue(ProfileService.SettingConfig.Version, out Dictionary<string, byte> speedOptions) &&
                    speedOptions.TryGetValue(selectedSpeed, out byte speedType))
                {
                    ProfileService.SettingConfig.SpeedType = speedType;
                    ProfileService.SaveSettings();
                }
                else
                {
                    Console.WriteLine("잘못된 속도 유형을 선택했습니다.");
                }
            }
        }

        private void AiSpeed_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (synchronizingSelections)
            {
                return;
            }

            if (AiSpeed_comboBox.SelectedItem is string selectedAiSpeed)
            {
                if (AiSpeed.Contains(selectedAiSpeed))
                {
                    ProfileService.SettingConfig.AiSpeedType = selectedAiSpeed;
                    ProfileService.SaveSettings();
                }
                else
                {
                    Console.WriteLine("잘못된 AI 난이도를 선택했습니다.");
                }
            }
        }

    }
}
