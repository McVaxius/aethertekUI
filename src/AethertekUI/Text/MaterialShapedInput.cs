using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

/// <summary>Uses the retained native editor for Unicode text and undo, with shaped visual geometry.</summary>
public static unsafe class MaterialShapedInput
{
    [ThreadStatic] private static EditorProxy? activeProxy;
    [ThreadStatic] private static ClipboardScope? clipboardScope;
    [ThreadStatic] private static Dictionary<(nint Context, uint Id, bool Multiline), EditorProxy>? spareProxies;

    /// <summary>
    /// A proxy equivalent to a new one. An idle field otherwise created a proxy (two dictionaries)
    /// every frame; one that never gained tokens or a preferred column is indistinguishable from new.
    /// </summary>
    private static EditorProxy FreshProxy(nint context, uint id, bool multiline)
    {
        spareProxies ??= new();
        var key = (context, id, multiline);
        if (spareProxies.TryGetValue(key, out var spare) && !spare.HasTokens && spare.PreferredX is null)
            return spare;
        if (spareProxies.Count >= 64) spareProxies.Clear();
        return spareProxies[key] = new EditorProxy(context, id, multiline);
    }

    /// <summary>Preserves the supplied native ID and existing field geometry. The caller reserves the shaped line height.</summary>
    public static bool SingleLine(string nativeId, string hint, ref string value, int maxLength, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        var renderer = MaterialTextRenderer.Current;
        if (renderer is null || (flags & ImGuiInputTextFlags.Password) != 0)
            return ImGui.InputTextWithHint(nativeId, hint, ref value, maxLength, flags);

        var id = ImGui.GetID(nativeId);
        var context = ImGui.GetCurrentContext();
        var retainedProxy = activeProxy is { } retained && retained.Context == (nint)context.Handle
            && retained.Id == id && context.InputTextState.ID == id && retained.HasTokens;
        if (!NeedsShaping(value) && !NeedsShaping(hint) && !retainedProxy)
            return ImGui.InputTextWithHint(nativeId, hint, ref value, maxLength, flags);

        var edit = SingleLineEdit.Rent();
        try { return edit.Run(renderer, nativeId, hint, ref value, maxLength, flags, id, context); }
        finally { edit.Return(); }
    }

    /// <summary>
    /// The shaped SingleLine editor. Its native callback reads per-call state from fields of a
    /// reused instance, so neither the native path nor each shaped frame allocates a closure.
    /// </summary>
    private sealed class SingleLineEdit
    {
        [ThreadStatic] private static SingleLineEdit? free;
        private readonly ImGui.ImGuiInputTextCallbackDelegate callback;
        private MaterialTextRenderer renderer = null!;
        private uint id;
        private float fontSize;
        private string before = "";
        private Vector2 padding, frameMin, frameMax, mousePoint;
        private EditorProxy proxy = null!;
        private bool wasActive, left, right, mouseClicked, dragging, shift, control;
        private int cursorBefore, anchorBefore, selectionEndBefore;
        private float scrollBefore;
        private ImGuiIOPtr io;
        private Exception? callbackError;
        private ClipboardScope clipboard = null!;

        private SingleLineEdit() => callback = Callback;

        internal static SingleLineEdit Rent()
        {
            var edit = free ?? new SingleLineEdit();
            free = null;
            return edit;
        }

        internal void Return()
        {
            renderer = null!; proxy = null!; clipboard = null!; before = ""; callbackError = null;
            free = this;
        }

