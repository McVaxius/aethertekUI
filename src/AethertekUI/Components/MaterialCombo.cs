using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AethertekUI;

/// <summary>Popup width is logical units; zero fits the field with a 200-unit minimum. Field width is independent.</summary>
public readonly record struct MaterialComboLayout(float PopupWidth = 0, int VisibleRows = 8, bool ConstrainFieldWidth = true)
{
    public MaterialComboLayout() : this(0,8,true) { }
    public static MaterialComboLayout Default => new(0,8,true);
    public MaterialIcon? LeadingIcon { get; init; }
}

public static class MaterialCombo
{
    public static bool Draw<T>(string id, MaterialOptions<T> options, ref T value, float width = -1,
        bool searchable = true, bool disabled = false, string emptyLabel = "Select...")
        => Draw(id,options,ref value,MaterialComboLayout.Default,width,searchable,disabled,emptyLabel);

    public static bool Draw<T>(string id, MaterialOptions<T> options, ref T value, MaterialComboLayout layout, float width = -1,
        bool searchable = true, bool disabled = false, string emptyLabel = "Select...")
        => DrawCore(id,options,ref value,layout,width,searchable,disabled,emptyLabel,null,"Filter options...","No matching options");

    public static bool Draw<T>(string id, MaterialOptions<T> options, ref T value, MaterialControlAppearance appearance,
        float width = -1, bool searchable = true, bool disabled = false, string emptyLabel = "Select...",
        string searchHint = "Filter options...", string noOptionsLabel = "No matching options")
        => DrawCore(id,options,ref value,MaterialComboLayout.Default,width,searchable,disabled,emptyLabel,appearance,searchHint,noOptionsLabel);

    public static bool Draw<T>(string id, MaterialOptions<T> options, ref T value, MaterialComboLayout layout,
        MaterialControlAppearance appearance, float width = -1, bool searchable = true, bool disabled = false,
        string emptyLabel = "Select...", string searchHint = "Filter options...", string noOptionsLabel = "No matching options")
        => DrawCore(id,options,ref value,layout,width,searchable,disabled,emptyLabel,appearance,searchHint,noOptionsLabel);

