PopupTest = setmetatable({}, UIPopup)
PopupTest.__index = PopupTest
PopupTest.m_uiClass = UI_PopupTest

function PopupTest:OnOpened()
    self.isMaskClickCloseThis = false
    UIPopup.OnOpened(self)
    self.m_ui.m_closeBtn.onClick:Add(function()
        UIFrame:Close(UIID.PopupTest)
    end)
end
