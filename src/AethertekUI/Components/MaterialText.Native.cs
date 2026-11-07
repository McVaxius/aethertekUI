using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

public static partial class MaterialText
{
    /// <summary>Replace only a retained native widget's caption, keeping its identity and decoration.</summary>
    public static void ReplaceNativeCaption(ImDrawListPtr list, int firstVertex, ImFontPtr nativeFont,
        string nativeLabel, Vector2 boundsMin, Vector2 boundsMax, Vector2 textPosition,
        string display, uint color)
    {
        ArgumentNullException.ThrowIfNull(nativeLabel);
        ArgumentNullException.ThrowIfNull(display);
        if (firstVertex < 0 || firstVertex > list.VtxBuffer.Size)
            throw new ArgumentOutOfRangeException(nameof(firstVertex));
        MaterialNativeCaption.HideGlyphs(list, firstVertex, nativeFont,
            MaterialNativeCaption.VisibleSpan(nativeLabel), boundsMin, boundsMax);
        list.PushClipRect(boundsMin, boundsMax, true);
        try { AddText(list, textPosition, color, display); }
        finally { list.PopClipRect(); }
    }

    /// <summary>Reserves natural shaped line height without changing widths or native IDs.</summary>
    public static MaterialStyleScope PushLineHeight(params string[] text) => PushLineHeight((ReadOnlySpan<string>)text);

    /// <inheritdoc cref="PushLineHeight(string[])"/>
    public static MaterialStyleScope PushLineHeight(params ReadOnlySpan<string> text)
    {
        var scope = new MaterialStyleScope();
        if (MaterialTextRenderer.Current is not null)
            ApplyLineHeight(ref scope, ShapedLineHeight(text));
        return scope;
    }

    /// <summary>Tallest shaped caption, or the native line height when none is taller.</summary>
    internal static float ShapedLineHeight(ReadOnlySpan<string> text)
    {
        var height = ImGui.GetTextLineHeight();
        foreach (var value in text)
            if (RequiresShaping(value)) height = Math.Max(height, Measure(value).Y);
        return height;
    }

    internal static void ApplyLineHeight(ref MaterialStyleScope scope, float height)
    {
        var line = ImGui.GetTextLineHeight();
        if (height <= line) return;
        var padding = ImGui.GetStyle().FramePadding;
        scope.Style(ImGuiStyleVar.FramePadding, new Vector2(padding.X, padding.Y + (height - line) * .5f));
    }

    public static void TextWrapped(string text)
    {
        if (MaterialTextRenderer.Current is null || !RequiresShaping(text)) { ImGui.TextWrapped(text); return; }
        Text(text, Math.Max(1, ImGui.GetContentRegionAvail().X));
    }

    public static void TextColored(Vector4 color, string text)
    {
        if (MaterialTextRenderer.Current is null || !RequiresShaping(text)) { ImGui.TextColored(color, text); return; }
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        try { Text(text); }
        finally { ImGui.PopStyleColor(); }
    }