    private static bool DrawCore<T>(string id, MaterialOptions<T> options, ref T value, MaterialComboLayout layout, float width,
        bool searchable, bool disabled, string emptyLabel, MaterialControlAppearance? appearance, string searchHint, string noOptionsLabel)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!float.IsFinite(layout.PopupWidth) || layout.PopupWidth < 0 || layout.VisibleRows < 1)
            throw new ArgumentOutOfRangeException(nameof(layout));
        if(appearance is { } validated) validated.Radius(MaterialTheme.Metrics.Scale);
        using var controls = MaterialControls.Push();
        var scale = MaterialTheme.Metrics.Scale;
        var fieldWidth = MaterialLayout.ResolveWidth(width,200*scale,ImGui.GetContentRegionAvail().X,scale,layout.ConstrainFieldWidth);
        var popupWidth = Math.Min(Math.Max(1,ImGui.GetIO().DisplaySize.X-2*ImGui.GetStyle().WindowPadding.X),
            layout.PopupWidth == 0 ? Math.Max(fieldWidth,200*scale) : Math.Max(fieldWidth,layout.PopupWidth*scale));
        var popupHeight = ImGui.GetTextLineHeightWithSpacing()*layout.VisibleRows + 2*ImGui.GetStyle().WindowPadding.Y
            + (searchable ? MaterialControls.Metrics.Height + ImGui.GetStyle().ItemSpacing.Y : 0);
        ImGui.PushID(id);
        ImGui.BeginDisabled(disabled);
        ImGui.SetNextItemWidth(fieldWidth);
        ImGui.SetNextWindowSizeConstraints(new(popupWidth,0),new(popupWidth,popupHeight));
        var changed = false;
        var origin=ImGui.GetCursorScreenPos();
        var dl=ImGui.GetWindowDrawList();
        var preview=options.LabelFor(value,emptyLabel);
        var shapedPreview=MaterialNativeCaption.IsShaped(MaterialNativeCaption.VisibleSpan(preview));
        var previewSize=MaterialText.Measure(preview);
        var previewFont=ImGui.GetFont();var previewFontSize=ImGui.GetFontSize();
        var fieldHeight=MaterialControls.Metrics.Height+(shapedPreview?Math.Max(0,previewSize.Y-ImGui.GetTextLineHeight()):0);
        var customPreview=appearance.HasValue || layout.LeadingIcon.HasValue;
        var previewForeground=appearance?.Foreground ?? ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var style=new MaterialStyleScope();
        if(appearance is { } a)
        {
            style.Color(ImGuiCol.FrameBg,a.Fill);
            style.Color(ImGuiCol.FrameBgHovered,MaterialColor.Layer(a.Fill,a.Foreground,.08f));
            style.Color(ImGuiCol.FrameBgActive,MaterialColor.Layer(a.Fill,a.Foreground,.14f));
            style.Color(ImGuiCol.Text,Vector4.Zero);
            style.Color(ImGuiCol.Border,a.Border);
            style.Style(ImGuiStyleVar.FrameRounding,a.Radius(scale));
            style.Style(ImGuiStyleVar.FrameBorderSize,MaterialTheme.Metrics.Border);
        }
        else if (customPreview) style.Color(ImGuiCol.Text,Vector4.Zero);
        if(customPreview && shapedPreview && fieldHeight>MaterialControls.Metrics.Height)
            style.Style(ImGuiStyleVar.FramePadding,new Vector2(ImGui.GetStyle().FramePadding.X,(fieldHeight-ImGui.GetTextLineHeight())*.5f));
        var flags=ImGuiComboFlags.HeightLarge|(customPreview?ImGuiComboFlags.NoArrowButton:ImGuiComboFlags.None);
        var opened=customPreview?ImGui.BeginCombo("##combo",preview,flags):MaterialText.BeginCombo("##combo",preview,flags);
        style.Dispose();
        if(customPreview)
        {
            var height=fieldHeight;
            var gap=MaterialControls.Metrics.Gap;
            var textX=gap;
            if(layout.LeadingIcon is { } icon)
            {
                var iconSize=Math.Min(MaterialControls.Metrics.IconSize,height);
                // BeginCombo can switch to the popup; artwork belongs to the retained field.
                dl.PushClipRect(origin,origin+new Vector2(fieldWidth,height),true);
                MaterialIcons.Draw(icon,dl,origin+new Vector2(gap,(height-iconSize)*.5f),iconSize,previewForeground);
                dl.PopClipRect();
                textX+=iconSize+gap;
            }
            dl.PushClipRect(origin+new Vector2(Math.Max(0,textX-gap),0),origin+new Vector2(Math.Max(textX,fieldWidth-height),height),true);
            MaterialText.AddText(dl,previewFont,previewFontSize,origin+new Vector2(textX,(height-(shapedPreview?previewSize.Y:ImGui.GetTextLineHeight()))*.5f),MaterialDraw.Color(previewForeground),preview);
            dl.PopClipRect();
            // Draw into the original field's list even while the popup is current.
            var center=origin+new Vector2(fieldWidth-height*.5f,height*.5f);
            var ink=MaterialDraw.Color(previewForeground);
            dl.AddLine(center+new Vector2(-4*scale,-2*scale),center+new Vector2(0,2*scale),ink,scale);
            dl.AddLine(center+new Vector2(0,2*scale),center+new Vector2(4*scale,-2*scale),ink,scale);
        }
        if (opened)
        {
            if (searchable)
            {
                var query = options.Query;
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                if (MaterialTextField.SearchField("search", ref query, searchHint)) options.SetQuery(query);
            }
            if (options.Count == 0) MaterialText.TextDisabled(noOptionsLabel);
            var rowHeight=ImGui.GetTextLineHeight();
            var shapedRows=false;
            for(var i=0;MaterialTextRenderer.Current is not null && i<options.Count;i++)
                if(MaterialNativeCaption.IsShaped(options[i].Label))
                {
                    // The native clipper requires the same line height for every option.
                    rowHeight=Math.Max(rowHeight,MaterialText.Measure(options[i].Label,true).Y);
                    shapedRows=true;break;
                }
            // Native clipping preserves scrolling/navigation without submitting thousands of rows.
            using (var clipper = new MaterialClipper(options.Count))
            {
            if (!searchable && ImGui.IsWindowAppearing())
                for (var i = 0; i < options.Count; i++)
                    if (EqualityComparer<T>.Default.Equals(value,options[i].Value)) { clipper.Include(i); break; }
            while (clipper.Step())
                for (var i = clipper.Start; i < clipper.End; i++)
                {
                    var option = options[i];
                    ImGui.PushID(option.Key);
                    var selected = EqualityComparer<T>.Default.Equals(value, option.Value);
                    ImGui.BeginDisabled(option.Disabled);
                    var clicked=MaterialText.Selectable(options.NativeLabel(i),selected,size:shapedRows?new Vector2(0,rowHeight):default);
                    if (clicked)
                    {
                        changed = !selected;
                        value = option.Value;
                    }
                    if (selected && !searchable) ImGui.SetItemDefaultFocus();
                    ImGui.EndDisabled();
                    ImGui.PopID();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        ImGui.PopID();
        return changed;
    }
}

// Retained native controls still own identity, input and decoration. Their caption alone is replaced.
internal static class MaterialNativeCaption
{
    internal static bool IsShaped(string text) => MaterialTextRenderer.Current is not null && MaterialText.RequiresShaping(text);
    internal static bool IsShaped(ReadOnlySpan<char> text) => MaterialTextRenderer.Current is not null && MaterialText.RequiresShaping(text);
    [ThreadStatic] private static Dictionary<string,string>? visibleCaptions;
    /// <summary>The caption before "##". Labels repeat every frame, so cut captions are kept instead of re-cut.</summary>
    internal static string Visible(string label)
    {
        var end=label.IndexOf("##",StringComparison.Ordinal);
        if(end<0) return label;
        visibleCaptions??=new(StringComparer.Ordinal);
        if(visibleCaptions.TryGetValue(label,out var caption)) return caption;
        if(visibleCaptions.Count>=1024) visibleCaptions.Clear();
        caption=label[..end];
        visibleCaptions.Add(label,caption);
        return caption;
    }
    /// <summary>Visible without allocating; materialize with Visible only where shaped text is drawn.</summary>
    internal static ReadOnlySpan<char> VisibleSpan(string label) => label.IndexOf("##",StringComparison.Ordinal) is var end && end>=0?label.AsSpan(0,end):label;

    internal static unsafe void HideGlyphs(ImDrawListPtr list,int first,ImFontPtr font,ReadOnlySpan<char> caption,Vector2 min,Vector2 max)
    {
        for(var index=first;index+3<list.VtxBuffer.Size;index++)
        {
            var a=list.VtxBuffer[index];var b=list.VtxBuffer[index+1];
            var c=list.VtxBuffer[index+2];var d=list.VtxBuffer[index+3];
            if(a.Uv.X>=c.Uv.X || a.Uv.Y>=c.Uv.Y || a.Uv.Y!=b.Uv.Y || b.Uv.X!=c.Uv.X
                || c.Uv.Y!=d.Uv.Y || d.Uv.X!=a.Uv.X || a.Pos.Y!=b.Pos.Y || b.Pos.X!=c.Pos.X
                || c.Pos.Y!=d.Pos.Y || d.Pos.X!=a.Pos.X || a.Pos.X<min.X-1 || c.Pos.X>max.X+1
                || a.Pos.Y<min.Y-1 || c.Pos.Y>max.Y+1) continue;
            foreach(var character in caption)
            {
                var glyph=ImGui.FindGlyphNoFallback(font,character).Handle;
                if(glyph==null) glyph=font.Handle->FallbackGlyph;
                if(glyph==null || glyph->U0>=glyph->U1 || glyph->V0>=glyph->V1) continue;
                if(a.Uv.X<glyph->U0-.00001f || a.Uv.Y<glyph->V0-.00001f
                    || c.Uv.X>glyph->U1+.00001f || c.Uv.Y>glyph->V1+.00001f) continue;
                for(var offset=0;offset<4;offset++) list.Handle->VtxBuffer.Data[index+offset].Col&=0x00FFFFFF;
                index+=3;break;
            }
        }
    }
}
