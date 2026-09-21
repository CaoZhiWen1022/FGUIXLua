-- 此文件由配置工具自动生成，请勿手动修改
-- 表数量 2 / 生成时间 2026-09-21T03:58:24.891Z

---@class ListCfg 列表.xlsx - 列表 / 列表表
---@field id string @ID
---@field test1 string @字段1
---@field test2 string @字段2
---@field test3 string @字段3

---@class ConstCfg 常数.xlsx - 常数表 / 常数表
---@field test1 string @常数1
---@field test2 string @常数2
---@field test3 string @常数3

GameConfigMgr = {}

local DefaultBinName = "gamedata.bin"
local DataBundleKey = "GameConfig"
local TextAssetType = typeof(CS.UnityEngine.TextAsset)

local _loaded = {}
local _luaCache = {}
local _parsed = false

local EnumToTableKey = {
    ["_列表"] = "ListCfg",
    ["_常数表"] = "ConstCfg",
}

local function IsABMode()
    return LaunchMode == "AssetBundle"
end

local function NormalizeBinFileName(assetName)
    if assetName == nil or assetName == "" then
        return DefaultBinName
    end
    local name = tostring(assetName)
    if not string.find(string.lower(name), "%.bin$") then
        name = name .. ".bin"
    end
    return name
end

local function AssetKey(assetName)
    return string.lower(NormalizeBinFileName(assetName))
end

local function GetCsTypeName(value)
    local ok, typ = pcall(function()
        return value:GetType()
    end)
    if not ok or typ == nil then
        return ""
    end
    return typ.Name or ""
end

local function IsJToken(value)
    local typeName = GetCsTypeName(value)
    return string.find(typeName, "JObject", 1, true)
        or string.find(typeName, "JArray", 1, true)
        or string.find(typeName, "JToken", 1, true)
        or string.find(typeName, "JProperty", 1, true)
        or string.find(typeName, "JValue", 1, true)
end

local function IsList(value)
    if value == nil or type(value) == "table" or IsJToken(value) then
        return false
    end
    local typeName = GetCsTypeName(value)
    return string.find(typeName, "List", 1, true) ~= nil
end

local function IsDict(value)
    if value == nil or type(value) == "table" or IsJToken(value) then
        return false
    end
    local typeName = GetCsTypeName(value)
    return string.find(typeName, "Dictionary", 1, true) ~= nil
end

local ToLuaValue

