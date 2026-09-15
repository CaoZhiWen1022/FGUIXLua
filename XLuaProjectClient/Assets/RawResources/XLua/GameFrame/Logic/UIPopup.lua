UIPopup = setmetatable({}, UIBase)
UIPopup.__index = UIPopup

function UIPopup:New()
    local o = UIBase.New(self)
    o.showMask = true
    o.isMaskClickCloseThis = true
    o.popupMask = nil
    o.isOpenAni = true
    o.thisHideOtherPopup = {}
    o.openAniTweener = nil
    return o
end

function UIPopup:Resize()
    local view = self.m_ui.view
    local x = GRoot.inst.width / 2 - view.width / 2
    local y = GRoot.inst.height / 2 - view.height / 2
    view:SetPosition(x, y, 0)
end

function UIPopup:OnOpened()
    UIBase.OnOpened(self)
    self:InitMask()
    self:OpenAni()
    self:HideOtherPopup()
    UIFrame:RefreshPopupMask()
end

function UIPopup:OpenAni()
    if not self.isOpenAni then
        return
    end
    local view = self.m_ui.view
    view:SetPivot(0.5, 0.5)
    view:SetScale(0.3, 0.3)
    view.touchable = false
    local ok = pcall(function()
        -- xLua 常把 OnUpdate 绑到无参 GTweenCallback，用 self.openAniTweener 取值
        self.openAniTweener = GTween.To(0.3, 1.2, 0.1)
            :SetTarget(view)
            :OnUpdate(function()
                local tweener = self.openAniTweener
                if tweener ~= nil and view ~= nil and not view.isDisposed then
                    local value = tweener.value.x
                    view:SetScale(value, value)
                end
            end)
            :OnComplete(function()
                self.openAniTweener = GTween.To(1.2, 1, 0.1)
                :SetTarget(view)
                :OnUpdate(function()
                    local tweener = self.openAniTweener
                    if tweener ~= nil and view ~= nil and not view.isDisposed then
                        local value = tweener.value.x
                        view:SetScale(value, value)
                    end
                end)
                :OnComplete(function()
                    if view ~= nil and not view.isDisposed then
                        view.touchable = true
                        view:SetScale(1, 1)
                    end
                    self.openAniTweener = nil
                end)
            end)
    end)
    if not ok then
        view:SetScale(1, 1)
        view.touchable = true
        self.openAniTweener = nil
    end
end

function UIPopup:InitMask()
    if self.showMask and self.popupMask == nil then
        local createFunc = UIFrame.popupMaskCreateFunc
        if createFunc == nil then
            print("[UIPopup] popupMaskCreateFunc not bound")
            return
        end
        self.popupMask = createFunc()
        self.popupMask:MakeFullScreen()
        self.popupMask.x = 0
        self.popupMask.y = 0
        self.popupMask.alpha = UIFrameConfig.POPUP_MASK_ALPHA
        UIFrame:GetUILayer(UILayer.Popup):AddChild(self.popupMask)
        if self.isMaskClickCloseThis then
            self.popupMask.onClick:Add(function()
                self:CloseThis()
            end)
        end
    end
    if self.popupMask ~= nil then
        self.popupMask.visible = true
    end
end

function UIPopup:OnClosed()
    UIBase.OnClosed(self)
    if self.popupMask ~= nil then
        self.popupMask:Dispose()
        self.popupMask = nil
    end
    self:ShowThisHideOtherPopup()
    UIFrame:RefreshPopupMask()
end

function UIPopup:Hide()
    UIBase.Hide(self)
    if self.popupMask ~= nil then
        self.popupMask.visible = false
    end
    UIFrame:RefreshPopupMask()
end

function UIPopup:HideOtherPopup()
    local allPopup = UIFrame:GetCurPopupAll()
    for i = 1, #allPopup do
        local element = allPopup[i]
        if element ~= self then
            local isclose = true
            if self.UIRegisterInfo.isSamePriorityMeanwhileOpen then
                isclose = element.UIRegisterInfo.popupPriority ~= self.UIRegisterInfo.popupPriority
            end
            if isclose then
                UIFrame:Close(element.UIID, false)
                table.insert(self.thisHideOtherPopup, element)
            end
        end
    end
end

function UIPopup:ShowThisHideOtherPopup()
    for i = 1, #self.thisHideOtherPopup do
        UIFrame:OpenUIIns(self.thisHideOtherPopup[i])
    end
    self.thisHideOtherPopup = {}
end

function UIPopup:Close()
    if self.openAniTweener ~= nil then
        pcall(function()
            self.openAniTweener:Kill()
        end)
        self.openAniTweener = nil
    end
    UIBase.Close(self)
end
