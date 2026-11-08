using System.Drawing;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentLight = Color.FromArgb(232, 241, 251);
        public static readonly Color Navy = Color.FromArgb(13, 27, 42);
        public static readonly Color NavyLight = Color.FromArgb(27, 42, 74);
        public static readonly Color SidebarText = Color.FromArgb(184, 196, 217);
        public static readonly Color SelectBlue = Color.FromArgb(37, 99, 235);
        public static readonly Color Bg = Color.FromArgb(243, 244, 246);
        public static readonly Color Panel = Color.White;
        public static readonly Color Text = Color.FromArgb(31, 41, 55);
        public static readonly Color Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color Border = Color.FromArgb(229, 231, 235);
        public static readonly Color Hover = Color.FromArgb(232, 241, 251);

        public static Font UiFont(float size = 9F)
        {
            return new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static void StyleToolStrip(ToolStrip ts)
        {
            ts.GripStyle = ToolStripGripStyle.Hidden;
            ts.BackColor = Panel;
            ts.ForeColor = Text;
            ts.Font = UiFont();
            ts.Renderer = new ModernRenderer();
            ts.Padding = new Padding(6, 3, 6, 3);
        }
    }

    public class ModernRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(e.ToolStrip.BackColor))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var btn = e.Item as ToolStripButton;
            if (btn == null) return;
            if (btn.Selected || btn.Pressed || btn.Checked)
            {
                Color c = btn.Pressed || btn.Checked ? Theme.AccentLight : Theme.Hover;
                using (var b = new SolidBrush(c))
                    e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var p = new Pen(Theme.Border))
            {
                int x = e.Item.Width / 2;
                e.Graphics.DrawLine(p, x, 4, x, e.Item.Height - 4);
            }
        }
    }
}