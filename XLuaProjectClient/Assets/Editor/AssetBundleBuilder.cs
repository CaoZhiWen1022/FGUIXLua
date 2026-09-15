using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 RawResources 下 UIPackage / XLua / Fonts / D3Prefabs 打成 AssetBundle，
/// 并生成 AB/index.json（逻辑名 → path，hash 嵌入文件名）。
/// 例：uipackage\common_c9b050.ab、d3prefabs_c9b050.ab
/// </summary>
public static class AssetBundleBuilder
{
    private const string RawUIPackageRoot = "Assets/RawResources/UIPackage";
    private const string RawXLuaRoot = "Assets/RawResources/XLua";
    private const string RawFontsRoot = "Assets/RawResources/Fonts";
    private const string RawD3PrefabsRoot = "Assets/RawResources/D3Prefabs";
    private const string OutputRoot = "Assets/StreamingAssets/AB";
    private const string XLuaTempRoot = "Assets/ABBuildTemp/XLua";
    private const string FontsBundleName = "fonts.ab";
    private const string D3PrefabsBundleName = "d3prefabs.ab";
    private const string IndexFileName = "index.json";

    private static readonly string[] FontExtensions =
    {
        ".ttf", ".otf", ".ttc", ".fontsettings",
    };

    private class IndexDraft
    {
        public string Key;
        public string Path;
    }

    private static readonly List<IndexDraft> IndexDrafts = new List<IndexDraft>();