        internal bool Run(MaterialTextRenderer renderer, string nativeId, string hint, ref string value, int maxLength,
            ImGuiInputTextFlags flags, uint id, ImGuiContextPtr context)
        {
            this.renderer = renderer;
            this.id = id;
            fontSize = ImGui.GetFontSize();
            before = value;
            padding = ImGui.GetStyle().FramePadding;
            frameMin = ImGui.GetCursorScreenPos();
            frameMax = frameMin + new Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
            proxy = activeProxy is { } existing && existing.Context == (nint)context.Handle
                && existing.Id == id && context.InputTextState.ID == id ? existing : FreshProxy((nint)context.Handle, id, false);
            var nativeValue = proxy.Encode(value);
            var stateBefore = ImGuiP.GetInputTextState(id);
            wasActive = !stateBefore.IsNull && ImGui.GetCurrentContext().ActiveId == id;
            cursorBefore = wasActive ? proxy.ToSourcePosition(nativeValue, stateBefore.Stb.Cursor) : -1;
            anchorBefore = wasActive ? proxy.ToSourcePosition(nativeValue, stateBefore.Stb.SelectStart) : -1;
            selectionEndBefore = wasActive ? proxy.ToSourcePosition(nativeValue, stateBefore.Stb.SelectEnd) : -1;
            scrollBefore = wasActive ? stateBefore.ScrollX : 0;
            io = ImGui.GetIO();
            left = ImGui.IsKeyPressed(ImGuiKey.LeftArrow);
            right = ImGui.IsKeyPressed(ImGuiKey.RightArrow);
            mousePoint = io.MousePos;
            mouseClicked = io.MouseClicked[0] && Inside(mousePoint, frameMin, frameMax);
            dragging = wasActive && io.MouseDown[0] && !stateBefore.SelectedAllMouseLock;
            shift = io.KeyShift;
            control = io.KeyCtrl;
            var tint = ImGui.GetColorU32(ImGuiCol.Text);
            var hintTint = ImGui.GetColorU32(ImGuiCol.TextDisabled);
            var selectionTint = ImGui.GetColorU32(ImGuiCol.TextSelectedBg);
            callbackError = null;
            clipboard = ClipboardScope.Open(proxy, Math.Max(maxLength, Encoding.UTF8.GetByteCount(before)));
            try
            {
                // Native word selection must hit the same source cluster as the shaped pointer.
                // The second click already has a native state, so its input-only scroll can map
                // the real pointer to native codepoint widths without altering global mouse data.
                if (mouseClicked && io.MouseClickedCount[0] >= 2 && wasActive)
                {
                    var layout = renderer.GetLayout(before, fontSize);
                    var hit = layout.HitTestPoint(mousePoint - TextOrigin(frameMin, frameMax, padding, layout.Size.Y)
                        + new Vector2(scrollBefore, 0));
                    var sourcePosition = hit.Position + (hit.Trailing ? hit.Length : 0);
                    var nativePosition = proxy.ToNativePosition(nativeValue, sourcePosition);
                    var nativeWidth = ImGui.CalcTextSize(nativeValue[..nativePosition]).X;
                    stateBefore.ScrollX = nativeWidth - (mousePoint.X - frameMin.X - padding.X);
                }


                bool changed;
                ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
                ImGui.PushStyleColor(ImGuiCol.TextDisabled, Vector4.Zero);
                ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Vector4.Zero);
                try
                {
                    changed = ImGui.InputTextWithHint(nativeId, hint, ref nativeValue, maxLength,
                        flags | ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackCharFilter, callback);
                }
                finally
                {
                    ImGui.PopStyleColor(3);
                    if (!stateBefore.IsNull)
                        stateBefore.ScrollX = scrollBefore;
                }
                value = proxy.Decode(nativeValue);
                if (context.InputTextState.ID == id)
                    activeProxy = proxy;
                if (callbackError is not null || clipboard.Error is not null)
                    throw new InvalidOperationException("The shaped editor callback failed.", callbackError ?? clipboard.Error);

                frameMin = ImGui.GetItemRectMin();
                frameMax = ImGui.GetItemRectMax();
                var active = ImGui.IsItemActive();
                var drawList = ImGui.GetWindowDrawList();
                var text = value.Length == 0 ? hint : value;
                var painted = renderer.GetLayout(text, fontSize);
                var originAfter = TextOrigin(frameMin, frameMax, padding, painted.Size.Y);
                var clipMin = new Vector2(frameMin.X + padding.X, frameMin.Y);
                var clipMax = new Vector2(frameMax.X - padding.X, frameMax.Y);
                clipMax = Vector2.Max(clipMin, clipMax);
                var state = ImGuiP.GetInputTextState(id);
                var scroll = scrollBefore;
                var caret = Vector2.Zero;
                var caretHeight = painted.Size.Y;
                if (active && !state.IsNull)
                {
                    var layout = renderer.GetLayout(value, fontSize);
                    var cursor = proxy.ToSourcePosition(nativeValue, state.Stb.Cursor);
                    caret = layout.HitTestPosition(cursor);
                    caretHeight = layout.Size.Y;
                    var visibleWidth = Math.Max(1, clipMax.X - clipMin.X);
                    if ((flags & ImGuiInputTextFlags.NoHorizontalScroll) != 0)
                        scroll = 0;
                    else
                    {
                        if (caret.X < scroll)
                            scroll = Math.Max(0, caret.X - visibleWidth * .25f);
                        else if (caret.X >= scroll + visibleWidth)
                            scroll = Math.Max(0, caret.X - visibleWidth * .75f);
                        scroll = Math.Clamp(scroll, 0, Math.Max(0, layout.Size.X - visibleWidth));
                    }
                    state.ScrollX = scroll;
                    ref var ime = ref ImGui.GetCurrentContext().PlatformImeData;
                    ime.InputPos = originAfter + caret - new Vector2(scroll, 0);
                    ime.InputLineHeight = caretHeight;
                }
                else
                    scroll = 0;

                // Admit leading glyph overhang into the native frame's existing padding.
                // Keep the padded boundary while scrolling, and retain the original
                // text origin, visible width, pointer geometry and caret positions.
                if (scroll == 0)
                    clipMin.X = frameMin.X;

                drawList.PushClipRect(clipMin, clipMax, true);
                try
                {
                    if (active && !state.IsNull && state.Stb.SelectStart != state.Stb.SelectEnd)
                    {
                        var layout = renderer.GetLayout(value, fontSize);
                        var start = proxy.ToSourcePosition(nativeValue, Math.Min(state.Stb.SelectStart, state.Stb.SelectEnd));
                        var end = proxy.ToSourcePosition(nativeValue, Math.Max(state.Stb.SelectStart, state.Stb.SelectEnd));
                        foreach (var rectangle in layout.HitTestRange(start, end - start))
                            drawList.AddRectFilled(originAfter + rectangle.Min - new Vector2(scroll, 0),
                                originAfter + rectangle.Max - new Vector2(scroll, 0), selectionTint);
                    }

                    renderer.Draw(drawList, originAfter - new Vector2(scroll, 0), value.Length == 0 ? hintTint : tint, text, fontSize);
                    if (active && !state.IsNull && (!io.ConfigInputTextCursorBlink || state.CursorAnim <= 0 || state.CursorAnim % 1.2f <= .8f))
                    {
                        var point = originAfter + caret - new Vector2(scroll, 0);
                        drawList.AddLine(point, point + new Vector2(0, caretHeight), tint);
                    }
                }
                finally
                {
                    drawList.PopClipRect();
                }
                return changed;
            }
            finally { clipboard.Dispose(); }
        }

