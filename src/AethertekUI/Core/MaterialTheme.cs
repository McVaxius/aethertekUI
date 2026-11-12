using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

/// <summary>Native retains host styling; the other modes apply themed colors with progressively stronger layout ownership.</summary>
public enum MaterialStyleMode { Compatible, Material, ColorsOnly, Native }

public sealed class MaterialTheme
{
    [ThreadStatic] private static MaterialTheme? current;
    [ThreadStatic] private static MaterialMetrics metrics;
    [ThreadStatic] private static MaterialColorScheme? nativeColors;
    public static MaterialTheme Current => current ?? throw new InvalidOperationException("Enter MaterialTheme.Push before drawing components.");
    internal static MaterialTheme? CurrentOrNull => current;
    public static MaterialMetrics Metrics => current is null ? throw new InvalidOperationException("No theme scope.") : metrics;
    private readonly MaterialColorScheme colors;
    public MaterialColorScheme Colors => IsNative ? nativeColors! : colors;
    /// <summary>The active scope retains host colors and native global styles.</summary>
    public bool IsNative => ReferenceEquals(current, this) && nativeColors != null;
    internal MaterialColorSettings? AppearanceSettings => Colors.AppearanceSettings;
    public MaterialColorMode ColorMode => AppearanceSettings?.Mode ?? (Colors.IsLcars ? MaterialColorMode.Lcars : MaterialColorMode.Auto);
    public bool IsLcars => Colors.IsLcars;
    public bool IsGlassmorphism => ColorMode == MaterialColorMode.Glassmorphism;
    public bool IsSpatial => ColorMode == MaterialColorMode.Spatial;
    public bool IsCyberpunk => ColorMode == MaterialColorMode.Cyberpunk;
    /// <summary>Optional host-owned feedback. No playback occurs unless the host supplies this callback.</summary>
    public Action<MaterialUiSoundCue>? Feedback { get; set; }
    public MaterialDensity Density { get; set; }
    public MaterialMotion Motion { get; } = new();
    private int highlightContrastPercent;
    /// <summary>Opt-in control contrast. Zero retains the existing appearance; the consumer owns persistence.</summary>
    public int HighlightContrastPercent
    {
        get => highlightContrastPercent;
        set => highlightContrastPercent = Math.Clamp(value, 0, 100);
    }
    /// <summary>Adjusts a themed control's RGB without changing its alpha, palette or semantic colours.</summary>
    public Vector4 ControlColor(Vector4 color, bool border = false)
    {
        if (HighlightContrastPercent == 0 || IsNative || color.W <= 0) return color;
        var result = Vector4.Lerp(color, Colors.OnSurface, HighlightContrastPercent / 100f * (border ? .35f : .18f));
        return new(result.X, result.Y, result.Z, color.W);
    }
    private float surfaceOpacity = .94f;
    public float SurfaceOpacity
    {
        get => surfaceOpacity;
        set => surfaceOpacity = float.IsFinite(value) ? Math.Clamp(value,0,1) : throw new ArgumentOutOfRangeException(nameof(value));
    }
    public MaterialTheme(Vector3 seed, MaterialDensity density = MaterialDensity.Compact, IPaletteGenerator? generator = null)
    {
        colors = new((generator ?? new OklchPaletteGenerator()).Generate(seed));
        Density = density;
    }
    public MaterialTheme(MaterialColorScheme colors, MaterialDensity density = MaterialDensity.Compact)
    {
        this.colors = colors ?? throw new ArgumentNullException(nameof(colors));
        Density = density;
    }
    public MaterialTheme(Vector3 seed, MaterialColorSettings colorSettings, MaterialDensity density = MaterialDensity.Compact,
        IPaletteGenerator? generator = null)
        : this((colorSettings ?? throw new ArgumentNullException(nameof(colorSettings)))
            .Apply(new MaterialColorScheme((generator ?? new OklchPaletteGenerator()).Generate(seed))), density) { }
    public Vector4 SurfaceColor(Vector4 role) => IsNative ? role : MaterialColor.Alpha(role,SurfaceOpacity);
    public static ThemeScope Push(MaterialTheme theme, float globalScale = 1, bool applyMetrics = true)
        => new(theme,globalScale,applyMetrics ? MaterialStyleMode.Compatible : MaterialStyleMode.ColorsOnly);
    public static ThemeScope Push(MaterialTheme theme, float globalScale, MaterialStyleMode mode) => new(theme,globalScale,mode);

