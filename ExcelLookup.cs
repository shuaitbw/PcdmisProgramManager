using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PcdmisProgramManager
{
    // 沿用 VBA 的固定路径
    public static class CncPaths
    {
        public const string MasterXls = @"K:\3D-Project\Application\393_03 designstatus sum.xls";
        public const string CncRoot = @"K:\CNC-Project\";
    }

    public class LookupResult
    {
        public string Tool;
        public string Job;
        public bool ToolFound;
        public bool JobFound;
    }

    public static class ExcelLookup
    {
        private static string Norm(string s)
        {
            return (s ?? "").Replace(" ", "").ToUpperInvariant();
        }

        // 一次打开主表，双向解析：工单号(D列)→模号(A列)，模号(A列)→工单号(D列)。
        // 主表 A 列=模号，D 列=工单号；两侧均忽略空格与大小写（修正 VBA 大小写不一致）。
        public static LookupResult Query(string tool, string job)
        {
            var result = new LookupResult { Tool = tool, Job = job };
            string tk = Norm(tool), jk = Norm(job);
            if (tk.Length == 0 && jk.Length == 0) return result;
            if (!File.Exists(CncPaths.MasterXls))
                throw new FileNotFoundException("主表不存在：" + CncPaths.MasterXls);

            Type t = Type.GetTypeFromProgID("Excel.Application");
            if (t == null) throw new InvalidOperationException("未安装 Excel，无法读取 .xls 主表。");

            dynamic app = null, books = null, book = null, sheets = null, sheet = null, dCell = null, lastCell = null, used = null;
            try
            {
                app = Activator.CreateInstance(t);
                app.Visible = false;
                app.DisplayAlerts = false;
                books = app.Workbooks;
                book = books.Open(CncPaths.MasterXls, 0, true);
                sheets = book.Sheets;
                sheet = sheets[1];

                dCell = sheet.Range["D20000"];
                lastCell = dCell.End[-4162]; // xlUp
                int lastRow = lastCell.Row;
                if (lastRow >= 1)
                {
                    used = sheet.Range["A1", "D" + lastRow];
                    object[,] vals = used.Value2;
                    int rows = vals.GetLength(0);
                    for (int r = 1; r <= rows; r++)
                    {
                        string a = Norm(Convert.ToString(vals[r, 1]));
                        string d = Norm(Convert.ToString(vals[r, 4]));
                        if (jk.Length > 0 && d == jk && !result.ToolFound)
                        { result.Tool = Convert.ToString(vals[r, 1]); result.ToolFound = true; }
                        if (tk.Length > 0 && a == tk && !result.JobFound)
                        { result.Job = Convert.ToString(vals[r, 4]); result.JobFound = true; }
                        if ((jk.Length == 0 || result.ToolFound) && (tk.Length == 0 || result.JobFound)) break;
                    }
                }
                return result;
            }
            finally
            {
                if (book != null) { try { book.Close(false); } catch { } }
                if (app != null) { try { app.Quit(); } catch { } }
                Release(used); Release(lastCell); Release(dCell); Release(sheet); Release(sheets); Release(book); Release(books); Release(app);
            }
        }

        private static void Release(object o)
        {
            if (o == null) return;
            try { Marshal.FinalReleaseComObject(o); } catch { }
        }

        // 复刻 VBA：K:\CNC-Project\<首字母>-series\<前5位>\<第7起7位去-01>\<分支>\Ele\
        public static string BuildElePath(string tool, string branch)
        {
            string tno = (tool ?? "").Replace(" ", "");
            if (tno.Length < 5) return null;
            string path1 = tno.Substring(0, 1);
            string path2 = tno.Substring(0, 5);
            string path3 = tno.Length >= 13 ? tno.Substring(6, 7) : (tno.Length > 6 ? tno.Substring(6) : "");
            path3 = path3.Replace("-01", "");
            string b = (branch ?? "").Trim();
            return CncPaths.CncRoot + path1 + "-series\\" + path2 + "\\" + path3 + "\\" + b + "\\Ele\\";
        }
    }
}