local function EnumerateToLuaArray(collection)
    local list = {}
    local enumerator = collection:GetEnumerator()
    while enumerator:MoveNext() do
        list[#list + 1] = ToLuaValue(enumerator.Current)
    end
    return list
end

ToLuaValue = function(value)
    if value == nil then
        return nil
    end
    local valueType = type(value)
    if valueType == "table" or valueType == "string" or valueType == "number" or valueType == "boolean" then
        return value
    end

    if IsJToken(value) then
        print("[GameConfigMgr] 收到 JToken，请走 XLuaGameConfigMgr.GetConfigByTableName")
        return nil
    end

    if IsList(value) then
        local ok, list = pcall(function()
            return EnumerateToLuaArray(value)
        end)
        if ok then
            return list
        end
    end

    if IsDict(value) then
        local dict = {}
        local ok, _ = pcall(function()
            local enumerator = value:GetEnumerator()
            while enumerator:MoveNext() do
                local pair = enumerator.Current
                dict[pair.Key] = ToLuaValue(pair.Value)
            end
        end)
        if ok then
            return dict
        end
    end

    local okTyp, typ = pcall(function()
        return value:GetType()
    end)
    if okTyp and typ ~= nil then
        local csTypeName = typ.Name or ""
        if string.find(csTypeName, "Int", 1, true)
            or string.find(csTypeName, "Double", 1, true)
            or string.find(csTypeName, "Single", 1, true)
            or string.find(csTypeName, "Decimal", 1, true)
            or string.find(csTypeName, "Float", 1, true) then
            return tonumber(tostring(value))
        end
    end

    return value
end

local function ResolveTableKey(cfgName)
    if cfgName == nil then
        return nil
    end
    if type(cfgName) == "string" then
        return EnumToTableKey[cfgName] or cfgName
    end
    local ok, name = pcall(function()
        return CS.System.Enum.GetName(typeof(CS.GameConfig.GameConfigName), cfgName)
    end)
    if ok and name then
        return EnumToTableKey[name] or name
    end
    local enumName = tostring(cfgName)
    return EnumToTableKey[enumName] or enumName
end

local function HasStringKey(value)
    for key, _ in pairs(value) do
        if type(key) == "string" then
            return true
        end
    end
    return false
end

local function IsLuaListTable(value)
    if type(value) ~= "table" then
        return false
    end
    if #value > 0 then
        return true
    end
    return not HasStringKey(value)
end

local function EnsureLoaded()
    if _parsed then
        return true
    end
    print("[GameConfigMgr] 配置尚未加载，请先调用 GameConfigMgr.Load")
    return false
end

local function GetXLuaMgr()
    return CS.GameConfig.XLuaGameConfigMgr
end

local function GetCachedTable(tableKey)
    if tableKey == nil or tableKey == "" then
        return nil
    end
    if _luaCache[tableKey] ~= nil then
        return _luaCache[tableKey]
    end
    local ok, raw = pcall(function()
        return GetXLuaMgr().GetConfigByTableName(tableKey)
    end)
    if not ok then
        print(string.format("[GameConfigMgr] GetConfigByTableName 失败: %s", tostring(raw)))
        return nil
    end
    local converted = ToLuaValue(raw)
    _luaCache[tableKey] = converted
    return converted
end

local function FinishParse(assetName, ok)
    if ok then
        _loaded[AssetKey(assetName)] = true
        _parsed = true
        _luaCache = {}
        print(string.format("[GameConfigMgr] loaded %s", tostring(NormalizeBinFileName(assetName))))
    end
end

local function ReadFolderBytes(fileName)
    local dataPath = CS.UnityEngine.Application.dataPath
    local fullPath = dataPath .. "/RawResources/GameConfig/" .. fileName
    local ok, bytes = pcall(function()
        return CS.System.IO.File.ReadAllBytes(fullPath)
    end)
    if not ok or bytes == nil then
        print(string.format("[GameConfigMgr] Folder 读取失败: %s", tostring(fullPath)))
        return nil
    end
    return bytes
end

local function LoadAssetFromBundle(bundle, assetName)
    local binName = NormalizeBinFileName(assetName)
    local noExt = string.gsub(binName, "%.[Bb][Ii][Nn]$", "")
    local names = {
        noExt,
        noExt .. ".bytes",
        binName
    }
    for i = 1, #names do
        local ta = bundle:LoadAsset(names[i], TextAssetType)
        if ta ~= nil then
            return ta
        end
    end
    return nil
end

local function ParseBytes(bytes)
    if bytes == nil then
        return false
    end
    local ok, err = pcall(function()
        GetXLuaMgr().ParseBinBytes(bytes)
    end)
    if not ok then
        print(string.format("[GameConfigMgr] ParseBinBytes 失败: %s", tostring(err)))
        return false
    end
    return true
end

local function LoadFolderBin(assetName, callback)
    local fileName = NormalizeBinFileName(assetName)
    local bytes = ReadFolderBytes(fileName)
    local ok = ParseBytes(bytes)
    FinishParse(fileName, ok)
    if callback then
        callback(ok)
    end
end

local function LoadABBin(assetName, callback)
    CS.AssetBundleLoader.LoadBundleByKeyAsync(DataBundleKey, function(bundle)
        if bundle == nil then
            print(string.format("[GameConfigMgr] AB 加载失败 key=%s", DataBundleKey))
            if callback then
                callback(false)
            end
            return
        end
        local ta = LoadAssetFromBundle(bundle, assetName)
        if ta == nil then
            print(string.format("[GameConfigMgr] AB 内找不到资源: %s", tostring(assetName)))
            if callback then
                callback(false)
            end
            return
        end
        local ok = ParseBytes(ta.bytes)
        FinishParse(assetName, ok)
        if callback then
            callback(ok)
        end
    end)
end

--- 是否已 Parse 过至少一份 bin
--- @return boolean
function GameConfigMgr.HasLoaded()
    return _parsed
end

--- 加载并 Parse 默认包 gamedata.bin
--- Folder：读 RawResources/GameConfig/*.bin 字节
--- AB：LoadBundleByKeyAsync("GameConfig") 后取 TextAsset.bytes
--- @param callback fun(ok: boolean)|nil
function GameConfigMgr.Load(callback)
    GameConfigMgr.LoadBin(DefaultBinName, callback)
end

--- 追加一份 bin（自定义分表）。assetName 不含路径，如 "ConstCfg.bin" / "ConstCfg"
--- @param assetName string
--- @param callback fun(ok: boolean)|nil
function GameConfigMgr.LoadBin(assetName, callback)
    local fileName = NormalizeBinFileName(assetName)
    local key = AssetKey(fileName)
    if _loaded[key] then
        print(string.format("[GameConfigMgr] skip already loaded %s", fileName))
        if callback then
            callback(true)
        end
        return
    end

    if IsABMode() then
        LoadABBin(fileName, callback)
        return
    end
    LoadFolderBin(fileName, callback)
end

--- 获取整张表。列表表返回数组（1 起下标），常数表返回对象
--- @param cfgName any CS.GameConfig.GameConfigName 或导出表名字符串
--- @return table|nil
function GameConfigMgr.GetConfig(cfgName)
    if not EnsureLoaded() then
        return nil
    end
    local tableKey = ResolveTableKey(cfgName)
    if tableKey == nil then
        return nil
    end
    return GetCachedTable(tableKey)
end

--- 按主键取一行，id 一律按 string 比较
--- @param cfgName any CS.GameConfig.GameConfigName 或导出表名字符串
--- @param id string
--- @return table|nil
function GameConfigMgr.GetConfigById(cfgName, id)
    if not EnsureLoaded() then
        return nil
    end
    local tableKey = ResolveTableKey(cfgName)
    local list = GetCachedTable(tableKey)
    if not IsLuaListTable(list) then
        print(string.format("[GameConfigMgr] 配置表%s不是列表表或不存在", tostring(tableKey)))
        return nil
    end
    local idText = tostring(id)
    for i = 1, #list do
        local row = list[i]
        if row ~= nil and tostring(row.id) == idText then
            return row
        end
    end
    print(string.format("[GameConfigMgr] 配置表%s不存在id:%s", tostring(tableKey), idText))
    return nil
end

--- 按字段等值筛选列表表，只比 template 给出的字段
--- @param cfgName any CS.GameConfig.GameConfigName 或导出表名字符串
--- @param template table
--- @return table
function GameConfigMgr.GetConfigByTemplate(cfgName, template)
    if not EnsureLoaded() then
        return {}
    end
    local tableKey = ResolveTableKey(cfgName)
    local list = GetCachedTable(tableKey)
    if not IsLuaListTable(list) then
        print(string.format("[GameConfigMgr] 配置表%s不是列表表或不存在", tostring(tableKey)))
        return {}
    end
    if #list == 0 then
        return {}
    end
    if type(template) ~= "table" then
        return list
    end

    local result = {}
    for i = 1, #list do
        local row = list[i]
        local matched = true
        for field, expect in pairs(template) do
            if expect ~= nil and row[field] ~= expect then
                matched = false
                break
            end
        end
        if matched then
            result[#result + 1] = row
        end
    end
    return result
end
