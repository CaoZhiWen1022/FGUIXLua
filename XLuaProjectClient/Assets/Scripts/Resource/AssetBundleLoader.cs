using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 异步 AssetBundle 加载器（WebGL 友好）：CDN / StreamingAssets，回调通知完成。
/// </summary>
public static class AssetBundleLoader
{
    private static readonly Dictionary<string, AssetBundle> LoadedBundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> RefCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, byte[]> LuaBytesCache =
        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

    private static bool _xluaPreloaded;

    /// <summary>用于启动协程，由 GameLaunch 赋值。</summary>
    public static MonoBehaviour Runner { get; set; }

    public static bool IsXLuaPreloaded
    {
        get { return _xluaPreloaded; }
    }

    public static void LoadBundleByRelativeAsync(string relativeUnderAb, Action<AssetBundle> onComplete)
    {
        EnsureRunner();
        Runner.StartCoroutine(LoadBundleByRelativeCo(relativeUnderAb, onComplete));
    }

    public static void LoadBundleByKeyAsync(string indexKey, Action<AssetBundle> onComplete)
    {
        string relative = AssetBundlePath.GetRelativePathByKey(indexKey);
        if (string.IsNullOrEmpty(relative))
        {
            Debug.LogError("[AssetBundleLoader] index miss: " + indexKey);
            if (onComplete != null)
            {
                onComplete(null);
            }

            return;
        }

        LoadBundleByRelativeAsync(relative, onComplete);
    }

    public static void UnloadBundleByKey(string indexKey)
    {
        ReleaseBundleByRelative(AssetBundlePath.GetRelativePathByKey(indexKey));
    }

    /// <summary>异步加载 AB/index.json 并注入 AssetBundlePath。</summary>
    public static void LoadIndexAsync(Action<bool> onComplete)
    {
        EnsureRunner();
        Runner.StartCoroutine(LoadIndexCo(onComplete));
    }

    public static void ReleaseBundleByRelative(string relativeUnderAb)
    {
        if (string.IsNullOrEmpty(relativeUnderAb))
        {
            return;
        }

        string key = NormalizeRelativeKey(relativeUnderAb);
        if (!LoadedBundles.ContainsKey(key))
        {
            return;
        }

        int count = GetRef(key) - 1;
        if (count > 0)
        {
            RefCounts[key] = count;
            return;
        }

        RefCounts.Remove(key);
        AssetBundle bundle = LoadedBundles[key];
        LoadedBundles.Remove(key);
        if (bundle != null)
        {
            bundle.Unload(false);
        }
    }

    /// <summary>异步预加载 index 中标记为 lua 的全部 AB，完成后回调 success。</summary>
    public static void PreloadAllLuaBundlesAsync(Action<bool> onComplete)
    {
        EnsureRunner();
        Runner.StartCoroutine(PreloadAllLuaBundlesCo(onComplete));
    }

    public static byte[] LoadLuaBytes(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        string key = relativePath.Replace('\\', '/').TrimStart('/');
        byte[] bytes;
        if (LuaBytesCache.TryGetValue(key, out bytes))
        {
            return bytes;
        }

        if (LuaBytesCache.TryGetValue(StripLuaExtension(key), out bytes))
        {
            return bytes;
        }

        return null;
    }

    public static void UnloadAll()
    {
        foreach (var pair in LoadedBundles)
        {
            if (pair.Value != null)
            {
                pair.Value.Unload(false);
            }
        }

        LoadedBundles.Clear();
        RefCounts.Clear();
        LuaBytesCache.Clear();
        _xluaPreloaded = false;
    }

    private static IEnumerator LoadIndexCo(Action<bool> onComplete)
    {
        AssetBundlePath.ClearIndex();

        string relative = AssetBundlePath.GetIndexRelative();
        string text = null;

        // index 必须每次拉最新：不读 persistentDataPath，远程带时间戳防 HTTP 缓存
        if (AssetBundlePath.HasCdn)
        {
            string cdnUrl = AppendCacheBust(AssetBundlePath.GetCdnBundlePath(relative));
            yield return DownloadTextCo(cdnUrl, t => text = t);
        }

        if (string.IsNullOrEmpty(text))
        {
            string streaming = AssetBundlePath.GetStreamingBundlePath(relative);
            if (AssetBundlePath.IsRemoteUrl(streaming))
            {
                yield return DownloadTextCo(AppendCacheBust(streaming), t => text = t);
            }
            else
            {
                yield return LoadTextFromResolvedCo(relative, streaming, t => text = t);
            }
        }

        bool ok = !string.IsNullOrEmpty(text) && AssetBundlePath.ApplyIndexJson(text);
        if (!ok)
        {
            Debug.LogWarning("[AssetBundleLoader] index.json missing or invalid: " + relative);
        }

        if (onComplete != null)
        {
            onComplete(ok);
        }
    }

