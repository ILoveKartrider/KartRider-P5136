using KartRider.Compatibility;
using Profile;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace KartRider.Connector
{
    internal sealed class ConnectorForm : Form
    {
        private const int MaximumLogCharacters = 500_000;

        private readonly string gameDirectory;
        private readonly ClientBuildProfile profile;
        private readonly TextBox serverAddressTextBox = new TextBox();
        private readonly NumericUpDown loginPortInput = new NumericUpDown();
        private readonly TextBox usernameTextBox = new TextBox();
        private readonly Label topologyValueLabel = new Label();
        private readonly Label statusLabel = new Label();
        private readonly Button saveButton = new Button();
        private readonly Button launchButton = new Button();
        private readonly RichTextBox logTextBox = new RichTextBox();
        private readonly Timer processTimer;

        private IClientLaunchStrategy launchStrategy;
        private ClientLaunchContext launchContext;
        private bool gameWasObserved;
        private DateTime launchStartedAtUtc;

        public ConnectorForm(string gameDirectory, ClientBuildProfile profile)
        {
            this.gameDirectory = gameDirectory ?? throw new ArgumentNullException(nameof(gameDirectory));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

            Text = "KartRider P5136 접속기";
            ClientSize = new Size(820, 610);
            MinimumSize = new Size(760, 560);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);

            BuildLayout();
            LoadSettings();

            processTimer = new Timer { Interval = 1000 };
            processTimer.Tick += ProcessTimer_Tick;
            processTimer.Start();
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 10
            };
            for (int index = 0; index < 9; index++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(new Label
            {
                Text = "KartRider P5136 접속기",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);
            root.Controls.Add(new Label
            {
                Text = "서버 런처에 표시된 광고 IPv4와 로그인 TCP 포트를 그대로 입력하세요.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 14)
            }, 0, 1);

            root.Controls.Add(CreateFieldRow("감지된 빌드", CreateValueLabel(profile.DisplayName)), 0, 2);
            root.Controls.Add(CreateFieldRow("클라이언트 경로", CreateValueLabel(gameDirectory)), 0, 3);

            usernameTextBox.Dock = DockStyle.Fill;
            usernameTextBox.MaxLength = 32;
            root.Controls.Add(CreateFieldRow("계정 이름", usernameTextBox), 0, 4);

            serverAddressTextBox.Dock = DockStyle.Fill;
            serverAddressTextBox.PlaceholderText = "예: 192.168.1.10";
            root.Controls.Add(CreateFieldRow("서버 IPv4", serverAddressTextBox), 0, 5);

            loginPortInput.Minimum = profile.Ports.LoginTcpOffset + 1;
            loginPortInput.Maximum = profile.Ports.MaximumLoginTcpPort;
            loginPortInput.Width = 130;
            loginPortInput.Dock = DockStyle.Left;
            loginPortInput.TextAlign = HorizontalAlignment.Right;
            root.Controls.Add(CreateFieldRow("로그인 TCP 포트", loginPortInput), 0, 6);

            topologyValueLabel.AutoSize = true;
            topologyValueLabel.ForeColor = Color.DimGray;
            root.Controls.Add(CreateFieldRow("파생 포트", topologyValueLabel), 0, 7);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(142, 8, 0, 8)
            };
            saveButton.Text = "설정 저장";
            saveButton.AutoSize = true;
            saveButton.Padding = new Padding(12, 6, 12, 6);
            saveButton.Click += SaveButton_Click;
            launchButton.Text = "게임 실행";
            launchButton.AutoSize = true;
            launchButton.Padding = new Padding(22, 6, 22, 6);
            launchButton.Font = new Font(Font, FontStyle.Bold);
            launchButton.Click += LaunchButton_Click;
            statusLabel.Text = "대기 중";
            statusLabel.AutoSize = true;
            statusLabel.Margin = new Padding(16, 11, 0, 0);
            actions.Controls.Add(saveButton);
            actions.Controls.Add(launchButton);
            actions.Controls.Add(statusLabel);
            root.Controls.Add(actions, 0, 8);

            logTextBox.Dock = DockStyle.Fill;
            logTextBox.ReadOnly = true;
            logTextBox.BackColor = Color.FromArgb(248, 248, 248);
            logTextBox.BorderStyle = BorderStyle.FixedSingle;
            logTextBox.Font = new Font("Consolas", 9F);
            logTextBox.DetectUrls = false;
            root.Controls.Add(logTextBox, 0, 9);

            Controls.Add(root);
            AcceptButton = launchButton;
            serverAddressTextBox.TextChanged += (_, _) => RefreshEndpoint();
            loginPortInput.ValueChanged += (_, _) => RefreshEndpoint();
            FormClosing += ConnectorForm_FormClosing;
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
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
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

        private static Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129)
            };
        }

        private void LoadSettings()
        {
            serverAddressTextBox.Text = ProfileService.SettingConfig.ServerIP;
            usernameTextBox.Text = ProfileService.SettingConfig.Name;
            ushort loginPort = profile.Ports.ResolveLoginTcpPort(
                ProfileService.SettingConfig.ServerPort);
            loginPortInput.Value = loginPort;
            RefreshEndpoint();
            AppendLog(
                $"설정 로드: server={serverAddressTextBox.Text}:{loginPort}, " +
                $"client={gameDirectory}");
        }

        private void RefreshEndpoint()
        {
            try
            {
                ushort loginPort = checked((ushort)loginPortInput.Value);
                ushort configuredPort = profile.Ports.ResolveConfiguredPortFromLoginTcp(loginPort);
                topologyValueLabel.Text =
                    $"로그인 {serverAddressTextBox.Text.Trim()}:{loginPort} · " +
                    $"게임 UDP {profile.Ports.ResolveUdpPort(configuredPort)} · " +
                    $"P2P UDP {profile.Ports.ResolveP2pPort(configuredPort)} · " +
                    $"메신저 TCP {profile.Ports.ResolveMessengerPort(configuredPort)}";
            }
            catch
            {
                topologyValueLabel.Text = "현재 로그인 포트로 파생 포트를 만들 수 없습니다.";
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveConnectorSettings();
                statusLabel.ForeColor = Color.DarkGreen;
                statusLabel.Text = "설정 저장됨";
            }
            catch (Exception exception)
            {
                ShowError("접속 설정 오류", exception);
            }
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            if (IsGameRunning())
            {
                MessageBox.Show(
                    this,
                    "KartRider가 이미 실행 중입니다.",
                    "게임 실행 중",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                (IPAddress serverAddress, ushort loginPort, ushort configuredPort, string username) =
                    SaveConnectorSettings();

                string pinFile = Path.Combine(gameDirectory, "KartRider.pin");
                string pinBackupFile = Path.Combine(gameDirectory, "KartRider-bak.pin");
                launchContext = new ClientLaunchContext(
                    gameDirectory,
                    pinFile,
                    pinBackupFile,
                    serverAddress.ToString(),
                    configuredPort,
                    username);
                launchStrategy = ClientLaunchStrategies.For(profile);
                launchStrategy.Launch(launchContext);

                launchStartedAtUtc = DateTime.UtcNow;
                gameWasObserved = IsGameRunning();
                statusLabel.ForeColor = Color.ForestGreen;
                statusLabel.Text = gameWasObserved ? "게임 실행 중" : "게임 시작 확인 중";
                AppendLog($"게임 실행: server={serverAddress}:{loginPort}, username={username}");
            }
            catch (Exception exception)
            {
                launchStrategy?.Restore(launchContext);
                launchStrategy = null;
                launchContext = null;
                ShowError("게임 실행 실패", exception);
            }
        }

        private (IPAddress Address, ushort LoginPort, ushort ConfiguredPort, string Username)
            SaveConnectorSettings()
        {
            string addressText = serverAddressTextBox.Text.Trim();
            if (!IPAddress.TryParse(addressText, out IPAddress address) ||
                address.AddressFamily != AddressFamily.InterNetwork ||
                address.Equals(IPAddress.Any))
            {
                throw new InvalidDataException("서버 주소는 0.0.0.0이 아닌 IPv4 주소여야 합니다.");
            }

            string username = usernameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidDataException("계정 이름을 입력하세요.");
            }

            ushort loginPort = checked((ushort)loginPortInput.Value);
            ushort configuredPort = profile.Ports.ResolveConfiguredPortFromLoginTcp(loginPort);
            _ = profile.Ports.ResolveMessengerPort(configuredPort);

            serverAddressTextBox.Text = address.ToString();
            ProfileService.SettingConfig.ServerIP = address.ToString();
            ProfileService.SettingConfig.ServerPort = configuredPort;
            ProfileService.SettingConfig.Name = username;
            ProfileService.SaveSettings();
            AppendLog($"접속 설정 저장: {address}:{loginPort}");
            return (address, loginPort, configuredPort, username);
        }

        private void ProcessTimer_Tick(object sender, EventArgs e)
        {
            if (launchStrategy == null)
            {
                return;
            }

            bool gameRunning = IsGameRunning();
            if (gameRunning)
            {
                gameWasObserved = true;
                statusLabel.ForeColor = Color.ForestGreen;
                statusLabel.Text = "게임 실행 중";
                return;
            }

            if (!gameWasObserved && DateTime.UtcNow - launchStartedAtUtc < TimeSpan.FromSeconds(3))
            {
                return;
            }

            bool wasObserved = gameWasObserved;
            launchStrategy.Restore(launchContext);
            launchStrategy = null;
            launchContext = null;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Text = wasObserved ? "게임 종료됨" : "게임이 바로 종료됨";
            AppendLog(statusLabel.Text + "; 임시 접속 설정을 복원했습니다.");
            gameWasObserved = false;
        }

        private void ConnectorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            launchStrategy?.Restore(launchContext);
        }

        private void AppendLog(string message)
        {
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
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = title;
            AppendLog($"{title}: {exception.Message}");
            MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                processTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private static bool IsGameRunning()
        {
            Process[] processes = Process.GetProcessesByName("KartRider");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }
}
