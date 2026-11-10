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
            return Query(tool, job, CncPaths.MasterXls);
        }

        // masterPath 可注入，便于用真实 .xls 验证；生产调用走上面的固定 K:\ 主表。
        internal static LookupResult Query(string tool, string job, string masterPath)
        {
            var result = new LookupResult { Tool = tool, Job = job };
            string tk = Norm(tool), jk = Norm(job);
            if (tk.Length == 0 && jk.Length == 0) return result;
            if (!File.Exists(masterPath))
                throw new FileNotFoundException("主表不存在：" + masterPath);

            Type t = Type.GetTypeFromProgID("Excel.Application");
            if (t == null) throw new InvalidOperationException("未安装 Excel，无法读取 .xls 主表。");

            dynamic app = null, books = null, book = null, worksheets = null, sheet = null, dCell = null, lastCell = null, used = null;
            string phase = "启动 Excel";
            try
            {
                app = Activator.CreateInstance(t);
                app.Visible = false;
                app.DisplayAlerts = false;

                phase = "打开主表（只读）";
                books = app.Workbooks;
                book = books.Open(masterPath, 0, true);

                // 用 Worksheets 而非 Sheets：首张若是图表/宏表，Sheets[1].Range 会报“所要求的对象不存在”。
                phase = "取得数据工作表";
                worksheets = book.Worksheets;
                sheet = worksheets[1];

                phase = "定位数据末行";
                dCell = sheet.Range["D20000"];
                lastCell = dCell.End[-4162]; // xlUp
                int lastRow = lastCell.Row;
                if (lastRow < 1) lastRow = 1;

                // 单字符串地址规避双参数动态索引器；整块 Value2 一次读入，速度快。
                phase = "读取主表数据";
                object[,] vals = null;
                int rows = 0;
                try
                {
                    used = sheet.Range["A1:D" + lastRow];
                    vals = used.Value2 as object[,];
                    if (vals != null) rows = vals.GetLength(0);
                }
                catch { vals = null; }

                // 回退：完全按 VBA 逐格读 Cells(i,1)/Cells(i,4)，应对整块读取在特定主表上失败。
                if (vals == null)
                {
                    phase = "读取主表数据（逐格）";
                    vals = new object[lastRow, 4];
                    rows = lastRow;
                    for (int r = 1; r <= lastRow; r++)
                    {
                        dynamic cA = sheet.Cells[r, 1];
                        dynamic cD = sheet.Cells[r, 4];
                        vals[r - 1, 0] = cA.Value2;
                        vals[r - 1, 3] = cD.Value2;
                        Release(cA); Release(cD);
                    }
                }

                phase = "匹配模号 / JOB No.";
                int rowBase = vals.GetLowerBound(0);
                int colBase = vals.GetLowerBound(1);
                for (int r = 0; r < rows; r++)
                {
                    string a = Norm(Convert.ToString(vals[rowBase + r, colBase + 0]));
                    string d = Norm(Convert.ToString(vals[rowBase + r, colBase + 3]));
                    if (jk.Length > 0 && d == jk && !result.ToolFound)
                    { result.Tool = Convert.ToString(vals[rowBase + r, colBase + 0]); result.ToolFound = true; }
                    if (tk.Length > 0 && a == tk && !result.JobFound)
                    { result.Job = Convert.ToString(vals[rowBase + r, colBase + 3]); result.JobFound = true; }
                    if ((jk.Length == 0 || result.ToolFound) && (tk.Length == 0 || result.JobFound)) break;
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("[" + phase + "] " + ex.Message, ex);
            }
            finally
            {
                if (book != null) { try { book.Close(false); } catch { } }
                if (app != null) { try { app.Quit(); } catch { } }
                Release(used); Release(lastCell); Release(dCell); Release(sheet); Release(worksheets); Release(book); Release(books); Release(app);
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