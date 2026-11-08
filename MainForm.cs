using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    public class MainForm : Form
    {
        private class ListEntry
        {
            public string Path;
            public bool IsDir;
            public DateTime Time;
            public string Note;
        }

        private readonly SettingsStore _settingsStore = new SettingsStore();
        private AppSettings _settings;
        private SharedStore _store;
        private List<TabItem> _tabs = new List<TabItem>();

        private readonly Panel _header = new Panel();
        private readonly ToolStrip _toolbar = new ToolStrip();
        private readonly TextBox _search = new TextBox();
        private readonly Panel _sidebar = new Panel();
        private readonly ListBox _tabList = new ListBox();
        private readonly ToolStrip _nav = new ToolStrip();
        private readonly ToolStripTextBox _address = new ToolStripTextBox();
        private readonly ToolStripButton _back = new ToolStripButton("←");
        private readonly ToolStripButton _up = new ToolStripButton("↑");
        private readonly ToolStripButton _forward = new ToolStripButton("→");
        private readonly Panel _main = new Panel();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly ListView _list = new ListView();
        private readonly ImageList _icons = new ImageList();
        private readonly Panel _detail = new Panel();
        private readonly Label _dName = new Label();
        private readonly Label _dPath = new Label();
        private readonly Label _dTime = new Label();
        private readonly TextBox _dNote = new TextBox();
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _lblPath = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _lblCount = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _lblShared = new ToolStripStatusLabel();

        private string _currentPath = "";
        private readonly List<string> _backStack = new List<string>();
        private readonly List<string> _forwardStack = new List<string>();

        public MainForm()
        {
            Text = "PC-DMIS 测量程序浏览器";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 700);
            MinimumSize = new Size(960, 520);
            Font = Theme.UiFont();
            BackColor = Theme.Bg;
            using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PcdmisProgramManager.app.ico"))
                if (s != null) Icon = new Icon(s);

            InitIcons();
            BuildHeader();
            BuildToolbar();
            BuildSidebar();
            BuildNav();
            BuildMain();
            BuildStatus();

            Controls.Add(_main);
            Controls.Add(_sidebar);
            Controls.Add(_status);
            Controls.Add(_toolbar);
            Controls.Add(_header);

            _settings = _settingsStore.Load();
            _store = new SharedStore(_settings.SharedDataDir);
            _tabs = _store.LoadTabs();
            RebuildTabs(null);
            UpdateSharedLabel();

            Shown += (s, e) =>
            {
                try { _split.SplitterDistance = Math.Max(300, _main.Width - 300); }
                catch { }
            };
        }

        private static bool IsPrg(string path)
        {
            return string.Equals(Path.GetExtension(path), ".prg", StringComparison.OrdinalIgnoreCase);
        }

        private void InitIcons()
        {
            _icons.ColorDepth = ColorDepth.Depth32Bit;
            _icons.ImageSize = new Size(16, 16);
            var f = ShellIcons.FolderSmall();
            var p = ShellIcons.PrgSmall();
            if (f != null) _icons.Images.Add("folder", f);
            if (p != null) _icons.Images.Add("prg", p);
        }

        private void BuildHeader()
        {
            _header.Dock = DockStyle.Top;
            _header.Height = 48;
            _header.BackColor = Theme.Navy;
            var title = new Label
            {
                Text = "PC-DMIS 测量程序浏览器",
                ForeColor = Color.White,
                Font = Theme.UiFont(12F),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            var sub = new Label
            {
                Text = "多机共享 · 标签快速访问",
                ForeColor = Theme.SidebarText,
                Font = Theme.UiFont(8.5F),
                AutoSize = true,
                Location = new Point(200, 18)
            };

            var searchLbl = new Label
            {
                Text = "搜索",
                ForeColor = Theme.SidebarText,
                Font = Theme.UiFont(9F),
                AutoSize = true
            };
            _search.Width = 260;
            _search.Height = 26;
            _search.Font = Theme.UiFont(9.5F);
            _search.BorderStyle = BorderStyle.FixedSingle;
            _search.TextChanged += (s, e) => PopulateList();

            _header.Controls.Add(title);
            _header.Controls.Add(sub);
            _header.Controls.Add(searchLbl);
            _header.Controls.Add(_search);
            _header.Resize += (s, e) =>
            {
                _search.Left = _header.Width - _search.Width - 16;
                _search.Top = (_header.Height - _search.Height) / 2;
                searchLbl.Left = _search.Left - 40;
                searchLbl.Top = (_header.Height - searchLbl.Height) / 2;
            };
        }

        private void BuildToolbar()
        {
            Theme.StyleToolStrip(_toolbar);
            _toolbar.Dock = DockStyle.Top;

            var open = new ToolStripButton("打开程序");
            open.Click += (s, e) => OpenSelected();
            var note = new ToolStripButton("备注");
            note.Click += (s, e) => EditNote();
            var refresh = new ToolStripButton("刷新");
            refresh.Click += (s, e) => PopulateList();
            var settings = new ToolStripButton("设置");
            settings.Click += (s, e) => OpenSettings();

            _toolbar.Items.AddRange(new ToolStripItem[] {
                open, note, refresh, new ToolStripSeparator(), settings });
        }

        private void BuildSidebar()
        {
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 210;
            _sidebar.BackColor = Theme.Navy;

            var title = new Label
            {
                Text = "标签 · 快速访问",
                Dock = DockStyle.Top,
                Height = 40,
                ForeColor = Theme.SidebarText,
                Font = Theme.UiFont(9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };

            _tabList.Dock = DockStyle.Fill;
            _tabList.BorderStyle = BorderStyle.None;
            _tabList.BackColor = Theme.Navy;
            _tabList.ForeColor = Theme.SidebarText;
            _tabList.DrawMode = DrawMode.OwnerDrawFixed;
            _tabList.ItemHeight = 36;
            _tabList.Font = Theme.UiFont(9.5F);
            _tabList.DrawItem += TabListDrawItem;
            _tabList.SelectedIndexChanged += (s, e) =>
            {
                var t = CurrentTab;
                if (t != null) { _backStack.Clear(); _forwardStack.Clear(); Navigate(t.Path, false); }
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Theme.Navy,
                Padding = new Padding(10, 6, 10, 6)
            };
            var add = SideButton("新增");
            add.Click += (s, e) => AddTab();
            var edit = SideButton("编辑");
            edit.Click += (s, e) => EditTab();
            var del = SideButton("删除");
            del.Click += (s, e) => RemoveTab();
            buttons.Controls.Add(add);
            buttons.Controls.Add(edit);
            buttons.Controls.Add(del);

            _sidebar.Controls.Add(_tabList);
            _sidebar.Controls.Add(buttons);
            _sidebar.Controls.Add(title);
        }

        private Button SideButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 60,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.NavyLight,
                ForeColor = Theme.SidebarText,
                Font = Theme.UiFont(8.5F),
                FlatAppearance = { BorderSize = 0 }
            };
        }

        private void TabListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = e.Index == _tabList.SelectedIndex;
            using (var b = new SolidBrush(selected ? Theme.SelectBlue : Theme.Navy))
                e.Graphics.FillRectangle(b, e.Bounds);
            var rect = new Rectangle(e.Bounds.Left + 14, e.Bounds.Top, e.Bounds.Width - 20, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, _tabList.Items[e.Index].ToString(), _tabList.Font, rect,
                selected ? Color.White : Theme.SidebarText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void BuildNav()
        {
            Theme.StyleToolStrip(_nav);
            _nav.Dock = DockStyle.Top;
            _back.Click += (s, e) => GoBack();
            _up.Click += (s, e) => GoUp();
            _forward.Click += (s, e) => GoForward();
            _address.AutoSize = false;
            _address.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Navigate(_address.Text.Trim(), true); e.SuppressKeyPress = true; }
            };
            _nav.Items.AddRange(new ToolStripItem[] { _back, _up, _forward, new ToolStripSeparator(), _address });
            _nav.Resize += (s, e) =>
            {
                int used = _back.Width + _up.Width + _forward.Width + 40;
                _address.Width = Math.Max(300, _nav.Width - used);
            };
        }

        private void BuildMain()
        {
            _main.Dock = DockStyle.Fill;
            _main.BackColor = Theme.Bg;

            _split.Dock = DockStyle.Fill;
            _split.BackColor = Theme.Border;
            _split.SplitterWidth = 5;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.Panel1.BackColor = Theme.Panel;
            _split.Panel2.BackColor = Theme.Panel;

            BuildList();
            BuildDetail();
            _split.Panel1.Controls.Add(_list);
            _split.Panel2.Controls.Add(_detail);

            _main.Controls.Add(_split);
            _main.Controls.Add(_nav);
        }

        private void BuildList()
        {
            _list.Dock = DockStyle.Fill;
            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = Theme.Panel;
            _list.ForeColor = Theme.Text;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.SmallImageList = _icons;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.Columns.Add("名称", 280);
            _list.Columns.Add("类型", 90);
            _list.Columns.Add("修改时间", 150);
            _list.Columns.Add("备注", 300);
            _list.ItemActivate += (s, e) => ActivateSelected();
            _list.SelectedIndexChanged += (s, e) => UpdateDetail();
            _list.ContextMenuStrip = BuildMenu();
        }

        private void BuildDetail()
        {
            _detail.Dock = DockStyle.Fill;
            _detail.BackColor = Theme.Panel;
            _detail.Padding = new Padding(14);

            _dName.Dock = DockStyle.Top;
            _dName.Height = 34;
            _dName.Font = Theme.UiFont(11F);
            _dName.ForeColor = Theme.Text;
            _dName.Text = "未选择程序";
            _dName.AutoEllipsis = true;

            _dPath.Dock = DockStyle.Top;
            _dPath.Height = 44;
            _dPath.ForeColor = Theme.Muted;
            _dPath.Font = Theme.UiFont(8.5F);
            _dPath.AutoEllipsis = true;

            _dTime.Dock = DockStyle.Top;
            _dTime.Height = 24;
            _dTime.ForeColor = Theme.Muted;
            _dTime.Font = Theme.UiFont(8.5F);

            var noteLbl = new Label { Text = "备注说明", Dock = DockStyle.Top, Height = 26, ForeColor = Theme.Text, Font = Theme.UiFont(9F) };

            _dNote.Dock = DockStyle.Fill;
            _dNote.Multiline = true;
            _dNote.ReadOnly = true;
            _dNote.BorderStyle = BorderStyle.None;
            _dNote.BackColor = Theme.Bg;
            _dNote.ScrollBars = ScrollBars.Vertical;

            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft };
            var btnNote = new Button { Text = "编辑备注", Width = 90, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Text };
            btnNote.FlatAppearance.BorderColor = Theme.Border;
            btnNote.Click += (s, e) => EditNote();
            var btnOpen = new Button { Text = "打开程序", Width = 90, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Theme.SelectBlue, ForeColor = Color.White };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) => OpenSelected();
            btns.Controls.Add(btnNote);
            btns.Controls.Add(btnOpen);

            _detail.Controls.Add(_dNote);
            _detail.Controls.Add(btns);
            _detail.Controls.Add(noteLbl);
            _detail.Controls.Add(_dTime);
            _detail.Controls.Add(_dPath);
            _detail.Controls.Add(_dName);
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Font = Theme.UiFont();
            var open = new ToolStripMenuItem("打开程序");
            open.Click += (s, e) => OpenSelected();
            var note = new ToolStripMenuItem("备注");
            note.Click += (s, e) => EditNote();
            var folder = new ToolStripMenuItem("打开所在文件夹");
            folder.Click += (s, e) =>
            {
                var en = SelectedEntry();
                if (en != null) Process.Start("explorer.exe", en.IsDir ? "\"" + en.Path + "\"" : "/select,\"" + en.Path + "\"");
            };
            var copy = new ToolStripMenuItem("复制路径");
            copy.Click += (s, e) => { var en = SelectedEntry(); if (en != null) Clipboard.SetText(en.Path); };
            menu.Items.AddRange(new ToolStripItem[] { open, note, new ToolStripSeparator(), folder, copy });
            return menu;
        }

        private void BuildStatus()
        {
            _status.Dock = DockStyle.Bottom;
            _status.BackColor = Theme.Panel;
            _status.Font = Theme.UiFont(8.5F);
            _lblPath.Spring = true; _lblPath.TextAlign = ContentAlignment.MiddleLeft; _lblPath.ForeColor = Theme.Muted;
            _lblCount.AutoSize = false; _lblCount.Width = 80; _lblCount.ForeColor = Theme.Muted;
            _lblShared.AutoSize = false; _lblShared.Width = 340; _lblShared.TextAlign = ContentAlignment.MiddleRight; _lblShared.ForeColor = Theme.Muted;
            _status.Items.AddRange(new ToolStripStatusLabel[] { _lblPath, _lblCount, _lblShared });
        }

        private TabItem CurrentTab
        {
            get
            {
                int i = _tabList.SelectedIndex;
                return i >= 0 && i < _tabs.Count ? _tabs[i] : null;
            }
        }

        private void RebuildTabs(string selectId)
        {
            _tabList.Items.Clear();
            foreach (var t in _tabs) _tabList.Items.Add(t.Name);
            if (_tabs.Count > 0)
            {
                int idx = _tabs.FindIndex(x => x.Id == selectId);
                _tabList.SelectedIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                _currentPath = "";
                _list.Items.Clear();
                _lblPath.Text = "暂无标签，请点左下「新增」绑定一个程序根目录。";
            }
        }

        // ---- 导航 ----
        private void Navigate(string path, bool push)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim();
            if (!Directory.Exists(path)) { _lblPath.Text = path + "（不存在）"; return; }
            if (push && _currentPath.Length > 0 && _currentPath != path)
            { _backStack.Add(_currentPath); _forwardStack.Clear(); }
            _currentPath = path;
            _address.Text = path;
            PopulateList();
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            _back.Enabled = _backStack.Count > 0;
            _forward.Enabled = _forwardStack.Count > 0;
            var root = CurrentTab == null ? "" : CurrentTab.Path;
            _up.Enabled = _currentPath.Length > 0 &&
                !string.Equals(_currentPath.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private void GoBack()
        {
            if (_backStack.Count == 0) return;
            string p = _backStack[_backStack.Count - 1]; _backStack.RemoveAt(_backStack.Count - 1);
            _forwardStack.Add(_currentPath); Navigate(p, false);
        }

        private void GoForward()
        {
            if (_forwardStack.Count == 0) return;
            string p = _forwardStack[_forwardStack.Count - 1]; _forwardStack.RemoveAt(_forwardStack.Count - 1);
            _backStack.Add(_currentPath); Navigate(p, false);
        }

        private void GoUp()
        {
            var parent = Directory.GetParent(_currentPath);
            if (parent != null) Navigate(parent.FullName, true);
        }

        // ---- 列表 ----
        private void PopulateList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            if (_currentPath.Length == 0 || !Directory.Exists(_currentPath)) { _list.EndUpdate(); _lblCount.Text = "0 项"; return; }

            var entries = new List<ListEntry>();
            try
            {
                foreach (string d in Directory.GetDirectories(_currentPath))
                    entries.Add(new ListEntry { Path = d, IsDir = true, Time = Directory.GetLastWriteTime(d), Note = "" });
                var opt = (CurrentTab != null && CurrentTab.Recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string f in Directory.GetFiles(_currentPath, "*", opt))
                {
                    if (!IsPrg(f)) continue;
                    var note = _store.LoadNote(f);
                    entries.Add(new ListEntry { Path = f, IsDir = false, Time = File.GetLastWriteTime(f), Note = note == null ? "" : note.Text });
                }
            }
            catch (Exception ex) { _lblPath.Text = _currentPath + "（读取失败：" + ex.Message + "）"; }

            string filter = _search.Text.Trim().ToLowerInvariant();
            if (filter.Length > 0)
                entries = entries.Where(x => Path.GetFileName(x.Path).ToLowerInvariant().Contains(filter) || x.Note.ToLowerInvariant().Contains(filter)).ToList();

            foreach (var en in entries.OrderBy(x => !x.IsDir).ThenBy(x => Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase))
            {
                var item = new ListViewItem(Path.GetFileName(en.Path.TrimEnd('\\'))) { ImageKey = en.IsDir ? "folder" : "prg", Tag = en };
                item.SubItems.Add(en.IsDir ? "文件夹" : "测量程序");
                item.SubItems.Add(en.Time.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(en.Note);
                if (en.Note.Length > 0) item.ToolTipText = en.Note;
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _lblPath.Text = _currentPath;
            _lblCount.Text = entries.Count + " 项";
            UpdateDetail();
        }

        private ListEntry SelectedEntry()
        {
            return _list.SelectedItems.Count == 0 ? null : _list.SelectedItems[0].Tag as ListEntry;
        }

        private void UpdateDetail()
        {
            var en = SelectedEntry();
            if (en == null) { _dName.Text = "未选择程序"; _dPath.Text = ""; _dTime.Text = ""; _dNote.Text = ""; return; }
            _dName.Text = Path.GetFileName(en.Path.TrimEnd('\\'));
            _dPath.Text = en.Path;
            _dTime.Text = (en.IsDir ? "文件夹 · " : "测量程序 · ") + en.Time.ToString("yyyy-MM-dd HH:mm");
            _dNote.Text = en.Note;
        }

        private void ActivateSelected()
        {
            var en = SelectedEntry();
            if (en == null) return;
            if (en.IsDir) Navigate(en.Path, true);
            else OpenEntry(en);
        }

        private void OpenSelected()
        {
            var en = SelectedEntry();
            if (en == null || en.IsDir) { MessageBox.Show(this, "请先选择一个测量程序。", "提示"); return; }
            OpenEntry(en);
        }

        private void OpenEntry(ListEntry en)
        {
            try { PcdmisLauncher.Open(en.Path, _settings); }
            catch (Exception ex) { MessageBox.Show(this, "打开失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void EditNote()
        {
            var en = SelectedEntry();
            if (en == null || en.IsDir) { MessageBox.Show(this, "请先选择一个测量程序。", "提示"); return; }
            using (var dlg = new NoteForm(Path.GetFileNameWithoutExtension(en.Path), en.Note))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _store.SaveNote(new NoteRecord { Path = en.Path, Text = dlg.NoteText, UpdatedBy = _settings.UserName, UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                    PopulateList();
                }
        }

        // ---- 标签管理 / 设置 ----
        private void AddTab()
        {
            using (var dlg = new TabEditForm(null))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                { _tabs.Add(dlg.Result); _store.SaveTabs(_tabs); RebuildTabs(dlg.Result.Id); }
        }

        private void EditTab()
        {
            var t = CurrentTab;
            if (t == null) { MessageBox.Show(this, "请先选择一个标签。", "提示"); return; }
            using (var dlg = new TabEditForm(t))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    int i = _tabs.FindIndex(x => x.Id == t.Id);
                    if (i >= 0) _tabs[i] = dlg.Result;
                    _store.SaveTabs(_tabs); RebuildTabs(dlg.Result.Id);
                }
        }

        private void RemoveTab()
        {
            var t = CurrentTab;
            if (t == null) return;
            if (MessageBox.Show(this, "删除标签「" + t.Name + "」？（不会删除程序文件）", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _tabs.RemoveAll(x => x.Id == t.Id);
            _store.SaveTabs(_tabs); RebuildTabs(null);
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(_settings))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings = dlg.Result; _settingsStore.Save(_settings);
                    _store = new SharedStore(_settings.SharedDataDir);
                    _tabs = _store.LoadTabs(); RebuildTabs(null); UpdateSharedLabel();
                }
        }

        private void UpdateSharedLabel()
        {
            _lblShared.Text = string.IsNullOrWhiteSpace(_settings.SharedDataDir)
                ? "本机模式 | " + _settings.UserName
                : "共享:" + _settings.SharedDataDir + " | " + _settings.UserName;
        }
    }
}