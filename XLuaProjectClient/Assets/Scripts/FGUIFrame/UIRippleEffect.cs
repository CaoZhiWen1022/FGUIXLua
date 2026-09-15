using FairyGUI;
using UnityEngine;

/// <summary>
/// 对 FairyGUI 对象做从中心扩散的水波纹。使用独立 painting requestor，可与 BlurFilter 共存。
/// </summary>
public class UIRippleEffect
{
    public const int PaintingRequestorId = 1024;

    public float duration = 0.8f;
    public float strength = 0.045f;
    public float width = 0.1f;
    public float frequency = 28f;
    public float centerX = 0.5f;
    public float centerY = 0.5f;

    DisplayObject _target;
    Material _material;
    TimerCallback _tick;
    float _elapsed;
    bool _attached;
    bool _playing;

    public bool isPlaying
    {
        get { return _playing; }
    }

    public UIRippleEffect()
    {
        _tick = OnTick;
        _material = new Material(ShaderConfig.GetShader("FairyGUI/Ripple"));
        _material.hideFlags = HideFlags.HideAndDontSave;
    }

    public void Attach(GObject gobj)
    {
        if (gobj == null || gobj.displayObject == null)
        {
            Debug.LogWarning("[UIRippleEffect] Attach failed, target is null");
            return;
        }

        DisplayObject next = gobj.displayObject;
        if (_target == next)
        {
            return;
        }

        Stop();
        _target = next;
    }

    public void Play()
    {
        if (_target == null || _target.isDisposed)
        {
            Debug.LogWarning("[UIRippleEffect] Play failed, target is null or disposed");
            return;
        }

        duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;
        ApplyParams(0f);
        EnsureAttached();
        _playing = true;
        if (!Timers.inst.Exists(_tick))
        {
            Timers.inst.AddUpdate(_tick);
        }
    }

    public void Stop()
    {
        _playing = false;
        Timers.inst.Remove(_tick);
        if (_material != null)
        {
            _material.SetFloat("_Progress", 0f);
        }

        if (_attached && _target != null && !_target.isDisposed)
        {
            _target.onPaint -= OnPaint;
            _target.LeavePaintingMode(PaintingRequestorId);
        }
        _attached = false;
    }

    public void Dispose()
    {
        Stop();
        _target = null;
        if (_material != null)
        {
            if (Application.isPlaying)
                Object.Destroy(_material);
            else
                Object.DestroyImmediate(_material);
            _material = null;
        }
    }

    void EnsureAttached()
    {
        if (_attached || _target == null || _target.isDisposed)
        {
            return;
        }

        _target.EnterPaintingMode(PaintingRequestorId, null);
        _target.onPaint += OnPaint;
        _attached = true;
    }

    void ApplyParams(float progress)
    {
        if (_material == null)
        {
            return;
        }

        _material.SetFloat("_Progress", progress);
        _material.SetFloat("_Strength", strength);
        _material.SetFloat("_Width", width);
        _material.SetFloat("_Frequency", frequency);
        _material.SetVector("_Center", new Vector4(centerX, centerY, 0f, 0f));
    }

    void OnTick(object param)
    {
        if (_target == null || _target.isDisposed)
        {
            Stop();
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(_elapsed / duration);
        float eased = 1f - (1f - p) * (1f - p);
        ApplyParams(eased);
        if (p >= 1f)
        {
            Stop();
        }
    }

    void OnPaint()
    {
        if (_material == null || _target == null || _target.paintingGraphics == null
            || _target.paintingGraphics.texture == null)
        {
            return;
        }

        RenderTexture source = _target.paintingGraphics.texture.nativeTexture as RenderTexture;
        if (source == null)
        {
            return;
        }

        _material.SetFloat("_Aspect", source.width / (float)Mathf.Max(1, source.height));
        RenderTexture tmp = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
        Graphics.Blit(source, tmp, _material);
        Graphics.Blit(tmp, source);
        RenderTexture.ReleaseTemporary(tmp);
    }
}
