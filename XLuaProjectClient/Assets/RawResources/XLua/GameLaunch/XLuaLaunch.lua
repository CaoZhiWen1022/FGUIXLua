-- LaunchMode 由 C# GameLaunch 在 require 前注入（Folder / AssetBundle）
if LaunchMode == nil then
    LaunchMode = "Folder"
end

require("XLuaCommon.XLuaCommonLaunch")
require("GameFrame.launch")
require("GameLaunch.UIID")
require("FGUIScripts.UIBootstrap")

print(string.format("[XLuaLaunch] LaunchMode=%s", tostring(LaunchMode)))
UIFrame.Init(nil, function(ok)
    if not ok then
        print("[XLuaLaunch] UIFrame.Init failed")
        return
    end
    UIFrame:OpenByUiid(UIID.GameLoading)
end)
