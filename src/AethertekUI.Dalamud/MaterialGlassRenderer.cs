using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Textures.TextureWraps;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Glasswork.Backend;
using Glasswork.Glass;

namespace AethertekUI.Dalamud;

/// <summary>Records Glasswork only at the consumer's explicit Background surfaces.</summary>
public sealed class MaterialGlassRenderer : IMaterialBackgroundRenderer, IDisposable
{
    private readonly Func<nint>? deviceHandle;
    private readonly Action<string, Exception?>? log;
    private readonly IPluginLog? pluginLog;
    private readonly Func<ImDrawListPtr, Vector2, Vector2, float, Vector4, float, bool, bool>? record;
    private GlassLayer? layer;
    private MaterialDetachedGlass? detached;
    private nint currentDevice;
    private bool disposed;
    private bool demandManaged;
    private bool usedLayer;
    private bool usedDetached;
    private IDalamudTextureWrap? imageWrap;
    private string? imagePath;
    public bool ImageAvailable => imageWrap is { Width: > 0, Height: > 0 };
    public bool ImageMode { get; private set; }
    public string? ImageProblem { get; private set; }
#if DEBUG
    private uint tracedRoutes;
#endif

    public float Opacity { get; set; } = 1;
    public bool ScreenshotsPaused => detached?.ScreenshotsPaused == true;
    public bool CanPauseForScreenshots => detached?.CanPauseForScreenshots == true;
    public bool TrySetScreenshotsPaused(bool paused) => detached?.TrySetScreenshotsPaused(paused) == true;

    public void ConfigureImage(ITextureProvider textures, bool enabled, string? path)
    {
        if (ImageMode != enabled || imagePath != path) detached?.ReleaseImageSource();
        imagePath = path;
        ImageMode = enabled;
        imageWrap = null;
        ImageProblem = null;
        if (!enabled) return;
        if (string.IsNullOrWhiteSpace(path)) { ImageProblem = "Choose an image file."; return; }
        try
        {
            if (!textures.GetFromFile(path).TryGetWrap(out imageWrap, out var error))
                ImageProblem = error == null ? "Loading image..." : "Image could not be loaded.";
        }
        catch { ImageProblem = "Image could not be loaded."; }
    }

    public bool DrawPlainImage(ImDrawListPtr drawList, Vector2 min, Vector2 max)
    {
        if (!ImageMode) return false;
        if (demandManaged && !ImGui.GetWindowViewport().IsNull
            && ImGui.GetWindowViewport().ID != ImGui.GetMainViewport().ID) GetDetached()?.MarkImageWindow();
        else detached?.MarkImageWindow();
        if (!ImageAvailable || imageWrap == null || drawList.IsNull) return false;
        var root = ImGuiP.GetCurrentWindowRead().RootWindow;
        var uv = CoverUv(new(imageWrap.Width, imageWrap.Height), root.Size);
        var range = new Vector2(uv.Z - uv.X, uv.W - uv.Y);
        var first = new Vector2(uv.X, uv.Y) + (min - root.Pos) / root.Size * range;
        var last = new Vector2(uv.X, uv.Y) + (max - root.Pos) / root.Size * range;
        drawList.AddImage(imageWrap.Handle, min, max, first, last, ImGui.GetColorU32(Vector4.One));
        return true;
    }

    internal static Vector4 CoverUv(Vector2 imageSize, Vector2 areaSize)
    {
        if (!float.IsFinite(imageSize.X) || !float.IsFinite(imageSize.Y) ||
            !float.IsFinite(areaSize.X) || !float.IsFinite(areaSize.Y) ||
            imageSize.X <= 0 || imageSize.Y <= 0 || areaSize.X <= 0 || areaSize.Y <= 0)
            return new(0, 0, 1, 1);
        var scale = MathF.Max(areaSize.X / imageSize.X, areaSize.Y / imageSize.Y);
        var extent = areaSize / (imageSize * scale);
        var start = (Vector2.One - extent) * .5f;
        return new(start.X, start.Y, start.X + extent.X, start.Y + extent.Y);
    }

