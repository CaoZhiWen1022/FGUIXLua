OpenUIParam = {}
OpenUIParam.__index = OpenUIParam

function OpenUIParam.New(uiid, data, openCall, closeCall, errorCall, reOpen, popupQueueOpen)
    if type(uiid) == "table" then
        local t = uiid
        return setmetatable({
            UIID = t.UIID or 0,
            data = t.data,
            openCall = t.openCall,
            closeCall = t.closeCall,
            errorCall = t.errorCall,
            reOpen = t.reOpen or false,
            popupQueueOpen = t.popupQueueOpen or false,
        }, OpenUIParam)
    end

    return setmetatable({
        UIID = uiid or 0,
        data = data,
        openCall = openCall,
        closeCall = closeCall,
        errorCall = errorCall,
        reOpen = reOpen or false,
        popupQueueOpen = popupQueueOpen or false,
    }, OpenUIParam)
end
