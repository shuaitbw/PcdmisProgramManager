using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PcdmisProgramManager
{
    // 打开优先级：COM 自动化（可附着已运行实例，可靠加载）→ .PRG 文件关联 → 配置 exe+参数。
    public static class PcdmisLauncher
    {
        public static void Open(string prgPath, AppSettings settings)
        {
            if (string.IsNullOrEmpty(prgPath) || !File.Exists(prgPath))
                throw new FileNotFoundException("程序文件不存在：" + prgPath);

            var errors = new List<string>();

            // COM 已注册则直接用 COM（错误如实上报，便于定位机器名/权限问题）
            if (Type.GetTypeFromProgID("PCDLRN.Application") != null)
            {
                OpenViaCom(prgPath, settings.MachineName);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = prgPath, UseShellExecute = true });
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
                        Arguments = "\"" + prgPath + "\"",
                        UseShellExecute = false
                    });
                    return;
                }
                catch (Exception ex) { errors.Add("exe: " + ex.Message); }
            }

            throw new InvalidOperationException("打开失败 → " + string.Join(" | ", errors));
        }

        private static void OpenViaCom(string prgPath, string machine)
        {
            Type t = Type.GetTypeFromProgID("PCDLRN.Application");
            if (t == null) throw new InvalidOperationException("未注册 PCDLRN.Application");

            object appObj;
            try { appObj = Marshal.GetActiveObject("PCDLRN.Application"); }
            catch { appObj = Activator.CreateInstance(t); }

            dynamic app = appObj;
            // 不设置 Visible/任何窗口属性，避免重置 PC-DMIS 的窗口布局
            app.PartPrograms.Open(prgPath, string.IsNullOrWhiteSpace(machine) ? "CMM1" : machine);
        }
    }
}