        private int Callback(ref ImGuiInputTextCallbackData data)
        {
            try
            {
                if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
                {
                    if (!clipboard.ReadProxy && EditorProxy.IsReserved((char)data.EventChar))
                        data.EventChar = proxy.Encode(((char)data.EventChar).ToString())[0];
                    var sourceBytes = Encoding.UTF8.GetByteCount(proxy.Decode(((char)data.EventChar).ToString()));
                    if (!clipboard.AcceptCharacter(sourceBytes))
                        return 1;
                    return 0;
                }
                if (data.EventFlag != ImGuiInputTextFlags.CallbackAlways)
                    return 0;

                var currentNative = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data.Buf, data.BufTextLen));
                var current = proxy.Decode(currentNative);
                var layout = renderer.GetLayout(current, fontSize);
                var cursor = proxy.ToSourcePosition(currentNative, FromBytes(currentNative, data.CursorPos));
                var anchor = proxy.ToSourcePosition(currentNative, FromBytes(currentNative, data.SelectionStart));
                var end = proxy.ToSourcePosition(currentNative, FromBytes(currentNative, data.SelectionEnd));
                var origin = TextOrigin(frameMin, frameMax, padding, layout.Size.Y);

                if ((mouseClicked || dragging) && io.MouseClickedCount[0] < 2 && !ImGuiP.GetInputTextState(id).SelectedAllMouseLock)
                {
                    var hit = layout.HitTestPoint(mousePoint - origin + new Vector2(scrollBefore, 0));
                    cursor = Math.Clamp(hit.Position + (hit.Trailing ? hit.Length : 0), 0, current.Length);
                    anchor = mouseClicked && !shift ? cursor : Math.Clamp(anchorBefore < 0 ? cursor : anchorBefore, 0, current.Length);
                    end = cursor;
                }
                else if (wasActive && current == before && (left || right) && !control)
                {
                    if (!shift && anchorBefore != selectionEndBefore)
                        cursor = left ? Math.Min(anchorBefore, selectionEndBefore) : Math.Max(anchorBefore, selectionEndBefore);
                    else
                        cursor = Step(layout, Math.Clamp(cursorBefore, 0, current.Length), right);
                    anchor = shift ? Math.Clamp(anchorBefore == selectionEndBefore ? cursorBefore : anchorBefore, 0, current.Length) : cursor;
                    end = cursor;
                }
                else
                {
                    var forward = !left;
                    cursor = Snap(layout, cursor, forward);
                    if (anchor == end)
                        anchor = end = cursor;
                    else
                    {
                        anchor = Snap(layout, anchor, anchor > end);
                        end = Snap(layout, end, end > anchor);
                    }
                }

