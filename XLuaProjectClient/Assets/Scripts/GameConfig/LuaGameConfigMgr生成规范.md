# Lua GameConfigMgr 生成规范

面向配置工具：在**另一工程**生成客户端可用的 `GameConfigMgr.lua`。  
C# 的 `XLuaGameConfigMgr` 只负责解 bin、按表名/id 取原始数据；**面向业务的查询与加载写在 Lua**。

---

## 1. 职责划分

| 层 | 文件 | 职责 | 不要做 |
|---|---|---|---|
| C# 数据 | `GameConfigMgr.cs` | 解 gzip bin、存 JObject、泛型查表 | 被 Lua 直接调泛型 |
| C# 桥 | `XLuaGameConfigMgr.cs` | 非泛型：`ParseBin` / `ParseBinBytes` / `GetConfig` / `GetConfigById` / `GetConfigByTableName*` | 不负责加载 AB、不假装返回 Lua table |
| Lua 门面 | **生成** `GameConfigMgr.lua` | 加载 bin、转 Lua table、按表查询、模板筛选 | 不直接 `GetConfig<T>`，不把 Lua table 当 `Dictionary<string,object>` 传给 C# |

Lua 只允许调 C# 的这些（或等价非泛型）：

- `ParseBin(TextAsset)`
- `ParseBinBytes(byte[])`
- `GetConfig(GameConfigName)` / `GetConfigByTableName(string)`
- `GetConfigById(GameConfigName, id)` / `GetConfigByTableNameAndId(string, id)`

`GetConfigByTemplate` **不要从 Lua 调 C#**，在 Lua 里对已转好的 table 做字段等值筛选。

---

## 2. 输出位置与命名

| 项 | 约定 |
|---|---|
| 路径 | `Assets/RawResources/XLua/GameConfig/GameConfigMgr.lua` |
| `require` | `require("GameConfig.GameConfigMgr")`（点号 = `RawResources/XLua` 下目录，**不要**写 `.lua` 后缀） |
| 全局名 | `GameConfigMgr`（PascalCase，与 `ResourcesMgr`、`TimerMgr` 一致） |
| 编码 | UTF-8 无 BOM |
| 换行 | LF |
| 扩展名 | 必须 `.lua`（不要 `.Lua`，Android / WebGL 区分大小写） |

启动接入（生成器可只出文件，由客户端手工 require 一次）：

```lua
-- GameFrame/launch.lua 或 XLuaCommonLaunch 之后
require("GameConfig.GameConfigMgr")
```

---

## 3. 文件头（生成标记）

```lua
-- 此文件由配置工具自动生成，请勿手动修改
-- 表数量 / 生成时间 可写在下一行，便于核对
```

不要生成手写业务逻辑。加载默认 bin 名、表名映射可以生成；打开 UI、进场景不要写进这份文件。

---

## 4. 代码风格

- 模块：`GameConfigMgr = {}`，全是 **`.` 函数**，不要 `:` 和 `self`（与 `ResourcesMgr` 一致）
- 局部：`local function IsABMode()`，不要污染全局
- 日志：`print(string.format("[GameConfigMgr] ...", ...))`
- 模式：`LaunchMode == "AssetBundle"`（C# 在 `require` 前注入，缺省当 `"Folder"`）
- 缩进：4 空格
- 空值：返回 `nil`，不要返回空 C# 对象
- 注释：EmmyLua

```lua
--- 按主键取一行
--- @param cfgName any CS.GameConfig.GameConfigName 或导出表名字符串
--- @param id string
--- @return table|nil
function GameConfigMgr.GetConfigById(cfgName, id)
end
```

---

## 5. 必须先做：C# 容器 → Lua table

`XLuaGameConfigMgr` 返回的是 `Dictionary<string,object>` / `List<object>`，**不是** Lua table。生成器必须包一层转换：

