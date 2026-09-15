UIBundleMgr = {}

local m_loadedPackage = {}
local refCountMap = {}

local function contains(list, value)
    for i = 1, #list do
        if list[i] == value then
            return true
        end
    end
    return false
end

local function IsABMode()
    return LaunchMode == "AssetBundle"
end

local function LoadPackageFolder(packageName)
    if contains(m_loadedPackage, packageName) then
        return true
    end
    if UIPackage.GetByName(packageName) ~= nil then
        table.insert(m_loadedPackage, packageName)
        return true
    end

    local ok, err = pcall(function()
        local path = UIFrameConfig.PACKAGE_PATH_PREFIX .. packageName .. "/" .. packageName
        UIPackage.AddPackage(path)
    end)
    if not ok then
        print(string.format("[UIBundleMgr] load package failed %s: %s", tostring(packageName), tostring(err)))
        return false
    end
    table.insert(m_loadedPackage, packageName)
    return true
end

local function LoadPackageAB(packageName, callback)
    if contains(m_loadedPackage, packageName) then
        if callback then callback(true) end
        return
    end
    if UIPackage.GetByName(packageName) ~= nil then
        table.insert(m_loadedPackage, packageName)
        if callback then callback(true) end
        return
    end

    ResourcesMgr.LoadUIPackage(packageName, function(bundle)
        if bundle == nil then
            if callback then callback(false) end
            return
        end
        local ok, err = pcall(function()
            UIPackage.AddPackage(bundle)
        end)
        if not ok then
            print(string.format("[UIBundleMgr] AddPackage failed %s: %s", tostring(packageName), tostring(err)))
            ResourcesMgr.UnloadUIPackage(packageName)
            if callback then callback(false) end
            return
        end
        table.insert(m_loadedPackage, packageName)
        if callback then callback(true) end
    end)
end

--- 加载 UI 包。Folder 同步；AB 异步。
--- callback(success) 可选；无 callback 时 Folder 返回 bool，AB 模式请务必传 callback。
function UIBundleMgr.LoadBundlePackage(packageNames, callback)
    if packageNames == nil then
        if callback then callback(true) end
        return true
    end

    if not IsABMode() then
        local allSuccess = true
        for i = 1, #packageNames do
            if not LoadPackageFolder(packageNames[i]) then
                allSuccess = false
            end
        end
        if callback then callback(allSuccess) end
        return allSuccess
    end

    local index = 1
    local function loadNext()
        if index > #packageNames then
            if callback then callback(true) end
            return
        end
        local name = packageNames[index]
        index = index + 1
        LoadPackageAB(name, function(ok)
            if not ok then
                if callback then callback(false) end
                return
            end
            loadNext()
        end)
    end
    loadNext()
    return true
end

function UIBundleMgr.AddRefCount(packageNames)
    if packageNames == nil then
        return
    end
    for i = 1, #packageNames do
        local name = packageNames[i]
        refCountMap[name] = (refCountMap[name] or 0) + 1
    end
end

local function UnBundlePackage(packageName)
    for i = #m_loadedPackage, 1, -1 do
        if m_loadedPackage[i] == packageName then
            UIPackage.RemovePackage(packageName)
            table.remove(m_loadedPackage, i)
            if IsABMode() then
                ResourcesMgr.UnloadUIPackage(packageName)
            end
            break
        end
    end
end

local function CheckAllowUnloadPackage()
    local max = UIFrameConfig.MAX_PKGS + #UIFrameConfig.PERMANENT_PKGS
    local keysToRemove = {}
    for name, count in pairs(refCountMap) do
        if #m_loadedPackage <= max then
            break
        end
        if count == 0 and not contains(UIFrameConfig.PERMANENT_PKGS, name) then
            UnBundlePackage(name)
            table.insert(keysToRemove, name)
        end
    end
    for i = 1, #keysToRemove do
        refCountMap[keysToRemove[i]] = nil
    end
end

function UIBundleMgr.RemoveRefCount(packageNames)
    if packageNames == nil then
        return
    end
    for i = 1, #packageNames do
        local name = packageNames[i]
        if refCountMap[name] ~= nil then
            refCountMap[name] = refCountMap[name] - 1
        end
    end
    CheckAllowUnloadPackage()
end
