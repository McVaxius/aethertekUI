using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

/// <summary>Optional host-owned focus request, also restores editing focus after clearing.</summary>
public sealed class MaterialSearchState
{
    public bool FocusRequested { get; set; }
}

public static class MaterialTextField
{
    public static bool SearchField(string id,ref string value,string hint="Search...",float width=-1,bool disabled=false,MaterialSearchState? state=null,int maxLength=1024)
        => Field(id,ref value,hint,width,disabled,false,MaterialIcon.Search,MaterialIcon.None,true,ImGuiInputTextFlags.None,maxLength,state);
    public static bool SearchField(string id,ref string value,MaterialControlAppearance appearance,string hint="Search...",float width=-1,
        bool disabled=false,MaterialSearchState? state=null,int maxLength=1024)
        => Field(id,ref value,hint,width,disabled,false,MaterialIcon.Search,MaterialIcon.None,value.Length>0,ImGuiInputTextFlags.None,maxLength,state,appearance);
    public static bool TextField(string id,ref string value,string hint="",float width=-1,bool disabled=false,bool error=false,
        MaterialIcon leading=MaterialIcon.None,MaterialIcon trailing=MaterialIcon.None,ImGuiInputTextFlags flags=ImGuiInputTextFlags.None,int maxLength=1024)
        => Field(id,ref value,hint,width,disabled,error,leading,trailing,false,flags,maxLength);

    private static bool Field(string id,ref string value,string hint,float width,bool disabled,bool error,
        MaterialIcon leading,MaterialIcon trailing,bool clear,ImGuiInputTextFlags flags,int maxLength,MaterialSearchState? searchState=null,
        MaterialControlAppearance? appearance=null)
    {
        if(maxLength<1) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var theme=MaterialTheme.Current;
        var m=MaterialTheme.Metrics; var c=theme.Colors;
        var controls=MaterialControls.Metrics;
        var height=controls.Height;
        if ((flags & ImGuiInputTextFlags.Password) == 0 && MaterialTextRenderer.Current is { } textRenderer
            && (MaterialText.RequiresShaping(value) || MaterialText.RequiresShaping(hint)))
        {
            var fontSize=ImGui.GetFontSize();
            var shapedHeight=Math.Max(textRenderer.GetLayout(value,fontSize).Size.Y,
                textRenderer.GetLayout(hint,fontSize).Size.Y);
            height+=Math.Max(0,shapedHeight-ImGui.GetTextLineHeight());
        }
        var rounding=appearance?.Radius(m.Scale) ?? controls.Rounding;
        var size=new Vector2(MaterialLayout.ResolveWidth(width,220*m.Scale,ImGui.GetContentRegionAvail().X,m.Scale),height);
        // Suppress decoration when a narrow panel cannot fit an editable area.
        if (size.X < height*3) leading=MaterialIcon.None;
        if (size.X < height*2) { clear=false; trailing=MaterialIcon.None; }
        var inset=Math.Min(controls.Gap,Math.Max(0,(size.X-1)*.25f));
        var origin=ImGui.GetCursorScreenPos();
        ImGui.PushID(id); ImGui.BeginGroup(); ImGui.BeginDisabled(disabled);
        try
        {
        var dl=ImGui.GetWindowDrawList();
        dl.AddRectFilled(origin,origin+size,MaterialDraw.Color(appearance?.Fill ?? theme.ControlColor(c.SurfaceContainerHighest)),rounding);
        var leadingWidth=leading==MaterialIcon.None?0:height-controls.Gap;
        var trailingWidth=clear||trailing!=MaterialIcon.None?height:0;
        var style=new MaterialStyleScope();
        bool changed,focused;
        try
        {
        if(appearance is { } a) style.Color(ImGuiCol.Text,a.Foreground);
        style.Color(ImGuiCol.FrameBg,Vector4.Zero);
        style.Color(ImGuiCol.FrameBgHovered,Vector4.Zero);
        style.Color(ImGuiCol.FrameBgActive,Vector4.Zero);
        style.Style(ImGuiStyleVar.FramePadding,new Vector2(inset,(height-ImGui.GetTextLineHeight())*.5f));
        style.Style(ImGuiStyleVar.FrameBorderSize,0);
        ImGui.SetCursorScreenPos(origin+new Vector2(leadingWidth+inset,0));
        ImGui.SetNextItemWidth(Math.Max(1,size.X-leadingWidth-trailingWidth-2*inset));
        if (!disabled && searchState is { FocusRequested: true })
        {
            ImGui.SetKeyboardFocusHere();
            searchState.FocusRequested=false;
        }
        changed=MaterialShapedInput.SingleLine("##input",hint,ref value,maxLength,flags);
        focused=ImGui.IsItemActive()||ImGui.IsItemFocused();
        }
        finally { style.Dispose(); }
        if(leading!=MaterialIcon.None) MaterialIcons.Draw(leading,origin+new Vector2(controls.Gap,(height-controls.IconSize)*.5f),controls.IconSize,c.OnSurfaceVariant);
        if(clear)
        {
            ImGui.SetCursorScreenPos(origin+new Vector2(Math.Max(0,size.X-height),0));
            if(MaterialButton.IconButton("clear",MaterialIcon.Close,disabled:disabled||value.Length==0,size:height/m.Scale))
            { value=string.Empty; changed=true; if(searchState is not null) searchState.FocusRequested=true; }
        }
        else if(trailing!=MaterialIcon.None)
            MaterialIcons.Draw(trailing,origin+new Vector2(size.X-height+(height-controls.IconSize)*.5f,(height-controls.IconSize)*.5f),controls.IconSize,c.OnSurfaceVariant);
        var hovered=!disabled&&ImGui.IsWindowHovered()&&ImGui.IsMouseHoveringRect(origin,origin+size);
        dl.AddRect(origin,origin+size,MaterialDraw.Color(error?c.Error:focused?c.Primary:hovered?c.Outline:appearance?.Border ?? theme.ControlColor(c.OutlineVariant, border: true)),
            rounding,ImDrawFlags.RoundCornersAll,focused||error?m.Border*2:m.Border);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        return changed;
        }
        finally { ImGui.EndDisabled(); ImGui.EndGroup(); ImGui.PopID(); }
    }
}
