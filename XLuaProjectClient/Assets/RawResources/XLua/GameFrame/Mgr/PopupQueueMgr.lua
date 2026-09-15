PopupQueueMgr = {}
PopupQueueMgr.__index = PopupQueueMgr

local function containsValue(list, value)
    if list == nil then
        return false
    end
    for i = 1, #list do
        if list[i] == value then
            return true
        end
    end
    return false
end

local function alreadyInQueue(queue, uiid)
    for i = 1, #queue do
        if queue[i].UIID == uiid then
            return true
        end
    end
    return false
end

function PopupQueueMgr.New(uiFrame)
    return setmetatable({
        queue = {},
        uiFrame = uiFrame,
    }, PopupQueueMgr)
end

function PopupQueueMgr:Push(param)
    param = OpenUIParam.New(param)
    if alreadyInQueue(self.queue, param.UIID) then
        print(string.format("[PopupQueueMgr] ui %s already in queue", tostring(param.UIID)))
        return
    end
    if self.uiFrame:GetUIInstance(param.UIID) ~= nil then
        print(string.format("[PopupQueueMgr] ui %s already open", tostring(param.UIID)))
        return
    end

    local registerInfo = UIRegister.GetUIInfo(param.UIID)
    if registerInfo == nil then
        print(string.format("[PopupQueueMgr] ui %s not register", tostring(param.UIID)))
        return
    end
    if registerInfo.UIType ~= UIType.Popup or (registerInfo.popupPriority or 0) == 0 then
        print(string.format("[PopupQueueMgr] ui %s popup register error", tostring(param.UIID)))
        return
    end

    print(string.format("[PopupQueueMgr] ui %s push queue", tostring(param.UIID)))
    table.insert(self.queue, param)
    self:CheckQueue()
end

function PopupQueueMgr:CheckQueue()
    if #self.queue == 0 then
        return
    end
    if self.uiFrame:GetOpenings() ~= nil then
        return
    end

    table.sort(self.queue, function(a, b)
        local ia = UIRegister.GetUIInfo(a.UIID)
        local ib = UIRegister.GetUIInfo(b.UIID)
        return (ia.popupPriority or 0) > (ib.popupPriority or 0)
    end)

    local target = nil
    for i = 1, #self.queue do
        local popup = self.queue[i]
        local popupInfo = UIRegister.GetUIInfo(popup.UIID)
        local curPanel = self.uiFrame:GetCurTopPanel()
        local topPopup = self.uiFrame:GetCurTopPopup()
        local curPopupPriority = topPopup and topPopup.UIRegisterInfo.popupPriority or PopupPriority.None

        local dependOk = true
        if popupInfo.popupDependPanel and #popupInfo.popupDependPanel > 0 then
            dependOk = curPanel ~= nil and containsValue(popupInfo.popupDependPanel, curPanel.UIID)
        end

        if (popupInfo.popupPriority > curPopupPriority and dependOk)
            or (popupInfo.popupPriority == curPopupPriority and popupInfo.isSamePriorityMeanwhileOpen) then
            target = popup
            break
        end
    end

    if target ~= nil then
        self:Show(target)
    end
end

function PopupQueueMgr:Show(param)
    param.popupQueueOpen = true
    for i = #self.queue, 1, -1 do
        if self.queue[i] == param or self.queue[i].UIID == param.UIID then
            table.remove(self.queue, i)
            break
        end
    end
    self.uiFrame:Open(param)
end
