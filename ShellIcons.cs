using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PcdmisProgramManager
{
    // 提取 Windows  shell 真实图标，让列表接近资源管理器观感
    public static class ShellIcons
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        public static Icon FolderSmall()
        {
            return Get("folder", FILE_ATTRIBUTE_DIRECTORY);
        }

        public static Icon PrgSmall()
        {
            return Get(".prg", FILE_ATTRIBUTE_NORMAL);
        }

        private static Icon Get(string path, uint attr)
        {
            var info = new SHFILEINFO();
            IntPtr res = SHGetFileInfo(path, attr, ref info,
                (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
            if (res == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
            Icon icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
            DestroyIcon(info.hIcon);
            return icon;
        }
    }
}