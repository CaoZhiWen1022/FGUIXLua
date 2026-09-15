require("FGUILua.GameLoading.UI_GameLoading")
require("FGUIScripts.GameLoadingPanel")
require("FGUILua.Common.UI_CommonTest")
require("FGUILua.Home.UI_Home")
require("FGUIScripts.HomePanel")
require("FGUILua.PopupTest.UI_PopupTest")
require("FGUIScripts.PopupTest")

local function RegisterAll()
    UIRegister.Register({
        UIID = UIID.GameLoading,
        uiClass = GameLoadingPanel,
        UIType = UIType.Panel,
        UILayer = UILayer.Panel,
        uiPackage = { "GameLoading" },
        name = "GameLoadingPanel",
    })
    UIRegister.Register({
        UIID = UIID.Home,
        uiClass = HomePanel,
        UIType = UIType.Panel,
        UILayer = UILayer.Panel,
        uiPackage = { "Common", "Home" },
        name = "HomePanel",
    })

    UIRegister.Register({
        UIID = UIID.PopupTest,
        uiClass = PopupTest,
        UIType = UIType.Popup,
        UILayer = UILayer.Popup,
        uiPackage = { "PopupTest" },
        name = "PopupTest",

    })
end

RegisterAll()
