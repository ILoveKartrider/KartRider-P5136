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
        private readonly string gameDirectory;
        private readonly ClientBuildProfile profile;
        private readonly TextBox serverAddressTextBox;
        private readonly NumericUpDown basePortInput;
        private readonly TextBox usernameTextBox;
        private readonly Label endpointValueLabel;
        private readonly Label statusLabel;
        private readonly Button launchButton;
        private readonly Timer processTimer;

        private IClientLaunchStrategy launchStrategy;
        private ClientLaunchContext launchContext;
        private bool gameWasObserved;
        private DateTime launchStartedAtUtc;

        public ConnectorForm(string gameDirectory, ClientBuildProfile profile)
        {
            this.gameDirectory = gameDirectory ?? throw new ArgumentNullException(nameof(gameDirectory));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

            Text = "카트라이더 5136 접속기";
            ClientSize = new Size(430, 310);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);

            Label buildLabel = CreateCaption("감지된 빌드", 20, 20);
            Label buildValueLabel = CreateValue(profile.DisplayName, 145, 20, 255);
            Label addressLabel = CreateCaption("서버 주소", 20, 58);
            serverAddressTextBox = new TextBox
            {
                Location = new Point(145, 55),
                Size = new Size(255, 23),
                Text = ProfileService.SettingConfig.ServerIP
            };

            Label portLabel = CreateCaption("기본 포트", 20, 96);
            basePortInput = new NumericUpDown
            {
                Location = new Point(145, 93),
                Size = new Size(115, 23),
                Minimum = 1,
                Maximum = 65534,
                Value = Math.Min(65534, Math.Max(1, (int)ProfileService.SettingConfig.ServerPort))
            };

            Label usernameLabel = CreateCaption("라이더 이름", 20, 134);
            usernameTextBox = new TextBox
            {
                Location = new Point(145, 131),
                Size = new Size(255, 23),
                MaxLength = 32,
                Text = ProfileService.SettingConfig.Name
            };

            Label endpointLabel = CreateCaption("로그인 TCP", 20, 172);
            endpointValueLabel = CreateValue(string.Empty, 145, 172, 255);

            launchButton = new Button
            {
                Location = new Point(20, 211),
                Size = new Size(380, 40),
                Text = "게임 접속",
                UseVisualStyleBackColor = true
            };
            launchButton.Click += LaunchButton_Click;

            statusLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = Color.DimGray,
                Location = new Point(20, 267),
                Size = new Size(380, 24),
                Text = "게임이 종료될 때까지 접속기를 열어 두세요."
            };

            Controls.AddRange(new Control[]
            {
                buildLabel,
                buildValueLabel,
                addressLabel,
                serverAddressTextBox,
                portLabel,
                basePortInput,
                usernameLabel,
                usernameTextBox,
                endpointLabel,
                endpointValueLabel,
                launchButton,
                statusLabel
            });

            serverAddressTextBox.TextChanged += (_, _) => RefreshEndpoint();
            basePortInput.ValueChanged += (_, _) => RefreshEndpoint();
            FormClosing += ConnectorForm_FormClosing;

            processTimer = new Timer { Interval = 1000 };
            processTimer.Tick += ProcessTimer_Tick;
            processTimer.Start();

            RefreshEndpoint();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                processTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Label CreateCaption(string text, int x, int y)
        {
            return new Label
            {
                AutoSize = true,
                Location = new Point(x, y),
                Text = text
            };
        }

        private static Label CreateValue(string text, int x, int y, int width)
        {
            return new Label
            {
                AutoEllipsis = true,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129),
                Location = new Point(x, y),
                Size = new Size(width, 23),
                Text = text
            };
        }

        private void RefreshEndpoint()
        {
            try
            {
                ushort basePort = checked((ushort)basePortInput.Value);
                ushort loginPort = profile.Ports.ResolveLoginTcpPort(basePort);
                endpointValueLabel.Text = $"{serverAddressTextBox.Text.Trim()}:{loginPort}";
            }
            catch
            {
                endpointValueLabel.Text = "올바른 포트를 입력하세요.";
            }
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            string serverAddress = serverAddressTextBox.Text.Trim();
            string username = usernameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                MessageBox.Show("서버 주소를 입력하세요.", "접속 설정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                serverAddressTextBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("라이더 이름을 입력하세요.", "접속 설정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                usernameTextBox.Focus();
                return;
            }
            string addressForParsing = serverAddress.Trim('[', ']');
            if (IPAddress.TryParse(addressForParsing, out IPAddress parsedAddress) &&
                parsedAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                MessageBox.Show(
                    "분리형 5136 접속기는 IPv6 전달을 지원하지 않습니다. IPv4 서버 주소를 입력하세요.",
                    "지원하지 않는 주소",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                serverAddressTextBox.Focus();
                return;
            }
            if (IsGameRunning())
            {
                MessageBox.Show("카트라이더가 이미 실행 중입니다.", "게임 실행 중", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ushort basePort = checked((ushort)basePortInput.Value);
                profile.Ports.ResolveLoginTcpPort(basePort);

                ProfileService.SettingConfig.ServerIP = serverAddress;
                ProfileService.SettingConfig.ServerPort = basePort;
                ProfileService.SettingConfig.Name = username;
                ProfileService.SaveSettings();

                string pinFile = Path.Combine(gameDirectory, "KartRider.pin");
                string pinBackupFile = Path.Combine(gameDirectory, "KartRider-bak.pin");
                launchContext = new ClientLaunchContext(
                    gameDirectory,
                    pinFile,
                    pinBackupFile,
                    serverAddress,
                    basePort,
                    username);
                launchStrategy = ClientLaunchStrategies.For(profile);
                launchStrategy.Launch(launchContext);

                launchStartedAtUtc = DateTime.UtcNow;
                gameWasObserved = IsGameRunning();
                statusLabel.ForeColor = Color.ForestGreen;
                statusLabel.Text = gameWasObserved
                    ? "5136 클라이언트를 실행했습니다."
                    : "5136 클라이언트 시작을 확인하고 있습니다.";
            }
            catch (Exception ex)
            {
                launchStrategy?.Restore(launchContext);
                launchStrategy = null;
                launchContext = null;
                MessageBox.Show(
                    $"게임을 실행하지 못했습니다.\n{ex.Message}",
                    "접속 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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
                statusLabel.Text = "5136 클라이언트가 실행 중입니다.";
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
            launchButton.Enabled = true;
            launchButton.Text = "게임 접속";
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Text = wasObserved
                ? "게임이 종료되어 임시 접속 설정을 복원했습니다."
                : "게임이 실행 직후 종료되어 임시 접속 설정을 복원했습니다.";
            gameWasObserved = false;
        }

        private void ConnectorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            launchStrategy?.Restore(launchContext);
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