    private static IEnumerator LoadTextFromResolvedCo(string relativeKey, string pathOrUrl, Action<string> onComplete)
    {
        // WebGL：只能走 http(s)，禁止落到 idbfs 后再用 file:// 读取
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            string url = pathOrUrl;
            if (!AssetBundlePath.IsRemoteUrl(url) && (url == null || url.IndexOf("://", StringComparison.Ordinal) < 0))
            {
                url = AssetBundlePath.GetStreamingBundlePath(relativeKey);
            }

            yield return DownloadTextCo(ToRequestUrl(url), onComplete);
            yield break;
        }

        string localPath = pathOrUrl;
        if (AssetBundlePath.IsRemoteUrl(pathOrUrl))
        {
            string hotPath = AssetBundlePath.GetHotUpdateBundlePath(relativeKey);
            bool ok = false;
            yield return DownloadToFileCo(pathOrUrl, hotPath, success => ok = success);
            if (!ok)
            {
                if (onComplete != null)
                {
                    onComplete(null);
                }

                yield break;
            }

            localPath = hotPath;
        }

        if (!AssetBundlePath.IsRemoteUrl(localPath)
            && localPath.IndexOf("://", StringComparison.Ordinal) < 0
            && File.Exists(localPath))
        {
            try
            {
                string text = File.ReadAllText(localPath);
                if (onComplete != null)
                {
                    onComplete(text);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AssetBundleLoader] read index failed: " + localPath + " " + e.Message);
                if (onComplete != null)
                {
                    onComplete(null);
                }
            }

            yield break;
        }

