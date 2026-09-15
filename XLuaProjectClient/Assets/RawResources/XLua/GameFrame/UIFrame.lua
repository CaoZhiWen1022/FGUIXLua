-- UI 框架核心（唯一对外入口）
UIFrame = {
    timerMgr = nil,
}

local M = UIFrame

function UIFrame.Init(opt, callback)
    UIFrameConfig.Init(opt)

    M:GameLaunchInit()
    M:BindPopupMaskCreateFunc(function()
        return M:DefaultPopupMaskCreate()
    end)
    M:GameLoadInit(function(ok)
        M.timerMgr = TimerMgr.New()
        print(string.format("[UIFrame] Init done ok=%s", tostring(ok)))
        if callback then callback(ok) end
    end)
    return M
end

function M:GameLaunchInit()
    self.popupQueueMgr = PopupQueueMgr.New(self)
    self.uiLayerMap = {}
    self.openings = {}
    self.openUIs = {}
    self.concealUIs = {}
    self.fullMaskUI = nil
    self.popupMaskCreateFunc = nil
    self.layerBlurFilters = {}
    self.layerRippleEffects = {}

    GRoot.inst:SetContentScaleFactor(
        UIFrameConfig.FRAME_WIDTH,
        UIFrameConfig.FRAME_HEIGHT,
        UIContentScaler.ScreenMatchMode.MatchWidthOrHeight
    )
    self:InitUILayer()
    print(string.format("[UIFrame] GameLaunchInit design=%dx%d",
        UIFrameConfig.FRAME_WIDTH, UIFrameConfig.FRAME_HEIGHT))
end

function M:InitUILayer()
    local defs = {}
    for name, id in pairs(UILayer) do
        table.insert(defs, { name = name, id = id })
    end
    table.sort(defs, function(a, b) return a.id < b.id end)

    for _, def in ipairs(defs) do
        local com = GComponent()
        com.name = def.name
        com.opaque = false
        com.sortingOrder = def.id
        com:SetSize(GRoot.inst.width, GRoot.inst.height)
        com:AddRelation(GRoot.inst, RelationType.Size)
        GRoot.inst:AddChild(com)
        self.uiLayerMap[def.id] = com
    end
end

function M:GameLoadInit(callback)
    self:InitFonts(function(fontOk)
        if not fontOk then
            print("[UIFrame] InitFonts failed")
            if callback then callback(false) end
            return
        end
        UIBundleMgr.LoadBundlePackage(UIFrameConfig.INIT_LOAD_PKGS, callback)
    end)
end

--- 加载可配置字体 AB / 字体名，并设置 FairyGUI 默认字体
function M:InitFonts(callback)
    local fonts = UIFrameConfig.INIT_FONTS
    if fonts == nil or #fonts == 0 then
        if callback then callback(true) end
        return
    end
    ResourcesMgr.InitFonts(fonts, callback)
end

function M:InitFullMask(uiid, callback)
    local info = UIRegister.GetUIInfo(uiid)
    if info == nil then
        print(string.format("[UIFrame] fullMask init fail, uiid=%s not registered", tostring(uiid)))
        if callback then callback(false) end
        return
    end
    if info.UIType ~= UIType.Panel then
        print(string.format("[UIFrame] fullMask init fail, uiid=%s not Panel", tostring(uiid)))
        if callback then callback(false) end
        return
    end
    if info.UILayer ~= UILayer.FullScreenMask then
        print(string.format("[UIFrame] fullMask init fail, uiid=%s not FullScreenMask layer", tostring(uiid)))
        if callback then callback(false) end
        return
    end

    self:Open(OpenUIParam.New({
        UIID = uiid,
        openCall = function()
            self.fullMaskUI = self:GetUIInstance(uiid)
            if self.fullMaskUI and self.fullMaskUI.m_ui then
                self.fullMaskUI.m_ui.view.visible = false
            end
            self:RemoveFromOpenList(self.fullMaskUI)
            if callback then callback(true) end
        end,
    }))
end

function M:BindPopupMaskCreateFunc(func)
    self.popupMaskCreateFunc = func
end

function M:DefaultPopupMaskCreate()
    local g = GGraph()
    local color = CS.UnityEngine.Color(0, 0, 0, 1)
    g:DrawRect(GRoot.inst.width, GRoot.inst.height, 0, color, color)
    g:AddRelation(GRoot.inst, RelationType.Size)
    return g
