using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using XLua;

/// <summary>
/// 供 Lua 调用的异步场景加载：先预加载到 90%（不切换），再手动 Activate。
/// </summary>
public class SceneLoadBridge : MonoBehaviour
{
    private static SceneLoadBridge _instance;
    private static AsyncOperation _pendingOp;
    private static string _pendingSceneName;

    private static SceneLoadBridge Ensure()
    {
        if (_instance == null)
        {
            var go = new GameObject("[SceneLoadBridge]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneLoadBridge>();
        }

        return _instance;
    }

    public static bool HasPendingScene
    {
        get { return _pendingOp != null; }
    }

    /// <summary>
    /// 异步预加载场景到约 90%，不激活。就绪后调用 onReady。
    /// </summary>
    public static void PreloadAsync(string sceneName, LuaFunction onReady)
    {
        Ensure().StartCoroutine(PreloadCoroutine(sceneName, onReady));
    }

    /// <summary>
    /// 激活已预加载的场景；完成后调用 onComplete。
    /// </summary>
    public static void ActivateLoadedScene(LuaFunction onComplete)
    {
        Ensure().StartCoroutine(ActivateCoroutine(onComplete));
    }

    /// <summary>
    /// 兼容旧接口：预加载 → onReady → 立即激活。
    /// </summary>
    public static void LoadAsync(string sceneName, LuaFunction onReady)
    {
        Ensure().StartCoroutine(LoadAndActivateCoroutine(sceneName, onReady));
    }

    private static IEnumerator PreloadCoroutine(string sceneName, LuaFunction onReady)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoadBridge] sceneName is empty");
            DisposeLua(onReady);
            yield break;
        }

        _pendingOp = null;
        _pendingSceneName = null;

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp == null)
        {
            Debug.LogError($"[SceneLoadBridge] LoadSceneAsync failed: {sceneName}. Check Build Settings.");
            DisposeLua(onReady);
            yield break;
        }

        asyncOp.allowSceneActivation = false;
        _pendingOp = asyncOp;
        _pendingSceneName = sceneName;
        Debug.Log($"[SceneLoadBridge] preloading {sceneName}...");

        while (asyncOp.progress < 0.9f)
        {
            yield return null;
        }

        Debug.Log($"[SceneLoadBridge] {sceneName} preload ready (not activated)");
        InvokeLua(onReady);
    }

    private static IEnumerator ActivateCoroutine(LuaFunction onComplete)
    {
        AsyncOperation asyncOp = _pendingOp;
        string sceneName = _pendingSceneName;
        if (asyncOp == null)
        {
            Debug.LogError("[SceneLoadBridge] ActivateLoadedScene: no pending scene");
            DisposeLua(onComplete);
            yield break;
        }

        asyncOp.allowSceneActivation = true;
        while (!asyncOp.isDone)
        {
            yield return null;
        }

        _pendingOp = null;
        _pendingSceneName = null;
        Debug.Log($"[SceneLoadBridge] {sceneName} activated");
        InvokeLua(onComplete);
    }

    private static IEnumerator LoadAndActivateCoroutine(string sceneName, LuaFunction onReady)
    {
        bool preloadDone = false;
        yield return PreloadCoroutine(sceneName, null);
        preloadDone = _pendingOp != null;
        if (!preloadDone)
        {
            DisposeLua(onReady);
            yield break;
        }

        InvokeLua(onReady);
        yield return ActivateCoroutine(null);
    }

    private static void InvokeLua(LuaFunction func)
    {
        if (func == null)
        {
            return;
        }

        try
        {
            func.Call();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SceneLoadBridge] lua callback error: {e}");
        }
        finally
        {
            func.Dispose();
        }
    }

    private static void DisposeLua(LuaFunction func)
    {
        if (func != null)
        {
            func.Dispose();
        }
    }
}
