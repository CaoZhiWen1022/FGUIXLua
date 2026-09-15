-- 全屏 Panel 逻辑基类（业务 Panel / UIFullMask 继承此类）
UILogicPanel = setmetatable({}, UIBase)
UILogicPanel.__index = UILogicPanel