    public MaterialGlassRenderer(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        this.pluginLog = pluginLog;
        deviceHandle = () => pluginInterface.UiBuilder.DeviceHandle;
        log = (message, exception) =>
        {
            if (exception == null) pluginLog.Debug("[AethertekUI] {Message}", message);
            else pluginLog.Warning(exception, "[AethertekUI] {Message}", message);
        };
    }

    // Records the native consumer contract in the focused CPU drawing test.
    internal MaterialGlassRenderer(Func<ImDrawListPtr, Vector2, Vector2, float, Vector4, float, bool, bool> record)
        => this.record = record ?? throw new ArgumentNullException(nameof(record));

    public void BeginFrame() => BeginFrameCore(blur: true, image: false);

    /// <summary>Starts a draw frame and releases resources unused by the selected backdrop.</summary>
    public void BeginFrame(bool blur, bool image)
    {
        demandManaged = true;
        BeginFrameCore(blur, image);
    }

    private void BeginFrameCore(bool blur, bool image)
    {
        if (disposed || deviceHandle == null) return;
        var device = deviceHandle();
        if (device != currentDevice || (demandManaged && (!blur || image || !usedLayer)))
        {
            layer?.Dispose();
            layer = null;
        }
        if (device != currentDevice || (demandManaged && ((!blur && !image) || !usedDetached)))
        {
            detached?.Dispose();
            detached = null;
        }
        currentDevice = device;
        usedLayer = false;
        usedDetached = false;
        if (!demandManaged && device != 0)
        {
            layer ??= CreateLayer();
            detached ??= new MaterialDetachedGlass(device, pluginLog!);
        }
        layer?.BeginFrame();
        detached?.BeginFrame();
    }

    private GlassLayer CreateLayer() => new(() => currentDevice, log)
    {
        MaxQuality = GlassQuality.Full,
        // Pin the material and continuous Frost percentage; Reduced quantizes Frost.
        BudgetMilliseconds = double.PositiveInfinity,
    };

    private MaterialDetachedGlass? GetDetached()
    {
        usedDetached = true;
        if (detached == null && demandManaged && currentDevice != 0)
        {
            detached = new MaterialDetachedGlass(currentDevice, pluginLog!);
            detached.BeginFrame();
        }
        return detached;
    }

    public unsafe void EndFrame(bool enabled)
    {
        if (detached == null) return;
        var viewport = ImGui.GetMainViewport();
        var gameWindow = viewport.IsNull ? 0 : (nint)viewport.PlatformHandleRaw;
        if (gameWindow == 0 && !viewport.IsNull) gameWindow = (nint)viewport.PlatformHandle;
        detached.EndFrame(gameWindow, enabled);
    }

    public bool DrawBackground(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        Vector4 tint, float frost, bool windowBackground = false)
    {
        if (disposed) return false;
        if (record != null) return record(drawList, min, max, rounding, tint, frost, windowBackground);
        var size = max - min;
        if (drawList.IsNull || !float.IsFinite(min.X) || !float.IsFinite(min.Y) ||
            !float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0 ||
            !float.IsFinite(rounding) || !float.IsFinite(frost)) return false;

        var material = CreateMaterial(tint, frost, Opacity, ImGuiHelpers.GlobalScale);
        if (ImageMode) return DrawImageBackground(drawList, min, max, rounding, in material, windowBackground);
        var viewport = ImGui.GetWindowViewport();
        if (!viewport.IsNull && viewport.ID != ImGui.GetMainViewport().ID)
        {
            TraceRoute(1, "detached viewport selected");
            return GetDetached()?.TryDraw(drawList, min, max, rounding, in material, windowBackground) == true;
        }
        TraceRoute(2, "main viewport selected");
        usedLayer = true;
        if (layer == null && demandManaged && currentDevice != 0)
        {
            layer = CreateLayer();
            layer.BeginFrame();
        }
        if (layer == null || !layer.CanRecord()) return false;

        if (windowBackground)
        {
            if (drawList != ImGui.GetWindowDrawList() || min != ImGui.GetWindowPos() ||
                max != min + ImGui.GetWindowSize()) return false;
            var callbacks = CountCallbacks(drawList);
            // This API prepends behind Begin's native title, buttons, borders and scrollbars.
            layer.DrawWindowBackground(in material, rounding);
            // The package's void window API can paint a fill on failure; only callbacks count as glass.
            return !layer.Faulted && CountCallbacks(drawList) > callbacks;
        }

        var shape = GlassShape.FromRect(min, size, rounding);
        return layer.TryDraw(drawList, in shape, in material);
    }

