using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

public static class MaterialContextMenu
{
    public static PopupScope Begin(string id, ImGuiPopupFlags flags = ImGuiPopupFlags.MouseButtonRight) => new(id, flags);
    public ref struct PopupScope
    {
        private bool active;
        public bool Visible => active;
        internal PopupScope(string id, ImGuiPopupFlags flags) => active = ImGui.BeginPopupContextItem(id, flags);
        public void Dispose() { if (!active) return; ImGui.EndPopup(); active = false; }
    }
}

public static class MaterialTree
{
    public static TreeScope Node(string id, string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None) => new(id,label,flags);
    public ref struct TreeScope
    {
        private bool active;
        private readonly bool popTree;
        public bool Visible { get; }
        internal TreeScope(string id, string label, ImGuiTreeNodeFlags flags)
        {
            ImGui.PushID(id);
            var opened = false;
            try
            {
                if (!MaterialNativeCaption.IsShaped(MaterialNativeCaption.VisibleSpan(label)))
                    Visible = ImGui.TreeNodeEx("##node", flags, label);
                else
                {
                    var caption = MaterialNativeCaption.Visible(label);
                    var origin = ImGui.GetCursorScreenPos();
                    var list = ImGui.GetWindowDrawList();
                    var first = list.VtxBuffer.Size;
                    var font = ImGui.GetFont();
                    var measured = MaterialText.Measure(caption);
                    var window = ImGuiP.GetCurrentWindow();
                    var padding = ImGui.GetStyle().FramePadding;
                    var framed = (flags & ImGuiTreeNodeFlags.Framed) != 0;
                    var padded = framed || (flags & ImGuiTreeNodeFlags.FramePadding) != 0;
                    var nativePaddingY = padded ? padding.Y : Math.Min(window.DC.CurrLineTextBaseOffset, padding.Y);
                    using var height = MaterialText.PushLineHeight(caption);
                    if (!window.SkipItems)
                    {
                        window.DC.CurrLineSize.Y = Math.Max(window.DC.CurrLineSize.Y, measured.Y + 2 * nativePaddingY);
                        window.DC.CurrLineTextBaseOffset += Math.Max(0, measured.Y - ImGui.GetFontSize()) * .5f;
                    }
                    Visible = ImGui.TreeNodeEx("##node", flags, label);
                    opened = Visible;
                    var bounds = ImGui.GetCurrentContext().LastItemData.DisplayRect;
                    if (ImGui.IsItemVisible())
                    {
                        MaterialNativeCaption.HideGlyphs(list, first, font, caption, bounds.Min, bounds.Max);
                        list.PushClipRect(bounds.Min, bounds.Max, true);
                        try
                        {
                            MaterialText.AddText(list, origin + new Vector2(ImGui.GetFontSize() + padding.X * (framed ? 3 : 2),
                                (bounds.Max.Y - bounds.Min.Y - measured.Y) * .5f), ImGui.GetColorU32(ImGuiCol.Text), caption);
                        }
                        finally { list.PopClipRect(); }
                    }
                }
                popTree = Visible && (flags & ImGuiTreeNodeFlags.NoTreePushOnOpen) == 0;
                active = true;
            }
            catch
            {
                if (opened && (flags & ImGuiTreeNodeFlags.NoTreePushOnOpen) == 0) ImGui.TreePop();
                ImGui.PopID();
                throw;
            }
        }
        public void Dispose()
        {
            if (!active) return;
            if (popTree) ImGui.TreePop();
            ImGui.PopID();
            active = false;
        }
    }
}

/// <summary>Domain colors are passed through unchanged. Theme scopes never reinterpret their meaning.</summary>
public static class MaterialSemantic
{
    public static MaterialStyleScope Text(Vector4 color)
    {
        var scope = new MaterialStyleScope();
        scope.Color(ImGuiCol.Text, color);
        return scope;
    }
    public static MaterialStyleScope Colors(ReadOnlySpan<(ImGuiCol Role, Vector4 Color)> overrides)
    {
        var scope = new MaterialStyleScope();
        foreach (var entry in overrides) scope.Color(entry.Role, entry.Color);
        return scope;
    }
    public static void Feedback(string text, Vector4? color = null)
    {
        using var scope = Text(color ?? MaterialTheme.Current.Colors.OnSurfaceVariant);
        MaterialText.TextWrapped(text);
    }
    public static void Tooltip(string text, bool allowDisabled = true)
    {
        if (ImGui.IsItemHovered(allowDisabled ? ImGuiHoveredFlags.AllowWhenDisabled : ImGuiHoveredFlags.None))
            DrawTooltip(text);
    }
    internal static void DrawTooltip(string text) => MaterialText.SetTooltip(text);
}