                data.CursorPos = ToBytes(currentNative, proxy.ToNativePosition(currentNative, cursor));
                data.SelectionStart = ToBytes(currentNative, proxy.ToNativePosition(currentNative, anchor));
                data.SelectionEnd = ToBytes(currentNative, proxy.ToNativePosition(currentNative, end));
                return 0;
            }
            catch (Exception error)
            {
                callbackError ??= error;
                return data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter ? 1 : 0;
            }
        }
    }


    /// <summary>Retains native multiline editing and caller size while shaping source text and both scroll axes.</summary>
    public static bool Multiline(string nativeId, ref string value, int maxLength, Vector2 size,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        var renderer = MaterialTextRenderer.Current;
        if (renderer is null || (flags & ImGuiInputTextFlags.Password) != 0)
            return ImGui.InputTextMultiline(nativeId, ref value, maxLength, size, flags);

        var context = ImGui.GetCurrentContext();
        var parent = ImGuiP.GetCurrentWindow();
        var id = ImGui.GetID(nativeId);
        var caption = MaterialNativeCaption.Visible(nativeId);
        var retained = activeProxy is { Multiline: true } old && old.Context == (nint)context.Handle
            && old.Id == id && context.InputTextState.ID == id && old.HasTokens;
        if (!NeedsShaping(value) && !NeedsShaping(caption) && !retained)
            return ImGui.InputTextMultiline(nativeId, ref value, maxLength, size, flags);
        return MultilineShaped(renderer, nativeId, ref value, maxLength, size, flags, context, parent, id, caption);
    }

    // Separate from Multiline: its callback's captured state is allocated on entry, so the
    // native path above stays allocation-free.
    private static bool MultilineShaped(MaterialTextRenderer renderer, string nativeId, ref string value, int maxLength,
        Vector2 size, ImGuiInputTextFlags flags, ImGuiContextPtr context, ImGuiWindowPtr parent, uint id, string caption)
    {
        var proxy = activeProxy is { Multiline: true } existing && existing.Context == (nint)context.Handle
            && existing.Id == id && context.InputTextState.ID == id ? existing : FreshProxy((nint)context.Handle, id, true);
        var before = value;
        var nativeValue = proxy.Encode(before);
        var fontSize = ImGui.GetFontSize();
        var font = ImGui.GetFont();
        var padding = ImGui.GetStyle().FramePadding;
        var frameMin = ImGui.GetCursorScreenPos();
        var frameSize = ImGuiP.CalcItemSize(size, ImGui.CalcItemWidth(), fontSize * 8 + 2 * padding.Y);
        var frameMax = frameMin + frameSize;
        var childBefore = FindInputChild(parent, id);
        var stateBefore = ImGuiP.GetInputTextState(id);
        var wasActive = !stateBefore.IsNull && context.ActiveId == id;
        var cursorBefore = wasActive ? proxy.ToSourcePosition(nativeValue, stateBefore.Stb.Cursor) : -1;
        var anchorBefore = wasActive ? proxy.ToSourcePosition(nativeValue, stateBefore.Stb.SelectStart) : -1;
        var endBefore = wasActive ? proxy.ToSourcePosition(nativeValue, stateBefore.Stb.SelectEnd) : -1;
        var scrollX = wasActive ? stateBefore.ScrollX : 0;
        var scrollY = childBefore.IsNull ? 0 : childBefore.Scroll.Y;
        var io = ImGui.GetIO();
        var left = ImGui.IsKeyPressed(ImGuiKey.LeftArrow);
        var right = ImGui.IsKeyPressed(ImGuiKey.RightArrow);
        var up = ImGui.IsKeyPressed(ImGuiKey.UpArrow);
        var down = ImGui.IsKeyPressed(ImGuiKey.DownArrow);
        var pageUp = ImGui.IsKeyPressed(ImGuiKey.PageUp);
        var pageDown = ImGui.IsKeyPressed(ImGuiKey.PageDown);
        var home = ImGui.IsKeyPressed(ImGuiKey.Home);
        var endKey = ImGui.IsKeyPressed(ImGuiKey.End);
        var mousePoint = io.MousePos;
        var mouseClicked = io.MouseClicked[0] && Inside(mousePoint, frameMin, frameMax)
            && (childBefore.IsNull || mousePoint.X < childBefore.InnerRect.Max.X);
        var dragging = wasActive && io.MouseDown[0] && !stateBefore.SelectedAllMouseLock;
        var tint = ImGui.GetColorU32(ImGuiCol.Text);
        var selectionTint = ImGui.GetColorU32(ImGuiCol.TextSelectedBg);
        var follow = false;
        Exception? callbackError = null;
        using var clipboard = ClipboardScope.Open(proxy, Math.Max(maxLength, Encoding.UTF8.GetByteCount(before)));

        ImGui.ImGuiInputTextCallbackDelegate callback = (ref ImGuiInputTextCallbackData data) =>
        {
            try
            {
                if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
                {
                    if (!clipboard.ReadProxy && EditorProxy.IsReserved((char)data.EventChar))
                        data.EventChar = proxy.Encode(((char)data.EventChar).ToString())[0];
                    return clipboard.AcceptCharacter(Encoding.UTF8.GetByteCount(proxy.Decode(((char)data.EventChar).ToString()))) ? 0 : 1;
                }
                if (data.EventFlag != ImGuiInputTextFlags.CallbackAlways)
                    return 0;

                var currentNative = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data.Buf, data.BufTextLen));
                var current = proxy.Decode(currentNative);
                var layout = renderer.GetLayout(current, fontSize);
                var child = ImGuiP.GetCurrentWindow();
                // BeginChild has already applied native wheel/scrollbar input.
                scrollY = child.Scroll.Y;
                var origin = child.DC.CursorStartPos + child.Scroll + padding;
                var cursor = proxy.ToSourcePosition(currentNative, FromBytes(currentNative, data.CursorPos));
                var anchor = proxy.ToSourcePosition(currentNative, FromBytes(currentNative, data.SelectionStart));
                var selectionEnd = proxy.ToSourcePosition(currentNative, FromBytes(currentNative, data.SelectionEnd));
                var pointer = (mouseClicked || dragging) && context.ActiveId == id && !ImGuiP.GetInputTextState(id).SelectedAllMouseLock;
                if (pointer)
                {
                    var hit = layout.HitTestPoint(mousePoint - origin + new Vector2(scrollX, scrollY));
                    cursor = Math.Clamp(hit.Position + (hit.Trailing ? hit.Length : 0), 0, current.Length);
                    if (mouseClicked && io.MouseClickedCount[0] >= 2 && !io.KeyShift)
                    {
                        if ((io.MouseClickedCount[0] - 2) % 2 == 0)
                        {
                            anchor = cursor == 0 || current[cursor - 1] is '\n' or '\r' ? cursor : WordLeft(current, cursor);
                            cursor = WordRight(current, anchor, true);
                            anchor = Snap(layout, anchor, false);
                            cursor = Snap(layout, cursor, true);
                            selectionEnd = cursor;
                        }
                        else
                        {
                            var starts = LineStarts(current);
                            var row = SourceLine(starts, cursor);
                            var rowEnd = row + 1 < starts.Count ? starts[row + 1] : current.Length;
                            var isNewline = cursor < current.Length && current[cursor] is '\n' or '\r';
                            anchor = isNewline ? starts[row] : rowEnd;
                            cursor = isNewline ? rowEnd : starts[row];
                            selectionEnd = cursor;
                        }
                    }
                    else
                    {
                        cursor = Snap(layout, cursor, hit.Trailing);
                        anchor = mouseClicked && !io.KeyShift ? cursor
                            : Math.Clamp(anchorBefore < 0 ? cursor : anchorBefore, 0, current.Length);
                        selectionEnd = cursor;
                    }
                    proxy.PreferredX = null;
                }
                else if (wasActive && current == before && (left || right))
                {
                    if (!io.KeyShift && anchorBefore != endBefore)
                        cursor = left ? Math.Min(anchorBefore, endBefore) : Math.Max(anchorBefore, endBefore);
                    else if (io.KeyCtrl)
                        cursor = Snap(layout, left ? WordLeft(current, cursorBefore)
                            : WordRight(current, cursorBefore, io.ConfigMacOSXBehaviors), right);
                    else
                        cursor = Step(layout, Math.Clamp(cursorBefore, 0, current.Length), right);
                    anchor = io.KeyShift ? Math.Clamp(anchorBefore == endBefore ? cursorBefore : anchorBefore, 0, current.Length) : cursor;
                    selectionEnd = cursor;
                    proxy.PreferredX = null;
                }
                else if (wasActive && current == before && (up || down || pageUp || pageDown) && !io.KeyCtrl)
                {
                    var starts = LineStarts(current);
                    var row = SourceLine(starts, cursorBefore);
                    var point = layout.HitTestPosition(Math.Clamp(cursorBefore, 0, current.Length));
                    proxy.PreferredX ??= point.X;
                    var rowHeight = CaretHeight(layout, point, fontSize);
                    var count = pageUp || pageDown ? Math.Max(1, (int)((frameSize.Y - 2 * padding.Y) / rowHeight)) : 1;
                    var target = Math.Clamp(row + (up || pageUp ? -count : count), 0, starts.Count - 1);
                    var targetPoint = layout.HitTestPosition(starts[target]);
                    var targetHeight = CaretHeight(layout, targetPoint, fontSize);
                    var hit = layout.HitTestPoint(new(proxy.PreferredX.Value, targetPoint.Y + targetHeight * .5f));
                    var rowEnd = LineEnd(current, starts, target);
                    cursor = Snap(layout, Math.Clamp(hit.Position + (hit.Trailing ? hit.Length : 0), starts[target], rowEnd), hit.Trailing);
                    anchor = io.KeyShift ? Math.Clamp(anchorBefore == endBefore ? cursorBefore : anchorBefore, 0, current.Length) : cursor;
                    selectionEnd = cursor;
                }
                else if (wasActive && current == before && (home || endKey))
                {
                    var starts = LineStarts(current);
                    var row = SourceLine(starts, cursorBefore);
                    cursor = io.KeyCtrl ? (home ? 0 : current.Length) : home ? starts[row] : LineEnd(current, starts, row);
                    anchor = io.KeyShift ? Math.Clamp(anchorBefore == endBefore ? cursorBefore : anchorBefore, 0, current.Length) : cursor;
                    selectionEnd = cursor;
                    proxy.PreferredX = null;
                }
                else
                {
                    cursor = Snap(layout, cursor, true);
                    if (anchor == selectionEnd) anchor = selectionEnd = cursor;
                    else
                    {
                        anchor = Snap(layout, anchor, anchor > selectionEnd);
                        selectionEnd = Snap(layout, selectionEnd, selectionEnd > anchor);
                    }
                    if (current != before) proxy.PreferredX = null;
                }

                data.CursorPos = ToBytes(currentNative, proxy.ToNativePosition(currentNative, cursor));
                data.SelectionStart = ToBytes(currentNative, proxy.ToNativePosition(currentNative, anchor));
                data.SelectionEnd = ToBytes(currentNative, proxy.ToNativePosition(currentNative, selectionEnd));
                var state = ImGuiP.GetInputTextState(id);
                follow = state.CursorFollow || cursor != cursorBefore || current != before || pointer;
                // The native editor retains editing; its unshaped follow geometry is replaced below.
                state.CursorFollow = false;
                return 0;
            }
            catch (Exception error)
            {
                callbackError ??= error;
                return data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter ? 1 : 0;
            }
        };

        bool changed;
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Vector4.Zero);
        try
        {
            changed = ImGui.InputTextMultiline(nativeId, ref nativeValue, maxLength, size,
                flags | ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackCharFilter, callback);
        }
        finally { ImGui.PopStyleColor(2); }
        value = proxy.Decode(nativeValue);
        if (context.InputTextState.ID == id) activeProxy = proxy;
        if (callbackError is not null || clipboard.Error is not null)
            throw new InvalidOperationException("The shaped multiline editor callback failed.", callbackError ?? clipboard.Error);

        var childAfter = FindInputChild(parent, id);
        if (childAfter.IsNull || childAfter.LastFrameActive != ImGui.GetFrameCount() || childAfter.SkipItems)
            return changed;
        var content = renderer.GetLayout(value, fontSize);
        var textOrigin = childAfter.DC.CursorStartPos + childAfter.Scroll + padding;
        var clipMin = childAfter.InnerClipRect.Min;
        var clipMax = childAfter.InnerClipRect.Max;
        var viewportHeight = Math.Max(1, childAfter.InnerRect.Max.Y - childAfter.InnerRect.Min.Y);
        var contentHeight = content.Size.Y + 2 * padding.Y;
        childAfter.ContentSize.Y = contentHeight;
        childAfter.DC.CursorMaxPos.Y = childAfter.DC.CursorStartPos.Y + contentHeight;
        childAfter.ScrollMax.Y = Math.Max(0, contentHeight - viewportHeight);
        scrollY = childAfter.Scroll.Y;
        var stateAfter = ImGuiP.GetInputTextState(id);
        var active = context.ActiveId == id && !stateAfter.IsNull;
        var caret = Vector2.Zero;
        var caretHeight = CaretHeight(content, caret, fontSize);
        if (active)
        {
            caret = content.HitTestPosition(proxy.ToSourcePosition(nativeValue, stateAfter.Stb.Cursor));
            caretHeight = CaretHeight(content, caret, fontSize);
            var visibleWidth = Math.Max(1, clipMax.X - textOrigin.X - padding.X);
            var visibleHeight = Math.Max(1, viewportHeight - 2 * padding.Y);
            if ((flags & ImGuiInputTextFlags.NoHorizontalScroll) != 0) scrollX = 0;
            else if (follow)
            {
                if (caret.X < scrollX) scrollX = Math.Max(0, caret.X - visibleWidth * .25f);
                else if (caret.X >= scrollX + visibleWidth) scrollX = Math.Max(0, caret.X - visibleWidth * .75f);
            }
            scrollX = Math.Clamp(scrollX, 0, Math.Max(0, content.Size.X - visibleWidth));
            if (follow)
            {
                if (caret.Y < scrollY) scrollY = caret.Y;
                else if (caret.Y + caretHeight > scrollY + visibleHeight) scrollY = caret.Y + caretHeight - visibleHeight;
            }
            stateAfter.ScrollX = scrollX;
        }
        else scrollX = 0;
        scrollY = Math.Clamp(scrollY, 0, childAfter.ScrollMax.Y);
        childAfter.Scroll.Y = scrollY;
        var scroll = new Vector2(scrollX, scrollY);
        var list = childAfter.DrawList;
        list.PushClipRect(clipMin, clipMax, true);
        try
        {
            if (active && stateAfter.Stb.SelectStart != stateAfter.Stb.SelectEnd)
            {
                var start = proxy.ToSourcePosition(nativeValue, Math.Min(stateAfter.Stb.SelectStart, stateAfter.Stb.SelectEnd));
                var end = proxy.ToSourcePosition(nativeValue, Math.Max(stateAfter.Stb.SelectStart, stateAfter.Stb.SelectEnd));
                foreach (var rectangle in content.HitTestRange(start, end - start))
                {
                    var max = rectangle.Max;
                    if (max.X == rectangle.Min.X) max.X += fontSize * .5f;
                    list.AddRectFilled(textOrigin + rectangle.Min - scroll, textOrigin + max - scroll, selectionTint);
                }
            }
            var starts = LineStarts(value);
            for (var row = 0; row < starts.Count; row++)
            {
                var position = content.HitTestPosition(starts[row]);
                var height = CaretHeight(content, position, fontSize);
                if (textOrigin.Y + position.Y + height - scrollY <= clipMin.Y
                    || textOrigin.Y + position.Y - scrollY >= clipMax.Y) continue;
                var line = value[starts[row]..LineEnd(value, starts, row)];
                if (line.Length != 0) renderer.Draw(list, textOrigin + position - scroll, tint, line, fontSize);
            }
            if (active && (!io.ConfigInputTextCursorBlink || stateAfter.CursorAnim <= 0 || stateAfter.CursorAnim % 1.2f <= .8f))
            {
                var point = textOrigin + caret - scroll;
                list.AddLine(point, point + new Vector2(0, caretHeight), tint);
            }
        }
        finally { list.PopClipRect(); }
        if (active)
        {
            ref var ime = ref context.PlatformImeData;
            ime.InputPos = textOrigin + caret - scroll;
            ime.InputLineHeight = caretHeight;
        }
        if (caption.Length != 0)
            MaterialText.AddText(parent.DrawList, font, fontSize,
                new Vector2(frameMax.X + ImGui.GetStyle().ItemInnerSpacing.X, frameMin.Y + padding.Y), tint, caption);
        return changed;
    }

    private static ImGuiWindowPtr FindInputChild(ImGuiWindowPtr parent, uint id)
    {
        var windows = ImGui.GetCurrentContext().Windows;
        for (var index = 0; index < windows.Size; index++)
        {
            var window = new ImGuiWindowPtr(windows.Data[index]);
            if (window.ChildId == id && window.ParentWindow == parent) return window;
        }
        return default;
    }

    private static float CaretHeight(MaterialTextLayout layout, Vector2 point, float fallback)
    {
        var hit = layout.HitTestPoint(point + new Vector2(0, .1f));
        var height = hit.Max.Y - hit.Min.Y;
        return height > 0 && float.IsFinite(height) ? height : fallback;
    }

    private static List<int> LineStarts(string text)
    {
        var result = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
            if (text[index] is '\r' or '\n')
            {
                if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                result.Add(index + 1);
            }
        return result;
    }

    private static int SourceLine(IReadOnlyList<int> starts, int position)
    {
        var result = 0;
        while (result + 1 < starts.Count && starts[result + 1] <= position) result++;
        return result;
    }

    private static int LineEnd(string text, IReadOnlyList<int> starts, int row)
    {
        var end = row + 1 < starts.Count ? starts[row + 1] - 1 : text.Length;
        return end > starts[row] && text[end - 1] == '\r' ? end - 1 : end;
    }

    // These are the retained ImGui 1.88 word boundaries; only the pointer's
    // shaped position is substituted, including its Mac-style double click.
    private static bool Separator(char character)
        => character is ' ' or '\t' or '\u3000' or ',' or ';' or '(' or ')' or '{' or '}' or '[' or ']' or '|' or '\n' or '\r';

    private static int WordLeft(string text, int position)
    {
        position--;
        while (position > 0 && !(Separator(text[position - 1]) && !Separator(text[position]))) position--;
        return Math.Max(0, position);
    }

    private static int WordRight(string text, int position, bool mac)
    {
        position++;
        while (position < text.Length && !(mac
            ? !Separator(text[position - 1]) && Separator(text[position])
            : Separator(text[position - 1]) && !Separator(text[position]))) position++;
        return Math.Min(text.Length, position);
    }

    private static bool NeedsShaping(string text)
        => MaterialText.RequiresShaping(text);

    private static Vector2 TextOrigin(Vector2 min, Vector2 max, Vector2 padding, float lineHeight)
        => new(min.X + padding.X, min.Y + Math.Max(0, (max.Y - min.Y - lineHeight) * .5f));

    private static bool Inside(Vector2 point, Vector2 min, Vector2 max)
        => point.X >= min.X && point.Y >= min.Y && point.X < max.X && point.Y < max.Y;

    private static int FromBytes(string text, int bytes)
        => Encoding.UTF8.GetCharCount(Encoding.UTF8.GetBytes(text).AsSpan(0, Math.Clamp(bytes, 0, Encoding.UTF8.GetByteCount(text))));

    private static int ToBytes(string text, int position)
        => Encoding.UTF8.GetByteCount(text.AsSpan(0, Math.Clamp(position, 0, text.Length)));

    private static int Snap(MaterialTextLayout layout, int position, bool trailing)
    {
        position = Math.Clamp(position, 0, layout.Text.Length);
        foreach (var cluster in layout.Clusters)
        {
            if (position <= cluster.Position)
                return cluster.Position;
            if (position < cluster.Position + cluster.Length)
                return trailing ? cluster.Position + cluster.Length : cluster.Position;
        }
        return layout.Text.Length;
    }

    private static int Step(MaterialTextLayout layout, int position, bool forward)
    {
        if (forward)
        {
            foreach (var cluster in layout.Clusters)
                if (cluster.Position + cluster.Length > position)
                    return cluster.Position + cluster.Length;
            return layout.Text.Length;
        }
        for (var index = layout.Clusters.Count - 1; index >= 0; index--)
            if (layout.Clusters[index].Position < position)
                return layout.Clusters[index].Position;
        return 0;
    }

    // This ImWchar16 host replaces supplementary characters before native editing.
    // Private-use tokens retain each complete source scalar through its existing
    // buffer, clipboard and undo paths. Tokens never enter the consumer's string.
    private sealed class EditorProxy(nint context, uint id, bool multiline = false)
    {
        private const char FirstToken = '\ue000';
        private const char LastToken = '\uf8ff';
        private readonly Dictionary<string, char> toToken = new(StringComparer.Ordinal);
        private readonly Dictionary<char, string> toSource = new();

        internal nint Context { get; } = context;
        internal uint Id { get; } = id;
        internal bool Multiline { get; } = multiline;
        internal float? PreferredX { get; set; }
        internal bool HasTokens => toToken.Count != 0;
        internal static bool IsReserved(char value) => value is >= FirstToken and <= LastToken;

        internal string Encode(string text)
        {
            // Nothing to map: the encoded text equals the source (common: BMP-only text).
            var span = text.AsSpan();
            if (span.IndexOfAnyInRange('\ud800', '\udfff') < 0 && span.IndexOfAnyInRange(FirstToken, LastToken) < 0
                && !(Multiline && span.Contains('\r')))
                return text;
            var result = new StringBuilder(text.Length);
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (!char.IsSurrogate(character) && !IsReserved(character) && !(Multiline && character == '\r'))
                {
                    result.Append(character);
                    continue;
                }
                var length = char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
                var source = text.Substring(index, length);
                if (!toToken.TryGetValue(source, out var token))
                {
                    if (toToken.Count > LastToken - FirstToken)
                        throw new InvalidOperationException("The active shaped editor exceeds its 6400 supplementary/private-use token bound.");
                    token = (char)(FirstToken + toToken.Count);
                    toToken.Add(source, token);
                    toSource.Add(token, source);
                }
                result.Append(token);
                index += length - 1;
            }
            return result.ToString();
        }

        internal string Decode(string text)
        {
            // Tokens only exist in the private-use range; without any the text is unchanged.
            if (toSource.Count == 0 || text.AsSpan().IndexOfAnyInRange(FirstToken, LastToken) < 0)
                return text;
            var result = new StringBuilder(text.Length);
            foreach (var character in text)
                if (toSource.TryGetValue(character, out var source))
                    result.Append(source);
                else
                    result.Append(character);
            return result.ToString();
        }

        internal int ToSourcePosition(string text, int position)
        {
            position = Math.Clamp(position, 0, text.Length);
            if (toSource.Count == 0) return position;
            var sourcePosition = 0;
            for (var index = 0; index < position; index++)
                sourcePosition += toSource.TryGetValue(text[index], out var source) ? source.Length : 1;
            return sourcePosition;
        }

        internal int ToNativePosition(string text, int position)
        {
            if (position <= 0)
                return 0;
            if (toSource.Count == 0) return Math.Min(position, text.Length);
            var sourcePosition = 0;
            for (var index = 0; index < text.Length; index++)
            {
                sourcePosition += toSource.TryGetValue(text[index], out var source) ? source.Length : 1;
                if (sourcePosition >= position)
                    return index + 1;
            }
            return text.Length;
        }

        internal int RemainingBytes(int maximum)
        {
            var state = ImGuiP.GetInputTextState(Id);
            if (state.IsNull)
                return maximum;
            var native = new char[state.CurLenW];
            for (var index = 0; index < native.Length; index++)
                native[index] = (char)state.TextW[index];
            var text = new string(native);
            var selectedStart = Math.Clamp(Math.Min(state.Stb.SelectStart, state.Stb.SelectEnd), 0, text.Length);
            var selectedEnd = Math.Clamp(Math.Max(state.Stb.SelectStart, state.Stb.SelectEnd), selectedStart, text.Length);
            return Math.Max(0, maximum - Encoding.UTF8.GetByteCount(Decode(text))
                + Encoding.UTF8.GetByteCount(Decode(text[selectedStart..selectedEnd])));
        }
    }

    private sealed class ClipboardScope : IDisposable
    {
        // Scopes are opened every frame a shaped field is drawn; disposed ones are reused.
        [ThreadStatic] private static Stack<ClipboardScope>? pool;
        private EditorProxy proxy = null!;
        private nint get;
        private nint set;
        private nint savedGet;
        private nint savedSet;
        private int maximumBytes;
        private ClipboardScope? previous;
        private int pasteAvailable;
        private int pasteAccepted;
        private GCHandle pinned;
        private bool open;
        internal bool ReadProxy { get; private set; }
        internal Exception? Error { get; private set; }

        private ClipboardScope() { }

        internal static ClipboardScope Open(EditorProxy proxy, int maximumBytes)
        {
            var scope = pool is { Count: > 0 } ? pool.Pop() : new ClipboardScope();
            scope.Begin(proxy, maximumBytes);
            return scope;
        }

        private void Begin(EditorProxy proxy, int maximumBytes)
        {
            open = true;
            pasteAvailable = pasteAccepted = 0;
            ReadProxy = false;
            Error = null;
            this.proxy = proxy;
            this.maximumBytes = maximumBytes;
            var io = ImGui.GetIO();
            previous = clipboardScope;
            savedGet = (nint)io.GetClipboardTextFn;
            savedSet = (nint)io.SetClipboardTextFn;
            get = previous?.get ?? savedGet;
            set = previous?.set ?? savedSet;
            clipboardScope = this;
            if (get != 0)
                io.GetClipboardTextFn = (void*)(delegate* unmanaged[Cdecl]<void*, byte*>)&Get;
            if (set != 0)
                io.SetClipboardTextFn = (void*)(delegate* unmanaged[Cdecl]<void*, byte*, void>)&Set;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static byte* Get(void* userData)
        {
            var scope = clipboardScope;
            if (scope is null || scope.get == 0)
                return null;
            try
            {
                var original = ((delegate* unmanaged[Cdecl]<void*, byte*>)scope.get)(userData);
                if (original is null)
                    return null;
                var source = Marshal.PtrToStringUTF8((nint)original) ?? string.Empty;
                var native = Encoding.UTF8.GetBytes(scope.proxy.Encode(source) + '\0');
                if (scope.pinned.IsAllocated)
                    scope.pinned.Free();
                scope.pinned = GCHandle.Alloc(native, GCHandleType.Pinned);
                scope.ReadProxy = true;
                scope.pasteAvailable = scope.proxy.RemainingBytes(scope.maximumBytes);
                scope.pasteAccepted = 0;
                return (byte*)scope.pinned.AddrOfPinnedObject();
            }
            catch (Exception error)
            {
                scope.Error ??= error;
                return null;
            }
        }

        internal bool AcceptCharacter(int sourceBytes)
        {
            if (!ReadProxy)
                return sourceBytes <= proxy.RemainingBytes(maximumBytes);
            if (sourceBytes > pasteAvailable - pasteAccepted)
                return false;
            pasteAccepted += sourceBytes;
            return true;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static void Set(void* userData, byte* native)
        {
            var scope = clipboardScope;
            if (scope is null || scope.set == 0 || native is null)
                return;
            try
            {
                var text = Marshal.PtrToStringUTF8((nint)native) ?? string.Empty;
                var source = Encoding.UTF8.GetBytes(scope.proxy.Decode(text) + '\0');
                fixed (byte* bytes = source)
                    ((delegate* unmanaged[Cdecl]<void*, byte*, void>)scope.set)(userData, bytes);
            }
            catch (Exception error)
            {
                scope.Error ??= error;
            }
        }

        public void Dispose()
        {
            if (!open) return;
            open = false;
            var io = ImGui.GetIO();
            io.GetClipboardTextFn = (void*)savedGet;
            io.SetClipboardTextFn = (void*)savedSet;
            clipboardScope = previous;
            if (pinned.IsAllocated)
                pinned.Free();
            previous = null;
            proxy = null!;
            (pool ??= new()).Push(this);
        }
    }
}
