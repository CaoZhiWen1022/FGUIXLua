# XLuaProject

Unity + XLua + FairyGUI 的客户端框架。逻辑与 UI 脚本走 Lua，资源与脚本按 AssetBundle 热更；GameFrame 负责分层开界面、弹窗队列和层特效。

仓库包含两个工程：

| 目录 | 说明 |
| --- | --- |
| `XLuaProjectClient` | Unity 客户端（2022.3），含 C# 启动、XLua、GameFrame、资源加载 |
| `XLuaProjectUI` | FairyGUI 源工程，发布到客户端 `Assets/RawResources/UIPackage/` |

设计分辨率：`1080 × 1920`。

---

## XLua

C# 只做启动与资源桥，业务在 Lua 里跑。

**启动**

1. `GameLaunch` 创建 `LuaEnv`，按启动模式注册 Loader。
2. 注入全局 `LaunchMode`（`Folder` / `AssetBundle`）。
3. `require 'GameLaunch.XLuaLaunch'` → 拉起 GameFrame → 打开 `GameLoading`。

**两种启动模式**

- **Folder（编辑器默认）**：直接读 `Assets/RawResources/XLua/**/*.lua`，改脚本即可热重进。
- **AssetBundle（真机 / 打包）**：先异步加载 `index.json`，再预加载全部 XLua AB，之后 `require` 从包内取 `.lua` 字节。

菜单切换：编辑器 `LaunchMode`（`LaunchModeConfig`）。非编辑器固定 AB。

**Lua 目录约定**

```
Assets/RawResources/XLua/
├── GameLaunch/      启动入口、UIID
├── GameFrame/       UI 框架
├── Resource/        资源门面（Folder / AB 分流）
├── FGUILua/         FairyGUI 导出的绑定脚本
└── FGUIScripts/     界面逻辑（HomePanel、PopupTest…）
```

`require` 路径与文件夹一致，例如 `GameFrame.UIFrame`、`FGUIScripts.HomePanel`。

XLua 与 FairyGUI 的类型别名在 `GameFrame/Core/FairyGUI.lua`，Lua 里可直接用 `GRoot`、`GTween`、`BlurFilter`、`UIRippleEffect` 等。

---

## 热更新

热更对象是带 hash 文件名的 AssetBundle，不是整包重装。路径由 `AssetBundlePath` 统一解析。

**优先级（由高到低）**

1. **热更本地**：`persistentDataPath/AB`（已下载的补丁）
2. **CDN**：`GameLaunchConfig.json` 的 `cdnUrl`
3. **包内底包**：`StreamingAssets/AB`

远程包会先落到热更目录再加载，下次启动可直接走本地。

**索引**

`StreamingAssets/AB/index.json` 把逻辑名映射到带 hash 的相对路径，例如：

```json
{
  "Home": { "path": "uipackage\\home_2c600c.ab" },
  "GameFrame": { "path": "xlua\\gameframe_10977a.ab" }
}
```

代码只认逻辑名（`Home`、`Fonts`、`D3Prefabs`），换包只改 index 和文件名，不必改业务。

**当前 AB 分类**

| 逻辑名 | 内容 |
| --- | --- |
| `xlua/*` | GameFrame、界面脚本、资源 Lua |
| `uipackage/*` | FairyGUI 包（Common / Home / GameLoading / PopupTest） |
| `Fonts` | 默认字体 |
| `D3Prefabs` | 3D 预制体（如 Home 里的 Deer） |

`AssetBundleLoader` 异步加载，WebGL 走 `UnityWebRequest`。UI 包、Lua、预制体、字体都走同一套解析。

**资源门面**

Lua 侧只调 `ResourcesMgr`：

- Folder：编辑器 `AssetDatabase` / 本地路径
- AB：`ResourcesAB` → `AssetBundleLoader`

界面开闭时 `UIBundleMgr` 做包引用计数，超出 `MAX_PKGS` 且非常驻包（`Common`）会卸载。

---

## GameFrame

GameFrame 是 Lua UI 框架，唯一对外入口是 `UIFrame`。

**启动流程**

```
XLuaLaunch
  → UIFrame.Init
      → 建层级、适配 1080×1920
      → 加载字体 AB
      → 预加载 Common / Home
  → 打开 GameLoading
      → 进度条 0→70 + 预加载 Main 场景
      → 关 Loading、开 Home、激活 Main
```

**层级（`UILayer`）**

`Panel → Popup → Guide → FullScreenMask → Tips → Fly → JumpScene`

每层一个铺满 `GRoot` 的 `GComponent`，`sortingOrder` 即层 ID。全屏 Panel 互斥：只显示最顶上一块，下面的 Hide。

**界面类型**