    private unsafe bool DrawImageBackground(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        in GlassMaterial material, bool windowBackground)
    {
        if (imageWrap == null || GetDetached() is not { } imageRenderer) return false;
        imageRenderer.MarkImageWindow();
        var view = (ID3D11ShaderResourceView*)(nint)imageWrap.Handle.Handle;
        if (view == null) return false;
        ID3D11Resource* resource = null;
        ID3D11Texture2D* texture = null;
        try
        {
            view->GetResource(&resource);
            if (resource == null) return false;
            var id = IID.IID_ID3D11Texture2D;
            void* queried = null;
            if (resource->QueryInterface(&id, &queried).FAILED || queried == null) return false;
            texture = (ID3D11Texture2D*)queried;
            var viewport = ImGui.GetWindowViewport();
            var root = ImGuiP.GetCurrentWindowRead().RootWindow;
            var origin = root.Pos - viewport.Pos;
            var bounds = new Vector4(origin.X, origin.Y, origin.X + root.Size.X, origin.Y + root.Size.Y);
            return imageRenderer.TryDraw(drawList, min, max, rounding, in material, windowBackground, (nint)texture, bounds);
        }
        finally
        {
            if (texture != null) texture->Release();
            if (resource != null) resource->Release();
        }
    }

    internal static GlassMaterial CreateMaterial(Vector4 tint, float frost, float opacity, float scale)
    {
        var material = GlassMaterial.Tinted(new Glasswork.Color(tint.X, tint.Y, tint.Z), Math.Clamp(tint.W, 0, 1));
        material.Frost = Math.Clamp(frost, 0, 1);
        material.Opacity *= float.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
        return material.Scaled(scale);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void TraceRoute(uint stage, string detail)
    {
#if DEBUG
        if ((tracedRoutes & stage) != 0) return;
        tracedRoutes |= stage;
        pluginLog?.Debug("[AethertekUI] Glasswork pilot: {Detail}", detail);
#endif
    }

    private static unsafe int CountCallbacks(ImDrawListPtr drawList)
    {
        var count = 0;
        for (var i = 0; i < drawList.CmdBuffer.Size; i++)
        {
            var command = drawList.CmdBuffer[i];
            if (command.UserCallback != null && command.UserCallbackData != null) count++;
        }
        return count;
    }

    /// <summary>Describes live routes and estimated payloads. The host owns shared image textures.</summary>
    public string DescribeResources()
    {
        var imageCount = ImageAvailable ? 1 : 0;
        var imageBytes = imageCount == 0 ? 0L : (long)imageWrap!.Width * imageWrap.Height * 4;
        return FormattableString.Invariant(
            $"glassMain={layer is not null} glassDetached={detached is not null} sharedImages={imageCount} estimatedSharedImageRgbaBytes={imageBytes} estimatedCaptureBytes={detached?.EstimatedCaptureBytes ?? 0}");
    }

    public void ReleaseResources()
    {
        layer?.ReleaseResources();
        detached?.ReleaseResources();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        imageWrap = null;
        layer?.Dispose();
        layer = null;
        detached?.Dispose();
        detached = null;
    }
}