        yield return DownloadTextCo(ToRequestUrl(localPath), onComplete);
    }

    private static IEnumerator PreloadAllLuaBundlesCo(Action<bool> onComplete)
    {
        LuaBytesCache.Clear();
        _xluaPreloaded = false;

        Debug.Log(string.Format(
            "[AssetBundleLoader] preload Lua async hot={0} cdn={1} streaming={2} index={3}",
            AssetBundlePath.HotUpdateAbRoot,
            AssetBundlePath.HasCdn ? AssetBundlePath.CdnAbRoot : "(none)",
            AssetBundlePath.StreamingAbRoot,
            AssetBundlePath.HasIndex));

        var relatives = new List<string>();
        if (AssetBundlePath.HasIndex)
        {
            relatives.AddRange(AssetBundlePath.GetLuaRelativePathsFromIndex());
        }

        if (relatives.Count == 0)
        {
            Debug.LogError("[AssetBundleLoader] no Lua AB in index (rebuild AB to write lua flags)");
            if (onComplete != null)
            {
                onComplete(false);
            }

            yield break;
        }

        bool allOk = true;
        for (int i = 0; i < relatives.Count; i++)
        {
            string relative = relatives[i];
            AssetBundle bundle = null;
            yield return LoadBundleByRelativeCo(relative, b => bundle = b);
            if (bundle == null)
            {
                allOk = false;
                continue;
            }

            CacheLuaAssetsFromBundle(bundle);
        }

        _xluaPreloaded = allOk;
        Debug.Log(string.Format("[AssetBundleLoader] Lua preload done, bundles={0}, lua={1}, ok={2}",
            relatives.Count, LuaBytesCache.Count, allOk));
        if (onComplete != null)
        {
            onComplete(allOk);
        }
    }

    private static IEnumerator LoadBundleByRelativeCo(string relativeUnderAb, Action<AssetBundle> onComplete)
    {
        if (string.IsNullOrEmpty(relativeUnderAb))
        {
            if (onComplete != null)
            {
                onComplete(null);
            }

            yield break;
        }

        string key = NormalizeRelativeKey(relativeUnderAb);
        AssetBundle cached;
        if (LoadedBundles.TryGetValue(key, out cached) && cached != null)
        {
            RefCounts[key] = GetRef(key) + 1;
            if (onComplete != null)
            {
                onComplete(cached);
            }

            yield break;
        }

        // 热更本地 > CDN > StreamingAssets；CDN 会先落到热更目录再加载
        string resolved = AssetBundlePath.ResolveBundleUrl(key);
        AssetBundle bundle = null;
        yield return EnsureLocalThenLoadCo(key, resolved, b => bundle = b);

        if (bundle == null)
        {
            string streaming = AssetBundlePath.GetStreamingBundlePath(key);
            if (!string.Equals(streaming, resolved, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[AssetBundleLoader] resolve miss, fallback StreamingAssets: " + key);
                yield return EnsureLocalThenLoadCo(key, streaming, b => bundle = b);
            }
        }

        if (bundle == null)
        {
            Debug.LogError("[AssetBundleLoader] load failed: " + key);
            if (onComplete != null)
            {
                onComplete(null);
            }

            yield break;
        }

        LoadedBundles[key] = bundle;
        RefCounts[key] = 1;
        if (onComplete != null)
        {
            onComplete(bundle);
        }
    }

    /// <summary>
    /// 远程 URL：非 WebGL 先落到热更目录再 Load；WebGL 直接用 UWR 加载（禁止 file://）。
    /// </summary>
    private static IEnumerator EnsureLocalThenLoadCo(string relativeKey, string pathOrUrl, Action<AssetBundle> onComplete)
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            string url = pathOrUrl;
            if (!AssetBundlePath.IsRemoteUrl(url) && (url == null || url.IndexOf("://", StringComparison.Ordinal) < 0))
            {
                url = AssetBundlePath.GetStreamingBundlePath(relativeKey);
            }

            yield return LoadAssetBundleFromUrlOrPathCo(url, onComplete);
            yield break;
        }

        string localPath = pathOrUrl;
        if (AssetBundlePath.IsRemoteUrl(pathOrUrl))
        {
            string hotPath = AssetBundlePath.GetHotUpdateBundlePath(relativeKey);
            bool ok = false;
            yield return DownloadToFileCo(pathOrUrl, hotPath, success => ok = success);
            if (!ok)
            {
                if (onComplete != null)
                {
                    onComplete(null);
                }

                yield break;
            }

            localPath = hotPath;
        }

        yield return LoadAssetBundleFromUrlOrPathCo(localPath, onComplete);
    }

    private static IEnumerator DownloadToFileCo(string url, string savePath, Action<bool> onComplete)
    {
        string dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (IsRequestFailed(req))
            {
                Debug.LogError("[AssetBundleLoader] download failed: " + url + " err=" + req.error);
                if (onComplete != null)
                {
                    onComplete(false);
                }

                yield break;
            }

            try
            {
                File.WriteAllBytes(savePath, req.downloadHandler.data);
                if (onComplete != null)
                {
                    onComplete(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AssetBundleLoader] save failed: " + savePath + " " + e.Message);
                if (onComplete != null)
                {
                    onComplete(false);
                }
            }
        }
    }

    private static IEnumerator LoadAssetBundleFromUrlOrPathCo(string pathOrUrl, Action<AssetBundle> onComplete)
    {
        string url = ToRequestUrl(pathOrUrl);
        if (string.IsNullOrEmpty(url))
        {
            if (onComplete != null)
            {
                onComplete(null);
            }

            yield break;
        }

        // 本地文件优先 LoadFromFile（WebGL 本地 file 不适用时仍走 UWR）
        if (!AssetBundlePath.IsRemoteUrl(pathOrUrl)
            && pathOrUrl.IndexOf("://", StringComparison.Ordinal) < 0
            && File.Exists(pathOrUrl)
            && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            AssetBundle fromFile = AssetBundle.LoadFromFile(pathOrUrl);
            if (fromFile == null)
            {
                Debug.LogError("[AssetBundleLoader] LoadFromFile failed: " + pathOrUrl);
            }

            if (onComplete != null)
            {
                onComplete(fromFile);
            }

            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestAssetBundle.GetAssetBundle(url))
        {
            yield return req.SendWebRequest();

            if (IsRequestFailed(req))
            {
                Debug.LogError("[AssetBundleLoader] UWR failed: " + url + " err=" + req.error);
                if (onComplete != null)
                {
                    onComplete(null);
                }

                yield break;
            }

            AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(req);
            if (onComplete != null)
            {
                onComplete(bundle);
            }
        }
    }

    private static IEnumerator DownloadTextCo(string url, Action<string> onComplete)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (onComplete != null)
            {
                onComplete(null);
            }

            yield break;
        }

        using (UnityWebRequest req = UnityWebRequest.Get(AppendCacheBust(url)))
        {
            // 不设 Pragma / Cache-Control：跨域会触发预检，http-server --cors 默认不允许这两个头
            yield return req.SendWebRequest();
            if (IsRequestFailed(req))
            {
                if (onComplete != null)
                {
                    onComplete(null);
                }

                yield break;
            }

            if (onComplete != null)
            {
                onComplete(req.downloadHandler.text);
            }
        }
    }

    private static string AppendCacheBust(string url)
    {
        if (string.IsNullOrEmpty(url) || !AssetBundlePath.IsRemoteUrl(url))
        {
            return url;
        }

        if (url.IndexOf("t=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return url;
        }

        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        char sep = url.IndexOf('?') >= 0 ? '&' : '?';
        return url + sep + "t=" + t;
    }

    private static string ToRequestUrl(string pathOrUrl)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
        {
            return null;
        }

        string p = pathOrUrl.Replace('\\', '/');
        if (AssetBundlePath.IsRemoteUrl(p) || p.Contains("://"))
        {
            // WebGL 禁止 file://（含 idbfs）
            if (Application.platform == RuntimePlatform.WebGLPlayer
                && p.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[AssetBundleLoader] WebGL blocked file URL: " + p);
                return null;
            }

            return p;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (p.Length >= 2 && p[1] == ':')
        {
            return "file:///" + p;
        }
#endif
        if (p.StartsWith("/"))
        {
            return "file://" + p;
        }

        return "file:///" + p;
    }

    private static bool IsRequestFailed(UnityWebRequest req)
    {
#if UNITY_2020_2_OR_NEWER
        return req.result != UnityWebRequest.Result.Success;
#else
        return req.isNetworkError || req.isHttpError;
#endif
    }

    private static void CacheLuaAssetsFromBundle(AssetBundle bundle)
    {
        string[] assetNames = bundle.GetAllAssetNames();
        for (int i = 0; i < assetNames.Length; i++)
        {
            TextAsset ta = bundle.LoadAsset<TextAsset>(assetNames[i]);
            if (ta == null)
            {
                continue;
            }

            string relative = ExtractLuaRelativePath(assetNames[i]);
            if (!string.IsNullOrEmpty(relative))
            {
                LuaBytesCache[relative] = ta.bytes;
            }
        }
    }

    private static string ExtractLuaRelativePath(string assetName)
    {
        string path = assetName.Replace('\\', '/');
        const string tempMarker = "/abbuildtemp/";
        int idx = path.IndexOf(tempMarker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            string relative = StripLuaExtension(path.Substring(idx + tempMarker.Length));
            int slash = relative.IndexOf('/');
            if (slash >= 0 && slash + 1 < relative.Length)
            {
                // require 相对 RawResources 下的一级目录（通常是 XLua）
                return relative.Substring(slash + 1);
            }

            return relative;
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    private static string StripLuaExtension(string path)
    {
        if (path.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(0, path.Length - ".bytes".Length);
        }

        if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(0, path.Length - ".lua".Length);
        }

        if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(0, path.Length - ".txt".Length);
        }

        return path;
    }

    private static int GetRef(string key)
    {
        int count;
        return RefCounts.TryGetValue(key, out count) ? count : 0;
    }

    private static string NormalizeRelativeKey(string relativeUnderAb)
    {
        return relativeUnderAb.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
    }

    private static void EnsureRunner()
    {
        if (Runner == null)
        {
            throw new InvalidOperationException("[AssetBundleLoader] Runner 未设置，请在 GameLaunch 中赋值");
        }
    }
}
