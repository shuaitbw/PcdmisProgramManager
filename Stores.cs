using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PcdmisProgramManager
{
    public static class Json
    {
        private static readonly JavaScriptSerializer Ser = new JavaScriptSerializer();

        public static string Serialize(object obj)
        {
            return Ser.Serialize(obj);
        }

        public static T Deserialize<T>(string text)
        {
            if (string.IsNullOrEmpty(text)) return default(T);
            return Ser.Deserialize<T>(text);
        }
    }

    public static class Paths
    {
        public static string LocalRoot
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PcdmisProgramManager");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        // 规范化路径作为备注键（统一大小写/斜杠，保证多机一致）
        public static string NormalizeKey(string path)
        {
            string p = (path ?? "").Trim().Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(p));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    public class SettingsStore
    {
        private string File => Path.Combine(Paths.LocalRoot, "settings.json");

        public AppSettings Load()
        {
            try
            {
                if (System.IO.File.Exists(File))
                    return Json.Deserialize<AppSettings>(System.IO.File.ReadAllText(File)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public void Save(AppSettings s)
        {
            System.IO.File.WriteAllText(File, Json.Serialize(s));
        }
    }

    // 仅本机的标签（不同步到共享目录）
    public class LocalTabStore
    {
        private string File => Path.Combine(Paths.LocalRoot, "tabs.local.json");

        public List<TabItem> Load()
        {
            try
            {
                if (System.IO.File.Exists(File))
                    return Json.Deserialize<List<TabItem>>(System.IO.File.ReadAllText(File)) ?? new List<TabItem>();
            }
            catch { }
            return new List<TabItem>();
        }

        public void Save(List<TabItem> tabs)
        {
            System.IO.File.WriteAllText(File, Json.Serialize(tabs));
        }
    }

    // 本机工作流：等候队列 + 打开历史（打开发生在本机，故按机器保存）
    public class WorkflowStore
    {
        private string QueueFile => Path.Combine(Paths.LocalRoot, "queue.json");
        private string HistoryFile => Path.Combine(Paths.LocalRoot, "history.json");

        public List<OpenItem> LoadQueue()
        {
            return LoadList(QueueFile);
        }

        public void SaveQueue(List<OpenItem> items)
        {
            System.IO.File.WriteAllText(QueueFile, Json.Serialize(items));
        }

        public List<OpenItem> LoadHistory()
        {
            return LoadList(HistoryFile);
        }

        public void SaveHistory(List<OpenItem> items)
        {
            System.IO.File.WriteAllText(HistoryFile, Json.Serialize(items));
        }

        private static List<OpenItem> LoadList(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    return Json.Deserialize<List<OpenItem>>(System.IO.File.ReadAllText(path)) ?? new List<OpenItem>();
            }
            catch { }
            return new List<OpenItem>();
        }
    }

    // 共享数据目录：tabs.json + notes/<key>.json。未配置共享时回退本机目录。
    public class SharedStore
    {
        public string DataRoot { get; private set; }

        public SharedStore(string sharedDir)
        {
            DataRoot = string.IsNullOrWhiteSpace(sharedDir)
                ? Path.Combine(Paths.LocalRoot, "data")
                : sharedDir.Trim();
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(NotesDir);
        }

        private string NotesDir => Path.Combine(DataRoot, "notes");
        private string TabsFile => Path.Combine(DataRoot, "tabs.json");

        public List<TabItem> LoadTabs()
        {
            try
            {
                if (System.IO.File.Exists(TabsFile))
                    return Json.Deserialize<List<TabItem>>(Read(TabsFile)) ?? new List<TabItem>();
            }
            catch { }
            return new List<TabItem>();
        }

        public void SaveTabs(List<TabItem> tabs)
        {
            Write(TabsFile, Json.Serialize(tabs));
        }

        public NoteRecord LoadNote(string programPath)
        {
            string f = NoteFile(programPath);
            try
            {
                if (System.IO.File.Exists(f))
                    return Json.Deserialize<NoteRecord>(Read(f));
            }
            catch { }
            return null;
        }

        public void SaveNote(NoteRecord note)
        {
            Write(NoteFile(note.Path), Json.Serialize(note));
        }

        // 一次性读取整个备注目录，键为路径归一化哈希；避免逐文件探测共享目录（SMB 往返昂贵）
        public Dictionary<string, NoteRecord> LoadNoteIndex()
        {
            var dict = new Dictionary<string, NoteRecord>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string file in Directory.GetFiles(NotesDir, "*.json"))
                {
                    try
                    {
                        var n = Json.Deserialize<NoteRecord>(Read(file));
                        if (n != null) dict[Path.GetFileNameWithoutExtension(file)] = n;
                    }
                    catch { }
                }
            }
            catch { }
            return dict;
        }

        private string NoteFile(string programPath)
        {
            return Path.Combine(NotesDir, Paths.NormalizeKey(programPath) + ".json");
        }

        // 共享目录(SMB)偶发占用，做少量重试
        private static string Read(string path)
        {
            for (int i = 0; ; i++)
            {
                try { return System.IO.File.ReadAllText(path); }
                catch (IOException) { if (i >= 2) throw; System.Threading.Thread.Sleep(120); }
            }
        }

        private static void Write(string path, string text)
        {
            for (int i = 0; ; i++)
            {
                try { System.IO.File.WriteAllText(path, text); return; }
                catch (IOException) { if (i >= 2) throw; System.Threading.Thread.Sleep(120); }
            }
        }
    }
}