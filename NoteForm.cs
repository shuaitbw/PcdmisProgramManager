using System;
using System.Drawing;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class NoteForm : Form
    {
        private readonly TextBox _text = new TextBox();
        private readonly TextBox _author = new TextBox();

        public string NoteText => _text.Text;
        public string Author => _author.Text.Trim();

        public NoteForm(string programName, string currentNote, string currentAuthor)
        {
            Text = "备注 - " + programName;
            Font = Theme.UiFont();
            BackColor = Theme.Panel;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(480, 320);
            Size = new Size(560, 380);
            MaximizeBox = false;

            var lbl = new Label
            {
                Text = "为「" + programName + "」填写备注说明（共享给所有电脑）：",
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _text.Multiline = true;
            _text.ScrollBars = ScrollBars.Vertical;
            _text.Dock = DockStyle.Fill;
            _text.Text = currentNote ?? "";
            _text.AcceptsReturn = true;

            var authorPanel = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 6, 0, 0) };
            var lblAuthor = new Label { Text = "备注人：", Location = new Point(2, 10), AutoSize = true };
            _author.Location = new Point(72, 6);
            _author.Width = 220;
            _author.Text = string.IsNullOrWhiteSpace(currentAuthor) ? Environment.UserName : currentAuthor;
            authorPanel.Controls.Add(lblAuthor);
            authorPanel.Controls.Add(_author);

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(8)
            };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
            var ok = new Button { Text = "保存", DialogResult = DialogResult.OK, Width = 80 };
            panel.Controls.Add(cancel);
            panel.Controls.Add(ok);

            Controls.Add(_text);
            Controls.Add(panel);
            Controls.Add(authorPanel);
            Controls.Add(lbl);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}