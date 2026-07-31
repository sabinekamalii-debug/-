using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// ═══════════════════════════════════════════════════════════
///  数值设计汇总导出器（给开发者做数值平衡用，不是运行时日志）
///
///  把后台「设计好」的所有数值初始值聚到一个文件：
///     - 干员设计数值 (OperatorData 资产)
///     - 敌人设计数值 (EnemyData2 资产)
///     - 天赋卡设计数值 (TalentCardData 资产)
///     - 天赋树节点数值 (TalentTreeData 硬编码)
///     - 肉鸽平衡常量 (BalanceConfig)
///
///  产物：
///     Assets/数值设计汇总/数值设计汇总.csv   （Excel 打开，可筛选/排序，最适合平衡）
///     Assets/数值设计汇总/数值设计汇总.md    （分章节宽表，阅读一眼看）
///
///  触发：菜单 Tools → 数值设计汇总；编译后 / 保存 .asset 后自动刷新。
/// ═══════════════════════════════════════════════════════════
[InitializeOnLoad]
public static class DesignValueSummary
{
    private const string OutputDir = "数值设计汇总";
    private const string CsvName = "数值设计汇总.csv";
    private const string MdName = "数值设计汇总.md";

    private static bool _pending;

    static DesignValueSummary()
    {
        AssemblyReloadEvents.afterAssemblyReload += () => _pending = true;
        EditorApplication.update += OnUpdate;
    }

