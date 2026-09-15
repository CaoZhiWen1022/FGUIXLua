using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 FairyGUI 导出的 C# UI 脚本转换为 Lua 节点绑定脚本。
/// Scripts/FGUI/.../UI_XxxPanel.cs → RawResources/XLua/FGUILua/.../UI_Xxx.lua
/// </summary>
public static class FGUICsToLuaGenerator
{
    private const string CsRoot = "Assets/Scripts/FGUI";
    private const string LuaRoot = "Assets/RawResources/XLua/FGUILua";

    private static readonly Regex ClassRegex =
        new Regex(@"partial\s+class\s+(UI_\w+)", RegexOptions.Compiled);

    private static readonly Regex UrlRegex =
        new Regex(@"const\s+string\s+URL\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex CreateObjectRegex =
        new Regex(@"UIPackage\.CreateObject\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

    private static readonly Regex GetChildAtRegex =
        new Regex(@"(\w+)\s*=\s*\((\w+)\)GetChildAt\((\d+)\)\s*;", RegexOptions.Compiled);

    private static readonly Regex GetChildRegex =
        new Regex(@"(\w+)\s*=\s*\((\w+)\)GetChild\(\s*""([^""]+)""\s*\)\s*;", RegexOptions.Compiled);

    [MenuItem("FairyGUI/Generate Lua UI Scripts")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(CsRoot))
        {
            Debug.LogError($"[FGUICsToLua] CS root not found: {CsRoot}");
            return;
        }

        var csFiles = Directory.GetFiles(CsRoot, "UI_*.cs", SearchOption.AllDirectories);
        int count = 0;
        foreach (var csPath in csFiles)
        {
            if (csPath.EndsWith("Binder.cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (GenerateOne(csPath))
                count++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[FGUICsToLua] Generated {count} lua script(s) → {LuaRoot}");
    }

    [MenuItem("Assets/FairyGUI/Generate Lua UI Script", true)]
    private static bool ValidateGenerateSelected()
    {
        var obj = Selection.activeObject;
        if (obj == null)
            return false;
        string path = AssetDatabase.GetAssetPath(obj);
        return path.StartsWith(CsRoot, StringComparison.OrdinalIgnoreCase)
               && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
               && Path.GetFileName(path).StartsWith("UI_", StringComparison.Ordinal);
    }

    [MenuItem("Assets/FairyGUI/Generate Lua UI Script")]
    public static void GenerateSelected()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (GenerateOne(path))
        {
            AssetDatabase.Refresh();
            Debug.Log($"[FGUICsToLua] Generated from {path}");
        }
    }

    private static bool GenerateOne(string csAssetPath)
    {
        string fullCsPath = ToFullPath(csAssetPath);
        if (!File.Exists(fullCsPath))
        {
            Debug.LogError($"[FGUICsToLua] File not found: {csAssetPath}");
            return false;
        }

        string content = File.ReadAllText(fullCsPath, Encoding.UTF8);

        var classMatch = ClassRegex.Match(content);
        if (!classMatch.Success)
        {
            Debug.LogWarning($"[FGUICsToLua] Skip (no UI class): {csAssetPath}");
            return false;
        }

        string csClassName = classMatch.Groups[1].Value; // UI_GameLoadingPanel
        string luaClassName = ToLuaClassName(csClassName); // UI_GameLoading

        var urlMatch = UrlRegex.Match(content);
        string url = urlMatch.Success ? urlMatch.Groups[1].Value : "";

        var createMatch = CreateObjectRegex.Match(content);
        if (!createMatch.Success)
        {
            Debug.LogWarning($"[FGUICsToLua] Skip (no CreateObject): {csAssetPath}");
            return false;
        }

        string pkg = createMatch.Groups[1].Value;
        string res = createMatch.Groups[2].Value;

        var nodes = ParseNodes(content);
        string luaRelativeDir = BuildLuaRelativeDir(csAssetPath);
        string luaAssetPath = $"{LuaRoot}/{luaRelativeDir}/{luaClassName}.lua".Replace('\\', '/');
        string fullLuaPath = ToFullPath(luaAssetPath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullLuaPath) ?? LuaRoot);
        File.WriteAllText(fullLuaPath, BuildLua(luaClassName, url, pkg, res, nodes, csAssetPath), new UTF8Encoding(false));
        return true;
    }

    private static List<(string field, string getter)> ParseNodes(string content)
    {
        var nodes = new List<(string field, string getter)>();
        var seen = new HashSet<string>();

        foreach (Match m in GetChildAtRegex.Matches(content))
        {
            string field = m.Groups[1].Value;
            if (!seen.Add(field))
                continue;
            string castType = m.Groups[2].Value;
            string index = m.Groups[3].Value;
            string raw = $"view:GetChildAt({index})";
            nodes.Add((field, NeedAutoWrap(castType) ? $"FGUIPanel.AutoWrap({raw})" : raw));
        }

        foreach (Match m in GetChildRegex.Matches(content))
        {
            string field = m.Groups[1].Value;
            if (!seen.Add(field))
                continue;
            string castType = m.Groups[2].Value;
            string childName = m.Groups[3].Value;
            string raw = $"view:GetChild(\"{childName}\")";
            nodes.Add((field, NeedAutoWrap(castType) ? $"FGUIPanel.AutoWrap({raw})" : raw));
        }

        return nodes;
    }

    /// <summary>
    /// GComponent / 自定义 UI_Xxx 可能是跨包公共组件，需要 AutoWrap。
    /// </summary>
    private static bool NeedAutoWrap(string castType)
    {
        return castType == "GComponent"
               || castType.StartsWith("UI_", StringComparison.Ordinal);
    }

    /// <summary>
    /// Scripts/FGUI/GameLoading/GameLoading/UI_xxx.cs → GameLoading
    /// 去掉连续重复的目录名，保持包级目录结构。
    /// </summary>
    private static string BuildLuaRelativeDir(string csAssetPath)
    {
        string normalized = csAssetPath.Replace('\\', '/');
        string prefix = CsRoot.TrimEnd('/') + "/";
        string relative = normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(prefix.Length)
            : normalized;

        string dir = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";
        if (string.IsNullOrEmpty(dir))
            return "";

        var parts = new List<string>(dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
        // GameLoading/GameLoading → GameLoading
        for (int i = parts.Count - 1; i > 0; i--)
        {
            if (string.Equals(parts[i], parts[i - 1], StringComparison.OrdinalIgnoreCase))
                parts.RemoveAt(i);
        }

        return string.Join("/", parts);
    }

    private static string ToLuaClassName(string csClassName)
    {
        // UI_GameLoadingPanel → UI_GameLoading
        if (csClassName.EndsWith("Panel", StringComparison.Ordinal) && csClassName.Length > "UI_".Length + "Panel".Length)
            return csClassName.Substring(0, csClassName.Length - "Panel".Length);
        return csClassName;
    }

    private static string BuildLua(string className, string url, string pkg, string res,
        List<(string field, string getter)> nodes, string sourceCs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Auto generated by FGUICsToLuaGenerator. Do not modify.");
        sb.AppendLine($"-- Source: {sourceCs.Replace('\\', '/')}");
        sb.AppendLine();
        sb.AppendLine($"{className} = setmetatable({{}}, FGUIPanel)");
        sb.AppendLine($"{className}.__index = {className}");
        sb.AppendLine();
        sb.AppendLine($"{className}.URL = \"{url}\"");
        sb.AppendLine($"{className}.PKG = \"{pkg}\"");
        sb.AppendLine($"{className}.RES = \"{res}\"");
        sb.AppendLine();
        sb.AppendLine($"function {className}.Create()");
        sb.AppendLine($"    return FGUIPanel.Create({className}, {className}.PKG, {className}.RES)");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine($"function {className}:OnInit(view)");
        foreach (var node in nodes)
            sb.AppendLine($"    self.{node.field} = {node.getter}");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine($"FGUIPanel.RegisterExtension({className})");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string ToFullPath(string assetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? "";
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