end

function M:OpenByUiid(uiid)
    self:Open(OpenUIParam.New(uiid))
end

function M:Open(param)
    param = OpenUIParam.New(param)
    local info = UIRegister.GetUIInfo(param.UIID)
    if info == nil then
        error(string.format("[UIFrame] Open failed, uid=%s not registered", tostring(param.UIID)))
    end

    if self:ContainsOpening(param.UIID) then
        print(string.format("[UIFrame] ui %s is opening", tostring(param.UIID)))
        return
    end

    if info.UIType == UIType.Popup and not param.popupQueueOpen then
        print(string.format("[UIFrame] ui %s must open via popupQueueMgr:Push", tostring(param.UIID)))
        return
    end

    local existed = self:GetUIInstance(param.UIID)
    if existed ~= nil then
        if param.reOpen then
            self:Close(param.UIID, true)
        else
            print(string.format("[UIFrame] ui %s is open", tostring(param.UIID)))
            return existed
        end
    end

    table.insert(self.openings, param.UIID)
    self:SetFullScreenMaskPanelVisible(true)

    UIBundleMgr.LoadBundlePackage(info.uiPackage, function(ok)
        if not ok then
            print(string.format("[UIFrame] ui %s load package failed", tostring(param.UIID)))
            self:SetFullScreenMaskPanelVisible(false)
            self:RemoveOpening(param.UIID)
            if param.errorCall then param.errorCall() end
            return
        end

        local ui = info.uiClass:New()
        if ui:Open(param) then
            table.insert(self.openUIs, ui)
            self:RemoveOpening(param.UIID)
            ui:OnOpened()
            self:GetUILayer(info.UILayer):AddChild(ui.m_ui.view)
            self:RefPanelShow()
            self:SetFullScreenMaskPanelVisible(false)
            return
        end

        print(string.format("[UIFrame] ui %s open failed", tostring(param.UIID)))
        self:RemoveOpening(param.UIID)
        if param.errorCall then param.errorCall() end
        self:SetFullScreenMaskPanelVisible(false)
    end)
end

function M:RefPanelShow()
    local show = true
    for i = #self.openUIs, 1, -1 do
        local ui = self.openUIs[i]
        if ui.UIRegisterInfo.UIType == UIType.Panel and ui.UIRegisterInfo.UILayer == UILayer.Panel then
            if show then
                show = false
                ui:Show()
            else
                ui:Hide()
            end
        end
    end
end

function M:OpenUIIns(ui)
    if ui == nil or ui:isDisposed() then
        return
    end
    table.insert(self.openUIs, ui)
    ui:Show()
    self:RemoveFromConcealList(ui)
end

function M:GetUIInstance(uiid)
    for i = 1, #self.openUIs do
        if self.openUIs[i].UIID == uiid then
            return self.openUIs[i]
        end
    end
    return nil
end

function M:Close(uiid, dispose)
    if dispose == nil then
        dispose = true
    end
    local uiins = self:GetUIInstance(uiid)
    if uiins ~= nil and not uiins:isDisposed() then
        self:RemoveFromOpenList(uiins)
        if dispose then
            uiins.allowClose = true
            uiins:Close()
        else
            uiins:Hide()
            table.insert(self.concealUIs, uiins)
        end
    else
        print(string.format("[UIFrame] close error %s not found or disposed", tostring(uiid)))
    end
    self:RefPanelShow()
end

function M:CloseAll(exclude)
    exclude = exclude or {}
    local excludeMap = {}
    for i = 1, #exclude do
        excludeMap[exclude[i]] = true
    end
    local list = {}
    for i = 1, #self.openUIs do
        if not excludeMap[self.openUIs[i].UIID] then
            table.insert(list, self.openUIs[i].UIID)
        end
    end
    for i = 1, #list do
        self:Close(list[i], true)
    end
end

function M:GetUILayer(layer)
    return self.uiLayerMap[layer]
end

function M:SetFullScreenMaskPanelVisible(visible)
    if self.fullMaskUI ~= nil then
        self.fullMaskUI:SetMaskVisible(visible)
    end
end

function M:GetOpenings()
    if #self.openings > 0 then
        return self.openings
    end
    return nil
