-- AB 模式：异步加载 UIPackage AB，回调返回 AssetBundle
ResourcesAB = {}

function ResourcesAB.LoadUIPackage(packageName, callback)
    if packageName == nil or packageName == "" then
        if callback then callback(nil) end
        return
    end
    CS.AssetBundleLoader.LoadBundleByKeyAsync(packageName, function(bundle)
        if bundle == nil then
            print(string.format("[ResourcesAB] LoadUIPackage failed: %s", tostring(packageName)))
        end
        if callback then callback(bundle) end
    end)
end

function ResourcesAB.UnloadUIPackage(packageName)
    if packageName == nil or packageName == "" then
        return
    end
    CS.AssetBundleLoader.UnloadBundleByKey(packageName)
end

function ResourcesAB.LoadD3Prefabs(callback)
    CS.AssetBundleLoader.LoadBundleByKeyAsync("D3Prefabs", function(bundle)
        if bundle == nil then
            print("[ResourcesAB] LoadD3Prefabs failed")
        end
        if callback then callback(bundle) end
    end)
end

function ResourcesAB.UnloadD3Prefabs()
    CS.AssetBundleLoader.UnloadBundleByKey("D3Prefabs")
end