    private static MaterialColorScheme HostColors(MaterialColorScheme semantic)
    {
        var style = ImGui.GetStyle();
        Vector4 Color(ImGuiCol role) => style.Colors[(int)role];
        var text = Color(ImGuiCol.Text);
        var background = Color(ImGuiCol.WindowBg);
        var button = Color(ImGuiCol.Button);
        var accent = Color(ImGuiCol.CheckMark);
        return new()
        {
            Primary = accent, Secondary = accent, Tertiary = accent,
            OnPrimary = text, OnSecondary = text, OnTertiary = text,
            PrimaryContainer = button, SecondaryContainer = button, TertiaryContainer = button,
            OnPrimaryContainer = text, OnSecondaryContainer = text, OnTertiaryContainer = text,
            Background = background, OnBackground = text, Surface = background, OnSurface = text,
            SurfaceVariant = Color(ImGuiCol.FrameBgActive), OnSurfaceVariant = Color(ImGuiCol.TextDisabled),
            SurfaceContainerLowest = background, SurfaceContainerLow = Color(ImGuiCol.TitleBg),
            SurfaceContainer = Color(ImGuiCol.ChildBg), SurfaceContainerHigh = Color(ImGuiCol.PopupBg),
            SurfaceContainerHighest = Color(ImGuiCol.FrameBg), Outline = Color(ImGuiCol.Border),
            OutlineVariant = Color(ImGuiCol.Separator), InverseSurface = text,
            InverseOnSurface = background, InversePrimary = accent,
            Error = semantic.Error, OnError = semantic.OnError,
            ErrorContainer = semantic.ErrorContainer, OnErrorContainer = semantic.OnErrorContainer,
            Scrim = Color(ImGuiCol.ModalWindowDimBg),
        };
    }

