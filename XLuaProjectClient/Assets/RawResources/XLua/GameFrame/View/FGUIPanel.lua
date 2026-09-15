-- FairyGUI 视图绑定基类（仅负责：组件与节点绑定）
-- 生命周期由 CS.FGUILuaBinder 触发：OnInit / OnShown / OnHide / OnDispose
-- Create 内 LoadBundlePackage 与 UIFrame:Open 的包加载可并存（幂等 / 引用计数）

FGUIPanel = {}
FGUIPanel.__index = FGUIPanel

-- url -> UI_Xxx 类，用于嵌套公共组件自动绑定
local extensionMap = {}

--- 注册可被嵌套引用的组件脚本（对应 C# UIObjectFactory.SetPackageItemExtension）
function FGUIPanel.RegisterExtension(cls)
    if cls == nil or cls.URL == nil or cls.URL == "" then
        return
    end
    extensionMap[cls.URL] = cls
end

--- 将已存在的 GObject 绑定为指定 Lua 组件脚本
function FGUIPanel.Wrap(cls, view)
    if view == nil or cls == nil then
        return nil
    end
    local inst = setmetatable({ view = view }, cls)
    if cls.OnInit ~= nil then
        cls.OnInit(inst, view)
    end
    return inst
end

--- 按 resourceURL 自动包装；未注册扩展则返回原始 GObject
function FGUIPanel.AutoWrap(view)
    if view == nil then
        return nil
    end
    local url = view.resourceURL
    if url ~= nil and url ~= "" then
        local cls = extensionMap[url]
        if cls ~= nil then
            return FGUIPanel.Wrap(cls, view)
        end
    end
    return view
end

function FGUIPanel:Create(pkg, res)
    -- UIFrame:Open 已异步加载包；此处仅校验（Folder 模式可同步补加载）
    if UIPackage.GetByName(pkg) == nil then
        if LaunchMode ~= "AssetBundle" then
            UIBundleMgr.LoadBundlePackage({ pkg })
        end
        if UIPackage.GetByName(pkg) == nil then
            error(string.format("[FGUIPanel] package not loaded: %s", tostring(pkg)))
        end
    end
    local view = UIPackage.CreateObject(pkg, res)
    if view == nil then
        error(string.format("[FGUIPanel] CreateObject failed: %s/%s", tostring(pkg), tostring(res)))
    end

    local inst = setmetatable({ view = view }, self)
    CS.FGUILuaBinder.Bind(view, inst)
    return inst
end

function FGUIPanel:OnInit(view)
end

function FGUIPanel:OnShown(view)
end

function FGUIPanel:OnHide(view)
end

function FGUIPanel:OnDispose(view)
end

function FGUIPanel:Dispose()
    CS.FGUILuaBinder.Dispose(self.view, self)
    self.view = nil
end
