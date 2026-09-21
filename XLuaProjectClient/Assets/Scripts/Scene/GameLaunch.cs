using System;
using System.Collections;
using System.IO;
using UnityEngine;
using XLua;

[Serializable]
public class GameLaunchConfigData
{
    public string cdnUrl;
}

public class GameLaunch : MonoBehaviour
{
    private LuaEnv _luaEnv;
    private bool _booted;

    public GameObject gameLaunchPanel;

    [Tooltip("拖入 GameLaunchConfig.json，启动时解析 cdnUrl 赋给 AB 路径")]
    public TextAsset gameLaunchConfig;

    public static GameLaunch Instance;

    public static LaunchMode CurrentLaunchMode { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 切场景时不能销毁：否则 OnDestroy Dispose LuaEnv 会与仍存活的 C#→Lua 回调冲突
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (_booted)
        {
            return;
        }

        _booted = true;
        AssetBundleLoader.Runner = this;
        ApplyLaunchConfig();
        CurrentLaunchMode = LaunchModeConfig.Resolve();
        StartCoroutine(BootAsync());
    }

    private IEnumerator BootAsync()
    {
        _luaEnv = new LuaEnv();

        if (CurrentLaunchMode == LaunchMode.AssetBundle)
        {
            FairyGUI.UIPackage.unloadBundleByFGUI = false;
            _luaEnv.AddLoader(LoadFromAssetBundle);

            // 优先加载 index.json，路径不再写死在代码里
            bool indexDone = false;
            bool indexOk = false;
            AssetBundleLoader.LoadIndexAsync(success =>
            {
                indexOk = success;
                indexDone = true;
            });
            while (!indexDone)
            {
                yield return null;
            }

            if (!indexOk)
            {
                Debug.LogWarning("[GameLaunch] index.json 加载失败，将回退约定路径");
            }

            bool done = false;
            bool ok = false;
            AssetBundleLoader.PreloadAllLuaBundlesAsync(success =>
            {
                ok = success;
                done = true;
            });
            while (!done)
            {
                yield return null;
            }

            if (!ok)
            {
                Debug.LogError("[GameLaunch] XLua AB 预加载失败，中止启动");
                yield break;
            }
        }
        else
        {
            _luaEnv.AddLoader(LoadFromRawResources);
        }

        _luaEnv.DoString(string.Format(
            "LaunchMode = '{0}'",
            CurrentLaunchMode == LaunchMode.AssetBundle ? "AssetBundle" : "Folder"));

        Debug.Log(string.Format(
            "[GameLaunch] LaunchMode={0} Index={1} Cdn={2} HotUpdate={3} Streaming={4}",
            CurrentLaunchMode,
            AssetBundlePath.HasIndex ? "ok" : "none",
            AssetBundlePath.HasCdn ? AssetBundlePath.CdnRoot : "(none)",
            AssetBundlePath.HotUpdateAbRoot,
            AssetBundlePath.StreamingAbRoot));

        _luaEnv.DoString("require 'GameLaunch.XLuaLaunch'");
    }

    void Update()
    {
        if (_luaEnv != null)
        {
            _luaEnv.Tick();
        }
    }

    void OnApplicationQuit()
    {
        ShutdownLua();
    }

    void OnDestroy()
    {
        // 正常切场景不应走到这里（DDOL）；退出时做安全释放
        if (Instance == this)
        {
            ShutdownLua();
            Instance = null;
        }
    }

    private void ShutdownLua()
    {
        if (_luaEnv == null)
        {
            return;
        }

        try
        {
            // 清理 FairyGUI 定时器 / Tween，避免 C# 仍持有 Lua 回调
            _luaEnv.DoString(@"
                if Timers and Timers.inst then pcall(function() Timers.inst:Update(999999) end) end
                if GTween then pcall(function() GTween.Clean() end) end
            ");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameLaunch] clear lua callbacks: " + e.Message);
        }

        // 多 Tick 几次让 xLua 回收委托桥
        for (int i = 0; i < 5; i++)
        {
            _luaEnv.Tick();
        }

        try
        {
            _luaEnv.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameLaunch] LuaEnv.Dispose: " + e.Message);
        }

        _luaEnv = null;

        if (CurrentLaunchMode == LaunchMode.AssetBundle)
        {
            AssetBundleLoader.UnloadAll();
        }

        if (AssetBundleLoader.Runner == this)
        {
            AssetBundleLoader.Runner = null;
        }
    }

    private void ApplyLaunchConfig()
    {
        if (gameLaunchConfig == null)
        {
            AssetBundlePath.ClearCdnRoot();
            Debug.LogWarning("[GameLaunch] gameLaunchConfig 未赋值，CDN 为空 → StreamingAssets");
            return;
        }

        try
        {
            GameLaunchConfigData data = JsonUtility.FromJson<GameLaunchConfigData>(gameLaunchConfig.text);
            string cdnUrl = data != null ? data.cdnUrl : null;
            AssetBundlePath.SetCdnRoot(cdnUrl);
        }
        catch (Exception e)
        {
            AssetBundlePath.ClearCdnRoot();
            Debug.LogError("[GameLaunch] 解析 GameLaunchConfig 失败: " + e.Message);
        }
    }

    private static byte[] LoadFromRawResources(ref string filepath)
    {
        string relative = filepath.Replace('.', '/');
        string path = Application.dataPath + "/RawResources/XLua/" + relative + ".lua";
        if (File.Exists(path))
        {
            filepath = path;
            return File.ReadAllBytes(path);
        }

        return null;
    }

    private static byte[] LoadFromAssetBundle(ref string filepath)
    {
        string relative = filepath.Replace('.', '/');
        byte[] bytes = AssetBundleLoader.LoadLuaBytes(relative);
        if (bytes != null)
        {
            filepath = "ab://XLua/" + relative;
            return bytes;
        }

        return null;
    }
}