    public static void TextDisabled(string text)
    {
        if (MaterialTextRenderer.Current is null || !RequiresShaping(text)) { ImGui.TextDisabled(text); return; }
        TextColored(ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], text);
    }

    public static void BulletText(string text)
    {
        if (MaterialTextRenderer.Current is null || !RequiresShaping(text)) { ImGui.BulletText(text); return; }
        ImGui.BeginGroup();
        try { ImGui.Bullet(); ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X); Text(text); }
        finally { ImGui.EndGroup(); }
    }

    public static void SetTooltip(string text)
    {
        if (MaterialTextRenderer.Current is null || !RequiresShaping(text)) { ImGui.SetTooltip(text); return; }
        ImGui.BeginTooltip();
        try { Text(text); }
        finally { ImGui.EndTooltip(); }
    }

    public static bool CollapsingHeader(string nativeLabel, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        if (!MaterialNativeCaption.IsShaped(MaterialNativeCaption.VisibleSpan(nativeLabel))) return ImGui.CollapsingHeader(nativeLabel, flags);
        var caption = MaterialNativeCaption.Visible(nativeLabel);
        using var height = PushLineHeight(caption);
        var origin = ImGui.GetCursorScreenPos();
        var list = ImGui.GetWindowDrawList();
        var first = list.VtxBuffer.Size;
        var font = ImGui.GetFont();
        var padding = ImGui.GetStyle().FramePadding;
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        var size = Measure(caption);
        var open = ImGui.CollapsingHeader(nativeLabel, flags);
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        if (ImGui.IsItemVisible())
        {
            MaterialNativeCaption.HideGlyphs(list, first, font, caption, min, max);
            list.PushClipRect(min, max, true);
            try { AddText(list, origin + new Vector2(ImGui.GetFontSize() + 3 * padding.X, (max.Y - min.Y - size.Y) * .5f), color, caption); }
            finally { list.PopClipRect(); }
        }
        return open;
    }

    /// <summary>Retains the supplied native tree label/ID and arrow. The caller owns TreePop when opened.</summary>
    public static bool TreeNode(string nativeLabel, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None, string? display = null)
    {
        var nativeCaption = MaterialNativeCaption.VisibleSpan(nativeLabel);
        if (!(display is null ? MaterialNativeCaption.IsShaped(nativeCaption) : MaterialNativeCaption.IsShaped(display)))
            return ImGui.TreeNodeEx(nativeLabel, flags);
        var caption = display ?? MaterialNativeCaption.Visible(nativeLabel);
        var origin = ImGui.GetCursorScreenPos();
        var list = ImGui.GetWindowDrawList();
        var first = list.VtxBuffer.Size;
        var font = ImGui.GetFont();
        var measured = Measure(caption);
        var window = ImGuiP.GetCurrentWindow();
        var padding = ImGui.GetStyle().FramePadding;
        var framed = (flags & ImGuiTreeNodeFlags.Framed) != 0;
        // Retain the existing vertical padding around taller shaped ink, even on an
        // unframed row: native zero-baseline rows otherwise crop its top coverage.
        var nativePaddingY = padding.Y;
        using var height = PushLineHeight(caption);
        if (!window.SkipItems)
        {
            window.DC.CurrLineSize.Y = Math.Max(window.DC.CurrLineSize.Y, measured.Y + 2 * nativePaddingY);
            window.DC.CurrLineTextBaseOffset += Math.Max(0, measured.Y - ImGui.GetFontSize()) * .5f;
        }
        var open = ImGui.TreeNodeEx(nativeLabel, flags);
        try
        {
            var bounds = ImGui.GetCurrentContext().LastItemData.DisplayRect;
            if (ImGui.IsItemVisible())
            {
                MaterialNativeCaption.HideGlyphs(list, first, font, nativeCaption, bounds.Min, bounds.Max);
                list.PushClipRect(bounds.Min, bounds.Max, true);
                try { AddText(list, origin + new Vector2(ImGui.GetFontSize() + padding.X * (framed ? 3 : 2),
                    (bounds.Max.Y - bounds.Min.Y - measured.Y) * .5f), ImGui.GetColorU32(ImGuiCol.Text), caption); }
                finally { list.PopClipRect(); }
            }
        }
        catch
        {
            if (open && (flags & ImGuiTreeNodeFlags.NoTreePushOnOpen) == 0) ImGui.TreePop();
            throw;
        }
        return open;
    }

    /// <summary>Retains native table sorting, header identity and arrows; the owner reserves its row height.</summary>
    public static void TableHeader(string nativeLabel, string? display = null)
    {
        var nativeCaption = MaterialNativeCaption.VisibleSpan(nativeLabel);
        if (!(display is null ? MaterialNativeCaption.IsShaped(nativeCaption) : MaterialNativeCaption.IsShaped(display)))
        { ImGui.TableHeader(nativeLabel); return; }
        var caption = display ?? MaterialNativeCaption.Visible(nativeLabel);
        var origin = ImGui.GetCursorScreenPos();
        var list = ImGui.GetWindowDrawList();
        var first = list.VtxBuffer.Size;
        var font = ImGui.GetFont();
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        ImGui.TableHeader(nativeLabel);
        if (!ImGui.IsItemVisible()) return;
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        MaterialNativeCaption.HideGlyphs(list, first, font, nativeCaption, min, max);
        list.PushClipRect(min, max, true);
        try { AddText(list, origin, color, caption); }
        finally { list.PopClipRect(); }
    }

    /// <summary>Retains native combo identity and arrow; the caller retains popup ownership.</summary>
    public static bool BeginCombo(string nativeLabel, string preview, ImGuiComboFlags flags = ImGuiComboFlags.None)
    {
        var shapedLabel = MaterialNativeCaption.IsShaped(MaterialNativeCaption.VisibleSpan(nativeLabel));
        var shapedPreview = (flags & ImGuiComboFlags.NoPreview) == 0
            && MaterialNativeCaption.IsShaped(MaterialNativeCaption.VisibleSpan(preview));
        if (!shapedLabel && !shapedPreview)
            return ImGui.BeginCombo(nativeLabel, preview, flags);
        var caption = shapedLabel ? MaterialNativeCaption.Visible(nativeLabel) : "";
        var visiblePreview = shapedPreview ? MaterialNativeCaption.Visible(preview) : "";
        using var height = PushLineHeight(caption, visiblePreview);
        var origin = ImGui.GetCursorScreenPos();
        var window = ImGuiP.GetCurrentWindow();
        var drawList = ImGui.GetWindowDrawList();
        var frameHeight = ImGui.GetFrameHeight();
        var width = (flags & ImGuiComboFlags.NoPreview) != 0 ? frameHeight : ImGui.CalcItemWidth();
        var padding = ImGui.GetStyle().FramePadding;
        var labelGap = ImGui.GetStyle().ItemInnerSpacing.X;
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        var textSize = shapedPreview ? Measure(visiblePreview) : default;
        var labelSize = shapedLabel ? Measure(caption) : default;
        var first = drawList.VtxBuffer.Size;
        var open = ImGui.BeginCombo(nativeLabel, shapedPreview ? "" : preview, flags);
        try
        {
        if (shapedPreview)
        {
            var rightInset = (flags & ImGuiComboFlags.NoArrowButton) == 0 ? frameHeight : padding.X;
            drawList.PushClipRect(origin,
                origin + new Vector2(Math.Max(padding.X, width - rightInset), frameHeight), true);
            try { AddText(drawList, font, fontSize, origin + new Vector2(padding.X, (frameHeight - textSize.Y) * .5f), color, visiblePreview); }
            finally { drawList.PopClipRect(); }
        }
        if (shapedLabel)
        {
            var labelOrigin = origin + new Vector2(width + labelGap, 0);
            MaterialNativeCaption.HideGlyphs(drawList, first, font, caption, labelOrigin,
                new Vector2(float.MaxValue, origin.Y + frameHeight));
            AddText(drawList, font, fontSize, labelOrigin + new Vector2(0, (frameHeight - labelSize.Y) * .5f), color, caption);
            window.DC.CursorMaxPos.X = Math.Max(window.DC.CursorMaxPos.X, labelOrigin.X + labelSize.X);
        }
        }
        catch
        {
            if (open) ImGui.EndCombo();
            throw;
        }
        return open;
    }

    /// <summary>Retains the original native selectable label/ID while shaping its visible caption.</summary>
    public static bool Selectable(string nativeLabel, bool selected = false,
        ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, Vector2 size = default, string? display = null)
    {
        // Without a display caption it is the visible native caption, so only shaping can differ.
        var visible = MaterialNativeCaption.VisibleSpan(nativeLabel);
        if (display is null ? !MaterialNativeCaption.IsShaped(visible)
            : !MaterialNativeCaption.IsShaped(display) && visible.SequenceEqual(display))
            return ImGui.Selectable(nativeLabel, selected, flags, size);
        display ??= MaterialNativeCaption.Visible(nativeLabel);
        var textSize = Measure(display);
        size.Y = Math.Max(size.Y, textSize.Y);
        var origin = ImGui.GetCursorScreenPos();
        var alignmentWidth = size.X > 0 ? size.X : Math.Max(ImGui.CalcTextSize(nativeLabel, true).X, ImGui.GetContentRegionAvail().X);
        var drawList = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        if ((flags & ImGuiSelectableFlags.Disabled) != 0
            && (ImGui.GetCurrentContext().CurrentItemFlags & ImGuiItemFlags.Disabled) == 0)
            color = (color & 0x00ffffff) | ((uint)MathF.Round((color >> 24) * ImGui.GetStyle().DisabledAlpha) << 24);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        bool pressed;
        try { pressed = ImGui.Selectable(nativeLabel, selected, flags, size); }
        finally { ImGui.PopStyleColor(); }
        if (ImGui.IsItemVisible())
        {
            var bounds = ImGui.GetItemRectMax();
            var align = ImGui.GetStyle().SelectableTextAlign;
            var position = origin + new Vector2(Math.Max(0, alignmentWidth - textSize.X) * align.X,
                Math.Max(0, size.Y - textSize.Y) * align.Y);
            drawList.PushClipRect(ImGui.GetItemRectMin(), bounds, true);
            try { AddText(drawList, position, color, display); }
            finally { drawList.PopClipRect(); }
        }
        return pressed;
    }
}
