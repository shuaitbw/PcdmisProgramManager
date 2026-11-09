using System;

namespace PcdmisProgramManager
{
    // 标签 = 一个路径快速访问书签（Shared=true 存共享目录，false 仅本机）
    public class TabItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool Recursive { get; set; }
        public bool Shared { get; set; }

        public TabItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "";
            Path = "";
            Recursive = false;
            Shared = true;
        }
    }

    // 每个程序的备注（旁路存储，不改动 PRG）
    public class NoteRecord
    {
        public string Path { get; set; }
        public string Text { get; set; }
        public string UpdatedBy { get; set; }
        public string UpdatedAt { get; set; }

        public NoteRecord()
        {
            Path = "";
            Text = "";
            UpdatedBy = "";
            UpdatedAt = "";
        }
    }

    // 本机配置（指向共享数据目录、PC-DMIS 路径、当前使用人）
    public class AppSettings
    {
        public string SharedDataDir { get; set; }
        public string PcdmisExePath { get; set; }
        public string UserName { get; set; }
        public string MachineName { get; set; }

        public AppSettings()
        {
            SharedDataDir = "";
            PcdmisExePath = "";
            UserName = Environment.UserName;
            MachineName = "CMM1";
        }
    }

    // 等候队列 / 打开历史 的条目（本机持久化）
    public class OpenItem
    {
        public string Path { get; set; }
        public string Time { get; set; }

        public OpenItem()
        {
            Path = "";
            Time = "";
        }
    }

    // 列表运行时行
    public class ProgramItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public DateTime LastWrite { get; set; }
        public string Note { get; set; }

        public ProgramItem(string name, string fullPath, DateTime lastWrite, string note)
        {
            Name = name;
            FullPath = fullPath;
            LastWrite = lastWrite;
            Note = note ?? "";
        }
    }
}