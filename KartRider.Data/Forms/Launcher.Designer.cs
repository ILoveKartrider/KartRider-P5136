using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    partial class Launcher
    {
        private void InitializeComponent()
        {
            ServerGroup = new GroupBox();
            ServerStatusLabel = new Label();
            ServerStatusValue = new Label();
            ServerBuildLabel = new Label();
            ServerBuildValue = new Label();
            ServerEndpointLabel = new Label();
            ServerEndpointValue = new Label();
            ServerToggleButton = new Button();
            SettingButton = new Button();
            ConsoleToggleButton = new Button();
            SaveLogButton = new Button();
            LauncherVersionLabel = new Label();
            LauncherVersionValue = new Label();
            ServerGroup.SuspendLayout();
            SuspendLayout();
            // 
            // ServerGroup
            // 
            ServerGroup.Controls.Add(ServerStatusLabel);
            ServerGroup.Controls.Add(ServerStatusValue);
            ServerGroup.Controls.Add(ServerBuildLabel);
            ServerGroup.Controls.Add(ServerBuildValue);
            ServerGroup.Controls.Add(ServerEndpointLabel);
            ServerGroup.Controls.Add(ServerEndpointValue);
            ServerGroup.Location = new Point(15, 12);
            ServerGroup.Name = "ServerGroup";
            ServerGroup.Size = new Size(330, 112);
            ServerGroup.TabIndex = 0;
            ServerGroup.TabStop = false;
            ServerGroup.Text = "서버 정보";
            // 
            // ServerStatusLabel
            // 
            ServerStatusLabel.AutoSize = true;
            ServerStatusLabel.Location = new Point(14, 27);
            ServerStatusLabel.Name = "ServerStatusLabel";
            ServerStatusLabel.Size = new Size(63, 15);
            ServerStatusLabel.TabIndex = 0;
            ServerStatusLabel.Text = "서버 상태:";
            // 
            // ServerStatusValue
            // 
            ServerStatusValue.AutoSize = true;
            ServerStatusValue.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            ServerStatusValue.Location = new Point(92, 27);
            ServerStatusValue.Name = "ServerStatusValue";
            ServerStatusValue.Size = new Size(44, 15);
            ServerStatusValue.TabIndex = 1;
            ServerStatusValue.Text = "확인 중";
            // 
            // ServerBuildLabel
            // 
            ServerBuildLabel.AutoSize = true;
            ServerBuildLabel.Location = new Point(14, 54);
            ServerBuildLabel.Name = "ServerBuildLabel";
            ServerBuildLabel.Size = new Size(63, 15);
            ServerBuildLabel.TabIndex = 2;
            ServerBuildLabel.Text = "지원 빌드:";
            // 
            // ServerBuildValue
            // 
            ServerBuildValue.AutoEllipsis = true;
            ServerBuildValue.Location = new Point(92, 54);
            ServerBuildValue.Name = "ServerBuildValue";
            ServerBuildValue.Size = new Size(220, 15);
            ServerBuildValue.TabIndex = 3;
            ServerBuildValue.Text = "-";
            // 
            // ServerEndpointLabel
            // 
            ServerEndpointLabel.AutoSize = true;
            ServerEndpointLabel.Location = new Point(14, 81);
            ServerEndpointLabel.Name = "ServerEndpointLabel";
            ServerEndpointLabel.Size = new Size(63, 15);
            ServerEndpointLabel.TabIndex = 4;
            ServerEndpointLabel.Text = "수신 주소:";
            // 
            // ServerEndpointValue
            // 
            ServerEndpointValue.AutoEllipsis = true;
            ServerEndpointValue.Location = new Point(92, 81);
            ServerEndpointValue.Name = "ServerEndpointValue";
            ServerEndpointValue.Size = new Size(220, 15);
            ServerEndpointValue.TabIndex = 5;
            ServerEndpointValue.Text = "-";
            // 
            // ServerToggleButton
            // 
            ServerToggleButton.Location = new Point(15, 134);
            ServerToggleButton.Name = "ServerToggleButton";
            ServerToggleButton.Size = new Size(330, 34);
            ServerToggleButton.TabIndex = 1;
            ServerToggleButton.Text = "서버 시작";
            ServerToggleButton.UseVisualStyleBackColor = true;
            ServerToggleButton.Click += ServerToggleButton_Click;
            // 
            // SettingButton
            // 
            SettingButton.Location = new Point(15, 177);
            SettingButton.Name = "SettingButton";
            SettingButton.Size = new Size(104, 29);
            SettingButton.TabIndex = 2;
            SettingButton.Text = "서버 설정";
            SettingButton.UseVisualStyleBackColor = true;
            SettingButton.Click += SettingButton_Click;
            // 
            // ConsoleToggleButton
            // 
            ConsoleToggleButton.Location = new Point(128, 177);
            ConsoleToggleButton.Name = "ConsoleToggleButton";
            ConsoleToggleButton.Size = new Size(104, 29);
            ConsoleToggleButton.TabIndex = 3;
            ConsoleToggleButton.Text = "콘솔 열기";
            ConsoleToggleButton.UseVisualStyleBackColor = true;
            ConsoleToggleButton.Click += ConsoleToggleButton_Click;
            // 
            // SaveLogButton
            // 
            SaveLogButton.Location = new Point(241, 177);
            SaveLogButton.Name = "SaveLogButton";
            SaveLogButton.Size = new Size(104, 29);
            SaveLogButton.TabIndex = 4;
            SaveLogButton.Text = "로그 저장";
            SaveLogButton.UseVisualStyleBackColor = true;
            SaveLogButton.Click += SaveLogButton_Click;
            // 
            // LauncherVersionLabel
            // 
            LauncherVersionLabel.AutoSize = true;
            LauncherVersionLabel.ForeColor = Color.DimGray;
            LauncherVersionLabel.Location = new Point(15, 220);
            LauncherVersionLabel.Name = "LauncherVersionLabel";
            LauncherVersionLabel.Size = new Size(67, 15);
            LauncherVersionLabel.TabIndex = 5;
            LauncherVersionLabel.Text = "런처 빌드:";
            // 
            // LauncherVersionValue
            // 
            LauncherVersionValue.AutoSize = true;
            LauncherVersionValue.ForeColor = Color.DimGray;
            LauncherVersionValue.Location = new Point(88, 220);
            LauncherVersionValue.Name = "LauncherVersionValue";
            LauncherVersionValue.Size = new Size(12, 15);
            LauncherVersionValue.TabIndex = 6;
            LauncherVersionValue.Text = "-";
            // 
            // Launcher
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(360, 248);
            Controls.Add(ServerGroup);
            Controls.Add(ServerToggleButton);
            Controls.Add(SettingButton);
            Controls.Add(ConsoleToggleButton);
            Controls.Add(SaveLogButton);
            Controls.Add(LauncherVersionLabel);
            Controls.Add(LauncherVersionValue);
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            MaximizeBox = false;
            Name = "Launcher";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "카트라이더 서버 런처";
            FormClosing += OnFormClosing;
            Load += OnLoad;
            ServerGroup.ResumeLayout(false);
            ServerGroup.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private GroupBox ServerGroup;
        private Label ServerStatusLabel;
        private Label ServerStatusValue;
        private Label ServerBuildLabel;
        private Label ServerBuildValue;
        private Label ServerEndpointLabel;
        private Label ServerEndpointValue;
        private Button ServerToggleButton;
        private Button SettingButton;
        private Button ConsoleToggleButton;
        private Button SaveLogButton;
        private Label LauncherVersionLabel;
        private Label LauncherVersionValue;
    }
}
