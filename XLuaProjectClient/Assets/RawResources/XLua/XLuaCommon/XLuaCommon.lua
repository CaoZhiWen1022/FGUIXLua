-- 通用工具：字符串、随机数、数值、表
XLuaCommon = {}

math.randomseed(os.time())

-- ---------- 字符串 ----------

--- 判断字符串是否为 nil 或空串
--- @param str string|nil
--- @return boolean
function XLuaCommon.IsNilOrEmpty(str)
    return str == nil or str == ""
end

--- 按分隔符拆分字符串。sep 为空则按单字符拆
--- @param str string|nil
--- @param sep string|nil 分隔符，支持多字符；空/nil 表示逐字符
--- @return table 从 1 起的字符串数组
function XLuaCommon.Split(str, sep)
    local result = {}
    if str == nil then
        return result
    end
    str = tostring(str)
    if sep == nil or sep == "" then
        for i = 1, #str do
            result[i] = string.sub(str, i, i)
        end
        return result
    end

    sep = tostring(sep)
    local start = 1
    while true do
        local from, to = string.find(str, sep, start, true)
        if from == nil then
            result[#result + 1] = string.sub(str, start)
            break
        end
        result[#result + 1] = string.sub(str, start, from - 1)
        start = to + 1
    end
    return result
end

--- 用分隔符拼接数组
--- @param list table|nil 从 1 起的数组
--- @param sep string|nil 分隔符，默认空串
--- @return string
function XLuaCommon.Join(list, sep)
    if list == nil then
        return ""
    end
    return table.concat(list, sep or "")
end

--- 去掉首尾空白（空格、制表、换行）
--- @param str string|nil
--- @return string nil 时返回空串
function XLuaCommon.Trim(str)
    if str == nil then
        return ""
    end
    return (string.gsub(tostring(str), "^%s*(.-)%s*$", "%1"))
end

--- 是否以指定前缀开头
--- @param str string|nil
--- @param prefix string|nil
--- @return boolean
function XLuaCommon.StartsWith(str, prefix)
    if str == nil or prefix == nil then
        return false
    end
    str = tostring(str)
    prefix = tostring(prefix)
    return string.sub(str, 1, #prefix) == prefix
end

--- 是否以指定后缀结尾
--- @param str string|nil
--- @param suffix string|nil
--- @return boolean
function XLuaCommon.EndsWith(str, suffix)
    if str == nil or suffix == nil then
        return false
    end
    str = tostring(str)
    suffix = tostring(suffix)
    if #suffix == 0 then
        return true
    end
    return string.sub(str, -#suffix) == suffix
end

-- ---------- 随机 ----------

--- 设置随机种子。不传则用当前时间
--- @param seed number|nil
function XLuaCommon.Seed(seed)
    math.randomseed(seed or os.time())
end

--- 生成 [0, 1) 的浮点随机数
--- @return number
function XLuaCommon.Random()
    return math.random()
end

--- 生成 [min, max] 的浮点随机数。min > max 时自动对调
--- @param min number|nil 默认 0
--- @param max number|nil 默认 1
--- @return number
function XLuaCommon.RandomFloat(min, max)
    min = min or 0
    max = max or 1
    if min > max then
        min, max = max, min
    end
    return min + (max - min) * math.random()
end

--- 生成 [min, max] 闭区间随机整数。min > max 时自动对调
--- @param min number|nil 默认 0
--- @param max number|nil 默认 0
--- @return number
function XLuaCommon.RandomInt(min, max)
    min = math.floor(min or 0)
    max = math.floor(max or 0)
    if min > max then
        min, max = max, min
    end
    return math.random(min, max)
end

--- 按概率返回 true。probability 为 0~1，默认 0.5
--- @param probability number|nil
--- @return boolean
function XLuaCommon.RandomBool(probability)
    probability = probability or 0.5
    return math.random() < probability
end

--- 从数组中随机取一项。空表或 nil 返回 nil
--- @param list table|nil
--- @return any|nil
function XLuaCommon.RandomPick(list)
    if list == nil then
        return nil
    end
    local n = #list
    if n == 0 then
        return nil
    end
    return list[math.random(1, n)]
end

--- 原地乱序数组并返回同一张表
--- @param list table|nil
--- @return table|nil
function XLuaCommon.Shuffle(list)
    if list == nil then
        return list
    end
    for i = #list, 2, -1 do
        local j = math.random(1, i)
        list[i], list[j] = list[j], list[i]
    end
    return list
end

-- ---------- 数值 ----------

--- 将数值限制在 [min, max] 内
--- @param value number
--- @param min number
--- @param max number
--- @return number
function XLuaCommon.Clamp(value, min, max)
    if value < min then
        return min
    end
    if value > max then
        return max
    end
    return value
end

--- 线性插值。t 会被限制在 [0, 1]
--- @param from number
--- @param to number
--- @param t number|nil 默认 0
--- @return number
function XLuaCommon.Lerp(from, to, t)
    t = XLuaCommon.Clamp(t or 0, 0, 1)
    return from + (to - from) * t
end

--- 四舍五入到整数
--- @param value number
--- @return number
function XLuaCommon.Round(value)
    if value >= 0 then
        return math.floor(value + 0.5)
    end
    return math.ceil(value - 0.5)
end

-- ---------- 表 ----------

--- 统计表中的键值对数量（含哈希部分）
--- @param tb table|nil
--- @return number
function XLuaCommon.TableCount(tb)
    if tb == nil then
        return 0
    end
    local n = 0
    for _ in pairs(tb) do
        n = n + 1
    end
    return n
end

--- 判断表中是否包含指定值（遍历 pairs）
--- @param tb table|nil
--- @param value any
--- @return boolean
function XLuaCommon.TableContains(tb, value)
    if tb == nil then
        return false
    end
    for _, v in pairs(tb) do
        if v == value then
            return true
        end
    end
    return false
end

--- 在数组中查找值，返回从 1 起的下标；未找到返回 -1
--- @param list table|nil
--- @param value any
--- @return number
function XLuaCommon.IndexOf(list, value)
    if list == nil then
        return -1
    end
    for i = 1, #list do
        if list[i] == value then
            return i
        end
    end
    return -1
end

local function EncodeString(str)
    str = tostring(str)
    str = string.gsub(str, "\\", "\\\\")
    str = string.gsub(str, "\"", "\\\"")
    str = string.gsub(str, "\n", "\\n")
    str = string.gsub(str, "\r", "\\r")
    str = string.gsub(str, "\t", "\\t")
    return "\"" .. str .. "\""
end

local function IsArrayPart(tb)
    local n = #tb
    if n == 0 then
        return false
    end
    local count = 0
    for k, _ in pairs(tb) do
        if type(k) ~= "number" or k < 1 or k > n or k ~= math.floor(k) then
            return false
        end
        count = count + 1
    end
    return count == n
end

local function ValueToString(value, seen)
    if value == nil then
        return "nil"
    end
    local valueType = type(value)
    if valueType == "string" then
        return EncodeString(value)
    end
    if valueType == "number" or valueType == "boolean" then
        return tostring(value)
    end
    if valueType ~= "table" then
        return tostring(value)
    end
    if seen[value] then
        return "{...}"
    end
    seen[value] = true

    local parts = {}
    if IsArrayPart(value) then
        for i = 1, #value do
            parts[#parts + 1] = ValueToString(value[i], seen)
        end
        seen[value] = nil
        return "{" .. table.concat(parts, ", ") .. "}"
    end

    local keys = {}
    for k, _ in pairs(value) do
        keys[#keys + 1] = k
    end
    table.sort(keys, function(a, b)
        return tostring(a) < tostring(b)
    end)
    for i = 1, #keys do
        local key = keys[i]
        local keyText
        if type(key) == "string" and string.match(key, "^[_%a][_%w]*$") then
            keyText = key
        else
            keyText = "[" .. ValueToString(key, seen) .. "]"
        end
        parts[#parts + 1] = keyText .. " = " .. ValueToString(value[key], seen)
    end
    seen[value] = nil
    return "{" .. table.concat(parts, ", ") .. "}"
end

--- 将 table 转成可读字符串，便于 print。支持数组、哈希表、嵌套；环引用输出 {...}
--- 非 table 按类型直接转：nil / 数字 / 布尔 / 字符串（带引号）
--- @param tb any
--- @return string
function XLuaCommon.TableToString(tb)
    return ValueToString(tb, {})
end
