using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RawResources 下的文件夹 AB 包标记。
/// Self：本文件夹打成一个包，路径取相对 RawResources 的小写路径，如 D3Prefabs → d3prefabs.ab。
/// EachChild：每个直接子文件夹各打一个包，如 XLua/FGUILua → xlua/fguilua.ab。
/// </summary>
public enum ABFolderMarkMode
{
    None = 0,
    Self = 1,
    EachChild = 2,
}

public sealed class ABBundleFolder
{
    public string AssetPath;
    public string IndexKey;
    public string OutputRelative;
}

public static class ABFolderMark
{
    public const string RawRoot = "Assets/RawResources";
    public const string TokenSelf = "XLuaProject.ABFolder";
    public const string TokenEachChild = "XLuaProject.ABFolder.EachChild";

    private static readonly string[] DefaultSelfFolders =
    {
        RawRoot + "/Fonts",
        RawRoot + "/D3Prefabs",
    };

    private static readonly string[] DefaultEachChildFolders =
    {
        RawRoot + "/UIPackage",
        RawRoot + "/XLua",
    };

    public static bool IsUnderRawResources(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string path = NormalizeAssetPath(assetPath);
        return path == RawRoot
               || path.StartsWith(RawRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanMarkFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
        {
            return false;
        }

        string path = NormalizeAssetPath(assetPath);
        if (!IsUnderRawResources(path))
        {
            return false;
        }

        // RawResources 根只允许 EachChild，避免整棵树打成一个包
        return path != RawRoot;
    }

    public static bool CanMarkEachChild(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
        {
            return false;
        }

        return IsUnderRawResources(assetPath);
    }

    public static ABFolderMarkMode GetMode(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
        {
            return ABFolderMarkMode.None;
        }

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            return ABFolderMarkMode.None;
        }

        return ParseToken(importer.userData);
    }

    public static void SetMode(string assetPath, ABFolderMarkMode mode)
    {
        if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            return;
        }

        string token = ToToken(mode);
        if (string.Equals(importer.userData, token, StringComparison.Ordinal))
        {
            return;
        }

