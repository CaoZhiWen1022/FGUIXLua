-- 资源门面：按 LaunchMode 转发（Folder / AssetBundle）
ResourcesMgr = {}

local FontType = typeof(CS.UnityEngine.Font)
local GameObjectType = typeof(CS.UnityEngine.GameObject)
local D3PrefabFolderPrefix = "Assets/RawResources/D3Prefabs/"

local function IsABMode()
    return LaunchMode == "AssetBundle"
end

function ResourcesMgr.IsABMode()
    return IsABMode()
end

--- 异步加载 FairyGUI UI 包对应的 AssetBundle（仅 AB 模式）
--- callback(bundle)
function ResourcesMgr.LoadUIPackage(packageName, callback)
    if not IsABMode() then
        if callback then callback(nil) end
        return
    end
    ResourcesAB.LoadUIPackage(packageName, callback)
end

function ResourcesMgr.UnloadUIPackage(packageName)
    if not IsABMode() then
        return
    end
    ResourcesAB.UnloadUIPackage(packageName)
end

local function NormalizePrefabAssetName(assetName)
    local key = CS.AssetBundlePath.NormalizeD3PrefabKey(assetName)
    if key == nil or key == "" then
        return ""
    end
    return key .. ".prefab"
end

local function LoadPrefabFromBundle(bundle, assetName)
    if bundle == nil then
        return nil
    end

    local prefab = bundle:LoadAsset(assetName, GameObjectType)
    if prefab ~= nil then
        return prefab
    end

    local fileName = assetName
    local slash = string.match(assetName, ".*/([^/]+)$")
    if slash ~= nil then
        fileName = slash
    end
    prefab = bundle:LoadAsset(fileName, GameObjectType)
    if prefab ~= nil then
        return prefab
    end

    local nameNoExt = string.gsub(fileName, "%.[Pp]refab$", "")
    return bundle:LoadAsset(nameNoExt, GameObjectType)
end

--- 异步加载 D3Prefabs 下的预制体
--- assetName：相对 D3Prefabs 的路径，可带或不带 .prefab，如 Deer、Animals/Deer.prefab
--- callback(prefab)  返回预制体资源（未 Instantiate）
function ResourcesMgr.LoadPrefab(assetName, callback)
    if assetName == nil or assetName == "" then
        if callback then callback(nil) end
        return
    end

    local fileName = NormalizePrefabAssetName(assetName)

    if not IsABMode() then
        local path = D3PrefabFolderPrefix .. fileName
        local ok, prefab = pcall(function()
            return CS.UnityEditor.AssetDatabase.LoadAssetAtPath(path, GameObjectType)
        end)
        if not ok or prefab == nil then
            print(string.format("[ResourcesMgr] Folder prefab load failed: %s", tostring(path)))
            if callback then callback(nil) end
            return
        end
        if callback then callback(prefab) end
        return
    end

    ResourcesAB.LoadD3Prefabs(function(bundle)
        if bundle == nil then
            if callback then callback(nil) end
            return
        end

        local prefab = LoadPrefabFromBundle(bundle, fileName)
        if prefab == nil then
            print(string.format("[ResourcesMgr] prefab asset missing in AB: %s", tostring(fileName)))
        end
        if callback then callback(prefab) end
    end)
end

function ResourcesMgr.UnloadD3Prefabs()
    if not IsABMode() then
        return
    end
    ResourcesAB.UnloadD3Prefabs()
end

function ResourcesMgr.UnloadPrefab(_assetName)
    ResourcesMgr.UnloadD3Prefabs()
end

local function ResolveFontAbRelative(abKey)
    abKey = abKey or "Fonts"
    return CS.AssetBundlePath.GetRelativePathByKey(abKey)
end

--- assetName 为包内资源名（含后缀，如 AlibabaPuHuiTi.ttf）；注册名用 Font.name
local function RegisterUnityFont(unityFont, isDefault)
    if unityFont == nil then
        return false
    end

    local fontName = unityFont.name
    local df = CS.FairyGUI.DynamicFont(fontName, unityFont)
    CS.FairyGUI.FontManager.RegisterFont(df)
    if isDefault then
        UIConfig.defaultFont = fontName
    end
    print(string.format("[ResourcesMgr] font registered name=%s default=%s",
        tostring(fontName), tostring(isDefault)))
    return true
end

local function RegisterFontFromFolder(assetName, isDefault)
    local path = "Assets/RawResources/Fonts/" .. assetName
    local ok, font = pcall(function()
        return CS.UnityEditor.AssetDatabase.LoadAssetAtPath(path, FontType)
    end)
    if not ok or font == nil then
        print(string.format("[ResourcesMgr] Folder font load failed: %s", tostring(path)))
        return false
    end
    return RegisterUnityFont(font, isDefault)
end

local function RegisterFontFromAB(abKey, assetName, isDefault, callback)
    local relative = ResolveFontAbRelative(abKey)
    CS.AssetBundleLoader.LoadBundleByRelativeAsync(relative, function(bundle)
        if bundle == nil then
            print(string.format("[ResourcesMgr] font AB load failed ab=%s relative=%s",
                tostring(abKey), tostring(relative)))
            if callback then callback(false) end
            return
        end

        local font = bundle:LoadAsset(assetName, FontType)
        if font == nil then
            print(string.format("[ResourcesMgr] font asset missing in AB: %s", tostring(assetName)))
            if callback then callback(false) end
            return
        end

        if callback then callback(RegisterUnityFont(font, isDefault)) end
    end)
end

--- 按配置加载字体并设置 FairyGUI defaultFont
--- fontConfigs: { { ab="Fonts", name="AlibabaPuHuiTi.ttf", isDefault=true }, ... }
--- name 为 AB 内固定资源名（含后缀）
--- callback(success)
function ResourcesMgr.InitFonts(fontConfigs, callback)
    if fontConfigs == nil or #fontConfigs == 0 then
        if callback then callback(true) end
        return
    end

    local index = 1
    local function loadNext()
        if index > #fontConfigs then
            if callback then callback(true) end
            return
        end

        local cfg = fontConfigs[index]
        index = index + 1
        if cfg == nil or cfg.name == nil or cfg.name == "" then
            loadNext()
            return
        end

        local ab = cfg.ab or "Fonts"
        local isDefault = cfg.isDefault == true

        if not IsABMode() then
            if not RegisterFontFromFolder(cfg.name, isDefault) then
                if callback then callback(false) end
                return
            end
            loadNext()
            return
        end

        RegisterFontFromAB(ab, cfg.name, isDefault, function(ok)
            if not ok then
                if callback then callback(false) end
                return
            end
            loadNext()
        end)
    end
    loadNext()
end
