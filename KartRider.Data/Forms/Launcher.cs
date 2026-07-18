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
        private readonly NumericUpDown handshakeDelayNumeric = new NumericUpDown();
        private readonly Label topologyValueLabel = new Label();
        private readonly TextBox logDirectoryTextBox = new TextBox();
        private readonly CheckBox packetTraceCheckBox = new CheckBox();
        private readonly Button lanPresetButton = new Button();
        private readonly Button browseLogDirectoryButton = new Button();
        private readonly Button saveSettingsButton = new Button();
        private readonly Button startServerButton = new Button();
        private readonly Button stopServerButton = new Button();
        private readonly Button gameSettingsButton = new Button();
        private readonly Button itemProbabilityButton = new Button();
        private readonly Button extractKartCatalogButton = new Button();
        private readonly Button saveLogButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label buildValueLabel = new Label();
        private readonly RichTextBox logTextBox = new RichTextBox();

        private readonly TextWriter originalOut;
        private readonly TextWriter originalError;
        private readonly UiLogTextWriter uiOut;
        private readonly UiLogTextWriter uiError;

        private ServerLauncherSettings settings;
        private ItemProbabilityConfiguration itemProbabilityConfiguration =
            new ItemProbabilityConfiguration();
        private IPAddress confirmedUnassignedAdvertisedAddress;
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
                RowCount = 10
            };
            for (int index = 0; index < 9; index++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            bindAddressTextBox.Dock = DockStyle.Fill;
            bindAddressTextBox.PlaceholderText = "예: 0.0.0.0 또는 127.0.0.1";
            advertisedAddressTextBox.Dock = DockStyle.Fill;
            advertisedAddressTextBox.PlaceholderText = "이 서버 PC의 IPv4 (예: 192.168.1.15)";

            lanPresetButton.Text = "이 서버 PC 내부망 IPv4 자동 입력";
            lanPresetButton.AutoSize = true;
            lanPresetButton.Click += LanPresetButton_Click;
            root.Controls.Add(CreateFieldRow(
                "수신 IPv4 (서버 PC)",
                CreateAddressControls(bindAddressTextBox, lanPresetButton)), 0, 0);
            root.Controls.Add(CreateFieldRow("광고 IPv4 (이 서버 PC 주소)", advertisedAddressTextBox), 0, 1);

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

            handshakeDelayNumeric.Minimum = 0;
            handshakeDelayNumeric.Maximum = 5000;
            handshakeDelayNumeric.Increment = 25;
            handshakeDelayNumeric.Width = 130;
            handshakeDelayNumeric.Dock = DockStyle.Left;
            handshakeDelayNumeric.TextAlign = HorizontalAlignment.Right;
            root.Controls.Add(CreateFieldRow(
                "초기 핸드셰이크 지연 (ms)",
                handshakeDelayNumeric), 0, 4);

            logDirectoryTextBox.Dock = DockStyle.Fill;
            logDirectoryTextBox.PlaceholderText = "패킷 추적 로그 저장 폴더";
            browseLogDirectoryButton.Text = "폴더 선택";
            browseLogDirectoryButton.AutoSize = true;
            browseLogDirectoryButton.Click += BrowseLogDirectoryButton_Click;
            root.Controls.Add(CreateFieldRow(
                "로그 폴더",
                CreatePathControls(logDirectoryTextBox, browseLogDirectoryButton)), 0, 5);

            packetTraceCheckBox.Text = "전체 패킷 송수신 16진수 추적 기록 (로그가 빠르게 커질 수 있음)";
            packetTraceCheckBox.AutoSize = true;
            root.Controls.Add(CreateFieldRow("패킷 추적", packetTraceCheckBox), 0, 5);

            root.SetRow(packetTraceCheckBox.Parent, 6);

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
            ConfigureActionButton(
                itemProbabilityButton,
                "아이템 확률",
                ItemProbabilityButton_Click);
            ConfigureActionButton(
                extractKartCatalogButton,
                "카트 데이터 XML 추출",
                ExtractKartCatalogButton_Click);
            ConfigureActionButton(saveLogButton, "현재 로그 저장", SaveLogButton_Click);
            statusLabel.AutoSize = true;
            statusLabel.Margin = new Padding(16, 11, 0, 0);
            actions.Controls.AddRange(new Control[]
            {
                saveSettingsButton,
                startServerButton,
                stopServerButton,
                gameSettingsButton,
                itemProbabilityButton,
                extractKartCatalogButton,
                saveLogButton,
                statusLabel
            });
            root.Controls.Add(actions, 0, 7);

            buildValueLabel.AutoSize = true;
            buildValueLabel.ForeColor = Color.DimGray;
            buildValueLabel.Margin = new Padding(150, 5, 0, 0);
            root.Controls.Add(buildValueLabel, 0, 8);
            root.Controls.Add(new Label
            {
                Text = "광고 주소에는 클라이언트가 아니라 이 서버 PC의 IPv4를 입력하세요. " +
                       "내부망 사용 예: 수신 0.0.0.0, 광고 192.168.x.x. " +
                       "방화벽에는 로그인/메신저 TCP와 게임/P2P UDP 포트를 허용해야 합니다.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                MaximumSize = new Size(850, 0),
                Margin = new Padding(150, 8, 0, 0)
            }, 0, 9);

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
            handshakeDelayNumeric.Value = settings.FirstMessageDelayMilliseconds;
            itemProbabilityConfiguration = new ItemProbabilityConfiguration
            {
                RankBand = settings.ItemProbabilityRankBand,
                Individual = ItemProbabilityConfiguration.CloneEntries(
                    settings.IndividualItemProbabilities),
                Team = ItemProbabilityConfiguration.CloneEntries(
                    settings.TeamItemProbabilities)
            };
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
                EnablePacketTrace = packetTraceCheckBox.Checked,
                FirstMessageDelayMilliseconds = decimal.ToInt32(handshakeDelayNumeric.Value),
                ItemProbabilityRankBand = itemProbabilityConfiguration.RankBand,
                IndividualItemProbabilities = ItemProbabilityConfiguration.CloneEntries(
                    itemProbabilityConfiguration.Individual),
                TeamItemProbabilities = ItemProbabilityConfiguration.CloneEntries(
                    itemProbabilityConfiguration.Team)
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

            if (File.Exists(FileName.KartCatalog_LoadFile))
            {
                AppendLog($"카트 데이터 XML 준비됨: {FileName.KartCatalog_LoadFile}");
            }
            else
            {
                AppendLog(
                    "카트 데이터 XML이 없습니다. 서버 시작 전에 " +
                    "'카트 데이터 XML 추출'을 눌러 생성하세요.");
            }
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
            handshakeDelayNumeric.Enabled = canEdit;
            logDirectoryTextBox.Enabled = canEdit;
            packetTraceCheckBox.Enabled = canEdit;
            lanPresetButton.Enabled = canEdit;
            browseLogDirectoryButton.Enabled = canEdit;
            saveSettingsButton.Enabled = canEdit;
            startServerButton.Enabled = canEdit;
            stopServerButton.Enabled = !serverBusy && hasResources;
            gameSettingsButton.Enabled = !serverBusy;
            itemProbabilityButton.Enabled = canEdit;
            extractKartCatalogButton.Enabled = canEdit;
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
            AppendLog($"내부망 설정 적용: 수신=0.0.0.0, 광고={lanAddress}");
        }

        private void BrowseLogDirectoryButton_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "패킷 추적 로그를 저장할 폴더를 선택하세요.",
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
                if (!ConfirmAdvertisedAddress(settings))
                {
                    return;
                }
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
                var catalogLoad = await Task.Run(() =>
                {
                    bool loaded = KartRhoFile.TryLoadKartCatalogXml(
                        FileName.KartCatalog_LoadFile,
                        out int nameCount,
                        out int specCount,
                        out string error);
                    return (
                        loaded,
                        nameCount,
                        specCount,
                        abilityCount: KartCatalogAbilities.TotalRuleCount,
                        resolvedAbilityCount: KartCatalogAbilities.ResolvedRuleCount,
                        error);
                });
                if (!catalogLoad.loaded)
                {
                    throw new InvalidDataException(
                        "카트 데이터 XML을 불러올 수 없습니다. 서버를 시작하기 전에 " +
                        "'카트 데이터 XML 추출'을 눌러 다시 생성하세요.\n\n" +
                        $"{catalogLoad.error}\n{FileName.KartCatalog_LoadFile}");
                }
                AppendLog(
                    $"카트 데이터 XML 로드: 카트 {catalogLoad.nameCount}개, " +
                    $"스펙 {catalogLoad.specCount}개, " +
                    $"능력 {catalogLoad.resolvedAbilityCount}/{catalogLoad.abilityCount}개 해석");

                await Task.Run(() => ClientServerRuntime.Start(kartRiderDirectory, options));
                if (!ClientServerRuntime.IsRunning)
                {
                    throw new InvalidOperationException("모든 P5136 리스너가 시작되지 않았습니다.");
                }

                ClientPortTopology ports = ClientBuildProfiles.Active.Ports;
                AppendLog($"초기 핸드셰이크 지연: {options.FirstMessageDelayMilliseconds}ms");
                AppendLog(
                    $"아이템 확률: 순위 기준={options.ItemProbabilityRankBand}, " +
                    $"개인전 사용자 설정={options.IndividualItemProbabilities.Count}종, " +
                    $"팀전 사용자 설정={options.TeamItemProbabilities.Count}종");
                AppendLog(
                    $"서버 시작: 수신={options.BindAddress}, 광고={options.AdvertisedAddress}, " +
                    $"로그인 TCP={ports.ResolveLoginTcpPort(options.ConfiguredPort)}, " +
                    $"게임 UDP={ports.ResolveUdpPort(options.ConfiguredPort)}, " +
                    $"P2P UDP={ports.ResolveP2pPort(options.ConfiguredPort)}, " +
                    $"메신저 TCP={ports.ResolveMessengerPort(options.ConfiguredPort)}");
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

        private void ItemProbabilityButton_Click(object sender, EventArgs e)
        {
            string gameDirectory = string.IsNullOrWhiteSpace(kartRiderDirectory)
                ? FileName.appDir
                : kartRiderDirectory;
            ItemProbabilityConfiguration defaults =
                ItemProbabilityService.LoadClientDefaults(gameDirectory, out string source);
            using ItemProbabilityEditorForm dialog = new ItemProbabilityEditorForm(
                itemProbabilityConfiguration,
                defaults,
                source);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
            {
                return;
            }

            itemProbabilityConfiguration = dialog.Result.Clone();
            if (TrySaveSettings(showSuccess: false, showErrors: true))
            {
                string mode = itemProbabilityConfiguration.Individual.Count == 0 &&
                              itemProbabilityConfiguration.Team.Count == 0
                    ? "클라이언트 원본 자동 사용"
                    : "UI 사용자 가중치";
                AppendLog(
                    $"아이템 확률 설정 저장: {mode}, " +
                    $"순위 기준={itemProbabilityConfiguration.RankBand}");
            }
        }

        private async void ExtractKartCatalogButton_Click(object sender, EventArgs e)
        {
            if (serverBusy || ClientServerRuntime.HasResources)
            {
                return;
            }

            activeServerOperation = ExtractKartCatalogAsync();
            await activeServerOperation;
        }

        private async Task ExtractKartCatalogAsync()
        {
            serverBusy = true;
            string originalButtonText = extractKartCatalogButton.Text;
            extractKartCatalogButton.Text = "카트 데이터 추출 중...";
            RefreshServerStatus();
            AppendLog("클라이언트 RHO에서 카트 데이터 XML 추출을 시작합니다.");

            try
            {
                string gameDirectory = string.IsNullOrWhiteSpace(kartRiderDirectory)
                    ? FileName.appDir
                    : kartRiderDirectory;
                string aaaPkPath = Path.Combine(gameDirectory, "Data", "aaa.pk");
                string outputPath = FileName.KartCatalog_LoadFile;

                var result = await Task.Run(() =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    if (!KartRhoFile.TryExportKartCatalogXmlReadOnly(
                            aaaPkPath,
                            outputPath,
                            out int exportedNames,
                            out int exportedSpecs,
                            out string exportError))
                    {
                        throw new InvalidDataException(exportError);
                    }

                    if (!KartRhoFile.TryLoadKartCatalogXml(
                            outputPath,
                            out int loadedNames,
                            out int loadedSpecs,
                            out string loadError))
                    {
                        throw new InvalidDataException(
                            $"XML 파일은 생성했지만 다시 불러오지 못했습니다: {loadError}");
                    }

                    return (
                        exportedNames,
                        exportedSpecs,
                        loadedNames,
                        loadedSpecs,
                        abilityCount: KartCatalogAbilities.TotalRuleCount,
                        resolvedAbilityCount: KartCatalogAbilities.ResolvedRuleCount);
                });

                string fullOutputPath = Path.GetFullPath(outputPath);
                AppendLog(
                    $"카트 데이터 XML 추출 완료: 카트 {result.exportedNames}개, " +
                    $"스펙 {result.exportedSpecs}개, 로드 {result.loadedNames}/{result.loadedSpecs}개, " +
                    $"능력 {result.resolvedAbilityCount}/{result.abilityCount}개 해석, " +
                    $"경로={fullOutputPath}");
                MessageBox.Show(
                    this,
                    $"카트 데이터 XML을 생성하고 즉시 불러왔습니다.\n\n" +
                    $"카트 이름: {result.exportedNames}개\n" +
                    $"카트 스펙: {result.exportedSpecs}개\n" +
                    $"카트 능력: {result.resolvedAbilityCount}/{result.abilityCount}개 해석\n" +
                    $"저장 경로: {fullOutputPath}",
                    "카트 데이터 추출 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                ShowError(
                    "카트 데이터 XML 추출 실패",
                    new InvalidOperationException(
                        "클라이언트 Data 폴더와 RHO 파일을 확인하세요. " +
                        "기존 KartCatalog.xml은 성공적으로 교체되기 전까지 유지됩니다.\n\n" +
                        exception.Message,
                        exception));
            }
            finally
            {
                extractKartCatalogButton.Text = originalButtonText;
                serverBusy = false;
                RefreshServerStatus();
            }
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
                AppendLog($"화면 로그 저장: {path}");
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
                if (!ConfirmAdvertisedAddress(settings))
                {
                    return false;
                }
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

        private bool ConfirmAdvertisedAddress(ServerLauncherSettings candidate)
        {
            IPAddress advertisedAddress = IPAddress.Parse(candidate.AdvertisedAddress);
            if (ServerLauncherSettings.IsAddressAssignedLocally(advertisedAddress) ||
                advertisedAddress.Equals(confirmedUnassignedAdvertisedAddress))
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                this,
                $"광고 주소 {advertisedAddress}는 이 서버 PC의 네트워크 주소에서 찾을 수 없습니다.\n\n" +
                "클라이언트 PC 주소를 입력한 경우 취소하고 '이 서버 PC 내부망 IPv4 자동 입력'을 누르세요. " +
                "VPN/NAT 등으로 의도한 주소라면 계속할 수 있습니다.",
                "광고 주소 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return false;
            }

            confirmedUnassignedAdvertisedAddress = advertisedAddress;
            return true;
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
