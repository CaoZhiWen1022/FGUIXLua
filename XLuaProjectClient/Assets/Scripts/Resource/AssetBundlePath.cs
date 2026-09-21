using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// AssetBundle 路径解析：只认 index.json 的逻辑名 → 相对路径，不含具体包名约定。
/// 启动时由 GameLaunch 加载 index 注入映射（hash 嵌在文件名中）。
/// 解析优先级：热更本地目录 > CDN > StreamingAssets。
/// </summary>
public static class AssetBundlePath
{
    public const string RootFolderName = "AB";
    public const string BundleExtension = ".ab";
    public const string IndexFileName = "index.json";

    public sealed class IndexEntry
    {
        public string Path;
        public string Hash;
        public bool HasLua;
    }

    private static string _cdnRoot = string.Empty;
    private static string _hotUpdateAbRoot;
    private static readonly Dictionary<string, IndexEntry> Index =
        new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>CDN 根（可为本地目录或 http(s) URL，指向含 AB 的根或 AB 目录本身）。</summary>
    public static string CdnRoot
    {
        get { return _cdnRoot ?? string.Empty; }
    }

    public static bool HasCdn
    {
        get { return !string.IsNullOrEmpty(CdnRoot); }
    }

    public static bool HasIndex
    {
        get { return Index.Count > 0; }
    }

    /// <summary>包内只读 AB 根：StreamingAssets/AB。</summary>
    public static string StreamingAbRoot
    {
        get { return CombineUrlOrPath(Application.streamingAssetsPath, RootFolderName); }
    }

    /// <summary>
    /// 热更本地 AB 根（默认可写目录 persistentDataPath/AB）。
    /// </summary>
    public static string HotUpdateAbRoot
    {
        get
        {
            if (!string.IsNullOrEmpty(_hotUpdateAbRoot))
            {
                return _hotUpdateAbRoot;
            }

            return Path.Combine(Application.persistentDataPath, RootFolderName).Replace('\\', '/');
        }
    }

    /// <summary>规范化后的 CDN AB 根（含 /AB）。</summary>
    public static string CdnAbRoot
    {
        get
        {
            if (!HasCdn)
            {
                return string.Empty;
            }

            return NormalizeAbRoot(CdnRoot);
        }
    }

    public static void SetCdnRoot(string root)
    {
        _cdnRoot = string.IsNullOrEmpty(root) ? string.Empty : TrimSlash(root.Trim());
        Debug.Log(string.Format(
            "[AssetBundlePath] CdnRoot={0} HotUpdate={1} Streaming={2}",
            string.IsNullOrEmpty(_cdnRoot) ? "(empty)" : _cdnRoot,
            HotUpdateAbRoot,
            StreamingAbRoot));
    }

    public static void ClearCdnRoot()
    {
        SetCdnRoot(string.Empty);
    }

    public static void SetHotUpdateAbRoot(string root)
    {
        _hotUpdateAbRoot = string.IsNullOrEmpty(root) ? null : TrimSlash(root.Trim()).Replace('\\', '/');
        Debug.Log("[AssetBundlePath] HotUpdateAbRoot=" + HotUpdateAbRoot);
    }

    public static void ClearIndex()
    {
        Index.Clear();
    }