    private class Post : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
            {
                if (p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) { _pending = true; break; }
            }
        }
    }

    private static void OnUpdate()
    {
        if (_pending && !EditorApplication.isCompiling)
        {
            _pending = false;
            ExportAll();
        }
    }

    [MenuItem("Tools/数值设计汇总/全部导出")]
    public static void ExportAll() { ExportCsv(); ExportMarkdown(); }

    [MenuItem("Tools/数值设计汇总/导出CSV")]
    public static void ExportCsv() => WriteFile(CsvName, BuildCsv());

    [MenuItem("Tools/数值设计汇总/导出Markdown")]
    public static void ExportMarkdown() => WriteFile(MdName, BuildMarkdown());

    // ───────────────────────── 文件写入 ─────────────────────────

    private static void WriteFile(string name, string content)
    {
        string dir = System.IO.Path.Combine(Application.dataPath, OutputDir);
        Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    // ───────────────────────── 字段导出规则 ─────────────────────────

    private static bool IsExportable(Type t)
    {
        if (t.IsArray) return false;
        if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false; // 跳过预制体/图标等
        return t == typeof(int) || t == typeof(float) || t == typeof(double) ||
               t == typeof(long) || t == typeof(uint) || t == typeof(ulong) ||
               t == typeof(short) || t == typeof(byte) || t == typeof(decimal) ||
               t == typeof(bool) || t == typeof(string) || t.IsEnum;
    }

    private static List<FieldInfo> CollectFields(Type t)
    {
        var list = new List<FieldInfo>();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (IsExportable(f.FieldType)) list.Add(f);
        return list;
    }

    private static string FormatVal(FieldInfo f, object obj)
    {
        object v = f.GetValue(obj);
        if (v == null) return "-";
        if (f.FieldType.IsEnum) return v.ToString();
        if (f.FieldType == typeof(bool)) return (bool)v ? "true" : "false";
        if (f.FieldType == typeof(float)) return ((float)v).ToString("F2", CultureInfo.InvariantCulture);
        if (f.FieldType == typeof(double)) return ((double)v).ToString("F2", CultureInfo.InvariantCulture);
        return v.ToString();
    }

    private static string Friendly(Type t)
    {
        if (t.IsEnum) return t.Name;
        if (t == typeof(int)) return "int";
        if (t == typeof(float)) return "float";
        if (t == typeof(double)) return "double";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(string)) return "string";
        if (t == typeof(long)) return "long";
        return t.Name;
    }

    // ───────────────────────── CSV（扁平：每行一个数值，最利于 Excel 筛选排序） ─────────────────────────

    private static string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("分类,名称,字段,数值,类型");

        // 1. 平衡配置常量
        foreach (var f in typeof(BalanceConfig).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (!f.IsLiteral || f.IsInitOnly) continue;
            object v = f.GetRawConstantValue();
            if (v == null) continue;
            AppendCsv(sb, "平衡配置", f.Name, f.Name, Csv(FormatConst(f, v)), Friendly(f.FieldType));
        }

        // 2. 干员资产
        AppendAssetsCsv(sb, "干员", typeof(OperatorData));
        // 3. 敌人资产
        AppendAssetsCsv(sb, "敌人", typeof(EnemyData2));
        // 4. 天赋卡资产
        AppendAssetsCsv(sb, "天赋卡", typeof(TalentCardData));
        // 5. 天赋树节点
        foreach (var n in TalentTreeData.Nodes)
        {
            AppendCsv(sb, "天赋树", n.displayName, "nodeId", Csv(n.nodeId), "string");
            AppendCsv(sb, "天赋树", n.displayName, "branch", Csv(TalentTreeData.BranchDisplayName(n.branch)), "string");
            AppendCsv(sb, "天赋树", n.displayName, "order", n.order.ToString(), "int");
            AppendCsv(sb, "天赋树", n.displayName, "cost", n.Cost.ToString(), "int");
            AppendCsv(sb, "天赋树", n.displayName, "effectType", Csv(n.effect.type.ToString()), "enum");
            AppendCsv(sb, "天赋树", n.displayName, "effectValue", n.effect.value.ToString(), "int");
            AppendCsv(sb, "天赋树", n.displayName, "isBig", n.IsBig ? "true" : "false", "bool");
        }
        return sb.ToString();
    }

    private static void AppendAssetsCsv(StringBuilder sb, string cat, Type assetType)
    {
        var fields = CollectFields(assetType);
        var guids = AssetDatabase.FindAssets($"t:{assetType.Name}", new[] { "Assets" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath(path, assetType);
            if (asset == null) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (var f in fields)
                AppendCsv(sb, cat, name, f.Name, Csv(FormatVal(f, asset)), Friendly(f.FieldType));
        }
    }

    private static string FormatConst(FieldInfo f, object v)
    {
        if (f.FieldType == typeof(float)) return ((float)v).ToString("F2", CultureInfo.InvariantCulture);
        if (f.FieldType == typeof(double)) return ((double)v).ToString("F2", CultureInfo.InvariantCulture);
        return v.ToString();
    }

    private static void AppendCsv(StringBuilder sb, string cat, string name, string field, string val, string type)
    {
        sb.AppendLine($"{Csv(cat)},{Csv(name)},{Csv(field)},{val},{type}");
    }

    private static string Csv(string s)
    {
        if (s == null) s = "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ───────────────────────── Markdown（分章节宽表，一眼看） ─────────────────────────

    private static string BuildMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 📐 数值设计汇总（后台设计数值一览）");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("> 本文件汇总后台「设计好」的所有数值初始值，供开发者做数值平衡。");
        sb.AppendLine("> 每次编译后 / 保存 .asset 后自动刷新；也可通过 `Tools → 数值设计汇总` 手动导出。");
        sb.AppendLine("> 自由筛选排序请用同名 `.csv`（Excel 打开）。");
        sb.AppendLine();

        // 一、平衡配置
        sb.AppendLine("## 一、肉鸽平衡常量 (BalanceConfig)");
        sb.AppendLine();
        sb.AppendLine("| 字段 | 类型 | 值 |");
        sb.AppendLine("|------|------|-----|");
        foreach (var f in typeof(BalanceConfig).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (!f.IsLiteral || f.IsInitOnly) continue;
            object v = f.GetRawConstantValue();
            if (v == null) continue;
            sb.AppendLine($"| `{f.Name}` | {Friendly(f.FieldType)} | {Md(FormatConst(f, v))} |");
        }
        sb.AppendLine();

        // 二 ~ 四、资产宽表
        AppendAssetsMd(sb, "二、干员设计数值 (OperatorData)", typeof(OperatorData));
        AppendAssetsMd(sb, "三、敌人设计数值 (EnemyData2)", typeof(EnemyData2));
        AppendAssetsMd(sb, "四、天赋卡设计数值 (TalentCardData)", typeof(TalentCardData));

        // 五、天赋树节点
        sb.AppendLine("## 五、天赋树节点数值 (TalentTreeData)");
        sb.AppendLine();
        sb.AppendLine("| 节点 | 分支 | 阶 | 消耗天赋点 | 效果类型 | 效果值 | 大天赋 |");
        sb.AppendLine("|------|------|----|-----------|----------|--------|--------|");
        foreach (var n in TalentTreeData.Nodes)
        {
            sb.AppendLine($"| {Md(n.displayName)} | {TalentTreeData.BranchDisplayName(n.branch)} | {n.order} | {n.Cost} | {n.effect.type} | {n.effect.value} | {(n.IsBig ? "★" : "")} |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    private static void AppendAssetsMd(StringBuilder sb, string title, Type assetType)
    {
        var fields = CollectFields(assetType);
        var guids = AssetDatabase.FindAssets($"t:{assetType.Name}", new[] { "Assets" });
        if (guids.Length == 0) return;

        sb.AppendLine($"## {title}（{guids.Length} 个）");
        sb.AppendLine();

        // 表头
        sb.Append("| 名称 ");
        foreach (var f in fields) sb.Append($"| {f.Name} ");
        sb.AppendLine("|");

        sb.Append("|------");
        foreach (var _ in fields) sb.Append("|------");
        sb.AppendLine("|");

        // 行
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath(path, assetType);
            if (asset == null) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            sb.Append($"| {Md(name)} ");
            foreach (var f in fields)
                sb.Append($"| {Md(FormatVal(f, asset).Replace("\r", "").Replace("\n", " "))} ");
            sb.AppendLine("|");
        }
        sb.AppendLine();
    }

    private static string Md(string s)
    {
        if (s == null) s = "";
        return s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
