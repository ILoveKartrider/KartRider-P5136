using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    partial class Launcher
    {
        private void InitializeComponent()
        {
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(980, 720);
            MinimumSize = new Size(900, 650);
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            Name = "Launcher";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "KartRider P5136 서버 런처";
            Load += OnLoad;
        }
    }
}
