using KartRider.Compatibility;
using KartRider.ServerLauncher;
using LoggerLibrary;
using Profile;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KartRider
{
    public partial class Launcher : Form
    {
        private const int MaximumLogCharacters = 1_000_000;

        private readonly TextBox bindAddressTextBox = new TextBox();
        private readonly TextBox advertisedAddressTextBox = new TextBox();
        private readonly NumericUpDown loginPortNumeric = new NumericUpDown();
        private readonly Label topologyValueLabel = new Label();
        private readonly TextBox logDirectoryTextBox = new TextBox();
        private readonly CheckBox packetTraceCheckBox = new CheckBox();
        private readonly Button lanPresetButton = new Button();
        private readonly Button browseLogDirectoryButton = new Button();
        private readonly Button saveSettingsButton = new Button();
        private readonly Button startServerButton = new Button();
        private readonly Button stopServerButton = new Button();
        private readonly Button gameSettingsButton = new Button();
        private readonly Button saveLogButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label buildValueLabel = new Label();
        private readonly RichTextBox logTextBox = new RichTextBox();

        private readonly TextWriter originalOut;
        private readonly TextWriter originalError;
        private readonly UiLogTextWriter uiOut;
        private readonly UiLogTextWriter uiError;

        private ServerLauncherSettings settings;
        private bool serverBusy;
        private bool shutdownStarted;
        private bool allowClose;
        private Task activeServerOperation = Task.CompletedTask;

        public string kartRiderDirectory;

        public Launcher()
        {
            InitializeComponent();
            BuildLayout();
            LoadLauncherSettings();

            string existingLog = CachedConsoleWriter.cachedWriter?.Cache;
            if (!string.IsNullOrEmpty(existingLog))
            {
                if (existingLog.Length > MaximumLogCharacters / 2)
                {
                    existingLog = existingLog.Substring(existingLog.Length - MaximumLogCharacters / 2);
                }
                logTextBox.AppendText(existingLog);
            }

            originalOut = Console.Out;
            originalError = Console.Error;
            uiOut = new UiLogTextWriter(AppendLog);
            uiError = new UiLogTextWriter(AppendLog, "[오류] ");
            Console.SetOut(new TeeTextWriter(originalOut, uiOut));
            Console.SetError(new TeeTextWriter(originalError, uiError));
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));

            root.Controls.Add(new Label
            {
                Text = "KartRider P5136 서버 런처",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);
            root.Controls.Add(new Label
            {
                Text = "수신 주소와 클라이언트에 알릴 주소를 분리해 관리합니다.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 12)
            }, 0, 1);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildServerPage());
            root.Controls.Add(tabs, 0, 2);

            root.Controls.Add(new Label
            {
                Text = "실행 로그",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 5)
            }, 0, 3);

            logTextBox.Dock = DockStyle.Fill;
            logTextBox.ReadOnly = true;
            logTextBox.BackColor = Color.FromArgb(248, 248, 248);
            logTextBox.BorderStyle = BorderStyle.FixedSingle;
            logTextBox.Font = new Font("Consolas", 9F);
            logTextBox.DetectUrls = false;
            root.Controls.Add(logTextBox, 0, 4);

            Controls.Add(root);
        }

        private TabPage BuildServerPage()
        {
            TabPage page = new TabPage("서버 실행");
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                ColumnCount = 1,
                RowCount = 9
            };
            for (int index = 0; index < 8; index++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            bindAddressTextBox.Dock = DockStyle.Fill;
            bindAddressTextBox.PlaceholderText = "예: 0.0.0.0 또는 127.0.0.1";
            advertisedAddressTextBox.Dock = DockStyle.Fill;
            advertisedAddressTextBox.PlaceholderText = "클라이언트에 전달할 서버 IPv4";

            lanPresetButton.Text = "LAN 자동 설정";
            lanPresetButton.AutoSize = true;
            lanPresetButton.Click += LanPresetButton_Click;
            root.Controls.Add(CreateFieldRow(
                "바인드 IPv4",
                CreateAddressControls(bindAddressTextBox, lanPresetButton)), 0, 0);
            root.Controls.Add(CreateFieldRow("광고 IPv4", advertisedAddressTextBox), 0, 1);

            loginPortNumeric.Minimum = ClientBuildProfiles.Active.Ports.LoginTcpOffset + 1;
            loginPortNumeric.Maximum = ClientBuildProfiles.Active.Ports.MaximumLoginTcpPort;
            loginPortNumeric.Width = 130;
            loginPortNumeric.Dock = DockStyle.Left;
            loginPortNumeric.TextAlign = HorizontalAlignment.Right;
            loginPortNumeric.ValueChanged += (_, _) => RefreshTopology();
            root.Controls.Add(CreateFieldRow("로그인 TCP 포트", loginPortNumeric), 0, 2);

            topologyValueLabel.AutoSize = true;
            topologyValueLabel.ForeColor = Color.DimGray;
            topologyValueLabel.Margin = new Padding(0, 5, 0, 5);
            root.Controls.Add(CreateFieldRow("파생 포트", topologyValueLabel), 0, 3);

            logDirectoryTextBox.Dock = DockStyle.Fill;
            logDirectoryTextBox.PlaceholderText = "패킷 trace 로그 저장 폴더";
            browseLogDirectoryButton.Text = "폴더 선택";
            browseLogDirectoryButton.AutoSize = true;
            browseLogDirectoryButton.Click += BrowseLogDirectoryButton_Click;
            root.Controls.Add(CreateFieldRow(
                "로그 폴더",
                CreatePathControls(logDirectoryTextBox, browseLogDirectoryButton)), 0, 4);

            packetTraceCheckBox.Text = "전체 패킷 RX/TX hex trace 기록 (로그가 빠르게 커질 수 있음)";
            packetTraceCheckBox.AutoSize = true;
            root.Controls.Add(CreateFieldRow("패킷 trace", packetTraceCheckBox), 0, 5);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(145, 8, 0, 8)
            };
            ConfigureActionButton(saveSettingsButton, "설정 저장", SaveSettingsButton_Click);
            ConfigureActionButton(startServerButton, "서버 시작", StartServerButton_Click);
            startServerButton.Font = new Font(Font, FontStyle.Bold);
            ConfigureActionButton(stopServerButton, "서버 중지", StopServerButton_Click);
            ConfigureActionButton(gameSettingsButton, "게임 설정", GameSettingsButton_Click);
            ConfigureActionButton(saveLogButton, "현재 로그 저장", SaveLogButton_Click);
            statusLabel.AutoSize = true;
            statusLabel.Margin = new Padding(16, 11, 0, 0);
            actions.Controls.AddRange(new Control[]
            {
                saveSettingsButton,
                startServerButton,
                stopServerButton,
                gameSettingsButton,
                saveLogButton,
                statusLabel
            });
            root.Controls.Add(actions, 0, 6);

            buildValueLabel.AutoSize = true;
            buildValueLabel.ForeColor = Color.DimGray;
            buildValueLabel.Margin = new Padding(150, 5, 0, 0);
            root.Controls.Add(buildValueLabel, 0, 7);
            root.Controls.Add(new Label
            {
                Text = "LAN 사용 예: 바인드 0.0.0.0, 광고 192.168.x.x. " +
                       "방화벽에는 로그인/메신저 TCP와 게임/P2P UDP 포트를 허용해야 합니다.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                MaximumSize = new Size(850, 0),
                Margin = new Padding(150, 8, 0, 0)
            }, 0, 8);

            page.Controls.Add(root);
            return page;
        }

        private static void ConfigureActionButton(Button button, string text, EventHandler handler)
        {
            button.Text = text;
            button.AutoSize = true;
            button.Padding = new Padding(10, 5, 10, 5);
            button.Click += handler;
        }

        private static Control CreateFieldRow(string labelText, Control control)
        {
            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 4, 0, 4)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 8, 3)
            }, 0, 0);
            row.Controls.Add(control, 1, 0);
            return row;
        }

        private static TableLayoutPanel CreateAddressControls(TextBox textBox, Button actionButton)
        {
            TableLayoutPanel controls = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2
            };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            controls.Controls.Add(textBox, 0, 0);
            controls.Controls.Add(actionButton, 1, 0);
            return controls;
        }

        private static TableLayoutPanel CreatePathControls(TextBox textBox, Button browseButton)
        {
            return CreateAddressControls(textBox, browseButton);
        }

        private void LoadLauncherSettings()
        {
            try
            {
                settings = ServerLauncherSettingsStore.LoadOrDefault();
                settings.Validate(ClientBuildProfiles.Active);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is ArgumentException)
            {
                settings = ServerLauncherSettings.CreateDefault();
                MessageBox.Show(
                    this,
                    exception.Message,
                    "서버 설정 읽기 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            bindAddressTextBox.Text = settings.BindAddress;
            advertisedAddressTextBox.Text = settings.AdvertisedAddress;
            ushort loginPort = ClientBuildProfiles.Active.Ports.ResolveLoginTcpPort(
                checked((ushort)settings.ConfiguredPort));
            loginPortNumeric.Value = loginPort;
            logDirectoryTextBox.Text = settings.LogDirectory;
            packetTraceCheckBox.Checked = settings.EnablePacketTrace;
            RefreshTopology();
            RefreshServerStatus();
        }

        private ServerLauncherSettings CaptureSettings()
        {
            ushort loginPort = checked((ushort)loginPortNumeric.Value);
            ushort configuredPort = ClientBuildProfiles.Active.Ports
                .ResolveConfiguredPortFromLoginTcp(loginPort);
            ServerLauncherSettings captured = new ServerLauncherSettings
            {
                BindAddress = bindAddressTextBox.Text.Trim(),
                AdvertisedAddress = advertisedAddressTextBox.Text.Trim(),
                ConfiguredPort = configuredPort,
                LogDirectory = NormalizeRequiredPath(logDirectoryTextBox.Text, "로그 폴더"),
                EnablePacketTrace = packetTraceCheckBox.Checked
            };
            captured.Validate(ClientBuildProfiles.Active);
            return captured;
        }

        private static string NormalizeRequiredPath(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"{label} 값이 비어 있습니다.");
            }

            return Path.GetFullPath(value.Trim());
        }

        private void OnLoad(object sender, EventArgs e)
        {
            buildValueLabel.Text =
                $"대상 빌드: {ClientBuildProfiles.Active.DisplayName} · 런처 빌드: {CompileTime.Time}";
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
            bool hasResources = ClientServerRuntime.HasResources;
            statusLabel.Text = serverBusy
                ? "처리 중..."
                : isRunning ? "실행 중"
                : hasResources ? "부분 실행 상태 - 중지 필요"
                : "중지됨";
            statusLabel.ForeColor = isRunning
                ? Color.DarkGreen
                : hasResources ? Color.Firebrick : SystemColors.ControlText;

            bool canEdit = !serverBusy && !hasResources;
            bindAddressTextBox.Enabled = canEdit;
            advertisedAddressTextBox.Enabled = canEdit;
            loginPortNumeric.Enabled = canEdit;
            logDirectoryTextBox.Enabled = canEdit;
            packetTraceCheckBox.Enabled = canEdit;
            lanPresetButton.Enabled = canEdit;
            browseLogDirectoryButton.Enabled = canEdit;
            saveSettingsButton.Enabled = canEdit;
            startServerButton.Enabled = canEdit;
            stopServerButton.Enabled = !serverBusy && hasResources;
            gameSettingsButton.Enabled = !serverBusy;
            saveLogButton.Enabled = !serverBusy;
            RefreshWindowTitle();
        }

        public void RefreshWindowTitle()
        {
            Text = Program.SpeedPatch
                ? "KartRider P5136 서버 런처 (속도 패치)"
                : "KartRider P5136 서버 런처";
        }

        private void RefreshTopology()
        {
            try
            {
                ushort loginPort = checked((ushort)loginPortNumeric.Value);
                ushort basePort = ClientBuildProfiles.Active.Ports
                    .ResolveConfiguredPortFromLoginTcp(loginPort);
                ClientPortTopology ports = ClientBuildProfiles.Active.Ports;
                topologyValueLabel.Text =
                    $"게임 UDP {ports.ResolveUdpPort(basePort)} · " +
                    $"P2P UDP {ports.ResolveP2pPort(basePort)} · " +
                    $"메신저 TCP {ports.ResolveMessengerPort(basePort)}";
            }
            catch
            {
                topologyValueLabel.Text = "현재 로그인 포트로 파생 포트를 만들 수 없습니다.";
            }
        }

        private void LanPresetButton_Click(object sender, EventArgs e)
        {
            IPAddress lanAddress = ServerLauncherSettings.FindPreferredLanAddress();
            bindAddressTextBox.Text = IPAddress.Any.ToString();
            advertisedAddressTextBox.Text = lanAddress.ToString();
            AppendLog($"LAN 설정 적용: bind=0.0.0.0, advertise={lanAddress}");
        }

        private void BrowseLogDirectoryButton_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "패킷 trace 로그를 저장할 폴더를 선택하세요.",
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(logDirectoryTextBox.Text)
                    ? logDirectoryTextBox.Text
                    : string.Empty
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                logDirectoryTextBox.Text = Path.GetFullPath(dialog.SelectedPath);
            }
        }

        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            TrySaveSettings(showSuccess: true, showErrors: true);
        }

        private async void StartServerButton_Click(object sender, EventArgs e)
        {
            if (serverBusy || ClientServerRuntime.IsRunning)
            {
                return;
            }

            activeServerOperation = StartServerAsync();
            await activeServerOperation;
        }

        private async Task StartServerAsync()
        {
            P5136ServerOptions options;
            try
            {
                settings = CaptureSettings();
                ServerLauncherSettingsStore.Save(settings);
                options = settings.ToServerOptions(ClientBuildProfiles.Active);
            }
            catch (Exception exception)
            {
                ShowError("서버 설정 오류", exception);
                return;
            }

            serverBusy = true;
            RefreshServerStatus();
            try
            {
                await Task.Run(() => ClientServerRuntime.Start(kartRiderDirectory, options));
                if (!ClientServerRuntime.IsRunning)
                {
                    throw new InvalidOperationException("모든 P5136 리스너가 시작되지 않았습니다.");
                }

                ClientPortTopology ports = ClientBuildProfiles.Active.Ports;
                AppendLog(
                    $"서버 시작: bind={options.BindAddress}, advertise={options.AdvertisedAddress}, " +
                    $"login TCP={ports.ResolveLoginTcpPort(options.ConfiguredPort)}, " +
                    $"game UDP={ports.ResolveUdpPort(options.ConfiguredPort)}, " +
                    $"P2P UDP={ports.ResolveP2pPort(options.ConfiguredPort)}, " +
                    $"messenger TCP={ports.ResolveMessengerPort(options.ConfiguredPort)}");
            }
            catch (Exception exception)
            {
                ClientServerRuntime.Stop();
                ShowError("서버 시작 실패", exception);
            }
            finally
            {
                serverBusy = false;
                RefreshServerStatus();
            }
        }

        private async void StopServerButton_Click(object sender, EventArgs e)
        {
            if (serverBusy || !ClientServerRuntime.HasResources)
            {
                return;
            }

            activeServerOperation = StopServerAsync();
            await activeServerOperation;
        }

        private async Task StopServerAsync()
        {
            serverBusy = true;
            RefreshServerStatus();
            try
            {
                await Task.Run(ClientServerRuntime.Stop);
                AppendLog("서버가 중지되었습니다.");
            }
            catch (Exception exception)
            {
                ShowError("서버 중지 실패", exception);
            }
            finally
            {
                serverBusy = false;
                RefreshServerStatus();
            }
        }

        private void GameSettingsButton_Click(object sender, EventArgs e)
        {
            Program.SettingDlg = new Setting();
            Program.SettingDlg.ShowDialog(this);
            RefreshServerStatus();
        }

        private void SaveLogButton_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(logDirectoryTextBox.Text);
                string path = Path.Combine(
                    Path.GetFullPath(logDirectoryTextBox.Text),
                    $"server-ui_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.WriteAllText(path, logTextBox.Text);
                AppendLog($"UI 로그 저장: {path}");
            }
            catch (Exception exception)
            {
                ShowError("로그 저장 실패", exception);
            }
        }

        private bool TrySaveSettings(bool showSuccess, bool showErrors)
        {
            try
            {
                settings = CaptureSettings();
                ServerLauncherSettingsStore.Save(settings);
                AppendLog($"서버 설정 저장: {ServerLauncherSettingsStore.SettingsPath}");
                if (showSuccess)
                {
                    statusLabel.Text = "설정 저장됨";
                }
                return true;
            }
            catch (Exception exception)
            {
                if (showErrors)
                {
                    ShowError("서버 설정 저장 실패", exception);
                }
                else
                {
                    AppendLog($"서버 설정 저장 실패: {exception.Message}");
                }
                return false;
            }
        }

        private void AppendLog(string message)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            if (logTextBox.TextLength > MaximumLogCharacters)
            {
                logTextBox.Select(0, logTextBox.TextLength - MaximumLogCharacters / 2);
                logTextBox.SelectedText = string.Empty;
            }

            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            logTextBox.SelectionStart = logTextBox.TextLength;
            logTextBox.ScrollToCaret();
        }

        private void ShowError(string title, Exception exception)
        {
            AppendLog($"{title}: {exception.Message}");
            MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowClose)
            {
                e.Cancel = true;
                if (!shutdownStarted)
                {
                    shutdownStarted = true;
                    Enabled = false;
                    _ = ShutdownAndCloseAsync();
                }
            }

            base.OnFormClosing(e);
        }

        private async Task ShutdownAndCloseAsync()
        {
            try
            {
                await activeServerOperation;
                await Task.Run(ClientServerRuntime.Stop);
                TrySaveSettings(showSuccess: false, showErrors: false);
            }
            catch (Exception exception)
            {
                AppendLog($"종료 처리 오류: {exception.Message}");
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                allowClose = true;
                BeginInvoke(new Action(Close));
            }
        }
    }
}
