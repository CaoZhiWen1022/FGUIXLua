UIFrameConfig = {
    FRAME_WIDTH = 1080,
    FRAME_HEIGHT = 1920,
    INIT_LOAD_PKGS = { "Common", "Home" },
    PERMANENT_PKGS = { "Common" },
    MAX_PKGS = 5,
    POPUP_MASK_ALPHA = 0.6,
    -- 存在弹窗时对 Panel 层做动态毛玻璃
    PANEL_BLUR_ON_POPUP = true,
    PANEL_BLUR_SIZE = 0.2,
    -- 水波纹默认参数（PlayLayerRipple 的 opt 可覆盖）
    -- DURATION：波环从中心扩到屏幕外的时长（秒）。越大越慢；内部用 ease-out，前快后慢。建议 0.5~1.5
    PANEL_RIPPLE_DURATION = 1.2,
    -- STRENGTH：波环处 UV 扭曲幅度，越大画面挤得越狠。过大会撕裂采样。建议 0.02~0.08
    PANEL_RIPPLE_STRENGTH = 0.045,
    -- WIDTH：波环径向厚度（UV 空间）。越大环越宽、越软；越小环越细、越锐。建议 0.06~0.18
    PANEL_RIPPLE_WIDTH = 0.1,
    -- FREQUENCY：波环内正弦褶皱密度。越大一圈里高低起伏越多。建议 16~40
    PANEL_RIPPLE_FREQUENCY = 28,
    -- 编辑器下 UI 包路径前缀
    PACKAGE_PATH_PREFIX = "Assets/RawResources/UIPackage/",
    -- 启动时加载字体：ab=index 逻辑名；name=AB 内资源名（含后缀）；isDefault=设为 FGUI 默认字体
    INIT_FONTS = {
        { ab = "Fonts", name = "AlibabaPuHuiTi-3-55-Regular.ttf", isDefault = true },
    },
}

function UIFrameConfig.Init(opt)
    opt = opt or {}
    if opt.frameWidth then UIFrameConfig.FRAME_WIDTH = opt.frameWidth end
    if opt.frameHeight then UIFrameConfig.FRAME_HEIGHT = opt.frameHeight end
    if opt.initLoadPkgs then UIFrameConfig.INIT_LOAD_PKGS = opt.initLoadPkgs end
    if opt.permanentPkgs then UIFrameConfig.PERMANENT_PKGS = opt.permanentPkgs end
    if opt.maxPkgs then UIFrameConfig.MAX_PKGS = opt.maxPkgs end
    if opt.popupMaskAlpha then UIFrameConfig.POPUP_MASK_ALPHA = opt.popupMaskAlpha end
    if opt.panelBlurOnPopup ~= nil then UIFrameConfig.PANEL_BLUR_ON_POPUP = opt.panelBlurOnPopup end
    if opt.panelBlurSize then UIFrameConfig.PANEL_BLUR_SIZE = opt.panelBlurSize end
    if opt.panelRippleDuration then UIFrameConfig.PANEL_RIPPLE_DURATION = opt.panelRippleDuration end
    if opt.panelRippleStrength then UIFrameConfig.PANEL_RIPPLE_STRENGTH = opt.panelRippleStrength end
    if opt.panelRippleWidth then UIFrameConfig.PANEL_RIPPLE_WIDTH = opt.panelRippleWidth end
    if opt.panelRippleFrequency then UIFrameConfig.PANEL_RIPPLE_FREQUENCY = opt.panelRippleFrequency end
    if opt.packagePathPrefix then UIFrameConfig.PACKAGE_PATH_PREFIX = opt.packagePathPrefix end
    if opt.initFonts then UIFrameConfig.INIT_FONTS = opt.initFonts end
end
