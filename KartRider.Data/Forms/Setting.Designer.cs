using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    partial class Setting
    {
        private void InitializeComponent()
        {
            PlayerName = new TextBox();
            ServerIP = new TextBox();
            ServerPort = new TextBox();
            Version_comboBox = new ComboBox();
            Speed_comboBox = new ComboBox();
            AiSpeed_comboBox = new ComboBox();
            SoloRank = new CheckBox();
            EnableMod = new CheckBox();
            Name_label = new Label();
            IP_label = new Label();
            Port_label = new Label();
            Version_label = new Label();
            Speed_label = new Label();
            AiSpeed_label = new Label();
            PhysicsNote_label = new Label();
            SuspendLayout();

            Name_label.AutoSize = true;
            Name_label.Location = new Point(20, 24);
            Name_label.Text = "기본 계정";
            PlayerName.Location = new Point(135, 20);
            PlayerName.Size = new Size(205, 23);

            Version_label.AutoSize = true;
            Version_label.Location = new Point(20, 65);
            Version_label.Text = "물리 프리셋";
            Version_comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            Version_comboBox.Location = new Point(135, 61);
            Version_comboBox.Size = new Size(205, 23);

            Speed_label.AutoSize = true;
            Speed_label.Location = new Point(20, 101);
            Speed_label.Text = "속도 등급";
            Speed_comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            Speed_comboBox.Location = new Point(135, 97);
            Speed_comboBox.Size = new Size(205, 23);

            AiSpeed_label.AutoSize = true;
            AiSpeed_label.Location = new Point(20, 137);
            AiSpeed_label.Text = "AI 난이도";
            AiSpeed_comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            AiSpeed_comboBox.Location = new Point(135, 133);
            AiSpeed_comboBox.Size = new Size(205, 23);

            SoloRank.AutoSize = true;
            SoloRank.Location = new Point(135, 172);
            SoloRank.Text = "솔로 랭킹 사용";
            EnableMod.AutoSize = true;
            EnableMod.Location = new Point(255, 172);
            EnableMod.Text = "MOD 사용";

            PhysicsNote_label.AutoSize = true;
            PhysicsNote_label.ForeColor = Color.DimGray;
            PhysicsNote_label.Location = new Point(20, 207);
            PhysicsNote_label.MaximumSize = new Size(340, 0);

            // Retained only so old Profile/Settings.json values can round-trip.
            // Network controls moved to the P236-style server launcher.
            ServerIP.Visible = false;
            ServerPort.Visible = false;
            IP_label.Visible = false;
            Port_label.Visible = false;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(380, 270);
            Controls.Add(PlayerName);
            Controls.Add(Name_label);
            Controls.Add(Version_comboBox);
            Controls.Add(Version_label);
            Controls.Add(Speed_comboBox);
            Controls.Add(Speed_label);
            Controls.Add(AiSpeed_comboBox);
            Controls.Add(AiSpeed_label);
            Controls.Add(SoloRank);
            Controls.Add(EnableMod);
            Controls.Add(PhysicsNote_label);
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Setting";
            StartPosition = FormStartPosition.CenterParent;
            Text = "게임 설정";
            FormClosing += OnGameSettingsClosing;
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
