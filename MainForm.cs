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
            public string NoteBy;
            public string NoteAt;
        }

        private readonly SettingsStore _settingsStore = new SettingsStore();
        private AppSettings _settings;
        private SharedStore _store;
        private readonly LocalTabStore _localTabStore = new LocalTabStore();
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
        private readonly SplitContainer _vsplit = new SplitContainer();
        private readonly SplitContainer _listsSplit = new SplitContainer();
        private readonly ListView _queueList = new ListView();
        private readonly ListView _histList = new ListView();
        private readonly Label _queueTitle = new Label();
        private readonly Label _histTitle = new Label();
        private readonly WorkflowStore _workflow = new WorkflowStore();
        private List<OpenItem> _queue = new List<OpenItem>();
        private List<OpenItem> _history = new List<OpenItem>();
        private readonly ListView _list = new ListView();
        private readonly ImageList _icons = new ImageList();
        private readonly Panel _detail = new Panel();
        private readonly Label _dName = new Label();
        private readonly Label _dPath = new Label();
        private readonly Label _dTime = new Label();
        private readonly Label _dNoteMeta = new Label();
        private readonly TextBox _dNote = new TextBox();
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _lblPath = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _lblCount = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _lblShared = new ToolStripStatusLabel();

        private string _currentPath = "";
        private readonly List<string> _backStack = new List<string>();
        private readonly List<string> _forwardStack = new List<string>();
        private bool _sortByTime;
        private List<ListEntry> _entriesCache;
        private string _entriesCachePath;

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
            _tabs = LoadAllTabs();
            RebuildTabs(null);
            UpdateSharedLabel();
            _queue = _workflow.LoadQueue();
            _history = _workflow.LoadHistory();
            RefreshQueue();
            RefreshHistory();

            Shown += (s, e) =>
            {
                try { _split.SplitterDistance = Math.Max(300, _main.Width - 300); }
                catch { }
                int bottomHeight = Math.Max(280, (int)(_main.Height * 0.45));
                try { _vsplit.SplitterDistance = Math.Max(200, _main.Height - bottomHeight); }
                catch { }
                try { _listsSplit.SplitterDistance = Math.Max(200, _listsSplit.Width / 2); }
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
            var queue = new ToolStripButton("加入等候");
            queue.Click += (s, e) => AddSelectedToQueue();
            var note = new ToolStripButton("备注");
            note.Click += (s, e) => EditNote();
            var refresh = new ToolStripButton("刷新");
            refresh.Click += (s, e) => PopulateList(true);
            var settings = new ToolStripButton("设置");
            settings.Click += (s, e) => OpenSettings();

            _toolbar.Items.AddRange(new ToolStripItem[] {
                open, queue, note, refresh, new ToolStripSeparator(), settings });
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

            BuildBottom();
            _main.Controls.Add(_vsplit);
            _main.Controls.Add(_nav);
        }

        private void BuildBottom()
        {
            _vsplit.Dock = DockStyle.Fill;
            _vsplit.Orientation = Orientation.Horizontal;
            _vsplit.BackColor = Theme.Border;
            _vsplit.SplitterWidth = 5;
            _vsplit.FixedPanel = FixedPanel.Panel2;
            _vsplit.Panel1.Controls.Add(_split);

            _listsSplit.Dock = DockStyle.Fill;
            _listsSplit.Orientation = Orientation.Vertical;
            _listsSplit.BackColor = Theme.Border;
            _listsSplit.SplitterWidth = 5;
            _vsplit.Panel2.Controls.Add(_listsSplit);

            _listsSplit.Panel1.Controls.Add(BuildQueueGroup());
            _listsSplit.Panel2.Controls.Add(BuildHistoryGroup());
        }

        private Panel BuildQueueGroup()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(8) };
            _queueTitle.Dock = DockStyle.Top;
            _queueTitle.Height = 28;
            _queueTitle.Font = Theme.UiFont(9.5F);
            _queueTitle.ForeColor = Theme.Text;
            _queueTitle.Text = "等候打开 (0)";
            _queueTitle.TextAlign = ContentAlignment.MiddleLeft;

            _queueList.Dock = DockStyle.Fill;
            _queueList.BorderStyle = BorderStyle.None;
            _queueList.BackColor = Theme.Panel;
            _queueList.ForeColor = Theme.Text;
            _queueList.View = View.Details;
            _queueList.FullRowSelect = true;
            _queueList.HideSelection = false;
            _queueList.MultiSelect = false;
            _queueList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _queueList.Columns.Add("程序", 160);
            _queueList.Columns.Add("路径", 260);
            _queueList.Columns.Add("加入时间", 120);
            _queueList.ItemActivate += (s, e) => OpenQueueSelected();

            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
            var open = ListButton("打开", Theme.SelectBlue, Color.White);
            open.Click += (s, e) => OpenQueueSelected();
            var remove = ListButton("移除", Theme.Panel, Theme.Text);
            remove.Click += (s, e) => RemoveQueueSelected();
            var clear = ListButton("清空", Theme.Panel, Theme.Text);
            clear.Click += (s, e) => ClearQueue();
            btns.Controls.Add(open);
            btns.Controls.Add(remove);
            btns.Controls.Add(clear);

            p.Controls.Add(_queueList);
            p.Controls.Add(btns);
            p.Controls.Add(_queueTitle);
            return p;
        }

        private Panel BuildHistoryGroup()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(8) };
            _histTitle.Dock = DockStyle.Top;
            _histTitle.Height = 28;
            _histTitle.Font = Theme.UiFont(9.5F);
            _histTitle.ForeColor = Theme.Text;
            _histTitle.Text = "打开历史 (0)";
            _histTitle.TextAlign = ContentAlignment.MiddleLeft;

            _histList.Dock = DockStyle.Fill;
            _histList.BorderStyle = BorderStyle.None;
            _histList.BackColor = Theme.Panel;
            _histList.ForeColor = Theme.Text;
            _histList.View = View.Details;
            _histList.FullRowSelect = true;
            _histList.HideSelection = false;
            _histList.MultiSelect = false;
            _histList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _histList.Columns.Add("程序", 160);
            _histList.Columns.Add("打开时间", 120);
            _histList.Columns.Add("路径", 260);
            _histList.ItemActivate += (s, e) => OpenHistorySelected();

            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
            var open = ListButton("打开", Theme.SelectBlue, Color.White);
            open.Click += (s, e) => OpenHistorySelected();
            var clear = ListButton("清空", Theme.Panel, Theme.Text);
            clear.Click += (s, e) => ClearHistory();
            btns.Controls.Add(open);
            btns.Controls.Add(clear);

            p.Controls.Add(_histList);
            p.Controls.Add(btns);
            p.Controls.Add(_histTitle);
            return p;
        }

        private Button ListButton(string text, Color back, Color fore)
        {
            var b = new Button { Text = text, Width = 64, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = Theme.UiFont(8.5F) };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.BorderSize = back == Theme.SelectBlue ? 0 : 1;
            return b;
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
            _list.HeaderStyle = ColumnHeaderStyle.Clickable;
            _list.Columns.Add("名称", 280);
            _list.Columns.Add("类型", 90);
            _list.Columns.Add("修改时间", 150);
            _list.Columns.Add("备注", 300);
            _list.ColumnClick += (s, e) =>
            {
                if (e.Column == 2) { if (!AtRoot()) return; _sortByTime = !_sortByTime; }
                else if (e.Column == 0) _sortByTime = false;
                else return;
                PopulateList();
            };
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

            _dNoteMeta.Dock = DockStyle.Top;
            _dNoteMeta.Height = 22;
            _dNoteMeta.ForeColor = Theme.Muted;
            _dNoteMeta.Font = Theme.UiFont(8.5F);
            _dNoteMeta.AutoEllipsis = true;
            _dNoteMeta.TextAlign = ContentAlignment.MiddleLeft;

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
            _detail.Controls.Add(_dNoteMeta);
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
            var queue = new ToolStripMenuItem("加入等候列表");
            queue.Click += (s, e) => AddSelectedToQueue();
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
            menu.Items.AddRange(new ToolStripItem[] { open, queue, note, new ToolStripSeparator(), folder, copy });
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

        // 共享目录里的标签 = 多机共享；本机文件里的 = 仅本机
        private List<TabItem> LoadAllTabs()
        {
            var list = new List<TabItem>();
            foreach (var t in _store.LoadTabs()) { t.Shared = true; list.Add(t); }
            foreach (var t in _localTabStore.Load()) { t.Shared = false; list.Add(t); }
            return list;
        }

        private void SaveAllTabs()
        {
            _store.SaveTabs(_tabs.Where(x => x.Shared).ToList());
            _localTabStore.Save(_tabs.Where(x => !x.Shared).ToList());
        }

        private void RebuildTabs(string selectId)
        {
            _tabList.Items.Clear();
            foreach (var t in _tabs) _tabList.Items.Add(t.Name + (t.Shared ? "" : "（本机）"));
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
            _sortByTime = false;
            PopulateList(true);
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
            PopulateList(false);
        }

        // reload=true 重新枚举目录与备注（导航/刷新/改备注后）；false 复用缓存（搜索/排序，避免重复读盘）
        private void PopulateList(bool reload)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            if (_currentPath.Length == 0 || !Directory.Exists(_currentPath))
            { _list.EndUpdate(); _lblCount.Text = "0 项"; _entriesCache = null; _entriesCachePath = null; return; }

            if (reload || _entriesCache == null || !string.Equals(_entriesCachePath, _currentPath, StringComparison.OrdinalIgnoreCase))
            {
                _entriesCache = LoadEntries(_currentPath);
                _entriesCachePath = _currentPath;
            }

            string filter = _search.Text.Trim().ToLowerInvariant();
            IEnumerable<ListEntry> entries = _entriesCache;
            if (filter.Length > 0)
                entries = entries.Where(x => Path.GetFileName(x.Path).ToLowerInvariant().Contains(filter) || x.Note.ToLowerInvariant().Contains(filter));

            bool timeSort = AtRoot() && _sortByTime;
            _list.Columns[2].Text = timeSort ? "修改时间 ▼" : "修改时间";
            IEnumerable<ListEntry> ordered = timeSort
                ? entries.OrderByDescending(x => x.Time)
                : entries.OrderBy(x => !x.IsDir).ThenBy(x => Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase);

            int count = 0;
            foreach (var en in ordered)
            {
                var item = new ListViewItem(Path.GetFileName(en.Path.TrimEnd('\\'))) { ImageKey = en.IsDir ? "folder" : "prg", Tag = en };
                item.SubItems.Add(en.IsDir ? "文件夹" : "测量程序");
                item.SubItems.Add(en.Time.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(en.Note);
                if (en.Note.Length > 0) item.ToolTipText = en.Note + (en.NoteBy.Length > 0 ? "\n— " + en.NoteBy + " · " + en.NoteAt : "");
                _list.Items.Add(item);
                count++;
            }
            _list.EndUpdate();
            _lblPath.Text = _currentPath;
            _lblCount.Text = count + " 项";
            UpdateDetail();
        }

        private List<ListEntry> LoadEntries(string path)
        {
            var entries = new List<ListEntry>();
            try
            {
                foreach (string d in Directory.GetDirectories(path))
                    entries.Add(new ListEntry { Path = d, IsDir = true, Time = Directory.GetLastWriteTime(d), Note = "", NoteBy = "", NoteAt = "" });
                var opt = (CurrentTab != null && CurrentTab.Recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var noteIndex = _store.LoadNoteIndex();
                foreach (string f in Directory.GetFiles(path, "*", opt))
                {
                    if (!IsPrg(f)) continue;
                    NoteRecord note;
                    noteIndex.TryGetValue(Paths.NormalizeKey(f), out note);
                    entries.Add(new ListEntry { Path = f, IsDir = false, Time = File.GetLastWriteTime(f), Note = note == null ? "" : note.Text, NoteBy = note == null ? "" : (note.UpdatedBy ?? ""), NoteAt = note == null ? "" : (note.UpdatedAt ?? "") });
                }
            }
            catch (Exception ex) { _lblPath.Text = path + "（读取失败：" + ex.Message + "）"; }
            return entries;
        }

        // 当前是否停留在标签绑定的根目录（时间排序仅此处生效）
        private bool AtRoot()
        {
            var t = CurrentTab;
            return t != null && string.Equals(_currentPath.TrimEnd('\\'), t.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private ListEntry SelectedEntry()
        {
            return _list.SelectedItems.Count == 0 ? null : _list.SelectedItems[0].Tag as ListEntry;
        }

        private void UpdateDetail()
        {
            var en = SelectedEntry();
            if (en == null) { _dName.Text = "未选择程序"; _dPath.Text = ""; _dTime.Text = ""; _dNoteMeta.Text = ""; _dNote.Text = ""; return; }
            _dName.Text = Path.GetFileName(en.Path.TrimEnd('\\'));
            _dPath.Text = en.Path;
            _dTime.Text = (en.IsDir ? "文件夹 · " : "测量程序 · ") + en.Time.ToString("yyyy-MM-dd HH:mm");
            _dNote.Text = en.Note;
            string by = en.NoteBy ?? "";
            _dNoteMeta.Text = by.Length > 0 ? "由 " + by + " 添加 · " + (en.NoteAt ?? "") : "";
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
            OpenProgram(en.Path, false);
        }

        // 统一打开入口：成功后记历史；若来自等候队列则移除该条目
        private void OpenProgram(string path, bool fromQueue)
        {
            try { PcdmisLauncher.Open(path, _settings); }
            catch (Exception ex) { MessageBox.Show(this, "打开失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            _history.Insert(0, new OpenItem { Path = path, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            if (_history.Count > 300) _history.RemoveRange(300, _history.Count - 300);
            _workflow.SaveHistory(_history);
            RefreshHistory();

            if (fromQueue)
            {
                _queue.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
                _workflow.SaveQueue(_queue);
                RefreshQueue();
            }
        }

        // ---- 等候队列 / 打开历史 ----
        private void AddSelectedToQueue()
        {
            var en = SelectedEntry();
            if (en == null || en.IsDir) { MessageBox.Show(this, "请先选择一个测量程序。", "提示"); return; }
            if (_queue.Any(x => string.Equals(x.Path, en.Path, StringComparison.OrdinalIgnoreCase)))
            { _lblPath.Text = "已在等候列表：" + Path.GetFileName(en.Path); return; }
            _queue.Add(new OpenItem { Path = en.Path, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            _workflow.SaveQueue(_queue);
            RefreshQueue();
            _lblPath.Text = "已加入等候：" + Path.GetFileName(en.Path);
        }

        private string SelectedQueuePath()
        {
            return _queueList.SelectedItems.Count == 0 ? null : _queueList.SelectedItems[0].Tag as string;
        }

        private string SelectedHistoryPath()
        {
            return _histList.SelectedItems.Count == 0 ? null : _histList.SelectedItems[0].Tag as string;
        }

        private void OpenQueueSelected()
        {
            string p = SelectedQueuePath();
            if (p == null) { MessageBox.Show(this, "请先在等候列表选择一个程序。", "提示"); return; }
            OpenProgram(p, true);
        }

        private void RemoveQueueSelected()
        {
            string p = SelectedQueuePath();
            if (p == null) return;
            _queue.RemoveAll(x => string.Equals(x.Path, p, StringComparison.OrdinalIgnoreCase));
            _workflow.SaveQueue(_queue);
            RefreshQueue();
        }

        private void ClearQueue()
        {
            if (_queue.Count == 0) return;
            if (MessageBox.Show(this, "清空等候列表？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _queue.Clear();
            _workflow.SaveQueue(_queue);
            RefreshQueue();
        }

        private void OpenHistorySelected()
        {
            string p = SelectedHistoryPath();
            if (p == null) { MessageBox.Show(this, "请先在历史列表选择一个程序。", "提示"); return; }
            OpenProgram(p, false);
        }

        private void ClearHistory()
        {
            if (_history.Count == 0) return;
            if (MessageBox.Show(this, "清空打开历史？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _history.Clear();
            _workflow.SaveHistory(_history);
            RefreshHistory();
        }

        private void RefreshQueue()
        {
            _queueList.BeginUpdate();
            _queueList.Items.Clear();
            foreach (var q in _queue)
            {
                var item = new ListViewItem(Path.GetFileName(q.Path)) { Tag = q.Path };
                item.SubItems.Add(q.Path);
                item.SubItems.Add(q.Time);
                item.ToolTipText = q.Path;
                _queueList.Items.Add(item);
            }
            _queueList.EndUpdate();
            _queueTitle.Text = "等候打开 (" + _queue.Count + ")";
        }

        private void RefreshHistory()
        {
            _histList.BeginUpdate();
            _histList.Items.Clear();
            foreach (var h in _history)
            {
                var item = new ListViewItem(Path.GetFileName(h.Path)) { Tag = h.Path };
                item.SubItems.Add(h.Time);
                item.SubItems.Add(h.Path);
                item.ToolTipText = h.Path;
                _histList.Items.Add(item);
            }
            _histList.EndUpdate();
            _histTitle.Text = "打开历史 (" + _history.Count + ")";
        }

        private void EditNote()
        {
            var en = SelectedEntry();
            if (en == null || en.IsDir) { MessageBox.Show(this, "请先选择一个测量程序。", "提示"); return; }
            using (var dlg = new NoteForm(Path.GetFileNameWithoutExtension(en.Path), en.Note, _settings.UserName))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string author = dlg.Author.Length > 0 ? dlg.Author : Environment.UserName;
                    _store.SaveNote(new NoteRecord { Path = en.Path, Text = dlg.NoteText, UpdatedBy = author, UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                    _settings.UserName = author; _settingsStore.Save(_settings);
                    PopulateList(true);
                }
        }

        // ---- 标签管理 / 设置 ----
        private void AddTab()
        {
            using (var dlg = new TabEditForm(null))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                { _tabs.Add(dlg.Result); SaveAllTabs(); RebuildTabs(dlg.Result.Id); }
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
                    SaveAllTabs(); RebuildTabs(dlg.Result.Id);
                }
        }

        private void RemoveTab()
        {
            var t = CurrentTab;
            if (t == null) return;
            if (MessageBox.Show(this, "删除标签「" + t.Name + "」？（不会删除程序文件）", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _tabs.RemoveAll(x => x.Id == t.Id);
            SaveAllTabs(); RebuildTabs(null);
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(_settings))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings = dlg.Result; _settingsStore.Save(_settings);
                    _store = new SharedStore(_settings.SharedDataDir);
                    _tabs = LoadAllTabs(); RebuildTabs(null); UpdateSharedLabel();
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