- `Dictionary` → `{ [key] = value }`，键保持导出字段名（`id`、`test1`）
- `List` → **1 起下标**数组（C# 是 0 起，Lua 必须 +1）
- 递归处理嵌套 object / array
- `long` / `double` / `bool` / `string` / `nil` 原样留下
- 识别 C# 容器：`pairs` + `#` 不可靠，用 `GetType()` 或约定「有 `get_Item` / `Count` 的当 List，有字符串键的当 Dictionary」

转换后业务侧必须能写：

```lua
local row = GameConfigMgr.GetConfigById(name, "1001")
print(row.id)          -- 或 row["id"]
local list = GameConfigMgr.GetConfig(name)
print(list[1].id)      -- 第一项，不是 list[0]
```

---

## 6. 建议生成的 API

### 6.1 加载（异步，callback 一律 `function(ok)`）

```lua
--- 加载并 Parse 默认包。Folder：读 RawResources/GameConfig/*.bin
--- AB：LoadBundleByKeyAsync("GameConfig") 后 LoadAsset TextAsset
--- @param callback fun(ok: boolean)|nil
function GameConfigMgr.Load(callback)
end

--- 追加一份 bin（自定义分表）。assetName 不含路径，如 "ConstCfg.bin" / "ConstCfg"
--- @param assetName string
--- @param callback fun(ok: boolean)|nil
function GameConfigMgr.LoadBin(assetName, callback)
end
```

加载约定：

- 默认总表：`gamedata.bin`（与现有目录一致）
- 分表：`{TableKey}.bin`，如 `ConstCfg.bin`
- Folder 路径：`Assets/RawResources/GameConfig/` + 文件名
- `.bin` 在 Unity 里**不是** TextAsset。Folder 模式应读字节后走 `ParseBinBytes`，**不要**依赖 `AssetDatabase.LoadAssetAtPath<TextAsset>`
- 两种模式都优先 **`ParseBinBytes`**
- AB index 逻辑名：文件夹名 `GameConfig`（不要写死 `gameconfig.ab` 或 hash）
  - `CS.AssetBundleLoader.LoadBundleByKeyAsync("GameConfig", function(bundle) ... end)`
- AB 内资源名：构建会把 `.bin` 打成 `.bytes`，`LoadAsset` 对 `gamedata` / `gamedata.bytes` / `ConstCfg` 都试一遍
- 可重复 `ParseBin`，C# 会合并；Lua 侧用 `_loaded` 防重复加载同一份

### 6.2 查询（同步，须已 Load）

参数 `cfgName` 同时支持：

1. `CS.GameConfig.GameConfigName._列表`（与 C# 枚举一致）
2. 导出表名字符串 `"ListCfg"`（**推荐主推**，避免中文枚举在 Lua 里难写）

| 方法 | 行为 |
|---|---|
| `GetConfig(cfgName)` | 整表。列表 → 数组；常数 → 单对象 |
| `GetConfigById(cfgName, id)` | 列表一行，`id` 一律当 **string** 比 |
| `GetConfigByTemplate(cfgName, template)` | **Lua 内**筛选，`template` 为 `{ field = value }`，只比给出的字段，`==` |
| `HasLoaded()` | 是否已 Parse 过 |

未加载要 `print` 错误并返回 `nil` / `{}`，不要让 C# 抛 `Call ParseBin() first` 直接打崩 Lua。

### 6.3 按表生成的薄封装（推荐）

每张表再生成一组函数，业务不用记枚举：

```lua
-- 列表表 ListCfg（excel: 列表.xlsx）
function GameConfigMgr.GetListCfg()
end
function GameConfigMgr.GetListCfgById(id)
end
function GameConfigMgr.GetListCfgByTemplate(template)
end

-- 常数表 ConstCfg：只有整表
function GameConfigMgr.GetConstCfg()
end
```

- 命名：`Get` + **导出类名**（`ListCfg` / `ConstCfg`），与 `GameConfigInterfaces.cs` 一致
- 常数表不要生成 `ById`
- 映射与 C# 相同：

```
GameConfigName._列表   → "ListCfg"
GameConfigName._常数表 → "ConstCfg"
```

---

## 7. 表元数据（生成器输入）