        importer.userData = token;
        importer.SaveAndReimport();
    }

    public static string GetRelativeFromRaw(string folderAssetPath)
    {
        string path = NormalizeAssetPath(folderAssetPath);
        if (!path.StartsWith(RawRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return path.Substring(RawRoot.Length + 1);
    }

    public static string ToOutputRelative(string folderAssetPath)
    {
        string relative = GetRelativeFromRaw(folderAssetPath);
        if (string.IsNullOrEmpty(relative))
        {
            return string.Empty;
        }

        return relative.ToLowerInvariant() + ".ab";
    }

    public static string ToIndexKey(string folderAssetPath)
    {
        string path = NormalizeAssetPath(folderAssetPath);
        return Path.GetFileName(path);
    }

    /// <summary>把标记展开成实际要打包的文件夹列表。</summary>
    public static List<ABBundleFolder> CollectBundleFolders()
    {
        var result = new List<ABBundleFolder>();
        var seenOutput = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string[] folders = CollectRawResourceFolders();
        for (int i = 0; i < folders.Length; i++)
        {
            ABFolderMarkMode mode = GetMode(folders[i]);
            if (mode == ABFolderMarkMode.Self)
            {
                TryAddBundleFolder(result, seenOutput, seenKey, folders[i]);
            }
            else if (mode == ABFolderMarkMode.EachChild)
            {
                AddEachChildFolders(result, seenOutput, seenKey, folders[i]);
            }
        }

        result.Sort((a, b) => string.Compare(a.AssetPath, b.AssetPath, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>没有任何标记时写入与现有工程一致的默认标记。</summary>
    public static int EnsureDefaultMarks()
    {
        if (HasAnyMark())
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < DefaultSelfFolders.Length; i++)
        {
            if (AssetDatabase.IsValidFolder(DefaultSelfFolders[i]))
            {
                SetMode(DefaultSelfFolders[i], ABFolderMarkMode.Self);
                count++;
            }
        }

        for (int i = 0; i < DefaultEachChildFolders.Length; i++)
        {
            if (AssetDatabase.IsValidFolder(DefaultEachChildFolders[i]))
            {
                SetMode(DefaultEachChildFolders[i], ABFolderMarkMode.EachChild);
                count++;
            }
        }

        if (count > 0)
        {
            Debug.Log("[ABFolderMark] applied default marks: UIPackage/XLua=EachChild, Fonts/D3Prefabs=Self");
        }

        return count;
    }

    public static bool HasAnyMark()
    {
        string[] folders = CollectRawResourceFolders();
        for (int i = 0; i < folders.Length; i++)
        {
            if (GetMode(folders[i]) != ABFolderMarkMode.None)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsUnderFolder(string assetPath, string folderAssetPath)
    {
        string path = NormalizeAssetPath(assetPath);
        string folder = NormalizeAssetPath(folderAssetPath);
        return path == folder || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool BelongsToDeeperBundle(string assetPath, string currentFolder, List<ABBundleFolder> allFolders)
    {
        for (int i = 0; i < allFolders.Count; i++)
        {
            string other = allFolders[i].AssetPath;
            if (string.Equals(other, currentFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsUnderFolder(other, currentFolder) && IsUnderFolder(assetPath, other))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeAssetPath(string assetPath)
    {
        return string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
    }

    public static string DescribeMode(ABFolderMarkMode mode)
    {
        switch (mode)
        {
            case ABFolderMarkMode.Self:
                return "本文件夹 → 一个 AB 包";
            case ABFolderMarkMode.EachChild:
                return "每个子文件夹 → 各一个 AB 包";
            default:
                return "不标记";
        }
    }

    public static string DescribeOutput(string folderAssetPath, ABFolderMarkMode mode)
    {
        if (mode == ABFolderMarkMode.Self)
        {
            string relative = ToOutputRelative(folderAssetPath);
            return string.IsNullOrEmpty(relative) ? string.Empty : "AB/" + relative;
        }

        if (mode == ABFolderMarkMode.EachChild)
        {
            string relative = GetRelativeFromRaw(folderAssetPath);
            if (string.IsNullOrEmpty(relative))
            {
                return "AB/{子文件夹}.ab";
            }

            return "AB/" + relative.ToLowerInvariant() + "/{子文件夹}.ab";
        }

        return string.Empty;
    }

    private static void AddEachChildFolders(
        List<ABBundleFolder> result,
        HashSet<string> seenOutput,
        Dictionary<string, string> seenKey,
        string parentFolder)
    {
        string[] children = AssetDatabase.GetSubFolders(parentFolder);
        for (int i = 0; i < children.Length; i++)
        {
            ABFolderMarkMode childMode = GetMode(children[i]);
            if (childMode == ABFolderMarkMode.EachChild)
            {
                AddEachChildFolders(result, seenOutput, seenKey, children[i]);
                continue;
            }

            TryAddBundleFolder(result, seenOutput, seenKey, children[i]);
        }
    }

    private static void TryAddBundleFolder(
        List<ABBundleFolder> result,
        HashSet<string> seenOutput,
        Dictionary<string, string> seenKey,
        string folderAssetPath)
    {
        if (!FolderHasPackableAssets(folderAssetPath))
        {
            return;
        }

        string output = ToOutputRelative(folderAssetPath);
        string key = ToIndexKey(folderAssetPath);
        if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[ABFolderMark] skip invalid folder: " + folderAssetPath);
            return;
        }

        if (!seenOutput.Add(output))
        {
            return;
        }

        string existing;
        if (seenKey.TryGetValue(key, out existing))
        {
            Debug.LogError(string.Format(
                "[ABFolderMark] duplicate index key \"{0}\": {1} 与 {2} 文件夹同名。请改名，或给其中一方单独标记。",
                key,
                existing,
                folderAssetPath));
            return;
        }

        seenKey.Add(key, NormalizeAssetPath(folderAssetPath));
        result.Add(new ABBundleFolder
        {
            AssetPath = NormalizeAssetPath(folderAssetPath),
            IndexKey = key,
            OutputRelative = output,
        });
    }

    private static bool FolderHasPackableAssets(string folderAssetPath)
    {
        string abs = Path.GetFullPath(folderAssetPath);
        if (!Directory.Exists(abs))
        {
            return false;
        }

        string[] files = Directory.GetFiles(abs, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            if (!files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] CollectRawResourceFolders()
    {
        if (!AssetDatabase.IsValidFolder(RawRoot))
        {
            return new string[0];
        }

        var list = new List<string>();
        CollectFoldersRecursive(RawRoot, list);
        return list.ToArray();
    }

    private static void CollectFoldersRecursive(string folder, List<string> list)
    {
        list.Add(NormalizeAssetPath(folder));
        string[] children = AssetDatabase.GetSubFolders(folder);
        for (int i = 0; i < children.Length; i++)
        {
            CollectFoldersRecursive(children[i], list);
        }
    }

    private static ABFolderMarkMode ParseToken(string userData)
    {
        if (string.IsNullOrEmpty(userData))
        {
            return ABFolderMarkMode.None;
        }

        if (string.Equals(userData, TokenEachChild, StringComparison.Ordinal))
        {
            return ABFolderMarkMode.EachChild;
        }

        if (string.Equals(userData, TokenSelf, StringComparison.Ordinal))
        {
            return ABFolderMarkMode.Self;
        }

        return ABFolderMarkMode.None;
    }

    private static string ToToken(ABFolderMarkMode mode)
    {
        switch (mode)
        {
            case ABFolderMarkMode.Self:
                return TokenSelf;
            case ABFolderMarkMode.EachChild:
                return TokenEachChild;
            default:
                return string.Empty;
        }
    }
}

/// <summary>右键菜单、Inspector、工程窗口标记。</summary>
[InitializeOnLoad]
public static class ABFolderMarkEditor
{
    private static readonly string[] ModeLabels =
    {
        "不标记",
        "本文件夹 → 一个 AB 包",
        "每个子文件夹 → 各一个 AB 包",
    };

    static ABFolderMarkEditor()
    {
        Editor.finishedDefaultHeaderGUI += OnDefaultHeaderGUI;
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
    }

    [MenuItem("Assets/XLuaProject/标记为 AB 包/本文件夹打成一个包", false, 1200)]
    private static void MarkSelectedSelf()
    {
        MarkSelected(ABFolderMarkMode.Self);
    }

    [MenuItem("Assets/XLuaProject/标记为 AB 包/本文件夹打成一个包", true)]
    private static bool MarkSelectedSelfValidate()
    {
        return HasMarkableSelection(false);
    }

    [MenuItem("Assets/XLuaProject/标记为 AB 包/每个子文件夹各打一个包", false, 1201)]
    private static void MarkSelectedEachChild()
    {
        MarkSelected(ABFolderMarkMode.EachChild);
    }

    [MenuItem("Assets/XLuaProject/标记为 AB 包/每个子文件夹各打一个包", true)]
    private static bool MarkSelectedEachChildValidate()
    {
        return HasMarkableSelection(true);
    }

    [MenuItem("Assets/XLuaProject/取消 AB 包标记", false, 1202)]
    private static void UnmarkSelected()
    {
        MarkSelected(ABFolderMarkMode.None);
    }

    [MenuItem("Assets/XLuaProject/取消 AB 包标记", true)]
    private static bool UnmarkSelectedValidate()
    {
        string[] folders = GetSelectedRawFolders();
        return folders.Length > 0;
    }

    [MenuItem("XLuaProject/列出已标记的 AB 文件夹", false, 2)]
    private static void ListMarkedFolders()
    {
        ABFolderMark.EnsureDefaultMarks();
        List<ABBundleFolder> folders = ABFolderMark.CollectBundleFolders();
        if (folders.Count == 0)
        {
            Debug.Log("[ABFolderMark] no marked folders.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[ABFolderMark] " + folders.Count + " bundle(s):");
        for (int i = 0; i < folders.Count; i++)
        {
            ABBundleFolder folder = folders[i];
            sb.Append("  ");
            sb.Append(folder.AssetPath);
            sb.Append(" → AB/");
            sb.Append(folder.OutputRelative);
            sb.Append("  (index: ");
            sb.Append(folder.IndexKey);
            sb.AppendLine(")");
        }

        Debug.Log(sb.ToString());
    }

    private static void MarkSelected(ABFolderMarkMode mode)
    {
        string[] folders = GetSelectedRawFolders();
        int count = 0;
        for (int i = 0; i < folders.Length; i++)
        {
            if (mode == ABFolderMarkMode.Self && !ABFolderMark.CanMarkFolder(folders[i]))
            {
                continue;
            }

            if (mode == ABFolderMarkMode.EachChild && !ABFolderMark.CanMarkEachChild(folders[i]))
            {
                continue;
            }

            ABFolderMark.SetMode(folders[i], mode);
            count++;
            string output = ABFolderMark.DescribeOutput(folders[i], mode);
            if (mode == ABFolderMarkMode.None)
            {
                Debug.Log("[ABFolderMark] unmarked " + folders[i]);
            }
            else
            {
                Debug.Log(string.Format("[ABFolderMark] {0} → {1}  ({2})", folders[i], output, ABFolderMark.DescribeMode(mode)));
            }
        }

        if (count > 0)
        {
            AssetDatabase.SaveAssets();
        }
    }

    private static bool HasMarkableSelection(bool eachChild)
    {
        string[] folders = GetSelectedRawFolders();
        for (int i = 0; i < folders.Length; i++)
        {
            if (eachChild ? ABFolderMark.CanMarkEachChild(folders[i]) : ABFolderMark.CanMarkFolder(folders[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetSelectedRawFolders()
    {
        var list = new List<string>();
        string[] guids = Selection.assetGUIDs;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = ABFolderMark.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (AssetDatabase.IsValidFolder(path) && ABFolderMark.IsUnderRawResources(path))
            {
                list.Add(path);
            }
        }

        return list.ToArray();
    }

    private static void OnDefaultHeaderGUI(Editor editor)
    {
        if (editor == null || editor.targets == null || editor.targets.Length != 1)
        {
            return;
        }

        string path = ABFolderMark.NormalizeAssetPath(AssetDatabase.GetAssetPath(editor.target));
        if (!AssetDatabase.IsValidFolder(path) || !ABFolderMark.IsUnderRawResources(path))
        {
            return;
        }

        ABFolderMarkMode mode = ABFolderMark.GetMode(path);
        int index = (int)mode;
        EditorGUI.BeginChangeCheck();
        index = EditorGUILayout.Popup("AB 包标记", index, ModeLabels);
        if (EditorGUI.EndChangeCheck())
        {
            var next = (ABFolderMarkMode)index;
            if (next == ABFolderMarkMode.Self && !ABFolderMark.CanMarkFolder(path))
            {
                Debug.LogWarning("[ABFolderMark] RawResources 根不能标记为单个 AB 包，请改用「每个子文件夹」。");
                return;
            }

            ABFolderMark.SetMode(path, next);
            AssetDatabase.SaveAssets();
        }

        string preview = ABFolderMark.DescribeOutput(path, (ABFolderMarkMode)index);
        if (!string.IsNullOrEmpty(preview))
        {
            EditorGUILayout.HelpBox("输出 " + preview + "，index 逻辑名取文件夹名。", MessageType.Info);
        }
    }

    private static void OnProjectWindowItemGUI(string guid, Rect rect)
    {
        if (string.IsNullOrEmpty(guid) || Event.current.type != EventType.Repaint)
        {
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guid);
        ABFolderMarkMode mode = ABFolderMark.GetMode(path);
        if (mode == ABFolderMarkMode.None)
        {
            return;
        }

        string label = mode == ABFolderMarkMode.EachChild ? "AB+" : "AB";
        var badge = new Rect(rect.xMax - 24f, rect.y, 24f, rect.height);
        Color old = GUI.color;
        GUI.color = new Color(0.35f, 0.8f, 1f, 0.95f);
        GUI.Label(badge, label, EditorStyles.miniLabel);
        GUI.color = old;
    }
}
