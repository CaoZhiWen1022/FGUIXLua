UIRegister = {}
UIRegister.UIInfo = {}

--[[
注册表 info 参数说明：

【必填】
  UIID        number   界面唯一 ID（见 UIID.lua）
  uiClass     table    逻辑类（如 HomePanel / PopupTest），需可 New()，且通常带 m_uiClass

【基础（可选，有默认）】
  uiPackage   string[] 依赖的 FairyGUI 包名列表；默认取 uiClass.m_uiClass.PKG
  name        string   界面名（调试 / view.name）；默认取 uiClass.m_uiClass.RES
  UIType      number   UIType.Panel | UIType.Popup；默认 Panel
  UILayer     number   所在层级 UILayer.*；默认 UILayer.Panel

【弹窗队列相关（Popup 常用）】
  popupPriority              number   弹窗优先级 PopupPriority.*；默认 Normal
                                      （为 None/0 时不可进 PopupQueueMgr）
  isSamePriorityMeanwhileOpen bool    同优先级是否可同时打开多个；默认 false
  popupTimeout               number   弹窗超时（秒），0 表示不超时；默认 0
  popupDependPanel           number[] 依赖的顶层 Panel UIID 列表；默认 {}
                                      非空时：仅当当前顶层 Panel 在列表中才允许弹出

【弹窗表现（可写在注册表，供业务 / UIPopup 读取）】
  showMask              bool  是否显示遮罩；默认由 UIPopup 实例决定
  isMaskClickCloseThis  bool  点击遮罩是否关闭本弹窗
  isOpenAni             bool  是否播放打开缩放动画
]]
function UIRegister.Register(info)
    if type(info) ~= "table" or info.UIID == nil then
        error("[UIRegister] Register expects info table with UIID")
    end
    if UIRegister.UIInfo[info.UIID] ~= nil then
        print(string.format("[UIRegister] uiID:%s already registered", tostring(info.UIID)))
        return
    end

    local uiClass = info.uiClass
    info.uiPackage = info.uiPackage
        or (uiClass and uiClass.m_uiClass and { uiClass.m_uiClass.PKG })
        or {}
    info.name = info.name
        or (uiClass and uiClass.m_uiClass and uiClass.m_uiClass.RES)
        or tostring(info.UIID)
    info.UIType = info.UIType or UIType.Panel
    info.UILayer = info.UILayer or UILayer.Panel
    info.isSamePriorityMeanwhileOpen = info.isSamePriorityMeanwhileOpen or false
    info.popupPriority = info.popupPriority or PopupPriority.Normal
    info.popupTimeout = info.popupTimeout or 0
    info.popupDependPanel = info.popupDependPanel or {}

    UIRegister.UIInfo[info.UIID] = info
end

function UIRegister.GetUIInfo(uiid)
    return UIRegister.UIInfo[uiid]
end
