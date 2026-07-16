using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    partial class Setting
    {
        private void InitializeComponent()
        {
            AiSpeed_comboBox = new ComboBox();
            AiSpeed_label = new Label();
            Speed_comboBox = new ComboBox();
            Speed_label = new Label();
            Version_comboBox = new ComboBox();
            Version_label = new Label();
            PlayerName = new TextBox();
            Name_label = new Label();
            ServerIP = new TextBox();
            IP_label = new Label();
            ServerPort = new TextBox();
            Port_label = new Label();
            SoloRank = new CheckBox();
            EnableMod = new CheckBox();
            PhysicsNote_label = new Label();
            SuspendLayout();
            // 
            // PlayerName
            // 
            PlayerName.Location = new System.Drawing.Point(86, 20);
            PlayerName.Name = "PlayerName";
            PlayerName.Size = new System.Drawing.Size(124, 23);
            PlayerName.TabIndex = 1;
            // 
            // Name_label
            // 
            Name_label.AutoSize = true;
            Name_label.ForeColor = System.Drawing.Color.Blue;
            Name_label.Location = new System.Drawing.Point(19, 24);
            Name_label.Name = "Name_label";
            Name_label.Size = new System.Drawing.Size(30, 12);
            Name_label.Text = "기본 계정:";
            // 
            // ServerIP
            // 
            ServerIP.Location = new System.Drawing.Point(86, 49);
            ServerIP.Name = "ServerIP";
            ServerIP.Size = new System.Drawing.Size(124, 23);
            ServerIP.TabIndex = 2;
            ServerIP.Text = "127.0.0.1";
            // 
            // IP_label
            // 
            IP_label.AutoSize = true;
            IP_label.ForeColor = System.Drawing.Color.Blue;
            IP_label.Location = new System.Drawing.Point(19, 53);
            IP_label.Name = "IP_label";
            IP_label.Size = new System.Drawing.Size(30, 12);
            IP_label.Text = "바인드 IP:";
            // 
            // ServerPort
            // 
            ServerPort.Location = new System.Drawing.Point(86, 78);
            ServerPort.Name = "ServerPort";
            ServerPort.Size = new System.Drawing.Size(124, 23);
            ServerPort.TabIndex = 3;
            ServerPort.Text = "39311";
            // 
            // Port_label
            // 
            Port_label.AutoSize = true;
            Port_label.ForeColor = System.Drawing.Color.Blue;
            Port_label.Location = new System.Drawing.Point(19, 82);
            Port_label.Name = "Port_label";
            Port_label.Size = new System.Drawing.Size(30, 12);
            Port_label.Text = "기준 포트:";
            // 
            // Version_comboBox
            // 
            Version_comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            Version_comboBox.ForeColor = System.Drawing.Color.Red;
            Version_comboBox.FormattingEnabled = true;
            Version_comboBox.Location = new System.Drawing.Point(105, 107);
            Version_comboBox.Name = "Version_comboBox";
            Version_comboBox.Size = new System.Drawing.Size(105, 23);
            Version_comboBox.TabIndex = 4;
            // 
            // Version_label
            // 
            Version_label.AutoSize = true;
            Version_label.ForeColor = System.Drawing.Color.Blue;
            Version_label.Location = new System.Drawing.Point(19, 111);
            Version_label.Name = "Version_label";
            Version_label.Size = new System.Drawing.Size(30, 12);
            Version_label.Text = "물리 프리셋:";
            // 
            // Speed_comboBox
            // 
            Speed_comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            Speed_comboBox.ForeColor = System.Drawing.Color.Red;
            Speed_comboBox.FormattingEnabled = true;
            Speed_comboBox.Location = new System.Drawing.Point(105, 136);
            Speed_comboBox.Name = "Speed_comboBox";
            Speed_comboBox.Size = new System.Drawing.Size(105, 23);
            Speed_comboBox.TabIndex = 5;
            // 
            // Speed_label
            // 
            Speed_label.AutoSize = true;
            Speed_label.ForeColor = System.Drawing.Color.Blue;
            Speed_label.Location = new System.Drawing.Point(19, 140);
            Speed_label.Name = "Speed_label";
            Speed_label.Size = new System.Drawing.Size(30, 12);
            Speed_label.Text = "속도 등급:";
            // 
            // AiSpeed_comboBox
            // 
            AiSpeed_comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            AiSpeed_comboBox.ForeColor = System.Drawing.Color.Red;
            AiSpeed_comboBox.FormattingEnabled = true;
            AiSpeed_comboBox.Location = new System.Drawing.Point(86, 165);
            AiSpeed_comboBox.Name = "AiSpeed_comboBox";
            AiSpeed_comboBox.Size = new System.Drawing.Size(124, 23);
            AiSpeed_comboBox.TabIndex = 6;
            // 
            // AiSpeed_label
            // 
            AiSpeed_label.AutoSize = true;
            AiSpeed_label.ForeColor = System.Drawing.Color.Blue;
            AiSpeed_label.Location = new System.Drawing.Point(19, 169);
            AiSpeed_label.Name = "AiSpeed_label";
            AiSpeed_label.Size = new System.Drawing.Size(30, 12);
            AiSpeed_label.Text = "AI 난이도:";
            // 
            // SoloRank
            // 
            SoloRank.AutoSize = true;
            SoloRank.ForeColor = System.Drawing.Color.Blue;
            SoloRank.Location = new System.Drawing.Point(225, 140);
            SoloRank.Name = "SoloRank";
            SoloRank.Size = new System.Drawing.Size(52, 16);
            SoloRank.TabIndex = 7;
            SoloRank.Text = "솔로 랭킹";
            SoloRank.UseVisualStyleBackColor = true;
            // 
            // EnableMod
            // 
            EnableMod.AutoSize = true;
            EnableMod.ForeColor = System.Drawing.Color.Blue;
            EnableMod.Location = new System.Drawing.Point(225, 169);
            EnableMod.Name = "EnableMod";
            EnableMod.Size = new System.Drawing.Size(52, 16);
            EnableMod.TabIndex = 8;
            EnableMod.Text = "MOD 사용";
            EnableMod.UseVisualStyleBackColor = true;
            // 
            // PhysicsNote_label
            // 
            PhysicsNote_label.AutoSize = true;
            PhysicsNote_label.ForeColor = System.Drawing.Color.DimGray;
            PhysicsNote_label.Location = new System.Drawing.Point(19, 202);
            PhysicsNote_label.MaximumSize = new System.Drawing.Size(312, 0);
            PhysicsNote_label.Name = "PhysicsNote_label";
            PhysicsNote_label.Size = new System.Drawing.Size(0, 15);
            PhysicsNote_label.TabIndex = 9;
            // 
            // Setting
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new System.Drawing.Size(350, 240);
            Controls.Add(PlayerName);
            Controls.Add(Name_label);
            Controls.Add(ServerIP);
            Controls.Add(IP_label);
            Controls.Add(ServerPort);
            Controls.Add(Port_label);
            Controls.Add(Speed_comboBox);
            Controls.Add(Speed_label);
            Controls.Add(Version_comboBox);
            Controls.Add(Version_label);
            Controls.Add(AiSpeed_comboBox);
            Controls.Add(AiSpeed_label);
            Controls.Add(SoloRank);
            Controls.Add(EnableMod);
            Controls.Add(PhysicsNote_label);
            Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 129);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Setting";
            StartPosition = FormStartPosition.CenterParent;
            Text = "서버 설정";
            FormClosing += OnFormClosing;
            Load += OnLoad;
            ResumeLayout(false);
            PerformLayout();
        }

        private TextBox PlayerName;
        private TextBox ServerIP;
        private TextBox ServerPort;
        private ComboBox Speed_comboBox;
        private ComboBox Version_comboBox;
        private ComboBox AiSpeed_comboBox;
        private CheckBox SoloRank;
        private CheckBox EnableMod;
        private Label Name_label;
        private Label IP_label;
        private Label Port_label;
        private Label Speed_label;
        private Label Version_label;
        private Label AiSpeed_label;
        private Label PhysicsNote_label;
    }
}
