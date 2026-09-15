using FairyGUI;
using XLua;

/// <summary>
/// 把 FairyGUI 显示对象生命周期桥到 Lua peer 表。
/// Lua 侧约定方法（均可选）：OnInit / OnShown / OnHide / OnDispose
/// </summary>
public static class FGUILuaBinder
{
    public static void Bind(GObject view, LuaTable peer)
    {
        if (view == null || peer == null)
            return;

        Call(peer, "OnInit", view);

        view.onAddedToStage.Add(() => Call(peer, "OnShown", view));
        view.onRemovedFromStage.Add(() => Call(peer, "OnHide", view));
    }

    /// <summary>
    /// 销毁 UI：先调 Lua OnDispose，再 Dispose 原生对象。
    /// </summary>
    public static void Dispose(GObject view, LuaTable peer)
    {
        if (peer != null)
            Call(peer, "OnDispose", view);

        if (view != null && !view.isDisposed)
            view.Dispose();
    }

    private static void Call(LuaTable peer, string funcName, GObject view)
    {
        var func = peer.Get<LuaFunction>(funcName);
        if (func == null)
            return;

        try
        {
            func.Action(peer, view);
        }
        finally
        {
            func.Dispose();
        }
    }
}
