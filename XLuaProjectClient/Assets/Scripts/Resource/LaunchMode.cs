/// <summary>
/// 游戏资源启动模式。
/// </summary>
public enum LaunchMode
{
    /// <summary>直读 Assets/RawResources 文件夹。</summary>
    Folder = 1,

    /// <summary>从 StreamingAssets/AB 加载 AssetBundle。</summary>
    AssetBundle = 2,
}
