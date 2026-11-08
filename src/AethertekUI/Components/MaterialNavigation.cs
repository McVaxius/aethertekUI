using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

public static class MaterialNavigation
{
    public static MaterialSurface.SurfaceScope Rail(string id,bool expanded=true,float height=0)
    {
        var m=MaterialTheme.Metrics;
        return MaterialSurface.Begin(id,new((expanded?m.RailWidth:m.CompactRailWidth)/m.Scale,height),MaterialElevation.Lowest,padding:6);
    }
    public static bool Item(string id,string label,MaterialIcon icon,bool selected,bool expanded=true,bool disabled=false)
    {
        var m=MaterialTheme.Metrics; var c=MaterialTheme.Current.Colors;
        var controls=MaterialControls.Metrics;
        var size=new Vector2(Math.Max(1,ImGui.GetContentRegionAvail().X),controls.Height+controls.Gap);
        ImGui.PushID(id);
        var clicked=MaterialDraw.Hit("##navigation",size,disabled,out var state);
        var emphasis=MaterialDraw.Animate(state.Id,3,selected?1:0);
        var bg=MaterialColor.Alpha(c.SecondaryContainer,emphasis);
        MaterialDraw.Shape(state,bg,c.OnSecondaryContainer,size.Y*.5f);
        if(expanded) MaterialDraw.Content(state,label,selected?c.OnSecondaryContainer:c.OnSurfaceVariant,icon,MaterialIcon.None,true);
        else if(state.Visible)
        {
            var iconSize=Math.Min(controls.IconSize,Math.Max(1,size.X-2*m.Scale));
            MaterialIcons.Draw(icon,state.Min+(size-new Vector2(iconSize))*.5f,iconSize,selected?c.OnSecondaryContainer:c.OnSurfaceVariant,state.Alpha);
        }
        ImGui.PopID();
        return clicked;
    }
    public static bool ListItem(string id,string label,bool selected=false,MaterialIcon icon=MaterialIcon.None,bool disabled=false)
        => Item(id,label,icon,selected,true,disabled);
}

/// <summary>Secondary sibling tabs using native tab lifecycle and behavior.</summary>
public static class MaterialTabs
{
    public static TabBarScope Begin(string id,ImGuiTabBarFlags flags=ImGuiTabBarFlags.None) => new(id,flags);
    /// <summary>Declares captions before native Begin so mixed-language tabs share the natural shaped line height.</summary>
    public static TabBarScope Begin(string id,IReadOnlyList<string> captions,ImGuiTabBarFlags flags=ImGuiTabBarFlags.None) => new(id,flags,captions);
    public static TabItemScope Item(string label,ImGuiTabItemFlags flags=ImGuiTabItemFlags.None) => new(label,flags);
    public ref struct TabBarScope
    {
        private bool active;
        public bool Visible { get; }
        internal TabBarScope(string id,ImGuiTabBarFlags flags) { active=Visible=ImGui.BeginTabBar(id,flags); }
        internal TabBarScope(string id,ImGuiTabBarFlags flags,IReadOnlyList<string> captions)
        {
            ArgumentNullException.ThrowIfNull(captions);
            var height=new MaterialStyleScope();
            try
            {
                if(MaterialTextRenderer.Current is not null)
                {
                    var lineHeight=ImGui.GetTextLineHeight();
                    for(var i=0;i<captions.Count;i++)
                        if(MaterialText.RequiresShaping(MaterialNativeCaption.VisibleSpan(captions[i])))
                            lineHeight=Math.Max(lineHeight,MaterialText.Measure(MaterialNativeCaption.Visible(captions[i])).Y);
                    MaterialText.ApplyLineHeight(ref height,lineHeight);
                }
                active=Visible=ImGui.BeginTabBar(id,flags);
            }
            finally { height.Dispose(); }
        }
        public void Dispose() { if(active) ImGui.EndTabBar(); active=false; }
    }
    public ref struct TabItemScope
    {
        private bool active;
        public bool Visible { get; }
        internal TabItemScope(string label,ImGuiTabItemFlags flags)
        {
            // The bar owns its metrics; restore them only while submitting each
            // sibling tab, leaving the selected content's controls unchanged.
            using var height=new MaterialStyleScope();
            var owner=ImGui.GetCurrentContext().CurrentTabBar;
            if(!owner.IsNull && owner.FramePadding!=ImGui.GetStyle().FramePadding)
                height.Style(ImGuiStyleVar.FramePadding,owner.FramePadding);
            var shaped=MaterialNativeCaption.IsShaped(MaterialNativeCaption.VisibleSpan(label));
            var cyber=MaterialTheme.CurrentOrNull is { IsCyberpunk: true };
            if(!shaped && !cyber) { active=Visible=ImGui.BeginTabItem(label,flags);return; }
            var caption=shaped?MaterialNativeCaption.Visible(label):"";
            var measured=shaped?MaterialText.Measure(caption):default;
            // Keep an explicit caller width and the original native label/ID.
            if(shaped && ((int)ImGui.GetCurrentContext().NextItemData.Flags&1)==0)
                ImGui.SetNextItemWidth(Math.Max(ImGui.CalcTextSize(caption).X,measured.X)+2*ImGui.GetStyle().FramePadding.X);
            var list=ImGui.GetWindowDrawList();var first=list.VtxBuffer.Size;var font=ImGui.GetFont();
            var alpha=ImGui.GetStyle().Alpha;
            var splitter=cyber?ImGui.ImDrawListSplitter():default;
            active=Visible=false;
            try
            {
                if(cyber) { splitter.Split(list,2);splitter.SetCurrentChannel(list,1); }
                using(MaterialSkin.TransparentTabFace())
                    active=Visible=ImGui.BeginTabItem(label,shaped?flags|ImGuiTabItemFlags.NoTooltip:flags);
                var min=ImGui.GetItemRectMin();var max=ImGui.GetItemRectMax();
                if(cyber)
                {
                    splitter.SetCurrentChannel(list,0);
                    MaterialSkin.PaintTab(min,max,Visible,alpha);
                    splitter.SetCurrentChannel(list,1);
                }
                if(!shaped) return;
                MaterialNativeCaption.HideGlyphs(list,first,font,caption,min,max);
                var bar=ImGui.GetCurrentContext().CurrentTabBar;
                var clipMin=new Vector2(Math.Max(min.X,bar.ScrollingRectMinX),min.Y);
                var clipMax=new Vector2(Math.Min(max.X,bar.ScrollingRectMaxX),max.Y);
                if(clipMax.X>clipMin.X && clipMax.Y>clipMin.Y)
                {
                    list.PushClipRect(clipMin,clipMax,true);
                    try { MaterialText.AddText(list,min+new Vector2(ImGui.GetStyle().FramePadding.X,(max.Y-min.Y-measured.Y)*.5f),ImGui.GetColorU32(ImGuiCol.Text),caption); }
                    finally { list.PopClipRect(); }
                }
                if((flags&ImGuiTabItemFlags.NoTooltip)==0 && measured.X>max.X-min.X-2*ImGui.GetStyle().FramePadding.X && ImGui.IsItemHovered())
                    MaterialSemantic.DrawTooltip(caption);
            }
            catch
            {
                if(active) ImGui.EndTabItem();
                active=false;
                throw;
            }
            finally { if(cyber) { splitter.Merge(list);splitter.Destroy(); } }
        }
        public void Dispose() { if(active) ImGui.EndTabItem(); active=false; }
    }
}
