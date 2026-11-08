using System;
using System.Drawing;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class TabEditForm : Form
    {
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _path = new TextBox();
        private readonly CheckBox _recursive = new CheckBox();

        public TabItem Result { get; private set; }

        public TabEditForm(TabItem existing)
        {
            Text = existing == null ? "新增标签" : "编辑标签";
            Font = Theme.UiFont();
            BackColor = Theme.Panel;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 170);

            var lblName = new Label { Text = "标签名称", Location = new Point(12, 18), AutoSize = true };
            _name.Location = new Point(90, 14);
            _name.Width = 440;

            var lblPath = new Label { Text = "文件夹路径", Location = new Point(12, 52), AutoSize = true };
            _path.Location = new Point(90, 48);
            _path.Width = 360;
            var browse = new Button { Text = "浏览...", Location = new Point(456, 46), Width = 74 };
            browse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择要快速访问的程序文件夹（多机请用 UNC 共享路径）";
                    if (dlg.ShowDialog(this) == DialogResult.OK) _path.Text = dlg.SelectedPath;
                }
            };

            _recursive.Text = "递归扫描子文件夹";
            _recursive.Location = new Point(90, 84);
            _recursive.AutoSize = true;

            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(374, 126), Width = 80 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(460, 126), Width = 80 };
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_path.Text))
                {
                    MessageBox.Show(this, "名称和路径不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                Result = new TabItem
                {
                    Id = existing != null ? existing.Id : Guid.NewGuid().ToString("N"),
                    Name = _name.Text.Trim(),
                    Path = _path.Text.Trim(),
                    Recursive = _recursive.Checked
                };
            };

            if (existing != null)
            {
                _name.Text = existing.Name;
                _path.Text = existing.Path;
                _recursive.Checked = existing.Recursive;
            }

            Controls.AddRange(new Control[] { lblName, _name, lblPath, _path, browse, _recursive, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}