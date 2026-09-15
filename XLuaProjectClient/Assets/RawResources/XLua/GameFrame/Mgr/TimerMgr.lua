-- 基于 FairyGUI.Timers 的定时器管理
TimerMgr = {}
TimerMgr.__index = TimerMgr

function TimerMgr.New()
    return setmetatable({
        _list = {},
        _nextId = 1,
    }, TimerMgr)
end

local function makeKey(callback, target)
    return tostring(callback) .. "_" .. tostring(target)
end

--- 延迟执行一次（delay 秒，0 表示下一帧）
function TimerMgr:Once(delay, callback, target)
    local id = self._nextId
    self._nextId = id + 1
    local key = makeKey(callback, target)
    local function wrapper()
        self._list[key] = nil
        if callback then
            callback(target)
        end
    end
    self._list[key] = { id = id, wrapper = wrapper, callback = callback, target = target }
    Timers.inst:Add(delay or 0, 1, wrapper)
    return id
end

--- 循环执行
function TimerMgr:Loop(delay, callback, target)
    local key = makeKey(callback, target)
    local function wrapper()
        if callback then
            callback(target)
        end
    end
    self._list[key] = { wrapper = wrapper, callback = callback, target = target, loop = true }
    Timers.inst:Add(delay or 0, 0, wrapper) -- repeat 0 = infinite in FairyGUI Timers
    return key
end

function TimerMgr:Clear(callback, target)
    local key = makeKey(callback, target)
    local item = self._list[key]
    if item == nil then
        return
    end
    Timers.inst:Remove(item.wrapper)
    self._list[key] = nil
end

function TimerMgr:ClearAll(target)
    local toClear = {}
    for key, item in pairs(self._list) do
        if item.target == target then
            table.insert(toClear, item)
        end
    end
    for i = 1, #toClear do
        self:Clear(toClear[i].callback, toClear[i].target)
    end
end
