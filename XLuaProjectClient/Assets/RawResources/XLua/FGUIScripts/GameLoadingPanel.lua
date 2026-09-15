GameLoadingPanel = setmetatable({}, UILogicPanel)
GameLoadingPanel.__index = GameLoadingPanel
GameLoadingPanel.m_uiClass = UI_GameLoading

local PROGRESS_START = 0
local PROGRESS_SCENE = 70
local PROGRESS_TWEEN_DURATION = 1.2

function GameLoadingPanel:OnOpened()
    UILogicPanel.OnOpened(self)

    local data = self.data
    local progress = data and data.progress or PROGRESS_START
    if self.m_ui.m_bar ~= nil then
        self.m_ui.m_bar.value = progress
    end

    local launch = CS.GameLaunch.Instance
    if launch ~= nil and launch.gameLaunchPanel ~= nil then
        launch.gameLaunchPanel:SetActive(false)
    end

    self:StartLoad()
end

function GameLoadingPanel:OnShown()
    print("[GameLoadingPanel] OnShown")
end

function GameLoadingPanel:OnHidden()
    print("[GameLoadingPanel] OnHidden")
end

function GameLoadingPanel:OnClosed()
    self:KillProgressTween()
    UILogicPanel.OnClosed(self)
end

function GameLoadingPanel:KillProgressTween()
    if self._progressTweener ~= nil then
        pcall(function()
            self._progressTweener:Kill()
        end)
        self._progressTweener = nil
    end
end

function GameLoadingPanel:TweenProgressTo(from, to, duration, onComplete)
    self:KillProgressTween()
    local bar = self.m_ui and self.m_ui.m_bar
    if bar == nil then
        if onComplete then onComplete() end
        return
    end

    bar.value = from
    local ok = pcall(function()
        -- xLua 常把 OnUpdate 绑到无参 GTweenCallback，不能依赖回调参数 tweener
        self._progressTweener = GTween.To(from, to, duration or PROGRESS_TWEEN_DURATION)
            :SetTarget(bar)
            :OnUpdate(function()
                local tweener = self._progressTweener
                if tweener ~= nil and bar ~= nil and not bar.isDisposed then
                    bar.value = tweener.value.x
                end
            end)
            :OnComplete(function()
                self._progressTweener = nil
                if bar ~= nil and not bar.isDisposed then
                    bar.value = to
                end
                if onComplete then onComplete() end
            end)
    end)
    if not ok then
        bar.value = to
        self._progressTweener = nil
        if onComplete then onComplete() end
    end
end

function GameLoadingPanel:StartLoad()
    print("[GameLoadingPanel] StartLoad")
    self._progressReady = false
    self._sceneReady = false
    self._entered = false

    -- 进度 0→70，同时后台预加载 Main（不切换场景）
    self:TweenProgressTo(PROGRESS_START, PROGRESS_SCENE, PROGRESS_TWEEN_DURATION, function()
        self._progressReady = true
        self:TryEnterMain()
    end)

    SceneMgr.PreloadAsync("Main", function()
        print("[GameLoadingPanel] Main preload ready (not activated)")
        self._sceneReady = true
        self:TryEnterMain()
    end)
end

--- 进度与场景都就绪后，再关闭 Loading、打开 Home，并激活 Main
function GameLoadingPanel:TryEnterMain()
    if self._entered or not self._progressReady or not self._sceneReady then
        return
    end
    self._entered = true

    print("[GameLoadingPanel] enter Main")
    UIFrame:CloseAll()
    UIFrame:OpenByUiid(UIID.Home)
    SceneMgr.ActivateLoadedScene(function()
        print("[GameLoadingPanel] Main activated")
    end)
end
