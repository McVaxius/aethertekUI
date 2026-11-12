using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

internal readonly record struct MaterialState(Vector2 Min, Vector2 Max, uint Id, float Alpha,
    bool Hovered, bool Pressed, bool Focused, bool Visible);

internal static class MaterialDraw
{
    public static uint Color(Vector4 color, float? alpha = null)
    {
        color.W *= alpha ?? ImGui.GetStyle().Alpha;
        return ImGui.ColorConvertFloat4ToU32(color);
    }
    public static bool Hit(string id, Vector2 size, bool disabled, out MaterialState state)
    {
        var style = new MaterialStyleScope();
        style.Color(ImGuiCol.Button,Vector4.Zero);
        style.Color(ImGuiCol.ButtonHovered,Vector4.Zero);
        style.Color(ImGuiCol.ButtonActive,Vector4.Zero);
        style.Color(ImGuiCol.NavHighlight,Vector4.Zero);
        if (MaterialTheme.Current.IsSpatial) style.Style(ImGuiStyleVar.FrameBorderSize,0);
        ImGui.BeginDisabled(disabled);
        var clicked = ImGui.Button(id,size);
        state = new(ImGui.GetItemRectMin(),ImGui.GetItemRectMax(),ImGui.GetID(id),ImGui.GetStyle().Alpha,
            ImGui.IsItemHovered(),ImGui.IsItemActive(),ImGui.IsItemFocused(),ImGui.IsItemVisible());
        ImGui.EndDisabled();
        style.Dispose();
        MaterialLcars.Feedback(clicked);
        return clicked;
    }
    public static float Animate(uint id, byte channel, float target)
        => MaterialTheme.Current.Motion.Sample(((ulong)id << 8) | channel,target,ImGui.GetIO().DeltaTime,ImGui.GetFrameCount());

    public static void Shape(in MaterialState state, Vector4 background, Vector4 foreground, float radius,
        bool outline = false, ImDrawFlags corners = ImDrawFlags.RoundCornersAll, Vector4? border = null, bool preserveFill = false)
    {
        if (!state.Visible) return;
        var theme = MaterialTheme.Current;
        var c = theme.Colors;
        var m = MaterialTheme.Metrics;
        var layer = Animate(state.Id,0,state.Pressed ? .14f : state.Hovered ? .08f : 0);
        var dl = ImGui.GetWindowDrawList();
        var fill = MaterialColor.Layer(background,foreground,layer);
        if (!preserveFill) fill = theme.ControlColor(fill);
        if (MaterialSkin.HasMaterial) MaterialSkin.PaintControl(state,fill,radius,corners,preserveFill);
        else dl.AddRectFilled(state.Min,state.Max,Color(fill,state.Alpha),radius,corners);
        if (outline || state.Focused)
            dl.AddRect(state.Min+new Vector2(m.Border*.5f),state.Max-new Vector2(m.Border*.5f),
                Color(state.Focused ? c.Primary : border ?? theme.ControlColor(c.OutlineVariant, border: true),state.Alpha),radius,corners,state.Focused ? m.Border*2 : m.Border);
    }
    public static void Content(in MaterialState state, string label, Vector4 color, MaterialIcon leading, MaterialIcon trailing,
        bool alignLeft = false)
    {
        if (!state.Visible) return;
        var m = MaterialControls.Metrics;
        var textSize = MaterialText.Measure(label);
        var iconWidth = m.IconSize + m.Gap;
        var width = textSize.X + (leading == MaterialIcon.None ? 0 : iconWidth) + (trailing == MaterialIcon.None ? 0 : iconWidth);
        var x = alignLeft ? state.Min.X+m.Padding.X : state.Min.X+Math.Max(m.Gap,(state.Max.X-state.Min.X-width)*.5f);
        var centerY = (state.Min.Y+state.Max.Y)*.5f;
        var dl=ImGui.GetWindowDrawList();
        var inset=Math.Min(m.Gap,Math.Max(0,(state.Max.X-state.Min.X-1)*.5f));
        dl.PushClipRect(state.Min+new Vector2(inset,0),state.Max-new Vector2(inset,0),true);
        if(leading != MaterialIcon.None) { MaterialIcons.Draw(leading,new(x,centerY-m.IconSize*.5f),m.IconSize,color,state.Alpha); x += iconWidth; }
        MaterialText.AddText(dl,new(x,centerY-textSize.Y*.5f),Color(color,state.Alpha),label);
        if(trailing != MaterialIcon.None) MaterialIcons.Draw(trailing,new(x+textSize.X+m.Gap,centerY-m.IconSize*.5f),m.IconSize,color,state.Alpha);
        dl.PopClipRect();
    }
    public static Vector2 Size(Vector2 logicalSize, float automaticWidth, float automaticHeight)
    {
        var m=MaterialTheme.Metrics;
        return new(MaterialLayout.ResolveWidth(logicalSize.X,automaticWidth,ImGui.GetContentRegionAvail().X,m.Scale,logicalSize.X<=0),
            logicalSize.Y <= 0 ? automaticHeight : logicalSize.Y*m.Scale);
    }
}
