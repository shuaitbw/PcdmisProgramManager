using System;
using System.Windows.Forms;

namespace PcdmisProgramManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Log(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private static void Log(Exception ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcdmisProgramManager");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "error.log"),
                    DateTime.Now + "\r\n" + ex);
            }
            catch { }
            MessageBox.Show("启动/运行错误：" + ex.Message + "\r\n详情见 %AppData%\\PcdmisProgramManager\\error.log",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}