    [MenuItem("XLuaProject/Build AssetBundles", false, 0)]
    public static void BuildAll()
    {
        try
        {
            EnsureDirectory("Assets/StreamingAssets");
            EnsureDirectory(OutputRoot);
            EnsureDirectory(OutputRoot + "/uipackage");
            EnsureDirectory(OutputRoot + "/xlua");

            IndexDrafts.Clear();
            PrepareXLuaTempAssets();

            var builds = new List<AssetBundleBuild>();
            CollectUIPackageBuilds(builds);
            CollectXLuaBuilds(builds);
            CollectFontsBuilds(builds);
            CollectD3PrefabBuilds(builds);

            if (builds.Count == 0)
            {
                Debug.LogWarning("[AssetBundleBuilder] nothing to build.");
                return;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            var options = BuildAssetBundleOptions.ChunkBasedCompression
                          | BuildAssetBundleOptions.ForceRebuildAssetBundle;

            Debug.Log(string.Format("[AssetBundleBuilder] building {0} bundles for {1} ...", builds.Count, target));
            BuildPipeline.BuildAssetBundles(OutputRoot, builds.ToArray(), options, target);
            ApplyContentHashToBundles();
            WriteIndexJson();

            AssetDatabase.Refresh();
            Debug.Log("[AssetBundleBuilder] done → " + OutputRoot + " (+ index.json)");
        }
        finally
        {
            CleanupXLuaTempAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("XLuaProject/Clear AssetBundles", false, 1)]
    public static void ClearAll()
    {
        CleanupXLuaTempAssets();

        string abs = Path.GetFullPath(OutputRoot);
        if (!Directory.Exists(abs))
        {
            Debug.Log("[AssetBundleBuilder] AB folder already empty: " + OutputRoot);
            return;
        }

        string[] dirs = Directory.GetDirectories(abs);
        for (int i = 0; i < dirs.Length; i++)
        {
            string assetDir = ToAssetPath(dirs[i]);
            if (!string.IsNullOrEmpty(assetDir) && AssetDatabase.IsValidFolder(assetDir))
            {
                AssetDatabase.DeleteAsset(assetDir);
            }
            else if (Directory.Exists(dirs[i]))
            {
                Directory.Delete(dirs[i], true);
                string meta = dirs[i] + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }
        }

        string[] files = Directory.GetFiles(abs);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetFile = ToAssetPath(file);
            if (!string.IsNullOrEmpty(assetFile))
            {
                AssetDatabase.DeleteAsset(assetFile);
            }
            else
            {
                File.Delete(file);
                string meta = file + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[AssetBundleBuilder] cleared → " + OutputRoot);
    }

    /// <summary>
    /// 构建完成后：按内容生成 6 位 hash，将 xxx.ab 重命名为 xxx_{hash}.ab，并更新 IndexDraft.Path。
    /// </summary>
    private static void ApplyContentHashToBundles()
    {
        string outputAbs = Path.GetFullPath(OutputRoot);
        for (int i = 0; i < IndexDrafts.Count; i++)
        {
            IndexDraft draft = IndexDrafts[i];
            string relative = draft.Path.Replace('\\', '/');
            string abs = Path.Combine(outputAbs, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(abs))
            {
                Debug.LogWarning("[AssetBundleBuilder] hash rename miss file: " + abs);
                continue;
            }

            string hash = ComputeShortHash(abs);
            string dir = Path.GetDirectoryName(abs) ?? outputAbs;
            string fileName = Path.GetFileNameWithoutExtension(abs);
            string ext = Path.GetExtension(abs);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".ab";
            }

            // 清理同逻辑名的旧 hash 包：common_*.ab
            CleanupOldHashedBundles(dir, fileName, ext);

            string hashedFileName = fileName + "_" + hash + ext;
            string hashedAbs = Path.Combine(dir, hashedFileName);
            if (File.Exists(hashedAbs))
            {
                File.Delete(hashedAbs);
            }

            string metaSrc = abs + ".meta";
            string manifestSrc = abs + ".manifest";
            File.Move(abs, hashedAbs);

            if (File.Exists(manifestSrc))
            {
                string manifestDst = hashedAbs + ".manifest";
                if (File.Exists(manifestDst))
                {
                    File.Delete(manifestDst);
                }

                File.Move(manifestSrc, manifestDst);
            }

            if (File.Exists(metaSrc))
            {
                File.Delete(metaSrc);
            }

            string dirRel = Path.GetDirectoryName(relative);
            if (string.IsNullOrEmpty(dirRel))
            {
                draft.Path = hashedFileName.Replace('\\', '/');
            }
            else
            {
                draft.Path = (dirRel.Replace('\\', '/') + "/" + hashedFileName).Replace('\\', '/');
            }

            Debug.Log(string.Format("[AssetBundleBuilder] {0} → {1}", relative, draft.Path));
        }
    }

    private static void CleanupOldHashedBundles(string dir, string logicalBaseName, string ext)
    {
        if (!Directory.Exists(dir) || string.IsNullOrEmpty(logicalBaseName))
        {
            return;
        }

        string pattern = logicalBaseName + "_*" + ext;
        string[] files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            TryDeleteFile(files[i]);
            TryDeleteFile(files[i] + ".manifest");
            TryDeleteFile(files[i] + ".meta");
        }
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AssetBundleBuilder] delete failed: " + path + " " + e.Message);
        }
    }

    private static void WriteIndexJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        for (int i = 0; i < IndexDrafts.Count; i++)
        {
            IndexDraft draft = IndexDrafts[i];
            // path 已含 hash：uipackage\common_c9b050.ab
            string pathForJson = draft.Path.Replace('/', '\\');
            sb.Append("  \"");
            sb.Append(EscapeJson(draft.Key));
            sb.AppendLine("\": {");
            sb.Append("    \"path\": \"");
            sb.Append(EscapeJson(pathForJson));
            sb.AppendLine("\"");
            sb.Append("  }");
            if (i < IndexDrafts.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        sb.AppendLine("}");

        string indexPath = Path.Combine(Path.GetFullPath(OutputRoot), IndexFileName);
        File.WriteAllText(indexPath, sb.ToString(), new UTF8Encoding(false));
        Debug.Log(string.Format("[AssetBundleBuilder] wrote {0} entries → {1}", IndexDrafts.Count, indexPath));
    }

    private static string ComputeShortHash(string filePath)
    {
        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hash = md5.ComputeHash(stream);
            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2"));
            }

            string full = hex.ToString();
            return full.Length >= 6 ? full.Substring(0, 6) : full.PadRight(6, '0');
        }
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static void CollectUIPackageBuilds(List<AssetBundleBuild> builds)
    {
        if (!Directory.Exists(RawUIPackageRoot))
        {
            Debug.LogWarning("[AssetBundleBuilder] missing " + RawUIPackageRoot);
            return;
        }

        string[] dirs = Directory.GetDirectories(RawUIPackageRoot);
        for (int i = 0; i < dirs.Length; i++)
        {
            string dirName = Path.GetFileName(dirs[i]);
            string folderAssetPath = (RawUIPackageRoot + "/" + dirName).Replace('\\', '/');
            var assetNames = CollectAssetPathsUnder(folderAssetPath);
            if (assetNames.Count == 0)
            {
                continue;
            }

            string relative = "uipackage/" + dirName.ToLowerInvariant() + ".ab";
            builds.Add(new AssetBundleBuild
            {
                assetBundleName = relative,
                assetNames = assetNames.ToArray(),
            });
            IndexDrafts.Add(new IndexDraft { Key = dirName, Path = relative });
        }
    }

    private static void CollectXLuaBuilds(List<AssetBundleBuild> builds)
    {
        if (!Directory.Exists(XLuaTempRoot))
        {
            return;
        }

        string[] dirs = Directory.GetDirectories(XLuaTempRoot);
        for (int i = 0; i < dirs.Length; i++)
        {
            string dirName = Path.GetFileName(dirs[i]);
            string folderAssetPath = (XLuaTempRoot + "/" + dirName).Replace('\\', '/');
            var assetNames = CollectAssetPathsUnder(folderAssetPath, ".bytes");
            if (assetNames.Count == 0)
            {
                continue;
            }

            string relative = "xlua/" + dirName.ToLowerInvariant() + ".ab";
            builds.Add(new AssetBundleBuild
            {
                assetBundleName = relative,
                assetNames = assetNames.ToArray(),
            });
            IndexDrafts.Add(new IndexDraft { Key = dirName, Path = relative });
        }
    }

    private static void CollectFontsBuilds(List<AssetBundleBuild> builds)
    {
        if (!Directory.Exists(RawFontsRoot))
        {
            Debug.LogWarning("[AssetBundleBuilder] missing " + RawFontsRoot);
            return;
        }

        string abs = Path.GetFullPath(RawFontsRoot);
        string[] files = Directory.GetFiles(abs, "*", SearchOption.AllDirectories);
        var assetNames = new List<string>();
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsFontAssetFile(file))
            {
                continue;
            }

            string assetPath = ToAssetPath(file);
            if (!string.IsNullOrEmpty(assetPath))
            {
                assetNames.Add(assetPath);
            }
        }

        if (assetNames.Count == 0)
        {
            Debug.LogWarning("[AssetBundleBuilder] no font assets under " + RawFontsRoot);
            return;
        }

        builds.Add(new AssetBundleBuild
        {
            assetBundleName = FontsBundleName,
            assetNames = assetNames.ToArray(),
        });
        IndexDrafts.Add(new IndexDraft { Key = "Fonts", Path = FontsBundleName });
        Debug.Log(string.Format("[AssetBundleBuilder] fonts.ab assets: {0}", assetNames.Count));
    }

    /// <summary>
    /// 整个 D3Prefabs 打成一个包：d3prefabs.ab，index 逻辑名 D3Prefabs。
    /// </summary>
    private static void CollectD3PrefabBuilds(List<AssetBundleBuild> builds)
    {
        if (!Directory.Exists(RawD3PrefabsRoot))
        {
            Debug.LogWarning("[AssetBundleBuilder] missing " + RawD3PrefabsRoot);
            return;
        }

        var assetNames = CollectAssetPathsUnder(RawD3PrefabsRoot);
        if (assetNames.Count == 0)
        {
            Debug.LogWarning("[AssetBundleBuilder] no assets under " + RawD3PrefabsRoot);
            return;
        }

        builds.Add(new AssetBundleBuild
        {
            assetBundleName = D3PrefabsBundleName,
            assetNames = assetNames.ToArray(),
        });
        IndexDrafts.Add(new IndexDraft { Key = "D3Prefabs", Path = D3PrefabsBundleName });
        Debug.Log(string.Format("[AssetBundleBuilder] d3prefabs.ab assets: {0}", assetNames.Count));
    }

    private static bool IsFontAssetFile(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        for (int i = 0; i < FontExtensions.Length; i++)
        {
            if (ext.Equals(FontExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> CollectAssetPathsUnder(string folderAssetPath, string requiredExtension = null)
    {
        var result = new List<string>();
        string abs = Path.GetFullPath(folderAssetPath);
        if (!Directory.Exists(abs))
        {
            return result;
        }

        string[] files = Directory.GetFiles(abs, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (requiredExtension != null
                && !file.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetPath = ToAssetPath(file);
            if (!string.IsNullOrEmpty(assetPath))
            {
                result.Add(assetPath);
            }
        }

        return result;
    }

    private static string ToAssetPath(string absolutePath)
    {
        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (!full.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return "Assets" + full.Substring(dataPath.Length);
    }

    private static void PrepareXLuaTempAssets()
    {
        CleanupXLuaTempAssets();
        EnsureDirectory(XLuaTempRoot);

        if (!Directory.Exists(RawXLuaRoot))
        {
            Debug.LogWarning("[AssetBundleBuilder] missing " + RawXLuaRoot);
            return;
        }

        string absRaw = Path.GetFullPath(RawXLuaRoot);
        string absTemp = Path.GetFullPath(XLuaTempRoot);

        string[] luaFiles = Directory.GetFiles(absRaw, "*.lua", SearchOption.AllDirectories);
        for (int i = 0; i < luaFiles.Length; i++)
        {
            string src = luaFiles[i];
            string rel = src.Substring(absRaw.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dstRel = Path.ChangeExtension(rel, ".bytes");
            string dst = Path.Combine(absTemp, dstRel);

            string dstDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
            {
                Directory.CreateDirectory(dstDir);
            }

            File.Copy(src, dst, true);
        }

        AssetDatabase.Refresh();
    }

    private static void CleanupXLuaTempAssets()
    {
        if (AssetDatabase.IsValidFolder("Assets/ABBuildTemp"))
        {
            AssetDatabase.DeleteAsset("Assets/ABBuildTemp");
        }

        string abs = Path.GetFullPath("Assets/ABBuildTemp");
        if (Directory.Exists(abs))
        {
            Directory.Delete(abs, true);
        }

        string absMeta = abs + ".meta";
        if (File.Exists(absMeta))
        {
            File.Delete(absMeta);
        }
    }

    private static void EnsureDirectory(string assetOrAbsPath)
    {
        string assetPath = assetOrAbsPath.Replace('\\', '/');
        string abs = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
            ? Path.GetFullPath(assetPath)
            : assetPath;

        if (!Directory.Exists(abs))
        {
            Directory.CreateDirectory(abs);
        }
    }
}
