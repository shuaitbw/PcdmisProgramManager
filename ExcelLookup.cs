using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NPOI.SS.UserModel;

namespace PcdmisProgramManager
{
    public class LookupResult
    {
        public string Tool;
        public string Job;
        public bool ToolFound;
        public bool JobFound;
    }

    public static class ExcelLookup
    {
        // 由 MainForm 从 settings.json 注入；仅配置项，源码不留默认路径
        public static string MasterXlsPath { get; set; } = "";
        public static string CncRootPath { get; set; } = "";

        private static string Norm(string s)
        {
            return (s ?? "").Replace(" ", "").ToUpperInvariant();
        }

        private static string _cachePath;
        private static DateTime _cacheWriteUtc;
        private static long _cacheLength;
        private static Dictionary<string, string> _jobToTool;
        private static Dictionary<string, string> _toolToJob;

        // 一次解析主表，双向解析：工单号(D列)→模号(A列)，模号(A列)→工单号(D列)。
        // 主表 A 列=模号，D 列=工单号；两侧均忽略空格与大小写（修正 VBA 大小写不一致）。
        public static LookupResult Query(string tool, string job)
        {
            if (string.IsNullOrWhiteSpace(MasterXlsPath))
                throw new InvalidOperationException("请先在「设置」中配置互查主表路径。");
            return Query(tool, job, MasterXlsPath.Trim());
        }

        // masterPath 可注入，便于用真实 .xls 验证；生产调用走设置中配置的主表路径。
        internal static LookupResult Query(string tool, string job, string masterPath)
        {
            var result = new LookupResult { Tool = tool, Job = job };
            string tk = Norm(tool), jk = Norm(job);
            if (tk.Length == 0 && jk.Length == 0) return result;
            EnsureCache(masterPath);
            string v;
            if (jk.Length > 0 && _jobToTool.TryGetValue(jk, out v)) { result.Tool = v; result.ToolFound = true; }
            if (tk.Length > 0 && _toolToJob.TryGetValue(tk, out v)) { result.Job = v; result.JobFound = true; }
            return result;
        }

        // 主表多人频繁更新：写入时间或长度变化即重建缓存，避免查到旧数据。
        private static void EnsureCache(string path)
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) throw new FileNotFoundException("主表不存在：" + path);
            DateTime writeUtc = fi.LastWriteTimeUtc;
            long length = fi.Length;
            if (_jobToTool != null && _cachePath == path && _cacheWriteUtc == writeUtc && _cacheLength == length) return;

            var jobToTool = new Dictionary<string, string>();
            var toolToJob = new Dictionary<string, string>();
            try
            {
                // 主表常被别人的 Excel 占用，需 ReadWrite 共享以免共享冲突。
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    IWorkbook wb = WorkbookFactory.Create(fs);
                    ISheet sheet = wb.GetSheetAt(0);
                    for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
                    {
                        IRow row = sheet.GetRow(r);
                        if (row == null) continue;
                        string rawA = CellText(row.GetCell(0));
                        string rawD = CellText(row.GetCell(3));
                        string a = Norm(rawA), d = Norm(rawD);
                        if (d.Length > 0 && !jobToTool.ContainsKey(d)) jobToTool[d] = rawA;
                        if (a.Length > 0 && !toolToJob.ContainsKey(a)) toolToJob[a] = rawD;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("读取主表失败：" + ex.Message, ex);
            }
            _jobToTool = jobToTool;
            _toolToJob = toolToJob;
            _cachePath = path;
            _cacheWriteUtc = writeUtc;
            _cacheLength = length;
        }

        private static string CellText(ICell cell)
        {
            if (cell == null) return "";
            CellType type = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
            switch (type)
            {
                case CellType.String: return cell.StringCellValue;
                case CellType.Numeric: return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                case CellType.Boolean: return cell.BooleanCellValue.ToString(CultureInfo.InvariantCulture);
                default: return "";
            }
        }

        // 电极路径规则：<根目录>\<首字母>-series\<前5位>\<第7起7位去-01>\<分支>\Ele\
        public static string BuildElePath(string tool, string branch)
        {
            string root = (CncRootPath ?? "").Trim();
            if (root.Length == 0) return null;
            string tno = (tool ?? "").Replace(" ", "");
            if (tno.Length < 5) return null;
            string path1 = tno.Substring(0, 1);
            string path2 = tno.Substring(0, 5);
            string path3 = tno.Length >= 13 ? tno.Substring(6, 7) : (tno.Length > 6 ? tno.Substring(6) : "");
            path3 = path3.Replace("-01", "");
            string b = (branch ?? "").Trim();
            return root.TrimEnd('\\') + "\\" + path1 + "-series\\" + path2 + "\\" + path3 + "\\" + b + "\\Ele\\";
        }
    }
}