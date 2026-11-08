using System;
using System.Drawing;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class NoteForm : Form
    {
        private readonly TextBox _text = new TextBox();

        public string NoteText => _text.Text;

        public NoteForm(string programName, string currentNote)
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
                Text = "为程序「" + programName + "」填写备注说明（共享给所有电脑）：",
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _text.Multiline = true;
            _text.ScrollBars = ScrollBars.Vertical;
            _text.Dock = DockStyle.Fill;
            _text.Text = currentNote ?? "";
            _text.AcceptsReturn = true;

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
            Controls.Add(lbl);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}