    public ref struct ThemeScope
    {
        private MaterialStyleScope style;
        private readonly MaterialTheme? previous;
        private readonly MaterialMetrics previousMetrics;
        private readonly MaterialColorScheme? previousNativeColors;
        private readonly MaterialControlContext previousContext;
        private readonly MaterialControlMetrics? previousControls;
        private bool active;
        internal ThemeScope(MaterialTheme theme, float scale, MaterialStyleMode mode)
        {
            ArgumentNullException.ThrowIfNull(theme);
            if (!float.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            previous = current;
            previousMetrics = metrics;
            previousNativeColors = nativeColors;
            var hostColors = mode == MaterialStyleMode.Native ? HostColors(theme.colors) : null;
            current = theme;
            nativeColors = hostColors;
            metrics = new(theme.Density,scale);
            previousContext = MaterialControls.SetContext(MaterialControlContext.Regular);
            previousControls = MaterialControls.SetMetrics(null);
            style = new();
            active = true;
            if (mode == MaterialStyleMode.Native) return;
            var c = theme.Colors;
            var m = metrics;
            style.Color(ImGuiCol.Text,c.OnSurface);
            style.Color(ImGuiCol.TextDisabled,MaterialColor.Alpha(c.OnSurface,.38f));
            style.Color(ImGuiCol.WindowBg,theme.SurfaceColor(c.Background));
            style.Color(ImGuiCol.ChildBg,theme.SurfaceColor(c.SurfaceContainer));
            style.Color(ImGuiCol.PopupBg,theme.SurfaceColor(c.SurfaceContainerHigh));
            style.Color(ImGuiCol.Border,theme.ControlColor(c.OutlineVariant, border: true));
            var frameBackground = theme.IsSpatial ? c.Surface : c.SurfaceContainerHighest;
            style.Color(ImGuiCol.FrameBg,theme.ControlColor(frameBackground));
            style.Color(ImGuiCol.FrameBgHovered,theme.ControlColor(MaterialColor.Layer(frameBackground,c.OnSurface,.08f)));
            style.Color(ImGuiCol.FrameBgActive,theme.ControlColor(c.SurfaceVariant));
            var nativeButton = theme.IsLcars ? c.SurfaceContainerHighest : c.SecondaryContainer;
            var nativeButtonText = theme.IsLcars ? c.OnSurface : c.OnSecondaryContainer;
            style.Color(ImGuiCol.Button,theme.ControlColor(nativeButton));
            style.Color(ImGuiCol.ButtonHovered,theme.ControlColor(MaterialColor.Layer(nativeButton,nativeButtonText,.08f)));
            style.Color(ImGuiCol.ButtonActive,theme.ControlColor(MaterialColor.Layer(nativeButton,nativeButtonText,.14f)));
            style.Color(ImGuiCol.Header,theme.ControlColor(theme.IsLcars ? c.PrimaryContainer : c.SecondaryContainer));
            style.Color(ImGuiCol.HeaderHovered,theme.ControlColor(MaterialColor.Layer(c.SurfaceContainer,c.OnSurface,.08f)));
            style.Color(ImGuiCol.HeaderActive,theme.ControlColor(c.PrimaryContainer));
            style.Color(ImGuiCol.CheckMark,theme.IsLcars ? MaterialLcars.ControlBlue : c.Primary);
            style.Color(ImGuiCol.SliderGrab,theme.IsLcars ? MaterialLcars.ControlBlue : c.Primary);
            style.Color(ImGuiCol.SliderGrabActive,theme.IsLcars ? MaterialLcars.ControlBlue : c.OnPrimaryContainer);
            style.Color(ImGuiCol.Separator,c.OutlineVariant);
            style.Color(ImGuiCol.SeparatorHovered,c.Primary);
            style.Color(ImGuiCol.SeparatorActive,c.Primary);
            style.Color(ImGuiCol.TitleBg,c.SurfaceContainerLow);
            style.Color(ImGuiCol.TitleBgActive,c.SurfaceContainer);
            style.Color(ImGuiCol.TitleBgCollapsed,theme.SurfaceColor(c.SurfaceContainerLowest));
            style.Color(ImGuiCol.ScrollbarBg,Vector4.Zero);
            style.Color(ImGuiCol.ScrollbarGrab,c.OutlineVariant);
            style.Color(ImGuiCol.ScrollbarGrabHovered,c.Outline);
            style.Color(ImGuiCol.ScrollbarGrabActive,c.Primary);
            style.Color(ImGuiCol.ResizeGrip,MaterialColor.Alpha(c.Primary,.15f));
            style.Color(ImGuiCol.ResizeGripHovered,MaterialColor.Alpha(c.Primary,.35f));
            style.Color(ImGuiCol.ResizeGripActive,c.Primary);
            style.Color(ImGuiCol.TextSelectedBg,MaterialColor.Alpha(c.Primary,.3f));
            style.Color(ImGuiCol.Tab,c.SurfaceContainer);
            style.Color(ImGuiCol.TabHovered,theme.IsLcars ? c.PrimaryContainer : c.SecondaryContainer);
            style.Color(ImGuiCol.TabActive,c.PrimaryContainer);
            style.Color(ImGuiCol.TabUnfocused,c.SurfaceContainerLow);
            style.Color(ImGuiCol.TabUnfocusedActive,c.SecondaryContainer);
            style.Color(ImGuiCol.NavHighlight,c.Primary);
            style.Color(ImGuiCol.TableHeaderBg,c.SurfaceContainerHigh);
            style.Color(ImGuiCol.TableBorderStrong,c.OutlineVariant);
            style.Color(ImGuiCol.TableBorderLight,MaterialColor.Alpha(c.OutlineVariant,.5f));
            style.Color(ImGuiCol.TableRowBg,Vector4.Zero);
            style.Color(ImGuiCol.TableRowBgAlt,MaterialColor.Alpha(c.OnSurface,.025f));
            style.Color(ImGuiCol.ModalWindowDimBg,MaterialColor.Alpha(c.Scrim,.5f));
            if (mode == MaterialStyleMode.ColorsOnly) return;
            style.Style(ImGuiStyleVar.WindowRounding,m.OuterRadius);
            style.Style(ImGuiStyleVar.ChildRounding,m.OuterRadius);
            style.Style(ImGuiStyleVar.PopupRounding,m.Radius);
            // Native checkboxes, steppers and narrow editors are not pill buttons.
            style.Style(ImGuiStyleVar.FrameRounding,4*scale);
            style.Style(ImGuiStyleVar.GrabRounding,m.Radius);
            style.Style(ImGuiStyleVar.TabRounding,m.Radius*.5f);
            style.Style(ImGuiStyleVar.ScrollbarRounding,m.Radius);
            if (mode == MaterialStyleMode.Compatible) return;
            var controls = MaterialControls.Metrics;
            style.Style(ImGuiStyleVar.WindowBorderSize,0);
            style.Style(ImGuiStyleVar.ChildBorderSize,m.Border);
            style.Style(ImGuiStyleVar.FrameBorderSize,theme.IsSpatial ? m.Border : 0);
            style.Style(ImGuiStyleVar.PopupBorderSize,m.Border);
            style.Style(ImGuiStyleVar.WindowPadding,new Vector2(m.Padding));
            style.Style(ImGuiStyleVar.FramePadding,controls.NativePadding);
            style.Style(ImGuiStyleVar.ItemSpacing,controls.ItemSpacing);
            style.Style(ImGuiStyleVar.CellPadding,controls.CellPadding);
        }
        public void Dispose()
        {
            if (!active) return;
            style.Dispose();
            current = previous;
            metrics = previousMetrics;
            nativeColors = previousNativeColors;
            MaterialControls.SetContext(previousContext);
            MaterialControls.SetMetrics(previousControls);
            active = false;
        }
    }
}
