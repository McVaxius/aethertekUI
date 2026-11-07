using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

public readonly record struct MaterialColorLabels(string Auto, string Picker, string Default, string AutoHelp,
    string ThemeColor, string PickerHelp, string Background, string Controls, string Text,
    string PreviewText, string PreviewControl, string AutoHint, string PickerHint)
{
    /// <summary>Opt in after the consumer supports LCARS chrome; older consumers retain their two-mode editor.</summary>
    public string? Lcars { get; init; }
    public string? LcarsHelp { get; init; }
    /// <summary>Supply all three translated style names to expose the additional independent palettes.</summary>
    public string? Glassmorphism { get; init; }
    public string? Spatial { get; init; }
    public string? Cyberpunk { get; init; }
    public string? LcarsBar1 { get; init; }
    public string? LcarsBar2 { get; init; }
    public string? LcarsBar3 { get; init; }
    public string? Reset { get; init; }
    public string? ResetColors { get; init; }
    public string? Animation { get; init; }
    public string? AnimationSpeed { get; init; }
    public string? WaveColors { get; init; }
    public string? GlassWave1 { get; init; }
    public string? GlassWave2 { get; init; }
    public string? GlassWave3 { get; init; }
    public string? ResetWaveColors { get; init; }
    /// <summary>Supply the heading and all three mode labels to opt in to background selection.</summary>
    public string? BackgroundMode { get; init; }
    public string? Waves { get; init; }
    public string? Lights { get; init; }
    public string? Image { get; init; }
    public string? LightEffect { get; init; }
    public string? AmbientGlows { get; init; }
    public string? SweepingBeams { get; init; }
    public string? PointerGlow { get; init; }
    public string? ImageFile { get; init; }
    public string? BrowseImage { get; init; }
    public string? LoadImage { get; init; }
    public string? ImageHelp { get; init; }
    public string? LightColors { get; init; }
    public string? GlassLight1 { get; init; }
    public string? GlassLight2 { get; init; }
    public string? GlassLight3 { get; init; }
    public string? ResetLightColors { get; init; }
    /// <summary>Supply both labels to expose the chosen style's saved blur toggle and shared Frost percentage.</summary>
    public string? BackgroundBlur { get; init; }
    public string? BlurPercent { get; init; }
}

public static partial class MaterialAppearanceSelector
{
    /// <summary>
    /// Auto/Picker editor shared by Settings and the native-titlebar Colour popup.
    /// Retain choices and accent as consumer-owned drafts. Returns true for mode/preset changes
    /// and completed RGB edits; dragging updates the drafts without requesting a save each frame.
    /// The Auto palette retains DrawPalette's existing native IDs under id.
    /// </summary>
    public static bool DrawColors(string id, ref MaterialColorSettings choices, ref Vector3 accent,
        MaterialAppearanceLabels palette, MaterialColorLabels labels, float minimumWidth = 300)
        => DrawColorsCore(id, ref choices, ref accent, palette, labels, new(), null, minimumWidth);

    /// <summary>Uses consumer-owned reset defaults and an automatic seed without replacing Auto's existing palette.</summary>
    public static bool DrawColors(string id, ref MaterialColorSettings choices, ref Vector3 accent,
        MaterialAppearanceLabels palette, MaterialColorLabels labels, MaterialColorSettings defaults,
        Vector3 defaultAccent, float minimumWidth = 300)
        => DrawColorsCore(id, ref choices, ref accent, palette, labels, defaults, defaultAccent, minimumWidth);

    /// <summary>Invokes the consumer's image picker; Browse alone does not request persistence or load a file.</summary>
    public static bool DrawColors(string id, ref MaterialColorSettings choices, ref Vector3 accent,
        MaterialAppearanceLabels palette, MaterialColorLabels labels, MaterialColorSettings defaults,
        Vector3 defaultAccent, Action browseImage, float minimumWidth = 300)
    {
        ArgumentNullException.ThrowIfNull(browseImage);
        return DrawColorsCore(id, ref choices, ref accent, palette, labels, defaults, defaultAccent, minimumWidth, browseImage);
    }

