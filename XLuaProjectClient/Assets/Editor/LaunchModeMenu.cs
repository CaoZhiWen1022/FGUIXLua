using UnityEditor;
using UnityEngine;

/// <summary>
/// XLuaProject 菜单：编辑器下切换 Folder / AssetBundle 启动模式。
/// </summary>
public static class LaunchModeMenu
{
    private const string MenuFolder = "XLuaProject/Launch Mode/Folder";
    private const string MenuAB = "XLuaProject/Launch Mode/AssetBundle";

    [MenuItem(MenuFolder, false, 50)]
    private static void SetFolderMode()
    {
        LaunchModeConfig.SetEditorLaunchMode(LaunchMode.Folder);
        Debug.Log("[XLuaProject] LaunchMode → Folder（直读 RawResources）");
    }

    [MenuItem(MenuFolder, true)]
    private static bool SetFolderModeValidate()
    {
        Menu.SetChecked(MenuFolder, LaunchModeConfig.GetEditorLaunchMode() == LaunchMode.Folder);
        return true;
    }

    [MenuItem(MenuAB, false, 51)]
    private static void SetAssetBundleMode()
    {
        LaunchModeConfig.SetEditorLaunchMode(LaunchMode.AssetBundle);
        Debug.Log("[XLuaProject] LaunchMode → AssetBundle（StreamingAssets/AB）");
    }

    [MenuItem(MenuAB, true)]
    private static bool SetAssetBundleModeValidate()
    {
        Menu.SetChecked(MenuAB, LaunchModeConfig.GetEditorLaunchMode() == LaunchMode.AssetBundle);
        return true;
    }
}