    /// <summary>
    /// 解析 index.json：{ "逻辑名": { "path": "相对路径.ab", "lua": true }, ... }
    /// </summary>
    public static bool ApplyIndexJson(string json)
    {
        Index.Clear();
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            ParseIndexObject(json);
            Debug.Log(string.Format("[AssetBundlePath] index applied, entries={0}", Index.Count));
            return Index.Count > 0;
        }
        catch (Exception e)
        {
            Debug.LogError("[AssetBundlePath] ApplyIndexJson failed: " + e.Message);
            Index.Clear();
            return false;
        }
    }

    public static bool TryGetEntry(string key, out IndexEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return Index.TryGetValue(key, out entry);
    }

    public static string GetRelativePathByKey(string key)
    {
        IndexEntry entry;
        if (!TryGetEntry(key, out entry) || entry == null || string.IsNullOrEmpty(entry.Path))
        {
            return null;
        }

        return NormalizeRelative(entry.Path);
    }

    /// <summary>从 path 文件名解析 6 位 hash（xxx_{hash}.ab）；没有则返回 null。</summary>
    public static string GetHashByKey(string key)
    {
        IndexEntry entry;
        if (!TryGetEntry(key, out entry) || entry == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(entry.Hash))
        {
            return entry.Hash;
        }

        return ExtractHashFromPath(entry.Path);
    }

    public static string ExtractHashFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string name = Path.GetFileNameWithoutExtension(path.Replace('\\', '/'));
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        int idx = name.LastIndexOf('_');
        if (idx < 0 || idx + 1 >= name.Length)
        {
            return null;
        }

        string hash = name.Substring(idx + 1);
        return hash.Length == 6 ? hash : null;
    }

    /// <summary>从 index 收集所有相对路径（可用于枚举）。</summary>
    public static List<string> GetAllRelativePathsFromIndex()
    {
        var list = new List<string>();
        foreach (var pair in Index)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Value.Path))
            {
                continue;
            }

            string relative = NormalizeRelative(pair.Value.Path);
            if (!string.IsNullOrEmpty(relative) && !list.Contains(relative))
            {
                list.Add(relative);
            }
        }

        return list;
    }

    /// <summary>从 index 收集构建时标记为含 Lua 的包相对路径。</summary>
    public static List<string> GetLuaRelativePathsFromIndex()
    {
        var list = new List<string>();
        foreach (var pair in Index)
        {
            if (pair.Value == null || !pair.Value.HasLua || string.IsNullOrEmpty(pair.Value.Path))
            {
                continue;
            }

            string relative = NormalizeRelative(pair.Value.Path);
            if (!string.IsNullOrEmpty(relative) && !list.Contains(relative))
            {
                list.Add(relative);
            }
        }

        return list;
    }

    public static string GetIndexRelative()
    {
        return IndexFileName;
    }

    // ---------- 解析实际加载地址 ----------

    /// <summary>
    /// 按相对路径解析最终加载地址：热更本地(若存在) → CDN → StreamingAssets。
    /// WebGL 禁用热更本地 file 路径（浏览器禁止 file://idbfs）。
    /// </summary>
    public static string ResolveBundleUrl(string relativeUnderAb)
    {
        string relative = NormalizeRelative(relativeUnderAb);
        if (string.IsNullOrEmpty(relative))
        {
            return string.Empty;
        }

        // index.json 固定文件名：禁止走热更本地，否则旧 index 会一直命中，hash 包名失效
        bool isIndex = string.Equals(relative, IndexFileName, StringComparison.OrdinalIgnoreCase);

        // WebGL 不能用 persistentDataPath 的 file:// 回读，跳过热更本地优先
        if (!isIndex && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            string hotPath = CombineUrlOrPath(HotUpdateAbRoot, relative);
            if (LocalFileExists(hotPath))
            {
                return hotPath;
            }
        }

        if (HasCdn)
        {
            return CombineUrlOrPath(CdnAbRoot, relative);
        }

        return CombineUrlOrPath(StreamingAbRoot, relative);
    }

    public static string ResolveIndexUrl()
    {
        return ResolveBundleUrl(GetIndexRelative());
    }

    public static string ResolveBundleByKey(string key)
    {
        return ResolveBundleUrl(GetRelativePathByKey(key));
    }

    /// <summary>热更目录下的落盘路径（下载缓存用）。</summary>
    public static string GetHotUpdateBundlePath(string relativeUnderAb)
    {
        return CombineUrlOrPath(HotUpdateAbRoot, NormalizeRelative(relativeUnderAb));
    }

    public static string GetStreamingBundlePath(string relativeUnderAb)
    {
        return CombineUrlOrPath(StreamingAbRoot, NormalizeRelative(relativeUnderAb));
    }

    public static string GetCdnBundlePath(string relativeUnderAb)
    {
        if (!HasCdn)
        {
            return string.Empty;
        }

        return CombineUrlOrPath(CdnAbRoot, NormalizeRelative(relativeUnderAb));
    }

    public static bool IsRemoteUrl(string pathOrUrl)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
        {
            return false;
        }

        return pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static string CombineUrlOrPath(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return TrimSlash(root ?? string.Empty);
        }

        relative = relative.Replace('\\', '/').TrimStart('/');
        root = TrimSlash(root ?? string.Empty);

        if (IsRemoteUrl(root) || root.IndexOf("://", StringComparison.Ordinal) >= 0)
        {
            return root + "/" + relative;
        }

        return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)).Replace('\\', '/');
    }

    public static string NormalizeRelative(string relativeUnderAb)
    {
        if (string.IsNullOrEmpty(relativeUnderAb))
        {
            return string.Empty;
        }

        return relativeUnderAb.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
    }

    /// <summary>去掉 .prefab 后缀，统一为正斜杠相对路径（保留大小写，供 index 查找）。</summary>
    public static string NormalizeD3PrefabKey(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            return string.Empty;
        }

        string key = prefabName.Replace('\\', '/').Trim().TrimStart('/');
        if (key.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            key = key.Substring(0, key.Length - ".prefab".Length);
        }

        return key;
    }

    private static string NormalizeAbRoot(string root)
    {
        root = TrimSlash(root ?? string.Empty);
        if (root.EndsWith("/" + RootFolderName, StringComparison.OrdinalIgnoreCase)
            || root.EndsWith("\\" + RootFolderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(root.Replace('\\', '/')), RootFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return root.Replace('\\', '/');
        }

        return CombineUrlOrPath(root, RootFolderName);
    }

    private static bool LocalFileExists(string path)
    {
        if (string.IsNullOrEmpty(path) || IsRemoteUrl(path) || path.IndexOf("://", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string TrimSlash(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return s.TrimEnd('/', '\\');
    }

    // --- 轻量 JSON 解析（一层嵌套 path）---

    private static void ParseIndexObject(string json)
    {
        int i = 0;
        SkipWs(json, ref i);
        Expect(json, ref i, '{');

        while (true)
        {
            SkipWs(json, ref i);
            if (i >= json.Length)
            {
                break;
            }

            if (json[i] == '}')
            {
                i++;
                break;
            }

            string key = ReadJsonString(json, ref i);
            SkipWs(json, ref i);
            Expect(json, ref i, ':');
            SkipWs(json, ref i);

            string path = null;
            bool hasLua = false;
            Expect(json, ref i, '{');
            while (true)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    break;
                }

                string field = ReadJsonString(json, ref i);
                SkipWs(json, ref i);
                Expect(json, ref i, ':');
                SkipWs(json, ref i);
                string value = ReadJsonStringOrLiteral(json, ref i);

                if (string.Equals(field, "path", StringComparison.OrdinalIgnoreCase))
                {
                    path = value;
                }
                else if (string.Equals(field, "lua", StringComparison.OrdinalIgnoreCase))
                {
                    hasLua = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                }

                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                }
            }

            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(path))
            {
                Index[key] = new IndexEntry
                {
                    Path = path,
                    Hash = ExtractHashFromPath(path) ?? string.Empty,
                    HasLua = hasLua,
                };
            }

            SkipWs(json, ref i);
            if (i < json.Length && json[i] == ',')
            {
                i++;
            }
        }
    }

    private static void SkipWs(string s, ref int i)
    {
        while (i < s.Length)
        {
            char c = s[i];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                i++;
                continue;
            }

            break;
        }
    }

    private static void Expect(string s, ref int i, char ch)
    {
        SkipWs(s, ref i);
        if (i >= s.Length || s[i] != ch)
        {
            throw new Exception("expect '" + ch + "' at " + i);
        }

        i++;
    }

    private static string ReadJsonStringOrLiteral(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length)
        {
            throw new Exception("expect value at " + i);
        }

        if (s[i] == '"')
        {
            return ReadJsonString(s, ref i);
        }

        int start = i;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == ',' || c == '}' || c == ']' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                break;
            }

            i++;
        }

        if (i == start)
        {
            throw new Exception("expect value at " + start);
        }

        return s.Substring(start, i - start);
    }

    private static string ReadJsonString(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length || s[i] != '"')
        {
            throw new Exception("expect string at " + i);
        }

        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"')
            {
                return sb.ToString();
            }

            if (c == '\\' && i < s.Length)
            {
                char n = s[i++];
                if (n == 'n')
                {
                    sb.Append('\n');
                }
                else if (n == 'r')
                {
                    sb.Append('\r');
                }
                else if (n == 't')
                {
                    sb.Append('\t');
                }
                else
                {
                    sb.Append(n);
                }

                continue;
            }

            sb.Append(c);
        }

        throw new Exception("unterminated string");
    }
}
