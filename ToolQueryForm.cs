using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class ToolQueryForm : Form
    {
        private readonly TextBox _tool = new TextBox();
        private readonly TextBox _job = new TextBox();
        private readonly TextBox _box = new TextBox();
        private readonly Label _status = new Label();
        private readonly Action<string> _setSearch;

        public ToolQueryForm(Action<string> setSearch)
        {
            _setSearch = setSearch;

            Text = "模号 / JOB No.查询 · 电极文件夹";
            Font = Theme.UiFont();
            BackColor = Theme.Panel;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 280);

            var lblTool = new Label { Text = "模号", Location = new Point(12, 18), AutoSize = true };
            _tool.Location = new Point(90, 14);
            _tool.Width = 320;

            var lblJob = new Label { Text = "JOB No.", Location = new Point(12, 52), AutoSize = true };
            _job.Location = new Point(90, 48);
            _job.Width = 320;

            var lookup = new Button { Text = "互查（模号 ↔ JOB No.）", Location = new Point(90, 82), Width = 220, Height = 30 };
            lookup.Click += (s, e) => DoLookup();
            var search = new Button { Text = "搜索模号", Location = new Point(320, 82), Width = 110, Height = 30 };
            search.Click += (s, e) => SearchTool();

            var lblBox = new Label { Text = "第几盒", Location = new Point(12, 126), AutoSize = true };
            _box.Location = new Point(90, 122);
            _box.Width = 80;
            _box.Text = "1";
            var ele = new Button { Text = "打开电极文件夹", Location = new Point(180, 120), Width = 150, Height = 30 };
            ele.Click += (s, e) => OpenEle();

            _status.Location = new Point(12, 162);
            _status.Size = new Size(516, 70);
            _status.ForeColor = Theme.Muted;
            _status.Text = "填模号或 JOB No. 点「互查」；「搜索模号」把模号填入主界面搜索框；打开电极文件夹需模号与第几盒。";

            var close = new Button { Text = "关闭", DialogResult = DialogResult.Cancel, Location = new Point(450, 244), Width = 78 };

            Controls.AddRange(new Control[] { lblTool, _tool, lblJob, _job, lookup, search, lblBox, _box, ele, _status, close });
            CancelButton = close;
        }

        private void DoLookup()
        {
            string tool = _tool.Text.Trim();
            string job = _job.Text.Trim();
            if (tool.Length == 0 && job.Length == 0) { _status.Text = "请先输入模号或 JOB No."; return; }
            try
            {
                var r = ExcelLookup.Query(tool, job);
                if (r.ToolFound) _tool.Text = r.Tool;
                if (r.JobFound) _job.Text = r.Job;
                bool any = r.ToolFound || r.JobFound;
                _status.ForeColor = any ? Theme.Text : Theme.Muted;
                _status.Text = any
                    ? "查询成功：" + (r.ToolFound ? "JOB No.→模号 " : "") + (r.JobFound ? "模号→JOB No." : "")
                    : "主表中未找到匹配项（已忽略空格与大小写）。";
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "查询失败：" + ex.Message;
            }
        }

        // 仅把模号填入主界面搜索框，由搜索框过滤当前列表，不做跳转
        private void SearchTool()
        {
            string tool = _tool.Text.Trim();
            if (tool.Length == 0) { _status.Text = "请先填写模号。"; return; }
            if (_setSearch != null) _setSearch(tool);
            _status.ForeColor = Theme.Text;
            _status.Text = "已将模号填入搜索框：" + tool;
        }

        private void OpenEle()
        {
            string tool = _tool.Text.Trim();
            string box = _box.Text.Trim();
            if (tool.Length == 0) { _status.Text = "请先填写或查询出模号。"; return; }
            if (box.Length == 0) { _status.Text = "请填写第几盒。"; return; }
            string path = ExcelLookup.BuildElePath(tool, box);
            if (path == null) { _status.Text = "模号格式不足以生成路径（至少 5 位）。"; return; }
            if (!Directory.Exists(path)) { _status.Text = "文件夹不存在：" + path; return; }
            try
            {
                Process.Start("explorer.exe", "\"" + path + "\"");
                _status.ForeColor = Theme.Text;
                _status.Text = "已打开：" + path;
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "打开失败：" + ex.Message;
            }
        }
    }
}