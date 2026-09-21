using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按 RawResources 文件夹标记收集 AssetBundle，并生成 AB/index.json（逻辑名 → path，hash 嵌入文件名）。
/// 输出路径 = 相对 RawResources 的小写路径 + .ab，例如：
/// RawResources/D3Prefabs → AB/d3prefabs.ab
/// RawResources/XLua/FGUILua → AB/xlua/fguilua.ab
/// </summary>
public static class AssetBundleBuilder
{
    private const string OutputRoot = "Assets/StreamingAssets/AB";
    private const string LuaTempRoot = "Assets/ABBuildTemp";
    private const string IndexFileName = "index.json";

    /// <summary>Unity 默认不打进 AB 的源文件，构建时复制为 .bytes（TextAsset）。</summary>
    private static readonly string[] BytesSourceExtensions =
    {
        ".lua", ".bin",
    };

    private class IndexDraft
    {
        public string Key;
        public string Path;
        public bool HasLua;
    }

    private static readonly List<IndexDraft> IndexDrafts = new List<IndexDraft>();

    [MenuItem("XLuaProject/Build AssetBundles", false, 0)]
    public static void BuildAll()
    {
        try
        {
            EnsureDirectory("Assets/StreamingAssets");
            EnsureDirectory(OutputRoot);

            IndexDrafts.Clear();
            ABFolderMark.EnsureDefaultMarks();
            List<ABBundleFolder> folders = ABFolderMark.CollectBundleFolders();
            PrepareTempBytesAssets(folders);

            var builds = new List<AssetBundleBuild>();
            CollectMarkedBuilds(builds, folders);

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
            CleanupLuaTempAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("XLuaProject/Clear AssetBundles", false, 1)]
    public static void ClearAll()
    {
        CleanupLuaTempAssets();

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
                Debug.LogWarning("[AssetBundleBuilder] bundle not produced, drop index: " + draft.Key + " → " + relative);
                IndexDrafts.RemoveAt(i);
                i--;
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
            sb.Append("\"");
            if (draft.HasLua)
            {
                sb.AppendLine(",");
                sb.Append("    \"lua\": true");
            }

            sb.AppendLine();
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

    private static void CollectMarkedBuilds(List<AssetBundleBuild> builds, List<ABBundleFolder> folders)
    {
        for (int i = 0; i < folders.Count; i++)
        {
            ABBundleFolder folder = folders[i];
            List<string> assetNames = CollectBundleAssets(folder, folders);
            if (assetNames.Count == 0)
            {
                Debug.Log("[AssetBundleBuilder] skip empty " + folder.AssetPath);
                continue;
            }

            EnsureOutputParent(folder.OutputRelative);
            builds.Add(new AssetBundleBuild
            {
                assetBundleName = folder.OutputRelative,
                assetNames = assetNames.ToArray(),
            });
            IndexDrafts.Add(new IndexDraft
            {
                Key = folder.IndexKey,
                Path = folder.OutputRelative,
                HasLua = FolderHasLua(folder),
            });
            Debug.Log(string.Format(
                "[AssetBundleBuilder] {0} → AB/{1}  assets={2}  index={3}",
                folder.AssetPath,
                folder.OutputRelative,
                assetNames.Count,
                folder.IndexKey));
        }
    }

    private static bool FolderHasLua(ABBundleFolder folder)
    {
        string abs = Path.GetFullPath(folder.AssetPath);
        if (!Directory.Exists(abs))
        {
            return false;
        }

        return Directory.GetFiles(abs, "*.lua", SearchOption.AllDirectories).Length > 0;
    }

    private static List<string> CollectBundleAssets(ABBundleFolder folder, List<ABBundleFolder> allFolders)
    {
        var result = new List<string>();
        CollectFilesUnder(folder.AssetPath, folder, allFolders, result, false);
        string tempFolder = LuaTempRoot + "/" + ABFolderMark.GetRelativeFromRaw(folder.AssetPath);
        CollectFilesUnder(tempFolder, folder, allFolders, result, true);
        return result;
    }

    private static void CollectFilesUnder(
        string folderAssetPath,
        ABBundleFolder current,
        List<ABBundleFolder> allFolders,
        List<string> result,
        bool luaTemp)
    {
        string abs = Path.GetFullPath(folderAssetPath);
        if (!Directory.Exists(abs))
        {
            return;
        }

        string[] files = Directory.GetFiles(abs, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!luaTemp && IsBytesSourceFile(file))
            {
                continue;
            }

            if (luaTemp && !file.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetPath = ToAssetPath(file);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            if (ABFolderMark.BelongsToDeeperBundle(MapTempPathToRaw(assetPath), current.AssetPath, allFolders))
            {
                continue;
            }

            if (!result.Contains(assetPath))
            {
                result.Add(assetPath);
            }
        }
    }

    /// <summary>ABBuildTemp/XLua/FGUILua/x.bytes → Assets/RawResources/XLua/FGUILua/x.lua，供嵌套分包判断。</summary>
    private static string MapTempPathToRaw(string assetPath)
    {
        string path = ABFolderMark.NormalizeAssetPath(assetPath);
        string prefix = LuaTempRoot + "/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        string relative = path.Substring(prefix.Length);
        if (relative.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring(0, relative.Length - ".bytes".Length) + ".lua";
        }

        return ABFolderMark.RawRoot + "/" + relative;
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

    private static bool IsBytesSourceFile(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        for (int i = 0; i < BytesSourceExtensions.Length; i++)
        {
            if (ext.Equals(BytesSourceExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void PrepareTempBytesAssets(List<ABBundleFolder> folders)
    {
        CleanupLuaTempAssets();
        if (folders == null || folders.Count == 0)
        {
            return;
        }

        string absRawRoot = Path.GetFullPath(ABFolderMark.RawRoot);
        string absTempRoot = Path.GetFullPath(LuaTempRoot);
        int copied = 0;

        for (int i = 0; i < folders.Count; i++)
        {
            string absFolder = Path.GetFullPath(folders[i].AssetPath);
            if (!Directory.Exists(absFolder))
            {
                continue;
            }

            string[] files = Directory.GetFiles(absFolder, "*", SearchOption.AllDirectories);
            for (int j = 0; j < files.Length; j++)
            {
                string src = files[j];
                if (src.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || !IsBytesSourceFile(src))
                {
                    continue;
                }

                if (ABFolderMark.BelongsToDeeperBundle(ToAssetPath(src), folders[i].AssetPath, folders))
                {
                    continue;
                }

                string rel = src.Substring(absRawRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dst = Path.Combine(absTempRoot, Path.ChangeExtension(rel, ".bytes"));
                string dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                {
                    Directory.CreateDirectory(dstDir);
                }

                File.Copy(src, dst, true);
                copied++;
            }
        }

        if (copied > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log("[AssetBundleBuilder] copied " + copied + " lua/bin → " + LuaTempRoot + " as .bytes");
        }
    }

    private static void CleanupLuaTempAssets()
    {
        if (AssetDatabase.IsValidFolder(LuaTempRoot))
        {
            AssetDatabase.DeleteAsset(LuaTempRoot);
        }

        string abs = Path.GetFullPath(LuaTempRoot);
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

    private static void EnsureOutputParent(string outputRelative)
    {
        string dir = Path.GetDirectoryName(outputRelative.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        EnsureDirectory(OutputRoot + "/" + dir.Replace('\\', '/'));
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
