HomePanel = setmetatable({}, UILogicPanel)
HomePanel.__index = HomePanel
HomePanel.m_uiClass = UI_Home

function HomePanel:OnOpened()
    UILogicPanel.OnOpened(self)
    print("[HomePanel] OnOpened")
    -- self.m_ui.m_commonText.m_commonText.text = "测试"

    self.m_ui.m_popupTest.onClick:Add(function()
        UIFrame.popupQueueMgr:Push(UIID.PopupTest)
    end)
    self.m_ui.m_Ripple.onClick:Add(function()
        UIFrame:PlayLayerRipple()
    end)
    self.updateTime = 0
    self.updateTimeSec = 0
    self.deerObj = nil
    self:LoadDeer()
end

function HomePanel:OnClosed()
    self:ClearDeer()
    UILogicPanel.OnClosed(self)
end

function HomePanel:LoadDeer()
    ResourcesMgr.LoadPrefab("Deer", function(prefab)
        if self:isDisposed() then
            return
        end
        if prefab == nil then
            print("[HomePanel] load Deer failed")
            return
        end
        self:AttachDeer(prefab)
    end)
end

function HomePanel:AttachDeer(prefab)
    self:ClearDeer()
    local loader = self.m_ui and self.m_ui.m_DeerRoot
    if loader == nil then
        print("[HomePanel] DeerRoot missing")
        return
    end

    local go = CS.UnityEngine.Object.Instantiate(prefab)
    self.deerObj = go
    local wrapper = CS.FairyGUI.GoWrapper()
    wrapper:SetWrapTarget(go, true)
    loader.displayObject:AddChild(wrapper)
    wrapper:SetXY(loader.width * 0.5, loader.height * 0.5)
    self._deerWrapper = wrapper
end

function HomePanel:ClearDeer()
    self.deerObj = nil
    if self._deerWrapper == nil then
        return
    end
    self._deerWrapper:Dispose()
    self._deerWrapper = nil
end

function HomePanel:update(dt)
    self.updateTime = self.updateTime + dt
    self.m_ui.m_text1.text = string.format("%.2f", self.updateTime)
    if self.deerObj ~= nil then
        self.deerObj.transform:Rotate(0, 90 * dt, 0)
    end
end

function HomePanel:updateSec()
    self.updateTimeSec = self.updateTimeSec + 1
    self.m_ui.m_text2.text = string.format("%.2f", self.updateTimeSec)
end
