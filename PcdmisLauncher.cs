using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PcdmisProgramManager
{
    // 打开优先级：COM 自动化（可附着已运行实例，可靠加载）→ .PRG 文件关联 → 配置 exe+参数。
    // 只读程序：有 COM 时临时解除只读、打开后立即恢复；无 COM 时打开可写临时副本。
    public static class PcdmisLauncher
    {
        public static void Open(string prgPath, AppSettings settings)
        {
            if (string.IsNullOrEmpty(prgPath) || !File.Exists(prgPath))
                throw new FileNotFoundException("程序文件不存在：" + prgPath);

            var attrs = File.GetAttributes(prgPath);
            bool readOnly = (attrs & FileAttributes.ReadOnly) != 0;
            bool comRegistered = Type.GetTypeFromProgID("PCDLRN.Application") != null;

            // 只读 PRG 直接打开会走“归档错误→备份恢复”失败流程。COM 打开是同步的，
            // 可临时解除只读、打开完成后立即恢复；恢复后保存被只读阻挡，保护仍在。
            // 代价：打开瞬间 PC-DMIS 可能向原文件写入归档元数据。
            if (readOnly && comRegistered)
            {
                File.SetAttributes(prgPath, attrs & ~FileAttributes.ReadOnly);
                try { OpenViaCom(prgPath, settings.MachineName); }
                finally { File.SetAttributes(prgPath, attrs); }
                return;
            }

            // 无 COM：关联/exe 启动异步、无法保证恢复时机，只读改用可写副本
            string openPath = readOnly ? MakeWritableCopy(prgPath) : prgPath;

            var errors = new List<string>();

            // COM 已注册则直接用 COM（错误如实上报，便于定位机器名/权限问题）
            if (comRegistered)
            {
                OpenViaCom(openPath, settings.MachineName);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = openPath, UseShellExecute = true });
                return;
            }
            catch (Exception ex) { errors.Add("文件关联: " + ex.Message); }

            string exe = settings.PcdmisExePath;
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = "\"" + openPath + "\"",
                        UseShellExecute = false
                    });
                    return;
                }
                catch (Exception ex) { errors.Add("exe: " + ex.Message); }
            }

            throw new InvalidOperationException("打开失败 → " + string.Join(" | ", errors));
        }

        private static string MakeWritableCopy(string prgPath)
        {
            string dir = Path.Combine(Path.GetTempPath(), "PcdmisProgramManager", "readonly");
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, Path.GetFileName(prgPath));
            try
            {
                File.Copy(prgPath, dest, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 同名副本正被 PC-DMIS 占用时换名
                dest = Path.Combine(dir, Path.GetFileNameWithoutExtension(prgPath) + "_" + DateTime.Now.Ticks + Path.GetExtension(prgPath));
                File.Copy(prgPath, dest, true);
            }
            File.SetAttributes(dest, FileAttributes.Normal);
            return dest;
        }

        private static void OpenViaCom(string prgPath, string machine)
        {
            Type t = Type.GetTypeFromProgID("PCDLRN.Application");
            if (t == null) throw new InvalidOperationException("未注册 PCDLRN.Application");

            object appObj;
            try { appObj = Marshal.GetActiveObject("PCDLRN.Application"); }
            catch { appObj = Activator.CreateInstance(t); }

            dynamic app = appObj;
            // 冷启动时等初始化完成再 Open，避免在启动过程中调用打开
            app.WaitUntilReady(120);
            // 不设置 Visible/任何窗口属性，避免重置 PC-DMIS 的窗口布局
            app.PartPrograms.Open(prgPath, string.IsNullOrWhiteSpace(machine) ? "CMM1" : machine);
        }
    }
}