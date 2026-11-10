using System;
using System.Drawing;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class SettingsForm : Form
    {
        private readonly TextBox _shared = new TextBox();
        private readonly TextBox _exe = new TextBox();
        private readonly TextBox _master = new TextBox();
        private readonly TextBox _ele = new TextBox();
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
            ClientSize = new Size(620, 360);

            var lblShared = new Label { Text = "共享数据目录", Location = new Point(12, 18), AutoSize = true };
            _shared.Location = new Point(110, 14);
            _shared.Width = 400;
            var browseShared = new Button { Text = "浏览...", Location = new Point(516, 12), Width = 80 };
            browseShared.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择共享数据目录（多机共用，建议 UNC 路径，如 \\\\服务器名\\共享文件夹名）";
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

            var lblMaster = new Label { Text = "互查主表", Location = new Point(12, 138), AutoSize = true };
            _master.Location = new Point(110, 134);
            _master.Width = 400;
            var browseMaster = new Button { Text = "浏览...", Location = new Point(516, 132), Width = 80 };
            browseMaster.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Excel 工作簿|*.xls;*.xlsx|所有文件|*.*";
                    dlg.Title = "选择模号 / JOB No. 互查主表";
                    dlg.FileName = _master.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK) _master.Text = dlg.FileName;
                }
            };
            var hintMaster = new Label
            {
                Text = "模号/JOB No.互查的主表文件（A列模号、D列工单号）；未配置则互查不可用。",
                Location = new Point(110, 160),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            var lblEle = new Label { Text = "电极根目录", Location = new Point(12, 198), AutoSize = true };
            _ele.Location = new Point(110, 194);
            _ele.Width = 400;
            var browseEle = new Button { Text = "浏览...", Location = new Point(516, 192), Width = 80 };
            browseEle.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择电极文件夹根目录（其下按模号规则分子目录）";
                    if (dlg.ShowDialog(this) == DialogResult.OK) _ele.Text = dlg.SelectedPath;
                }
            };
            var hintEle = new Label
            {
                Text = "「打开电极文件夹」功能的根目录；未配置则该功能提示先配置。",
                Location = new Point(110, 220),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            var lblMachine = new Label { Text = "机器名", Location = new Point(12, 258), AutoSize = true };
            _machine.Location = new Point(110, 254);
            _machine.Width = 200;
            var hintMachine = new Label
            {
                Text = "COM 打开所用 CMM 机器名，默认 CMM1",
                Location = new Point(110, 282),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            var ok = new Button { Text = "保存", DialogResult = DialogResult.OK, Location = new Point(434, 316), Width = 80 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(520, 316), Width = 80 };
            ok.Click += (s, e) =>
            {
                Result = new AppSettings
                {
                    SharedDataDir = _shared.Text.Trim(),
                    PcdmisExePath = _exe.Text.Trim(),
                    MasterXlsPath = _master.Text.Trim(),
                    CncRootPath = _ele.Text.Trim(),
                    UserName = current.UserName,
                    MachineName = string.IsNullOrWhiteSpace(_machine.Text) ? "CMM1" : _machine.Text.Trim()
                };
            };

            _shared.Text = current.SharedDataDir;
            _exe.Text = current.PcdmisExePath;
            _master.Text = current.MasterXlsPath;
            _ele.Text = current.CncRootPath;
            _machine.Text = string.IsNullOrWhiteSpace(current.MachineName) ? "CMM1" : current.MachineName;

            Controls.AddRange(new Control[] {
                lblShared, _shared, browseShared, hintShared,
                lblExe, _exe, browseExe, hintExe,
                lblMaster, _master, browseMaster, hintMaster,
                lblEle, _ele, browseEle, hintEle,
                lblMachine, _machine, hintMachine, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}