end

function M:GetCurTopPopup()
    local cur = nil
    for i = 1, #self.openUIs do
        if self.openUIs[i].UIRegisterInfo.UIType == UIType.Popup then
            cur = self.openUIs[i]
        end
    end
    return cur
end

function M:GetCurPopupAll()
    local list = {}
    for i = 1, #self.openUIs do
        if self.openUIs[i].UIRegisterInfo.UIType == UIType.Popup then
            table.insert(list, self.openUIs[i])
        end
    end
    return list
end

function M:RefreshPopupMask()
    local popAll = self:GetCurPopupAll()
    for i = 1, #popAll do
        if popAll[i].popupMask ~= nil then
            popAll[i].popupMask.visible = (i == #popAll)
        end
    end
    self:RefreshPopupBlur(#popAll > 0)
end

--- 存在弹窗时模糊 Panel 层；最后一个弹窗关闭后撤掉
function M:RefreshPopupBlur(enabled)
    if not UIFrameConfig.PANEL_BLUR_ON_POPUP then
        enabled = false
    end
    self:SetLayerBlur(UILayer.Panel, enabled, UIFrameConfig.PANEL_BLUR_SIZE)
end

--- 播放层水波纹（从中心扩散）。layer 默认 Panel；也可只传 opt 表。
--- opt: duration / strength / width / frequency / centerX / centerY
function M:PlayLayerRipple(layer, opt)
    if type(layer) == "table" then
        opt = layer
        layer = UILayer.Panel
    end
    layer = layer or UILayer.Panel
    opt = opt or {}

    local com = self:GetUILayer(layer)
    if com == nil then
        print(string.format("[UIFrame] PlayLayerRipple fail, layer=%s not found", tostring(layer)))
        return
    end

    local effect = self.layerRippleEffects[layer]
    if effect == nil then
        effect = UIRippleEffect()
        self.layerRippleEffects[layer] = effect
    end
    effect:Attach(com)
    effect.duration = opt.duration or UIFrameConfig.PANEL_RIPPLE_DURATION
    effect.strength = opt.strength or UIFrameConfig.PANEL_RIPPLE_STRENGTH
    effect.width = opt.width or UIFrameConfig.PANEL_RIPPLE_WIDTH
    effect.frequency = opt.frequency or UIFrameConfig.PANEL_RIPPLE_FREQUENCY
    effect.centerX = opt.centerX or 0.5
    effect.centerY = opt.centerY or 0.5
    effect:Play()
end

function M:StopLayerRipple(layer)
    layer = layer or UILayer.Panel
    local effect = self.layerRippleEffects[layer]
    if effect ~= nil then
        effect:Stop()
    end
end

function M:SetLayerBlur(layer, enabled, blurSize)
    local com = self:GetUILayer(layer)
    if com == nil then
        return
    end
    if not enabled then
        if com.filter ~= nil then
            com.filter = nil
        end
        self.layerBlurFilters[layer] = nil
        return
    end
    local filter = self.layerBlurFilters[layer]
    if filter == nil then
        filter = BlurFilter()
        self.layerBlurFilters[layer] = filter
        com.filter = filter
    end
    filter.blurSize = blurSize or 2
end

function M:GetCurTopPanel()
    local cur = nil
    for i = 1, #self.openUIs do
        if self.openUIs[i].UIRegisterInfo.UIType == UIType.Panel then
            cur = self.openUIs[i]
        end
    end
    return cur
end

function M:RegisterUI(info)
    UIRegister.Register(info)
end

function M:ContainsOpening(uiid)
    for i = 1, #self.openings do
        if self.openings[i] == uiid then
            return true
        end
    end
    return false
end

function M:RemoveOpening(uiid)
    for i = #self.openings, 1, -1 do
        if self.openings[i] == uiid then
            table.remove(self.openings, i)
            break
        end
    end
end

function M:RemoveFromOpenList(ui)
    for i = #self.openUIs, 1, -1 do
        if self.openUIs[i] == ui then
            table.remove(self.openUIs, i)
            break
        end
    end
end

function M:RemoveFromConcealList(ui)
    for i = #self.concealUIs, 1, -1 do
        if self.concealUIs[i] == ui then
            table.remove(self.concealUIs, i)
            break
        end
    end
end