每张表至少提供：

| 字段 | 例 | 用途 |
|---|---|---|
| excel / 中文名 | `列表.xlsx` / `列表` | 注释 |
| enumName | `_列表` | 调 C# 枚举时用 |
| tableKey | `ListCfg` | `GetConfigByTableName`、bin 文件名 |
| kind | `list` / `const` | 是否生成 ById / Template |
| idField | `id` | 默认主键，string |
| fields[] | `{ name, type, comment }` | EmmyLua `@class` |

建议在同文件或旁路生成类型注释：

```lua
---@class ListCfg
---@field id string
---@field test1 string
```

---

## 8. Folder / AB 加载细则

```lua
-- Folder
-- 读文件字节 → CS.GameConfig.XLuaGameConfigMgr.ParseBinBytes(bytes)

-- AB
CS.AssetBundleLoader.LoadBundleByKeyAsync("GameConfig", function(bundle)
    local ta = bundle:LoadAsset(assetName, typeof(CS.UnityEngine.TextAsset))
    CS.GameConfig.XLuaGameConfigMgr.ParseBin(ta)
    -- 或 ta.bytes → ParseBinBytes
end)
```

禁止：

- 写死 `AB/gameconfig.ab`、hash 文件名
- 写死 `xlua/`、`uipackage/` 这类路径
- 依赖 `GetFontsBundleRelative` 那种约定回退

`LaunchMode` 只在 Lua 里分支，不要在生成器里写两套查询 API。

---

## 9. 与客户端工程的衔接

- 不要 `require` C#；用 `CS.GameConfig.XLuaGameConfigMgr`、`CS.AssetBundleLoader`、`CS.AssetBundlePath`
- 不要把 `GameConfigMgr.lua` 生成进 `FGUIScripts` / `FGUILua`（那是 UI）
- XLua 分包：`RawResources/XLua/GameConfig/` 在 `XLua` 的 AB+ 下会打成 `AB/xlua/gameconfig_{hash}.ab`，index 键为 `GameConfig`
- **脚本包**是 `xlua/gameconfig_*.ab`，**数据包**是 `gameconfig_*.ab`。加载数据必须用数据 key，不要和脚本包混用
- 若担心重名，数据文件夹可改成 `GameConfigData`（生成器和客户端 AB 标记必须一致）
- 生成后无需改 `AssetBundleBuilder`；有 `.lua` 就会进 XLua 包

---

## 10. 生成器不要做的事

- 不要改 `GameConfigMgr.cs` / `XLuaGameConfigMgr.cs` 的手写逻辑（它们也是生成物）
- 不要为了 Lua 再生成一份 C# `GetListCfg()` 包装
- 不要在 Lua 里 `new` `JObject` / 传 `Dictionary`
- 不要生成 `GetConfig<T>`
- 不要把 UI 进度、SceneMgr 写进配置 Mgr

---

## 11. 验收用例

用现有两张表，Folder 与 AB 各跑一遍：

```lua
GameConfigMgr.Load(function(ok)
    local cst = GameConfigMgr.GetConstCfg()
    print(cst.test1)

    local row = GameConfigMgr.GetListCfgById("1")  -- id 按表内真实值
    print(row.id, row.test1)

    local list = GameConfigMgr.GetListCfg()
    print(#list, list[1].id)

    local found = GameConfigMgr.GetListCfgByTemplate({ test1 = row.test1 })
    print(#found)
end)
```

AB 模式下 `index.json` 须有 `GameConfig` 路径，且存在对应 `.ab`。

---

## 12. 推荐生成物清单

1. `GameConfigMgr.lua`：加载 + 通用查询 + 转表 + 每表薄封装
2. （可选）`GameConfigTypes.lua`：仅 `---@class`，给 IDE
3. 不要生成 `GameConfigLaunch.lua`，除非所有模块都配 Launch

C# 桥保持「解析 + 按名取原始节点」即可。**表一变只重生成 Lua**，客户端业务只依赖 `GameConfigMgr.GetXxx`。
