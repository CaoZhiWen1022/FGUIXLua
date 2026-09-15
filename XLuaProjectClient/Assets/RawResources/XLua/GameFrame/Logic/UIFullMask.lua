UIFullMask = setmetatable({}, UILogicPanel)
UIFullMask.__index = UIFullMask

function UIFullMask:New()
    local o = UILogicPanel.New(self)
    o.visibleCount = 0
    return o
end

function UIFullMask:SetMaskVisible(visible)
    if visible then
        self.visibleCount = self.visibleCount + 1
    else
        self.visibleCount = self.visibleCount - 1
    end
    if self.visibleCount > 0 then
        self:SetShow()
    else
        self:SetHide()
    end
end

function UIFullMask:SetShow()
    if self.m_ui and self.m_ui.view and not self.m_ui.view.visible then
        self.m_ui.view.visible = true
    end
end

function UIFullMask:SetHide()
    if self.m_ui and self.m_ui.view and self.m_ui.view.visible then
        self.m_ui.view.visible = false
    end
end