    private static bool DrawColorsCore(string id, ref MaterialColorSettings choices, ref Vector3 accent,
        MaterialAppearanceLabels palette, MaterialColorLabels labels, MaterialColorSettings defaults,
        Vector3? defaultAccent, float minimumWidth, Action? browseImage = null)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(defaults);
        var styles = Has(labels.Glassmorphism) && Has(labels.Spatial) && Has(labels.Cyberpunk);
        var lcarsFields = Has(labels.LcarsBar1) && Has(labels.LcarsBar2) && Has(labels.LcarsBar3);
        var backgroundFields = Has(labels.BackgroundMode) && Has(labels.Waves) && Has(labels.Lights) && Has(labels.Image);
        var blurFields = Has(labels.BackgroundBlur) && Has(labels.BlurPercent);
        var modern = styles || lcarsFields || Has(labels.Reset) || Has(labels.ResetColors) || backgroundFields || blurFields;
        var scale = MaterialTheme.Metrics.Scale;
        var labelWidth = WidestLabel(labels.Background, labels.Controls, labels.Text);
        if (modern)
            labelWidth = Math.Max(labelWidth, WidestLabel(labels.LcarsBar1, labels.LcarsBar2, labels.LcarsBar3,
                labels.GlassWave1, labels.GlassWave2, labels.GlassWave3, labels.GlassLight1, labels.GlassLight2, labels.GlassLight3));
        if (float.IsNegativeInfinity(labelWidth)) throw new InvalidOperationException("Sequence contains no elements");
        var editorWidth = MathF.Ceiling(Math.Max(100 * scale, ImGui.CalcTextSize("#FFFFFF").X + 2 * ImGui.GetStyle().FramePadding.X)
            + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X);
        var resetWidth = Has(labels.Reset) ? MaterialText.Measure(labels.Reset!).X + 2 * ImGui.GetStyle().FramePadding.X : 0;
        var width = Math.Max(minimumWidth * scale, labelWidth + editorWidth + resetWidth + (resetWidth > 0 ? 3 : 2) * ImGui.GetStyle().ItemSpacing.X);
        if (modern)
        {
            foreach (var caption in new[] { labels.ResetColors, labels.ResetWaveColors, labels.Animation, labels.AnimationSpeed, labels.WaveColors,
                labels.BackgroundMode, labels.LightEffect, labels.ImageFile, labels.BrowseImage, labels.LoadImage,
                labels.LightColors, labels.ResetLightColors, labels.BackgroundBlur, labels.BlurPercent })
                if (Has(caption)) width = Math.Max(width, MaterialText.Measure(caption!).X + 2 * ImGui.GetStyle().FramePadding.X);
            foreach (var caption in new[] { labels.Waves, labels.Lights, labels.Image, labels.AmbientGlows, labels.SweepingBeams, labels.PointerGlow })
                if (Has(caption)) width = Math.Max(width, MaterialText.Measure(caption!).X + ImGui.GetFrameHeight() + 4 * ImGui.GetStyle().FramePadding.X);
            width = Math.Min(width, Math.Max(editorWidth, ImGui.GetIO().DisplaySize.X - 2 * ImGui.GetStyle().WindowPadding.X - 40 * scale));
            if ((ImGuiP.GetCurrentWindow().Flags & ImGuiWindowFlags.AlwaysAutoResize) == 0)
                width = Math.Min(width, Math.Max(editorWidth, ImGui.GetContentRegionAvail().X));
        }
        var choiceRight = modern ? ImGui.GetCursorScreenPos().X + width
            : ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        var mode = choices.Mode;
        var committed = false;
        ImGui.PushID(id);
        try
        {
            MaterialText.Text(palette.Color);
            ImGui.Dummy(new(width, 0));
            using (MaterialControls.Push(MaterialControlContext.Toolbar))
            {
                committed |= MaterialSelection.Radio("auto", labels.Auto, MaterialColorMode.Auto, ref mode);
                if (!modern || ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + MaterialText.Measure(labels.Default).X <= choiceRight)
                    ImGui.SameLine();
                using (var subdued = new MaterialStyleScope())
                {
                    subdued.Color(ImGuiCol.Text, MaterialTheme.Current.Colors.OnSurfaceVariant);
                    MaterialText.Text(labels.Default);
                }
                var nextWidth = MaterialText.Measure(labels.Picker).X + MaterialControls.Metrics.Height + 2 * MaterialControls.Metrics.Gap;
                if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + nextWidth
                    <= choiceRight) ImGui.SameLine();
                committed |= MaterialSelection.Radio("picker", labels.Picker, MaterialColorMode.Picker, ref mode);
                if (!string.IsNullOrWhiteSpace(labels.Lcars))
                {
                    var lcarsWidth = MaterialText.Measure(labels.Lcars).X + MaterialControls.Metrics.Height + 2 * MaterialControls.Metrics.Gap;
                    if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + lcarsWidth
                        <= choiceRight) ImGui.SameLine();
                    committed |= MaterialSelection.Radio("lcars", labels.Lcars, MaterialColorMode.Lcars, ref mode);
                }
                if (styles)
                {
                    StyleChoice("glassmorphism", labels.Glassmorphism!, MaterialColorMode.Glassmorphism, choiceRight, ref mode, ref committed);
                    StyleChoice("spatial", labels.Spatial!, MaterialColorMode.Spatial, choiceRight, ref mode, ref committed);
                    StyleChoice("cyberpunk", labels.Cyberpunk!, MaterialColorMode.Cyberpunk, choiceRight, ref mode, ref committed);
                }
            }
            if (mode != choices.Mode) choices = choices with { Mode = mode };
            ImGui.Separator();
            if (mode == MaterialColorMode.Picker || styles && mode is (MaterialColorMode.Glassmorphism or MaterialColorMode.Spatial or MaterialColorMode.Cyberpunk))
            {
                Wrapped(labels.PickerHelp);
                var selected = choices.GetPalette(mode);
                var reset = defaults.GetPalette(mode);
                var background = selected.BackgroundRgb;
                var controls = selected.ControlsRgb;
                var text = selected.TextRgb;
                if (modern)
                {
                    // Retain the original table's native ID scope even when the translated rows need to wrap.
                    if (ImGui.BeginTable("picker-fields", 1,
                        ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.NoPadInnerX, new(width, 0)))
                    {
                        try
                        {
                            ImGui.TableSetupColumn("fields", ImGuiTableColumnFlags.WidthFixed, width);
                            ImGui.TableNextRow(); ImGui.TableNextColumn();
                            var fieldWidth = width;
                            committed |= DrawRgbField("background", labels.Background, ref background, reset.BackgroundRgb, labels.Reset, fieldWidth, labelWidth, editorWidth);
                            ImGui.TableNextRow(); ImGui.TableNextColumn();
                            committed |= DrawRgbField("controls", labels.Controls, ref controls, reset.ControlsRgb, labels.Reset, fieldWidth, labelWidth, editorWidth);
                            ImGui.TableNextRow(); ImGui.TableNextColumn();
                            committed |= DrawRgbField("text", labels.Text, ref text, reset.TextRgb, labels.Reset, fieldWidth, labelWidth, editorWidth);
                        }
                        finally { ImGui.EndTable(); }
                    }
                }
                else if (ImGui.BeginTable("picker-fields", 2, ImGuiTableFlags.SizingStretchProp, new(width, 0)))
                {
                    try
                    {
                        ImGui.TableSetupColumn("labels", ImGuiTableColumnFlags.WidthStretch, 1);
                        ImGui.TableSetupColumn("colours", ImGuiTableColumnFlags.WidthFixed, editorWidth);
                        committed |= Field("background", labels.Background, ref background);
                        committed |= Field("controls", labels.Controls, ref controls);
                        committed |= Field("text", labels.Text, ref text);
                    }
                    finally { ImGui.EndTable(); }
                }
                if (ResetButton("reset-colours", labels.ResetColors, (background, controls, text) == reset, width))
                { (background, controls, text) = reset; committed = true; }
                if ((background, controls, text) != selected) choices = choices.WithPalette(mode, background, controls, text);
                Preview(background, controls, text, labels, width);
                Wrapped(labels.PickerHint);
                if (mode == MaterialColorMode.Glassmorphism)
                    committed |= DrawGlass(ref choices, defaults, labels, width, labelWidth, editorWidth, browseImage);
            }
            else if (mode == MaterialColorMode.Lcars)
            {
                if (!string.IsNullOrWhiteSpace(labels.LcarsHelp)) Wrapped(labels.LcarsHelp);
                if (lcarsFields)
                {
                    var background = choices.LcarsBackgroundRgb;
                    var bar1 = choices.LcarsBar1Rgb;
                    var bar2 = choices.LcarsBar2Rgb;
                    var bar3 = choices.LcarsBar3Rgb;
                    committed |= RgbField("lcars-background", labels.Background, ref background, defaults.LcarsBackgroundRgb);
                    committed |= RgbField("lcars-bar1", labels.LcarsBar1!, ref bar1, defaults.LcarsBar1Rgb);
                    committed |= RgbField("lcars-bar2", labels.LcarsBar2!, ref bar2, defaults.LcarsBar2Rgb);
                    committed |= RgbField("lcars-bar3", labels.LcarsBar3!, ref bar3, defaults.LcarsBar3Rgb);
                    if (ResetButton("reset-colours", labels.ResetColors,
                            (background, bar1, bar2, bar3) == (defaults.LcarsBackgroundRgb, defaults.LcarsBar1Rgb, defaults.LcarsBar2Rgb, defaults.LcarsBar3Rgb), width))
                    {
                        (background, bar1, bar2, bar3) = (defaults.LcarsBackgroundRgb, defaults.LcarsBar1Rgb, defaults.LcarsBar2Rgb, defaults.LcarsBar3Rgb);
                        committed = true;
                    }
                    if ((background, bar1, bar2, bar3) != (choices.LcarsBackgroundRgb, choices.LcarsBar1Rgb, choices.LcarsBar2Rgb, choices.LcarsBar3Rgb))
                        choices = choices with { LcarsBackgroundRgb = background, LcarsBar1Rgb = bar1, LcarsBar2Rgb = bar2, LcarsBar3Rgb = bar3 };
                }
                MaterialLcars.Header(labels.Lcars ?? "LCARS");
            }
            else
            {
                Wrapped(labels.AutoHelp);
                MaterialText.Text(labels.ThemeColor);
            }
        }
        finally { ImGui.PopID(); }
        if (mode == MaterialColorMode.Auto)
        {
            committed |= DrawPalette(id, ref accent, palette with { Color = string.Empty });
            if (defaultAccent is { } automatic)
            {
                ImGui.PushID(id);
                try
                {
                    if (ResetButton("reset-colours", labels.ResetColors, MaterialColorSettings.ToRgb(accent) == MaterialColorSettings.ToRgb(automatic), width))
                    { accent = automatic; committed = true; }
                }
                finally { ImGui.PopID(); }
            }
            Wrapped(labels.AutoHint);
        }
        if (blurFields)
        {
            ImGui.PushID(id);
            try { committed |= DrawBackgroundBlur(ref choices, labels, width); }
            finally { ImGui.PopID(); }
        }
        return committed;

        bool RgbField(string fieldId, string label, ref uint rgb, uint defaultRgb)
            => DrawRgbField(fieldId, label, ref rgb, defaultRgb, labels.Reset, width, labelWidth, editorWidth);

        bool Field(string fieldId, string label, ref uint rgb)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); MaterialText.Text(label);
            ImGui.TableNextColumn();
            var color = MaterialColorSettings.Rgb(rgb);
            ImGui.SetNextItemWidth(editorWidth);
            var style = ImGui.GetStyle();
            var oldPosition = style.ColorButtonPosition;
            bool edited;
            try
            {
                style.ColorButtonPosition = ImGuiDir.Left;
                edited = ImGui.ColorEdit3("##" + fieldId, ref color,
                    ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.NoOptions);
            }
            finally { style.ColorButtonPosition = oldPosition; }
            if (edited) rgb = MaterialColorSettings.ToRgb(color);
            return ImGui.IsItemDeactivatedAfterEdit();
        }

        void Wrapped(string text)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
            try { MaterialText.Text(text); }
            finally { ImGui.PopTextWrapPos(); }
        }
    }

    private static bool Has(string? label) => !string.IsNullOrWhiteSpace(label);
    /// <summary>Widest present label, or negative infinity when none is present.</summary>
    private static float WidestLabel(params ReadOnlySpan<string?> labels)
    {
        var width = float.NegativeInfinity;
        foreach (var label in labels)
            if (Has(label)) width = Math.Max(width, MaterialText.Measure(label!).X);
        return width;
    }

    private static void StyleChoice(string id, string label, MaterialColorMode option, float right, ref MaterialColorMode mode, ref bool committed)
    {
        var nextWidth = MaterialText.Measure(label).X + MaterialControls.Metrics.Height + 2 * MaterialControls.Metrics.Gap;
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + nextWidth
            <= right) ImGui.SameLine();
        committed |= MaterialSelection.Radio(id, label, option, ref mode);
    }

    private static bool ResetButton(string id, string? label, bool atDefault, float width)
    {
        if (!Has(label)) return false;
        ImGui.BeginDisabled(atDefault);
        try { return CaptionButton(id, label!, width); }
        finally { ImGui.EndDisabled(); }
    }

    private static bool CaptionButton(string id, string label, float width, bool small = false)
    {
        var padding = ImGui.GetStyle().FramePadding;
        // Native glyph bounds can extend slightly beyond the nominal line height (for example Cyrillic descenders).
        if (small) padding.Y = MaterialTheme.Metrics.Scale;
        var available = Math.Max(1, width);
        var wrap = Math.Max(1, available - 2 * padding.X);
        var text = MaterialText.Measure(label, wrapWidth: wrap);
        var size = new Vector2(Math.Min(available, text.X + 2 * padding.X), text.Y + 2 * padding.Y);
        // The native button owns input and decoration; the caption also supports shaped and wrapped translations.
        var pressed = ImGui.Button("###" + id, size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var list = ImGui.GetWindowDrawList();
        list.PushClipRect(min, max, true);
        try { MaterialText.AddText(list, min + (size - text) * .5f, ImGui.GetColorU32(ImGuiCol.Text), label, wrap); }
        finally { list.PopClipRect(); }
        return pressed;
    }

    private static bool DrawRgbField(string id, string label, ref uint rgb, uint defaultRgb,
        string? resetLabel, float width, float labelWidth, float editorWidth)
    {
        var start = ImGui.GetCursorPosX();
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var resetWidth = Has(resetLabel) ? MaterialText.Measure(resetLabel!).X + 2 * ImGui.GetStyle().FramePadding.X : 0;
        ImGui.SetCursorPosX(start + MaterialTheme.Metrics.Scale);
        ImGui.PushTextWrapPos(start + width);
        try { MaterialText.Text(label); }
        finally { ImGui.PopTextWrapPos(); }
        if (width >= labelWidth + gap + editorWidth + (resetWidth > 0 ? gap + resetWidth : 0))
        { ImGui.SameLine(); ImGui.SetCursorPosX(start + labelWidth + gap); }
        var available = width - (ImGui.GetCursorPosX() - start);
        var inlineReset = resetWidth > 0 && available >= editorWidth + gap + resetWidth;
        ImGui.SetNextItemWidth(Math.Min(editorWidth, available - (inlineReset ? gap + resetWidth : 0)));
        var color = MaterialColorSettings.Rgb(rgb);
        var style = ImGui.GetStyle();
        var oldPosition = style.ColorButtonPosition;
        bool edited;
        try
        {
            style.ColorButtonPosition = ImGuiDir.Left;
            edited = ImGui.ColorEdit3("##" + id, ref color,
                ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.NoOptions);
        }
        finally { style.ColorButtonPosition = oldPosition; }
        if (edited) rgb = MaterialColorSettings.ToRgb(color);
        var completed = ImGui.IsItemDeactivatedAfterEdit();
        if (resetWidth > 0)
        {
            if (inlineReset) ImGui.SameLine();
            ImGui.BeginDisabled(rgb == defaultRgb);
            try
            {
                if (CaptionButton("reset-" + id, resetLabel!, inlineReset ? resetWidth : width, small: true))
                { rgb = defaultRgb; completed = true; }
            }
            finally { ImGui.EndDisabled(); }
        }
        return completed;
    }

    private static bool DrawGlass(ref MaterialColorSettings choices, MaterialColorSettings defaults,
        MaterialColorLabels labels, float width, float labelWidth, float editorWidth, Action? browseImage)
    {
        var committed = false;
        var backgrounds = Has(labels.BackgroundMode) && Has(labels.Waves) && Has(labels.Lights) && Has(labels.Image);
        var background = backgrounds ? choices.GlassBackground : MaterialGlassBackground.Waves;
        if (backgrounds)
        {
            WrappedCaption(labels.BackgroundMode!, width);
            var options = new MaterialOptions<MaterialGlassBackground>([
                new("waves", MaterialGlassBackground.Waves, labels.Waves!),
                new("lights", MaterialGlassBackground.Lights, labels.Lights!),
                new("image", MaterialGlassBackground.Image, labels.Image!),
            ]);
            if (MaterialCombo.Draw("glass-background", options, ref background, new MaterialComboLayout(0, 3),
                width / MaterialTheme.Metrics.Scale, searchable: false, emptyLabel: labels.Waves!))
            { choices = choices with { GlassBackground = background }; committed = true; }
        }
        if (background == MaterialGlassBackground.Image)
            return DrawGlassImage(ref choices, labels, width, browseImage) || committed;
        if (background == MaterialGlassBackground.Lights && Has(labels.LightEffect) &&
            Has(labels.AmbientGlows) && Has(labels.SweepingBeams) && Has(labels.PointerGlow))
        {
            WrappedCaption(labels.LightEffect!, width);
            var effect = choices.GlassLights;
            var options = new MaterialOptions<MaterialGlassLights>([
                new("ambient", MaterialGlassLights.AmbientGlows, labels.AmbientGlows!),
                new("sweeping", MaterialGlassLights.SweepingBeams, labels.SweepingBeams!),
                new("pointer", MaterialGlassLights.PointerGlow, labels.PointerGlow!),
            ]);
            if (MaterialCombo.Draw("glass-lights", options, ref effect, new MaterialComboLayout(0, 3),
                width / MaterialTheme.Metrics.Scale, searchable: false, emptyLabel: labels.AmbientGlows!))
            { choices = choices with { GlassLights = effect }; committed = true; }
        }
        if (Has(labels.Animation))
        {
            var animation = choices.GlassAnimation;
            if (MaterialSelection.Checkbox("glass-animation", labels.Animation!, ref animation))
            { choices = choices with { GlassAnimation = animation }; committed = true; }
        }
        if (Has(labels.AnimationSpeed))
        {
            ImGui.BeginDisabled(!choices.GlassAnimation);
            try
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
                try { MaterialText.Text(labels.AnimationSpeed!); }
                finally { ImGui.PopTextWrapPos(); }
                var inputWidth = Math.Max(110 * MaterialTheme.Metrics.Scale,
                    ImGui.CalcTextSize("10.00").X + 2 * ImGui.GetStyle().FramePadding.X);
                inputWidth = Math.Min(inputWidth, width);
                var gap = ImGui.GetStyle().ItemSpacing.X;
                var resetWidth = Has(labels.Reset) ? MaterialText.Measure(labels.Reset!).X + 2 * ImGui.GetStyle().FramePadding.X : 0;
                var inlineReset = resetWidth > 0 && width >= inputWidth + gap + resetWidth;
                var speed = choices.GlassAnimationSpeed;
                ImGui.SetNextItemWidth(inputWidth);
                var edited = ImGui.InputFloat("##glass-animation-speed", ref speed, format: "%.2f");
                committed |= ImGui.IsItemDeactivatedAfterEdit();
                if (edited) choices = choices with { GlassAnimationSpeed = speed };
                if (resetWidth > 0)
                {
                    if (inlineReset) ImGui.SameLine();
                    ImGui.BeginDisabled(choices.GlassAnimationSpeed == defaults.GlassAnimationSpeed);
                    try
                    {
                        if (CaptionButton("reset-glass-animation-speed", labels.Reset!, inlineReset ? resetWidth : width, small: true))
                        { choices = choices with { GlassAnimationSpeed = defaults.GlassAnimationSpeed }; committed = true; }
                    }
                    finally { ImGui.EndDisabled(); }
                }
            }
            finally { ImGui.EndDisabled(); }
        }
        if (background == MaterialGlassBackground.Lights && Has(labels.GlassLight1) && Has(labels.GlassLight2) && Has(labels.GlassLight3))
        {
            ImGui.Spacing();
            if (Has(labels.LightColors)) WrappedCaption(labels.LightColors!, width);
            var light1 = choices.GlassLight1Rgb;
            var light2 = choices.GlassLight2Rgb;
            var light3 = choices.GlassLight3Rgb;
            committed |= DrawRgbField("glass-light1", labels.GlassLight1!, ref light1, defaults.GlassLight1Rgb, labels.Reset, width, labelWidth, editorWidth);
            committed |= DrawRgbField("glass-light2", labels.GlassLight2!, ref light2, defaults.GlassLight2Rgb, labels.Reset, width, labelWidth, editorWidth);
            committed |= DrawRgbField("glass-light3", labels.GlassLight3!, ref light3, defaults.GlassLight3Rgb, labels.Reset, width, labelWidth, editorWidth);
            if (ResetButton("reset-light-colours", labels.ResetLightColors,
                    (light1, light2, light3) == (defaults.GlassLight1Rgb, defaults.GlassLight2Rgb, defaults.GlassLight3Rgb), width))
            { (light1, light2, light3) = (defaults.GlassLight1Rgb, defaults.GlassLight2Rgb, defaults.GlassLight3Rgb); committed = true; }
            if ((light1, light2, light3) != (choices.GlassLight1Rgb, choices.GlassLight2Rgb, choices.GlassLight3Rgb))
                choices = choices with { GlassLight1Rgb = light1, GlassLight2Rgb = light2, GlassLight3Rgb = light3 };
        }
        if (background == MaterialGlassBackground.Waves && Has(labels.GlassWave1) && Has(labels.GlassWave2) && Has(labels.GlassWave3))
        {
            ImGui.Spacing();
            if (Has(labels.WaveColors)) MaterialText.Text(labels.WaveColors!);
            var wave1 = choices.GlassWave1Rgb;
            var wave2 = choices.GlassWave2Rgb;
            var wave3 = choices.GlassWave3Rgb;
            committed |= DrawRgbField("glass-wave1", labels.GlassWave1!, ref wave1, defaults.GlassWave1Rgb, labels.Reset, width, labelWidth, editorWidth);
            committed |= DrawRgbField("glass-wave2", labels.GlassWave2!, ref wave2, defaults.GlassWave2Rgb, labels.Reset, width, labelWidth, editorWidth);
            committed |= DrawRgbField("glass-wave3", labels.GlassWave3!, ref wave3, defaults.GlassWave3Rgb, labels.Reset, width, labelWidth, editorWidth);
            if (ResetButton("reset-wave-colours", labels.ResetWaveColors,
                    (wave1, wave2, wave3) == (defaults.GlassWave1Rgb, defaults.GlassWave2Rgb, defaults.GlassWave3Rgb), width))
            { (wave1, wave2, wave3) = (defaults.GlassWave1Rgb, defaults.GlassWave2Rgb, defaults.GlassWave3Rgb); committed = true; }
            if ((wave1, wave2, wave3) != (choices.GlassWave1Rgb, choices.GlassWave2Rgb, choices.GlassWave3Rgb))
                choices = choices with { GlassWave1Rgb = wave1, GlassWave2Rgb = wave2, GlassWave3Rgb = wave3 };
        }
        return committed;
    }

    private static bool DrawGlassImage(ref MaterialColorSettings choices, MaterialColorLabels labels, float width, Action? browseImage)
    {
        var committed = false;
        if (Has(labels.ImageFile))
        {
            WrappedCaption(labels.ImageFile!, width);
            var path = choices.GlassImagePath ?? string.Empty;
            ImGui.SetNextItemWidth(width);
            if (MaterialShapedInput.SingleLine("##glass-image-path", string.Empty, ref path, 4096))
                choices = choices with { GlassImagePath = path };
            committed |= ImGui.IsItemDeactivatedAfterEdit();
        }
        var browse = browseImage != null && Has(labels.BrowseImage);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var padding = 2 * ImGui.GetStyle().FramePadding.X;
        var browseWidth = browse ? MaterialText.Measure(labels.BrowseImage!).X + padding : 0;
        var loadWidth = Has(labels.LoadImage) ? MaterialText.Measure(labels.LoadImage!).X + padding : 0;
        var inline = browse && loadWidth > 0 && browseWidth + gap + loadWidth <= width;
        if (browse && CaptionButton("browse-glass-image", labels.BrowseImage!, inline ? browseWidth : width)) browseImage!();
        if (Has(labels.LoadImage))
        {
            if (inline) ImGui.SameLine();
            if (CaptionButton("load-glass-image", labels.LoadImage!, inline ? loadWidth : width)) committed = true;
        }
        if (Has(labels.ImageHelp)) WrappedCaption(labels.ImageHelp!, width);
        return committed;
    }

    private static bool DrawBackgroundBlur(ref MaterialColorSettings choices, MaterialColorLabels labels, float width)
    {
        ImGui.Separator();
        var enabled = choices.HasBackgroundBlur(choices.Mode);
        var committed = MaterialSelection.Checkbox("##background-blur", labels.BackgroundBlur!, ref enabled, width);
        if (committed) choices = choices.WithBackgroundBlur(choices.Mode, enabled);
        ImGui.BeginDisabled(!enabled);
        try
        {
            WrappedCaption(labels.BlurPercent!, width);
            var percent = choices.BackgroundBlurPercent;
            ImGui.SetNextItemWidth(width);
            if (ImGui.SliderInt("##background-blur-percent", ref percent, 0, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp))
                choices = choices with { BackgroundBlurPercent = percent };
            committed |= ImGui.IsItemDeactivatedAfterEdit();
        }
        finally { ImGui.EndDisabled(); }
        return committed;
    }

    private static void WrappedCaption(string caption, float width)
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        try { MaterialText.Text(caption); }
        finally { ImGui.PopTextWrapPos(); }
    }

    private static void Preview(uint backgroundRgb, uint controlsRgb, uint textRgb, MaterialColorLabels labels, float width)
    {
        var scale = MaterialTheme.Metrics.Scale;
        var textSize = MaterialText.Measure(labels.PreviewText);
        var controlSize = MaterialText.Measure(labels.PreviewControl);
        var padding = 10 * scale;
        var height = Math.Max(textSize.Y, controlSize.Y) + 2 * padding;
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new(width, height));
        var list = ImGui.GetWindowDrawList();
        var text = new Vector4(MaterialColorSettings.Rgb(textRgb), 1);
        list.AddRectFilled(origin, origin + new Vector2(width, height),
            MaterialCanvas.Color(new(MaterialColorSettings.Rgb(backgroundRgb), 1)), 4 * scale);
        list.AddRect(origin, origin + new Vector2(width, height), MaterialCanvas.Color(MaterialTheme.Current.Colors.OutlineVariant), 4 * scale);
        MaterialText.AddText(list, origin + new Vector2(padding, (height - textSize.Y) / 2), MaterialCanvas.Color(text), labels.PreviewText);
        var buttonMin = origin + new Vector2(width - controlSize.X - 3 * padding, padding / 2);
        list.AddRectFilled(buttonMin, origin + new Vector2(width - padding / 2, height - padding / 2),
            MaterialCanvas.Color(new(MaterialColorSettings.Rgb(controlsRgb), 1)), 4 * scale);
        MaterialText.AddText(list, buttonMin + new Vector2(padding, (height - padding - controlSize.Y) / 2),
            MaterialCanvas.Color(text), labels.PreviewControl);
    }
}
