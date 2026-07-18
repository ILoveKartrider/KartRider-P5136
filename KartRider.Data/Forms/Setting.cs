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
            ["国服"] = "중국 현행 물리",
            ["国服复古"] = "중국 클래식 물리",
            ["韩服复古"] = "한국 클래식 물리",
            ["S0"] = "S0 · 보통",
            ["S1"] = "S1 · 빠름",
            ["S2"] = "S2 · 매우 빠름",
            ["S3"] = "S3 · 가장 빠름",
            ["S4"] = "S4 · 무한부스터",
            ["S5"] = "S5 · CGS LTE",
            ["S6"] = "S6 · 진·무한부스터",
            ["S7"] = "S7 · 통합 스피드",
            ["S8"] = "S8 · 통합 아이템",
            ["新手"] = "초보",
            ["初级"] = "루키",
            ["Pro"] = "PRO",
            ["简单"] = "쉬움",
            ["困难"] = "어려움",
            ["地狱"] = "지옥"
        };

        private const string RoomNameKeywordHelp =
            "방 이름 키워드 (위 서버 설정보다 우선)\r\n" +
            "현행: S0=보통 · S1=빠름 · S2=매우 빠름 · S3=가장 빠름\r\n" +
            "특수: S4=무한부스터 · S5=CGS LTE · S6=진·무한부스터\r\n" +
            "통합: S7=스피드 · S8=아이템\r\n" +
            "클래식: BEGINNER · ROOKIE · L3 · L2 · L1 · PRO (한국 물리: KR 추가)";

        public string[] AiSpeed = new string[] { "简单", "困难", "地狱" };

        private bool synchronizingSelections;

        public Setting()
        {
            InitializeComponent();
            Version_comboBox.FormattingEnabled = true;
            Speed_comboBox.FormattingEnabled = true;
            AiSpeed_comboBox.FormattingEnabled = true;
            Version_comboBox.Format += LocalizeComboBoxItem;
            Speed_comboBox.Format += LocalizeComboBoxItem;
            AiSpeed_comboBox.Format += LocalizeComboBoxItem;
            Version_comboBox.SelectedIndexChanged += Version_comboBox_SelectedIndexChanged;
            Speed_comboBox.SelectedIndexChanged += Speed_comboBox_SelectedIndexChanged;
            AiSpeed_comboBox.SelectedIndexChanged += AiSpeed_comboBox_SelectedIndexChanged;
        }

        private void OnGameSettingsClosing(object sender, FormClosingEventArgs e)
        {
            string playerName = PlayerName.Text.Trim();
            if (string.IsNullOrWhiteSpace(playerName))
            {
                MessageBox.Show(
                    "기본 계정 이름을 입력하세요.",
                    "게임 설정 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            ProfileService.SettingConfig.Name = playerName;
            ProfileService.SettingConfig.SoloRank = SoloRank.Checked;
            ProfileService.SettingConfig.EnableMod = EnableMod.Checked;
            ProfileService.SaveSettings();
            Program.LauncherDlg?.RefreshServerStatus();
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

        private static string GetDefaultSpeedOption(
            string version,
            Dictionary<string, byte> speedOptions)
        {
            if (version == "国服")
            {
                string integratedSpeed = speedOptions
                    .FirstOrDefault(pair => pair.Value == 7).Key;
                if (integratedSpeed != null)
                {
                    return integratedSpeed;
                }
            }

            return speedOptions.Keys.First();
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

            // Network settings belong to the P5136 server launcher (or to the
            // connector on a client machine). Game-profile changes must never
            // restart the server with the connector's shared legacy settings.
            bool serverWasRunning = false;
            bool serverConfigurationChanged = false;

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
                configuredSpeed = GetDefaultSpeedOption(
                    configuredVersion,
                    configuredSpeedOptions);
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
                    : "※ 서버가 경기 시작 패킷으로 전달하는 주행 물리 설정입니다.";
                RoomNameKeyword_label.Text = RoomNameKeywordHelp;
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
                            string selectedSpeed = GetDefaultSpeedOption(
                                selectedVersion,
                                speedOptions);
                            Speed_comboBox.SelectedItem = selectedSpeed;
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