- `UIType.Panel`：全屏页，`UIFrame:Open` / `OpenByUiid`
- `UIType.Popup`：必须走 `popupQueueMgr:Push`，禁止直接 Open

**注册**

`UIBootstrap` 里 `UIRegister.Register`，必填 `UIID` + `uiClass`。可选包名、层级、弹窗优先级、依赖 Panel、遮罩与开场动画等。

当前界面：

| UIID | 类 | 类型 |
| --- | --- | --- |
| `10001` GameLoading | GameLoadingPanel | Panel |
| `10002` Home | HomePanel | Panel |
| `100001` PopupTest | PopupTest | Popup |

**类继承**

```
UIBase
  ├── UILogicPanel   全屏逻辑页（update / updateSec）
  ├── UIPopup        弹窗（遮罩、缩放开场、互斥）
  └── UIFullMask     全屏遮罩（加载中挡操作）
```

`UIBase` 用 `m_uiClass.Create()` 实例化 FGUI 视图；子类覆写 `update` / `updateSec` 时自动挂 `TimerMgr`。

**弹窗队列 `PopupQueueMgr`**

- 按 `PopupPriority`（Normal → Highest）出队
- 可声明依赖顶层 Panel，不在依赖页则不出
- 同优先级默认互斥；`isSamePriorityMeanwhileOpen` 可同时开
- 点遮罩可关（`isMaskClickCloseThis`）；只显示最顶层遮罩

**打开参数 `OpenUIParam`**

`UIID`、`data`、`openCall` / `closeCall` / `errorCall`、`reOpen`。

---

## UI 效果

效果挂在层上或弹窗上，不写死在某个面板里。

**弹窗毛玻璃**

有弹窗时对 Panel 层加 `BlurFilter`（`PANEL_BLUR_ON_POPUP`）。最后一个弹窗关闭后撤掉。模糊半径 `PANEL_BLUR_SIZE`。

**层水波纹**

`UIFrame:PlayLayerRipple(layer, opt)`：从层中心向外扩一圈 UV 扭曲。C# `UIRippleEffect` + `FairyGUI-Ripple` shader，独立 painting requestor，可与毛玻璃共存。

Home 上「水波纹效果」按钮即调用默认参数播放。可调：

| 参数 | 含义 | 默认 |
| --- | --- | --- |
| `duration` | 扩到屏外时长（秒），ease-out | 1.2 |
| `strength` | 扭曲强度 | 0.045 |
| `width` | 波环厚度 | 0.1 |
| `frequency` | 环内褶皱密度 | 28 |
| `centerX/Y` | 圆心（0~1） | 0.5 |

**弹窗开场**

`UIPopup:OpenAni`：`0.3 → 1.2 → 1.0` 两段缩放（GTween），动画中不可点。

**加载进度**

`GameLoadingPanel` 用 GTween 推进度条，与 `SceneMgr.PreloadAsync` 并行，两边都完成后才进 Home。

**3D 嵌入 UI**

Home 的 `DeerRoot`（GLoader）用 `GoWrapper` 挂 Deer 预制体，`update` 里每帧旋转。预制体走 `ResourcesMgr.LoadPrefab`，同样支持 Folder / AB。

**遮罩**

弹窗半透明全屏遮罩，alpha 默认 `0.6`。加载过程可走 FullScreenMask 层挡误点。

---

## FairyGUI 工程（`XLuaProjectUI`）

FairyGUI 5，发布类型 Unity。

**包**

- `Common`：通用按钮、遮罩
- `GameLoading`：加载页 + 进度条
- `Home`：主页（弹窗测试、水波纹、Deer 容器）
- `PopupTest`：测试弹窗

**发布**（`settings/Publish.json`）

- UI 包 → `XLuaProjectClient/Assets/RawResources/UIPackage/{包名}`
- 绑定代码 → `XLuaProjectClient/Assets/Scripts/FGUI/{包名}`（前缀 `UI_`）
- 业务 Lua 手写在 `FGUIScripts/`，不要改发布目录当逻辑源

编辑器缓存 `.objs/` 已忽略，只提交 `assets/`、`settings/`、`*.fairy`。

---

## 关键路径

```
XLuaProjectClient/Assets/Scripts/Scene/GameLaunch.cs
XLuaProjectClient/Assets/Scripts/Resource/AssetBundlePath.cs
XLuaProjectClient/Assets/Scripts/Resource/AssetBundleLoader.cs
XLuaProjectClient/Assets/RawResources/XLua/GameLaunch/XLuaLaunch.lua
XLuaProjectClient/Assets/RawResources/XLua/GameFrame/UIFrame.lua
XLuaProjectClient/Assets/RawResources/XLua/GameFrame/Core/UIFrameConfig.lua
XLuaProjectClient/Assets/StreamingAssets/AB/index.json
```
