using System;
using System.Drawing;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class SettingsForm : Form
    {
        private readonly TextBox _shared = new TextBox();
        private readonly TextBox _exe = new TextBox();
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _machine = new TextBox();

        public AppSettings Result { get; private set; }

        public SettingsForm(AppSettings current)
        {
            Text = "设置";
            Font = Theme.UiFont();
            BackColor = Theme.Panel;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 240);

            var lblShared = new Label { Text = "共享数据目录", Location = new Point(12, 18), AutoSize = true };
            _shared.Location = new Point(110, 14);
            _shared.Width = 400;
            var browseShared = new Button { Text = "浏览...", Location = new Point(516, 12), Width = 80 };
            browseShared.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择共享数据目录（多机共用，建议 UNC 路径，如 \\\\PC-01\\MeasureLib）";
                    if (dlg.ShowDialog(this) == DialogResult.OK) _shared.Text = dlg.SelectedPath;
                }
            };
            var hintShared = new Label
            {
                Text = "存放标签与备注，多台电脑填同一个共享路径；留空则仅本机使用。",
                Location = new Point(110, 40),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            var lblExe = new Label { Text = "PC-DMIS 程序", Location = new Point(12, 78), AutoSize = true };
            _exe.Location = new Point(110, 74);
            _exe.Width = 400;
            var browseExe = new Button { Text = "浏览...", Location = new Point(516, 72), Width = 80 };
            browseExe.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "PC-DMIS 可执行文件|*.exe|所有文件|*.*";
                    dlg.Title = "选择 PC-DMIS 2015.1 主程序（如 PCDLRN.exe）";
                    if (dlg.ShowDialog(this) == DialogResult.OK) _exe.Text = dlg.FileName;
                }
            };
            var hintExe = new Label
            {
                Text = "指向 PC-DMIS 2015.1 主程序；留空则按 .PRG 文件关联打开。",
                Location = new Point(110, 100),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            var lblUser = new Label { Text = "当前使用人", Location = new Point(12, 138), AutoSize = true };
            _user.Location = new Point(110, 134);
            _user.Width = 200;

            var lblMachine = new Label { Text = "机器名", Location = new Point(330, 138), AutoSize = true };
            _machine.Location = new Point(384, 134);
            _machine.Width = 126;
            var hintMachine = new Label
            {
                Text = "COM 打开所用 CMM 机器名，默认 CMM1",
                Location = new Point(110, 162),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            var ok = new Button { Text = "保存", DialogResult = DialogResult.OK, Location = new Point(434, 196), Width = 80 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(520, 196), Width = 80 };
            ok.Click += (s, e) =>
            {
                Result = new AppSettings
                {
                    SharedDataDir = _shared.Text.Trim(),
                    PcdmisExePath = _exe.Text.Trim(),
                    UserName = string.IsNullOrWhiteSpace(_user.Text) ? Environment.UserName : _user.Text.Trim(),
                    MachineName = string.IsNullOrWhiteSpace(_machine.Text) ? "CMM1" : _machine.Text.Trim()
                };
            };

            _shared.Text = current.SharedDataDir;
            _exe.Text = current.PcdmisExePath;
            _user.Text = current.UserName;
            _machine.Text = string.IsNullOrWhiteSpace(current.MachineName) ? "CMM1" : current.MachineName;

            Controls.AddRange(new Control[] {
                lblShared, _shared, browseShared, hintShared,
                lblExe, _exe, browseExe, hintExe,
                lblUser, _user, lblMachine, _machine, hintMachine, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}