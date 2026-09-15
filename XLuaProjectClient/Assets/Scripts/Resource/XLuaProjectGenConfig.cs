using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XLua;

/// <summary>
/// xLua 回调委托生成配置（异步 AB 加载需要把 C# Action 适配为 Lua function）。
/// 修改后请执行 XLua → Generate Code。
/// </summary>
public static class XLuaProjectGenConfig
{
    [CSharpCallLua]
    public static List<Type> CSharpCallLua = new List<Type>()
    {
        typeof(Action),
        typeof(Action<bool>),
        typeof(Action<UnityEngine.AssetBundle>),
    };
}
