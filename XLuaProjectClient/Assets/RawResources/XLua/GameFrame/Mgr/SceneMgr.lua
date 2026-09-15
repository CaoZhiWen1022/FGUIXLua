SceneMgr = {}

--- 异步预加载场景（不切换 / 不激活）
--- @param sceneName string
--- @param callback function|nil 预加载到约 90% 后回调
function SceneMgr.PreloadAsync(sceneName, callback)
    CS.SceneLoadBridge.PreloadAsync(sceneName, function()
        if callback then
            callback()
        end
    end)
end

--- 激活已预加载的场景
--- @param callback function|nil 激活完成后回调
function SceneMgr.ActivateLoadedScene(callback)
    CS.SceneLoadBridge.ActivateLoadedScene(function()
        if callback then
            callback()
        end
    end)
end

--- 异步加载并切换场景（预加载 → 回调里可切 UI → 再激活）
--- @param sceneName string
--- @param jumpSceneUIID number|nil 激活前打开的过渡 UI（可选）
--- @param callback function|nil 激活前、CloseAll 后回调（可选）
function SceneMgr.StartAsyncLoadScene(sceneName, jumpSceneUIID, callback)
    CS.SceneLoadBridge.LoadAsync(sceneName, function()
        UIFrame:CloseAll()
        if jumpSceneUIID then
            UIFrame:OpenByUiid(jumpSceneUIID)
        end
        if callback then
            callback()
        end
    end)
end
