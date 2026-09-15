/// <summary>
/// 解析当前启动模式：编辑器读菜单偏好，非编辑器固定 AssetBundle。
/// </summary>
public static class LaunchModeConfig
{
    public const string EditorPrefsKey = "XLuaProject.LaunchMode";

    public static LaunchMode Resolve()
    {
#if UNITY_EDITOR
        int value = UnityEditor.EditorPrefs.GetInt(EditorPrefsKey, (int)LaunchMode.Folder);
        if (value == (int)LaunchMode.AssetBundle)
        {
            return LaunchMode.AssetBundle;
        }

        return LaunchMode.Folder;
#else
        return LaunchMode.AssetBundle;
#endif
    }

#if UNITY_EDITOR
    public static void SetEditorLaunchMode(LaunchMode mode)
    {
        UnityEditor.EditorPrefs.SetInt(EditorPrefsKey, (int)mode);
    }

    public static LaunchMode GetEditorLaunchMode()
    {
        return Resolve();
    }
#endif
}
