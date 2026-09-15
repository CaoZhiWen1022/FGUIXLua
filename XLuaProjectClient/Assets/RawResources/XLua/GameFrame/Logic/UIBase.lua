-- UI 逻辑基类
-- m_ui: FGUIPanel 视图脚本实例（含 .view / 节点字段）
UIBase = {}
UIBase.__index = UIBase
UIBase.m_uiClass = nil

function UIBase:New()
    local o = setmetatable({}, self)
    o.allowClose = false
    o.UIRegisterInfo = nil
    o.UIID = -1
    o.openParam = nil
    o.m_ui = nil
    o.data = nil
    return o
end

function UIBase:isDisposed()
    return self.m_ui == nil or self.m_ui.view == nil or self.m_ui.view.isDisposed
end

function UIBase:Open(openParam)
    self.UIID = openParam.UIID
    self.openParam = openParam
    self.data = openParam.data
    self.UIRegisterInfo = UIRegister.GetUIInfo(openParam.UIID)
    if self.UIRegisterInfo == nil then
        print(string.format("[UIBase] ui %s not found", tostring(self.UIID)))
        return false
    end

    local uiClass = self.m_uiClass
    if uiClass == nil then
        print(string.format("[UIBase] ui %s missing m_uiClass", tostring(self.UIID)))
        return false
    end

    self.m_ui = uiClass.Create()
    if self.m_ui == nil or self.m_ui.view == nil then
        print(string.format("[UIBase] ui %s create failed", tostring(self.UIID)))
        return false
    end
    self.m_ui.view.name = tostring(self.UIRegisterInfo.name or self.UIID)
    self:_startUpdateTimers()
    return true
end

function UIBase:Resize()
    if self.m_ui and self.m_ui.view then
        self.m_ui.view:MakeFullScreen()
    end
end

function UIBase:OnOpened()
    if self.openParam and self.openParam.openCall then
        self.openParam.openCall()
        self.openParam.openCall = nil
    end
    self:Resize()
    UIFrame.popupQueueMgr:CheckQueue()
    UIBundleMgr.AddRefCount(self.UIRegisterInfo.uiPackage)
end

function UIBase:Close()
    if not self.allowClose then
        return
    end
    if self:isDisposed() then
        return
    end
    self:_stopUpdateTimers()
    self.m_ui:Dispose()
    self.m_ui = nil
    self:OnClosed()
end

function UIBase:OnClosed()
    if self.openParam and self.openParam.closeCall then
        self.openParam.closeCall()
    end
    UIFrame.popupQueueMgr:CheckQueue()
    if self.UIRegisterInfo then
        UIBundleMgr.RemoveRefCount(self.UIRegisterInfo.uiPackage)
    end
end

function UIBase:CloseThis()
    UIFrame:Close(self.UIID)
end

function UIBase:Hide()
    if self.m_ui and self.m_ui.view then
        self.m_ui.view.visible = false
    end
    self:OnHidden()
end

function UIBase:OnHidden()
end

function UIBase:Show()
    if self.m_ui and self.m_ui.view then
        self.m_ui.view.visible = true
    end
    self:OnShown()
end

function UIBase:OnShown()
end

--- 每帧调用（子类按需覆写，dt 为 Time.deltaTime）
function UIBase:update(dt)
end

--- 每秒调用（子类按需覆写）
function UIBase:updateSec()
end

function UIBase:_startUpdateTimers()
    if self._updateTimersStarted then
        return
    end
    local tm = UIFrame.timerMgr
    if tm == nil then
        return
    end
    -- 只给真正覆写的界面挂定时器
    if self.update ~= UIBase.update then
        self._onUpdate = function()
            if not self:isDisposed() then
                self:update(CS.UnityEngine.Time.deltaTime)
            end
        end
        tm:Loop(0, self._onUpdate, self)
    end
    if self.updateSec ~= UIBase.updateSec then
        self._onUpdateSec = function()
            if not self:isDisposed() then
                self:updateSec()
            end
        end
        tm:Loop(1, self._onUpdateSec, self)
    end
    self._updateTimersStarted = true
end

function UIBase:_stopUpdateTimers()
    if not self._updateTimersStarted then
        return
    end
    local tm = UIFrame.timerMgr
    if tm ~= nil then
        tm:ClearAll(self)
    end
    self._onUpdate = nil
    self._onUpdateSec = nil
    self._updateTimersStarted = false
end
