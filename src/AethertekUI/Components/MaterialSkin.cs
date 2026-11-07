using System.Numerics;
using System.Runtime.Intrinsics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

/// <summary>Selected materials painted inside the caller's current native window.</summary>
public static partial class MaterialSkin
{
    internal static bool HasMaterial => MaterialTheme.CurrentOrNull is { } theme
        && (theme.IsGlassmorphism || theme.IsSpatial || theme.IsCyberpunk);

    /// <summary>Wrap the adopting owner's Begin with this scope, then dispose before its content. Theme the whole Begin/End lifetime.</summary>
    public static MaterialStyleScope BackdropStyle(bool popup = false)
        => BackdropStyle(popup, 1);

    /// <summary>Explicit translucent backdrop for a consumer that has selected native background blur.</summary>
    public static MaterialStyleScope BackdropStyle(bool popup, float backgroundOpacity)
    {
        var scope = new MaterialStyleScope();
        var target = popup ? ImGuiCol.PopupBg : ImGuiCol.WindowBg;
        if (HasMaterial || HasBackgroundRequest) scope.Color(target, Vector4.Zero);
        else if (backgroundOpacity < 1)
        {
            var background = ImGui.GetStyle().Colors[(int)target];
            background.W *= float.IsFinite(backgroundOpacity) ? Math.Clamp(backgroundOpacity, 0, 1) : 1;
            scope.Color(target, background);
        }
        return scope;
    }

    /// <summary>Call first after native Begin and BackdropStyle, before tables or draw-channel splits.</summary>
    public static unsafe void Backdrop()
        => Backdrop(1);

    /// <summary>Paints the selected background, using the scoped renderer when ready and retaining ordinary paint otherwise.</summary>
    public static unsafe void Backdrop(float backgroundOpacity)
    {
        if (MaterialTheme.CurrentOrNull is not { } theme || (!HasMaterial && !HasBackgroundRequest)) return;
        var window = ImGuiP.GetCurrentWindow();
        if ((window.Flags & ImGuiWindowFlags.NoBackground) != 0 && backgroundRenderer == null) return;
        var windowMin = ImGui.GetWindowPos();
        var min = windowMin + new Vector2(0, ImGuiP.TitleBarHeight(window));
        var max = windowMin + ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();
        if (dl.Splitter.Count > 1)
            throw new InvalidOperationException("Call MaterialSkin.Backdrop first after Begin, before tables or draw-channel splits.");
        var c = theme.Colors;
        var alpha = ImGui.GetStyle().Alpha * theme.SurfaceOpacity *
            (float.IsFinite(backgroundOpacity) ? Math.Clamp(backgroundOpacity, 0, 1) : 1) *
            (backgroundRenderer?.Opacity ?? 1);
        var selectedRadius = window.DockIsActive ? 0 : window.ViewportOwned ? ImGui.GetStyle().WindowRounding : window.WindowRounding;
        var radius = Math.Clamp(selectedRadius,
            0, Math.Min(max.X - min.X, max.Y - min.Y) * .5f);
        var roundTop = (window.Flags & ImGuiWindowFlags.NoTitleBar) != 0;
        var corners = roundTop ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersBottom;
        var state = backgroundRenderer;
        var glass = state?.BlurEnabled == true && state.Renderer.DrawBackground(dl, windowMin, max, radius,
            (theme.IsLcars ? MaterialLcars.Background : c.Background) with { W = .42f * ImGui.GetStyle().Alpha },
            state.Frost, windowBackground: true);
        if (glass)
        {
            state!.SetCurrentRoot(true);
            if (theme.IsGlassmorphism)
            {
                dl.PushClipRect(Vector2.Max(min, window.InnerRect.Min), Vector2.Min(max, window.InnerRect.Max), false);
                try { GlassBackdrop(dl, min, max, theme, alpha, radius, roundTop, true); }
                finally { dl.PopClipRect(); }
            }
            return;
        }
        state?.SetCurrentRoot(false);
        // Native chrome is already emitted by Begin. Merge this bounded background before it,
        // retaining the owner's outer clip and all native border/title/scrollbar/resize ink.
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        dl.PushClipRect(window.OuterRectClipped.Min, window.OuterRectClipped.Max, false);
        try
        {
            if (theme.IsGlassmorphism && theme.AppearanceSettings?.GlassBackground == MaterialGlassBackground.Image)
            {
                dl.AddRectFilled(min, max, Ink(c.Background, alpha), radius, corners);
                if (state?.Renderer is { ImageMode: true } renderer && renderer.DrawPlainImage(dl, min, max))
                    state.SetCurrentRoot(true);
            }
            else if (theme.IsGlassmorphism) GlassBackdrop(dl, min, max, theme, alpha, radius, roundTop, false);
            else if (theme.IsSpatial)
            {
                var first = dl.VtxBuffer.Size;
                dl.AddRectFilled(min, max, Ink(Vector4.One, alpha), radius, corners);
                for (var i = first; i < dl.VtxBuffer.Size; i++)
                {
                    var vertex = dl.VtxBuffer[i];
                    var u = Math.Clamp((vertex.Pos.X - min.X) / Math.Max(1, max.X - min.X), 0, 1);
                    var v = Math.Clamp((vertex.Pos.Y - min.Y) / Math.Max(1, max.Y - min.Y), 0, 1);
                    var color = u >= v
                        ? c.SurfaceContainerLow * (1 - u) + c.Background * (u - v) + c.SurfaceContainer * v
                        : c.SurfaceContainerLow * (1 - v) + c.Background * (v - u) + c.SurfaceContainer * u;
                    vertex.Col = Ink(color, (vertex.Col >> 24) / 255f);
                    dl.VtxBuffer[i] = vertex;
                }
            }
            else dl.AddRectFilled(min, max, Ink(theme.IsLcars ? MaterialLcars.Background : c.Background, alpha), radius, corners);
        }
        finally
        {
            dl.PopClipRect();
            dl.ChannelsSetCurrent(0);
            var first = dl.Splitter.Channels.Data[0];
            dl.Splitter.Channels.Data[0] = dl.Splitter.Channels.Data[1];
            dl.Splitter.Channels.Data[1] = first;
            dl.Splitter.Current = 1;
            dl.ChannelsMerge();
        }
    }

    private static void GlassBackdrop(ImDrawListPtr dl, Vector2 min, Vector2 max, MaterialTheme theme, float alpha,
        float radius, bool roundTop, bool glass)
    {
        var size = max - min;
        if (size.X <= 0 || size.Y <= 0) return;
        var c = theme.Colors;
        var settings = theme.AppearanceSettings;
        if (settings?.GlassBackground == MaterialGlassBackground.Image) return;
        if (settings?.GlassBackground == MaterialGlassBackground.Lights)
        {
            GlassLights(dl, min, max, theme, alpha, radius, roundTop, glass);
            return;
        }
        var wave1 = Rgb(settings?.GlassWave1Rgb ?? MaterialColorSettings.DefaultGlassWave1Rgb);
        var wave2 = Rgb(settings?.GlassWave2Rgb ?? MaterialColorSettings.DefaultGlassWave2Rgb);
        var wave3 = Rgb(settings?.GlassWave3Rgb ?? MaterialColorSettings.DefaultGlassWave3Rgb);
        var animated = theme.Motion.Enabled && settings?.GlassAnimation != false;
        var time = animated ? (float)ImGui.GetTime() * .65f * (settings?.GlassAnimationSpeed ?? 1) : 0;
        var scale = MaterialTheme.Metrics.Scale;
        // Bilinear corner colours form curved ribbons. The grid is bounded at 89 x 65 vertices.
        var columns = Math.Clamp((int)MathF.Ceiling(size.X / (12 * scale)), 48, 88);
        var rows = Math.Clamp((int)MathF.Ceiling(size.Y / (12 * scale)), 32, 64);
        var grid = GlassGrid.Shared;
        // Still backgrounds repeat their colours every frame; only the positions follow the window.
        var key = new GlassGrid.Key(null, c.Background, c.OnSurface, wave1, wave2, wave3, size, radius, roundTop, alpha, glass, columns, rows);
        var colors = animated ? null : grid.Find(key);
        var fill = colors == null;
        colors ??= animated ? grid.Colors : grid.Store(key);
        var stride = columns + 1;
        var topRows = radius > 0 && roundTop ? 4 : 0;
        var bottomRows = radius > 0 ? 4 : 0;
        var columnInset = float.NaN;
        for (var row = 0; row <= rows; row++)
        {
            var y = topRows > 0 && row <= topRows ? radius * row / topRows
                : bottomRows > 0 && row >= rows - bottomRows ? size.Y - radius + radius * (row - rows + bottomRows) / bottomRows
                : (topRows > 0 ? radius : 0) + (size.Y - (topRows > 0 ? radius : 0) - (bottomRows > 0 ? radius : 0))
                    * (row - topRows) / (rows - topRows - bottomRows);
            var v = y / size.Y;
            var cornerY = topRows > 0 && y < radius ? radius - y : y > size.Y - radius ? y - size.Y + radius : 0;
            var inset = radius - MathF.Sqrt(Math.Max(0, radius * radius - cornerY * cornerY));
            // Columns depend on the row only through the corner inset, which is 0 away from the corners.
            if (!inset.Equals(columnInset))
            {
                columnInset = inset;
                for (var column = 0; column <= columns; column++)
                {
                    var x = min.X + inset + (size.X - 2 * inset) * column / columns;
                    var u = (x - min.X) / size.X;
                    grid.ColumnX[column] = x;
                    grid.ColumnU[column] = u;
                    if (fill) grid.ColumnTerm[column] = .80f - .68f * u + .14f * MathF.Sin(u * 5.3f - time * .83f + .7f);
                }
            }
            grid.Y[row] = min.Y + y;
            var offset = row * stride;
            grid.ColumnX.AsSpan(0, stride).CopyTo(grid.X.AsSpan(offset, stride));
            if (!fill) continue;
            var primaryCenter = Vector128.Create(.22f + .38f * v + .11f * MathF.Sin(v * 5 - time * .9f));
            var secondaryCenter = Vector128.Create(.84f - .38f * v + .13f * MathF.Sin(v * 5.2f + time * .72f + .8f));
            var rowV = Vector128.Create(v);
            var shade = Vector128.Create(.14f + .04f * v);
            var background = c.Background;
            var ink = c.OnSurface;
            // Four columns at a time; the column arrays are padded past the last column.
            for (var column = 0; column < stride; column += 4)
            {
                var u = Vector128.Create(grid.ColumnU.AsSpan(column, 4));
                var d1 = u - primaryCenter;
                var d2 = u - secondaryCenter;
                var d3 = rowV - Vector128.Create(grid.ColumnTerm.AsSpan(column, 4));
                var band1 = Vector128.Exp(d1 * d1 * -45f);
                var band2 = Vector128.Exp(d2 * d2 * -55f);
                var band3 = Vector128.Exp(d3 * d3 * -65f);
                var mix = shade + u * .07f;
                var crest = Vector128.Max(band1, Vector128.Max(band2, band3));
                var opacity = glass ? crest * (.58f * alpha) : Vector128.Create(alpha);
                crest *= crest; crest *= crest; crest *= crest;
                band1 *= .58f; band2 *= .50f; band3 *= .50f; crest *= .10f;
                GlassGrid.Pack(
                    GlassGrid.Ribbon(background.X, wave1.X, wave2.X, wave3.X, ink.X, mix, band1, band2, band3, crest),
                    GlassGrid.Ribbon(background.Y, wave1.Y, wave2.Y, wave3.Y, ink.Y, mix, band1, band2, band3, crest),
                    GlassGrid.Ribbon(background.Z, wave1.Z, wave2.Z, wave3.Z, ink.Z, mix, band1, band2, band3, crest),
                    opacity).CopyTo(grid.RowColors, column);
            }
            grid.RowColors.AsSpan(0, stride).CopyTo(colors.AsSpan(offset, stride));
        }
        grid.Emit(dl, colors, columns, rows);
    }

    private static void GlassLights(ImDrawListPtr dl, Vector2 min, Vector2 max, MaterialTheme theme, float alpha,
        float radius, bool roundTop, bool glass)
    {
        var size = max - min;
        if (size.X <= 0 || size.Y <= 0) return;
        var settings = theme.AppearanceSettings;
        var first = Rgb(settings?.GlassLight1Rgb ?? MaterialColorSettings.DefaultGlassWave1Rgb);
        var second = Rgb(settings?.GlassLight2Rgb ?? MaterialColorSettings.DefaultGlassWave2Rgb);
        var third = Rgb(settings?.GlassLight3Rgb ?? MaterialColorSettings.DefaultGlassWave3Rgb);
        var animated = theme.Motion.Enabled && settings?.GlassAnimation != false;
        var time = animated ? (float)ImGui.GetTime() * .35f * (settings?.GlassAnimationSpeed ?? 1) : 0;
        var pointer = animated ? (ImGui.GetMousePos() - min) / size : new Vector2(.5f);
        if (!float.IsFinite(pointer.X) || !float.IsFinite(pointer.Y)) pointer = new(.5f);
        pointer = Vector2.Clamp(pointer, Vector2.Zero, Vector2.One);
        var effect = settings?.GlassLights ?? MaterialGlassLights.AmbientGlows;
        var scale = MaterialTheme.Metrics.Scale;
        var columns = Math.Clamp((int)MathF.Ceiling(size.X / (16 * scale)), 32, 64);
        var rows = Math.Clamp((int)MathF.Ceiling(size.Y / (16 * scale)), 24, 48);
        var grid = GlassGrid.Shared;
        var key = new GlassGrid.Key(effect, theme.Colors.Background, default, first, second, third, size, radius, roundTop, alpha, glass, columns, rows);
        var colors = animated ? null : grid.Find(key);
        var fill = colors == null;
        colors ??= animated ? grid.Colors : grid.Store(key);
        var center1 = new Vector2(.28f + .17f * MathF.Sin(time * .61f), .33f + .20f * MathF.Cos(time * .47f));
        var center2 = new Vector2(.73f + .14f * MathF.Cos(time * .53f), .62f + .19f * MathF.Sin(time * .43f));
        var center3 = new Vector2(.50f + .22f * MathF.Sin(time * .37f + 1), .72f + .14f * MathF.Cos(time * .57f));
        var beam1 = .25f * MathF.Sin(time * .6f);
        var beam2 = .22f * MathF.Cos(time * .51f);
        var beam3 = .25f * MathF.Sin(time * .43f + 1);
        var pointer2 = pointer + new Vector2(.15f, -.10f);
        var pointer3 = pointer + new Vector2(-.12f, .13f);
        var stride = columns + 1;
        var topRows = radius > 0 && roundTop ? 4 : 0;
        var bottomRows = radius > 0 ? 4 : 0;
        var columnInset = float.NaN;
        for (var row = 0; row <= rows; row++)
        {
            var y = topRows > 0 && row <= topRows ? radius * row / topRows
                : bottomRows > 0 && row >= rows - bottomRows ? size.Y - radius + radius * (row - rows + bottomRows) / bottomRows
                : (topRows > 0 ? radius : 0) + (size.Y - (topRows > 0 ? radius : 0) - (bottomRows > 0 ? radius : 0))
                    * (row - topRows) / (rows - topRows - bottomRows);
            var v = y / size.Y;
            var cornerY = topRows > 0 && y < radius ? radius - y : y > size.Y - radius ? y - size.Y + radius : 0;
            var inset = radius - MathF.Sqrt(Math.Max(0, radius * radius - cornerY * cornerY));
            if (!inset.Equals(columnInset))
            {
                columnInset = inset;
                for (var column = 0; column <= columns; column++)
                {
                    var x = min.X + inset + (size.X - 2 * inset) * column / columns;
                    grid.ColumnX[column] = x;
                    grid.ColumnU[column] = (x - min.X) / size.X;
                }
            }
            grid.Y[row] = min.Y + y;
            var offset = row * stride;
            grid.ColumnX.AsSpan(0, stride).CopyTo(grid.X.AsSpan(offset, stride));
            if (!fill) continue;
            var background = theme.Colors.Background;
            for (var column = 0; column < stride; column += 4)
            {
                var x = Vector128.Create(grid.ColumnU.AsSpan(column, 4));
                Vector128<float> a, b, c;
                if (effect == MaterialGlassLights.SweepingBeams)
                {
                    a = GlassGrid.Band(x - Vector128.Create(.32f * v + .23f + beam1), 100);
                    b = GlassGrid.Band(x + Vector128.Create(.41f * v - .88f - beam2), 85);
                    c = GlassGrid.Band(Vector128.Create(v - .55f - beam3) - x * .21f, 110);
                }
                else if (effect == MaterialGlassLights.PointerGlow)
                {
                    a = GlassGrid.Glow(x, v, pointer, 13);
                    b = GlassGrid.Glow(x, v, pointer2, 18) * .65f;
                    c = GlassGrid.Glow(x, v, pointer3, 18) * .55f;
                }
                else
                {
                    a = GlassGrid.Glow(x, v, center1, 9);
                    b = GlassGrid.Glow(x, v, center2, 8);
                    c = GlassGrid.Glow(x, v, center3, 11);
                }
                var opacity = glass ? Vector128.Max(a, Vector128.Max(b, c)) * (.55f * alpha) : Vector128.Create(alpha);
                a *= .68f; b *= .60f; c *= .56f;
                GlassGrid.Pack(
                    GlassGrid.Light(background.X, first.X, second.X, third.X, a, b, c),
                    GlassGrid.Light(background.Y, first.Y, second.Y, third.Y, a, b, c),
                    GlassGrid.Light(background.Z, first.Z, second.Z, third.Z, a, b, c),
                    opacity).CopyTo(grid.RowColors, column);
            }
            grid.RowColors.AsSpan(0, stride).CopyTo(colors.AsSpan(offset, stride));
        }
        grid.Emit(dl, colors, columns, rows);
    }

    /// <summary>
    /// Glass background grids, written straight into the draw list. Neighbouring cells share
    /// their corner vertices; the triangles and their order match one AddRectFilledMultiColor
    /// per cell, and fully transparent cells are skipped the same way. Colours are computed
    /// four vertices at a time with vector Exp, within a step of the scalar formulas.
    /// </summary>
    private sealed class GlassGrid
    {
        private const int MaxColumns = 88, MaxRows = 64, MaxVertices = (MaxColumns + 1) * (MaxRows + 1);
        private const int CacheCapacity = 8;
        [ThreadStatic] private static GlassGrid? shared;
        public static GlassGrid Shared => shared ??= new();

        // Column data is read four at a time, so it is padded to a multiple of four.
        private const int PaddedColumns = (MaxColumns + 1 + 3) / 4 * 4;
        public readonly float[] ColumnX = new float[MaxColumns + 1];
        public readonly float[] ColumnU = new float[PaddedColumns];
        public readonly float[] ColumnTerm = new float[PaddedColumns];
        public readonly uint[] RowColors = new uint[PaddedColumns];
        public readonly float[] X = new float[MaxVertices];
        public readonly float[] Y = new float[MaxRows + 1];
        public readonly uint[] Colors = new uint[MaxVertices];
        private readonly Key[] keys = new Key[CacheCapacity];
        private readonly uint[]?[] cached = new uint[CacheCapacity][];
        private readonly long[] used = new long[CacheCapacity];
        private long clock;

        /// <summary>
        /// Everything a still background's colours depend on, by value, so consumers that build
        /// a new theme every frame still hit the cache. The window position is not part of it.
        /// </summary>
        public readonly record struct Key(MaterialGlassLights? Lights, Vector4 Background, Vector4 Ink, Vector4 First,
            Vector4 Second, Vector4 Third, Vector2 Size, float Radius, bool RoundTop, float Alpha, bool Glass, int Columns, int Rows);

        public uint[]? Find(in Key key)
        {
            for (var i = 0; i < CacheCapacity; i++)
                if (cached[i] != null && keys[i] == key)
                {
                    used[i] = ++clock;
                    return cached[i];
                }
            return null;
        }

        /// <summary>A slot for new colours under this key, replacing the least recently used one.</summary>
        public uint[] Store(in Key key)
        {
            var slot = 0;
            for (var i = 1; i < CacheCapacity; i++)
                if (used[i] < used[slot]) slot = i;
            keys[slot] = key;
            used[slot] = ++clock;
            return cached[slot] ??= new uint[MaxVertices];
        }

        // One colour channel of the waves for four vertices: base shade, three ribbons, crest highlight.
        public static Vector128<float> Ribbon(float background, float wave1, float wave2, float wave3, float ink,
            Vector128<float> mix, Vector128<float> band1, Vector128<float> band2, Vector128<float> band3, Vector128<float> crest)
        {
            var value = Vector128.Create(background) + Vector128.Create(wave2 - background) * mix;
            value += (Vector128.Create(wave1) - value) * band1;
            value += (Vector128.Create(wave2) - value) * band2;
            value += (Vector128.Create(wave3) - value) * band3;
            return value + (Vector128.Create(ink) - value) * crest;
        }

        // One colour channel of the lights for four vertices.
        public static Vector128<float> Light(float background, float first, float second, float third,
            Vector128<float> a, Vector128<float> b, Vector128<float> c)
        {
            var value = Vector128.Create(background) + Vector128.Create(first - background) * a;
            value += (Vector128.Create(second) - value) * b;
            return value + (Vector128.Create(third) - value) * c;
        }

        public static Vector128<float> Band(Vector128<float> distance, float falloff) => Vector128.Exp(distance * distance * -falloff);

        public static Vector128<float> Glow(Vector128<float> x, float y, Vector2 center, float falloff)
        {
            var dx = x - Vector128.Create(center.X);
            var dy = y - center.Y;
            return Vector128.Exp((dx * dx + Vector128.Create(dy * dy)) * -falloff);
        }

        // ImGui's colour packing (saturate, scale, round) for four vertices.
        public static Vector128<uint> Pack(Vector128<float> r, Vector128<float> g, Vector128<float> b, Vector128<float> a)
            => (Byte(r) | Byte(g) << 8 | Byte(b) << 16 | Byte(a) << 24).AsUInt32();

        private static Vector128<int> Byte(Vector128<float> value)
            => Vector128.ConvertToInt32(Vector128.Min(Vector128.Max(value, Vector128<float>.Zero), Vector128<float>.One) * 255f + Vector128.Create(.5f));

        public unsafe void Emit(ImDrawListPtr dl, uint[] colors, int columns, int rows)
        {
            var stride = columns + 1;
            var cells = 0;
            for (var row = 1; row <= rows; row++)
                for (var column = 0; column < columns; column++)
                    if (Visible(colors, (row - 1) * stride + column, stride)) cells++;
            if (cells == 0) return;
            var vertices = stride * (rows + 1);
            dl.PrimReserve(cells * 6, vertices);
            var list = dl.Handle;
            var uv = list->Data->TexUvWhitePixel;
            var vertex = list->VtxWritePtr;
            for (var row = 0; row <= rows; row++)
            {
                var y = Y[row];
                for (int column = 0, i = row * stride; column <= columns; column++, i++)
                    vertex[i] = new ImDrawVert { Pos = new(X[i], y), Uv = uv, Col = colors[i] };
            }
            var index = list->IdxWritePtr;
            var start = list->VtxCurrentIdx;
            for (var row = 1; row <= rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var top = (row - 1) * stride + column;
                    if (!Visible(colors, top, stride)) continue;
                    var topLeft = (ushort)(start + top);
                    var bottomLeft = (ushort)(start + top + stride);
                    index[0] = topLeft;
                    index[1] = (ushort)(topLeft + 1);
                    index[2] = (ushort)(bottomLeft + 1);
                    index[3] = topLeft;
                    index[4] = (ushort)(bottomLeft + 1);
                    index[5] = bottomLeft;
                    index += 6;
                }
            list->VtxWritePtr = vertex + vertices;
            list->IdxWritePtr = index;
            list->VtxCurrentIdx = start + (uint)vertices;
        }

        // ImGui skips a multicolour rectangle whose four corners are all fully transparent.
        private static bool Visible(uint[] colors, int top, int stride)
            => ((colors[top] | colors[top + 1] | colors[top + stride] | colors[top + stride + 1]) & 0xFF000000) != 0;
    }


    /// <summary>Paint behind existing content. LCARS owners reserve the rail band outside their controls.</summary>
    public static void PanelFrame(Vector2 min, Vector2 max, int accent = 0, bool inset = false)
    {
        if (MaterialTheme.CurrentOrNull is not { } theme || max.X <= min.X || max.Y <= min.Y) return;
        if (theme.IsLcars)
        {
            if (!inset && !RootBackgroundAvailable) MaterialLcars.PanelFrame(min, max, accent);
            else
            {
                var dl = ImGui.GetWindowDrawList();
                var alpha = ImGui.GetStyle().Alpha;
                var lcarsScale = MaterialTheme.Metrics.Scale;
                var rounding = Math.Min((inset ? 9 : 18) * lcarsScale, Math.Min(max.X - min.X, max.Y - min.Y) * .5f);
                if (!RootBackgroundAvailable)
                    dl.AddRectFilled(min, max, Ink(MaterialLcars.Background, alpha, theme.SurfaceOpacity), rounding);
                dl.AddRect(min, max, Ink(MaterialLcars.Accent(accent), alpha, inset ? .35f : 1), rounding,
                    ImDrawFlags.None, inset ? 1 : 2 * lcarsScale);
                if (!inset)
                {
                    dl.AddRectFilled(min, new(min.X + 12 * lcarsScale, max.Y), Ink(MaterialLcars.Accent(accent), alpha), rounding, ImDrawFlags.RoundCornersLeft);
                    dl.AddRectFilled(min + new Vector2(9 * lcarsScale, 0), new(Math.Min(max.X, min.X + 44 * lcarsScale), min.Y + 8 * lcarsScale),
                        Ink(MaterialLcars.Accent(accent), alpha));
                }
            }
            return;
        }
        if (!HasMaterial) return;
        var scale = MaterialTheme.Metrics.Scale;
        var radius = (theme.IsCyberpunk ? 3 : theme.IsSpatial && inset ? 6 : theme.IsSpatial ? 11 : 10) * scale;
        PaintPanel(min, max, inset, inset ? theme.Colors.SurfaceContainerLowest : theme.Colors.SurfaceContainerLow,
            radius, ImGui.GetStyle().Alpha, structuralBackground: true);
    }

    internal static void PaintPanel(Vector2 min, Vector2 max, bool inset, Vector4 fill, float radius, float alpha,
        ImDrawListPtr? drawList = null, bool structuralBackground = false)
    {
        var theme = MaterialTheme.Current;
        var c = theme.Colors;
        var dl = drawList ?? ImGui.GetWindowDrawList();
        var s = MaterialTheme.Metrics.Scale;
        var reveal = structuralBackground && RootBackgroundAvailable;
        radius = Math.Clamp(radius, 0, Math.Min(max.X - min.X, max.Y - min.Y) * .5f);
        if (theme.IsGlassmorphism)
        {
            if (!reveal) dl.AddRectFilled(min + new Vector2(0, 3 * s), max + new Vector2(0, 3 * s), Ink(new(0, 0, 0, .15f), alpha), radius);
            GlassGlow(dl, min, max, radius, alpha, inset ? .55f : 1);
            if (!reveal) dl.AddRectFilled(min, max, Ink(fill, alpha, (inset ? .68f : .42f) * theme.SurfaceOpacity), radius);
            dl.AddRect(min, max, Ink(Vector4.Lerp(c.Primary, c.OnSurface, .35f), alpha, inset ? .5f : .8f), radius);
            TopLight(dl, min, max, radius, c.OnSurface, alpha, .36f);
        }
        else if (theme.IsSpatial)
        {
            if (!inset && !reveal) ContactShadow(dl, min, max, radius, alpha);
            if (!reveal) dl.AddRectFilled(min, max, Ink(fill, alpha, theme.SurfaceOpacity), radius);
            dl.AddRect(min, max, Ink(c.Outline, alpha, inset ? .25f : .22f), radius);
            TopLight(dl, min, max, radius, inset ? c.Outline : Vector4.One, alpha, inset ? .3f : .95f, inset ? 2 : 1);
            if (inset && max.X - min.X > 2 * radius)
                dl.AddLine(new(min.X + radius, max.Y - s), max - new Vector2(radius, s), Ink(Vector4.One, alpha, .8f), s);
        }
        else if (theme.IsCyberpunk)
        {
            if (inset || radius == 0)
            {
                if (!inset && !reveal)
                    dl.AddRectFilled(min + new Vector2(0, 3 * s), max + new Vector2(0, 3 * s), Ink(new(0, 0, 0, .22f), alpha), radius);
                if (!reveal) dl.AddRectFilled(min, max, Ink(fill, alpha, theme.SurfaceOpacity), radius);
                TopLight(dl, min, max, radius, c.OnSurface, alpha, .06f);
            }
            else PaintCyberPlate(dl, min, max, fill with { W = fill.W * theme.SurfaceOpacity }, radius * 2, alpha, .48f, !reveal);
        }
    }

    private static void PaintCyberPlate(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 fill, float cut, float alpha, float emphasis,
        bool paintFill = true)
    {
        var extent = max - min;
        if (extent.X <= 0 || extent.Y <= 0 || paintFill && fill.W <= 0) return;
        var c = MaterialTheme.Current.Colors;
        var s = MaterialTheme.Metrics.Scale;
        cut = Math.Clamp(cut, 0, Math.Min(extent.X, extent.Y) * .25f);
        dl.PushClipRect(min, max, true);
        try
        {
            if (paintFill)
            {
                CyberPlatePath(dl, min, max, cut);
                dl.PathFillConvex(Ink(fill, alpha));
            }
            var inset = Math.Min(s, Math.Min(extent.X, extent.Y) * .125f);
            var innerMin = min + new Vector2(inset);
            var innerMax = max - new Vector2(inset);
            var innerCut = Math.Max(0, cut - inset * .5f);
            CyberPlatePath(dl, innerMin, innerMax, innerCut);
            dl.PathStroke(Ink(Vector4.Lerp(c.OnSurface, c.Primary, .35f), alpha, emphasis), ImDrawFlags.Closed, s);
            var length = Math.Min(32 * s, Math.Max(0, extent.X - 2 * cut) * .3f);
            dl.AddLine(new(min.X + cut, min.Y + inset), new(min.X + cut + length, min.Y + inset),
                Ink(c.Primary, alpha, Math.Min(1, emphasis + .25f)), 2 * s);
            dl.AddLine(new(max.X - cut - length * .6f, max.Y - inset), new(max.X - cut, max.Y - inset),
                Ink(c.Secondary, alpha, emphasis), s);
        }
        finally { dl.PopClipRect(); }
    }

    private static void CyberPlatePath(ImDrawListPtr dl, Vector2 min, Vector2 max, float cut)
    {
        dl.PathClear();
        dl.PathLineTo(new(min.X + cut, min.Y));
        dl.PathLineTo(new(max.X - cut, min.Y));
        dl.PathLineTo(new(max.X, min.Y + cut));
        dl.PathLineTo(new(max.X, max.Y - cut));
        dl.PathLineTo(new(max.X - cut, max.Y));
        dl.PathLineTo(new(min.X + cut, max.Y));
        dl.PathLineTo(new(min.X, max.Y - cut));
        dl.PathLineTo(new(min.X, min.Y + cut));
    }

    public static void Heading(string title, int accent = 0)
    {
        if (MaterialTheme.Current.IsLcars) { MaterialLcars.PanelHeading(title, accent); return; }
        if (!MaterialTheme.Current.IsCyberpunk) { MaterialText.Text(title); ImGui.Separator(); return; }
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var s = MaterialTheme.Metrics.Scale;
        var c = MaterialTheme.Current.Colors;
        var left = Math.Min((accent == 0 ? 18 : 52) * s, width * .25f);
        var right = Math.Min(24 * s, width * .15f);
        var tabWidth = Math.Min(width, Math.Max(256 * s, MaterialText.Measure(title).X + left + right));
        var textWidth = Math.Max(1, tabWidth - left - right);
        var text = MaterialText.Measure(title, wrapWidth: textWidth);
        var height = Math.Max(28 * s, text.Y + 8 * s);
        var cut = Math.Min(right, height * .7f);
        var lead = accent == 0 ? 0 : Math.Min(32 * s, left * .65f);
        var start = accent == 0 ? 0 : Math.Min(8 * s, left * .15f);
        var gap = accent == 0 ? 0 : Math.Min(6 * s, left * .15f);
        var tab = accent == 0 ? c.Primary : Vector4.Lerp(c.OnSurface, c.Primary, .10f);
        var dl = ImGui.GetWindowDrawList();
        var alpha = ImGui.GetStyle().Alpha;
        dl.PushClipRect(origin, origin + new Vector2(width, height), true);
        try
        {
            dl.AddRectFilled(origin, origin + new Vector2(width, height), Ink(c.SurfaceContainerHigh, alpha), 2 * s);
            if (accent != 0)
                dl.AddQuadFilled(origin, origin + new Vector2(lead + start, 0), origin + new Vector2(start, height), origin + new Vector2(0, height), Ink(c.Secondary, alpha));
            dl.AddQuadFilled(origin + new Vector2(lead + start + gap, 0), origin + new Vector2(tabWidth - cut, 0),
                origin + new Vector2(tabWidth, height), origin + new Vector2(start + gap, height), Ink(tab, alpha));
            if (accent == 0 && width - tabWidth >= 104 * s)
                for (var i = 0; i < 4; i++)
                {
                    var slash = origin + new Vector2(width - (62 - i * 14) * s, height * .34f);
                    dl.AddQuadFilled(slash + new Vector2(6 * s, 0), slash + new Vector2(14 * s, 0),
                        slash + new Vector2(8 * s, height * .42f), slash + new Vector2(0, height * .42f), Ink(c.Primary, alpha));
                }
            MaterialText.AddText(dl, origin + new Vector2(left, (height - text.Y) * .5f), Ink(ReadableInk(tab), alpha), title, textWidth);
        }
        finally { dl.PopClipRect(); }
        ImGui.Dummy(new(width, height));
    }

    /// <summary>Suppress only the five native Cyberpunk tab faces during submission. End this scope before PaintTab.</summary>
    public static MaterialStyleScope TransparentTabFace()
    {
        var scope = new MaterialStyleScope();
        if (MaterialTheme.CurrentOrNull is not { IsCyberpunk: true }) return scope;
        scope.Color(ImGuiCol.Tab, Vector4.Zero);
        scope.Color(ImGuiCol.TabHovered, Vector4.Zero);
        scope.Color(ImGuiCol.TabActive, Vector4.Zero);
        scope.Color(ImGuiCol.TabUnfocused, Vector4.Zero);
        scope.Color(ImGuiCol.TabUnfocusedActive, Vector4.Zero);
        return scope;
    }

    /// <summary>Paint the last retained native Cyberpunk tab in its caller's background channel while its bar is active, after restoring transparent tab colours. Other modes are unchanged.</summary>
    public static void PaintTab(Vector2 min, Vector2 max, bool selected, float? alpha = null)
    {
        if (MaterialTheme.CurrentOrNull is not { IsCyberpunk: true } || max.X <= min.X || max.Y <= min.Y) return;
        var style = ImGui.GetStyle();
        var hovered = ImGui.IsItemHovered();
        var bar = ImGui.GetCurrentContext().CurrentTabBar;
        if (bar.IsNull) return;
        var focused = (bar.Flags & (ImGuiTabBarFlags)ImGuiTabBarFlagsPrivate.IsFocused) != 0;
        var fill = style.Colors[(int)(hovered ? ImGuiCol.TabHovered : selected
            ? focused ? ImGuiCol.TabActive : ImGuiCol.TabUnfocusedActive
            : focused ? ImGuiCol.Tab : ImGuiCol.TabUnfocused)];
        var emphasis = selected ? .85f : hovered || ImGui.IsItemActive() || ImGui.IsItemFocused() ? .7f : .38f;
        var clipMin = min;
        var clipMax = max;
        clipMin.X = Math.Max(clipMin.X, bar.ScrollingRectMinX);
        clipMax.X = Math.Min(clipMax.X, bar.ScrollingRectMaxX);
        if (clipMax.X <= clipMin.X) return;
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(clipMin, clipMax, true);
        try { PaintCyberPlate(dl, min, max, fill, 8 * MaterialTheme.Metrics.Scale, alpha ?? style.Alpha, emphasis); }
        finally { dl.PopClipRect(); }
    }

    /// <summary>Use with PaintButton in a background draw channel; semantic native Button colours remain intact.</summary>
    public static MaterialStyleScope NativeButtonStyle(Vector4? accent = null)
    {
        if (MaterialTheme.Current.IsLcars) return accent.HasValue ? MaterialLcars.NativeButtonStyle(accent.Value) : MaterialLcars.NativeButtonStyle();
        var scope = new MaterialStyleScope();
        if (!HasMaterial) return scope;
        scope.Style(ImGuiStyleVar.FrameRounding, (MaterialTheme.Current.IsCyberpunk ? 3 : 6) * MaterialTheme.Metrics.Scale);
        scope.Style(ImGuiStyleVar.FrameBorderSize, 0);
        scope.Color(ImGuiCol.Border, accent ?? MaterialTheme.Current.Colors.Outline);
        return scope;
    }

    /// <summary>Suppress native button ink while retaining its native item. Paint its face in the caller's background channel.</summary>
    public static MaterialStyleScope TransparentButtonFace()
    {
        var scope = new MaterialStyleScope();
        if (!HasMaterial && !MaterialTheme.Current.IsLcars) return scope;
        scope.Color(ImGuiCol.Button, Vector4.Zero);
        scope.Color(ImGuiCol.ButtonHovered, Vector4.Zero);
        scope.Color(ImGuiCol.ButtonActive, Vector4.Zero);
        scope.Color(ImGuiCol.Border, Vector4.Zero);
        return scope;
    }

    /// <summary>Paint the last native button behind its caption. Pass captured alpha if its disabled scope has already ended.</summary>
    public static void PaintButton(Vector2 min, Vector2 max, float? alpha = null)
    {
        var theme = MaterialTheme.Current;
        PaintButton(min, max, alpha, (theme.IsLcars ? 7 : theme.IsCyberpunk ? 3 : 6) * MaterialTheme.Metrics.Scale);
    }

    /// <summary>Paint the last native button with an already-scaled pixel radius, clamped to its bounds. Alpha remains the third argument.</summary>
    public static void PaintButton(Vector2 min, Vector2 max, float? alpha, float cornerRadiusPixels)
    {
        if (!float.IsFinite(cornerRadiusPixels) || cornerRadiusPixels < 0)
            throw new ArgumentOutOfRangeException(nameof(cornerRadiusPixels));
        var theme = MaterialTheme.Current;
        if (!HasMaterial && !theme.IsLcars) return;
        var style = ImGui.GetStyle();
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var state = new MaterialState(min, max, 0, alpha ?? style.Alpha, hovered, active,
            ImGui.IsItemFocused() && ImGui.GetIO().NavVisible, ImGui.IsItemVisible());
        var fill = style.Colors[(int)(active ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];
        var radius = Math.Min(cornerRadiusPixels, Math.Min(max.X - min.X, max.Y - min.Y) * .5f);
        PaintControl(state, fill, radius, ImDrawFlags.RoundCornersAll, false);
        if (theme.IsLcars)
            ImGui.GetWindowDrawList().AddRect(min, max, Ink(style.Colors[(int)ImGuiCol.Border], state.Alpha, hovered || active ? 1 : .7f), radius,
                ImDrawFlags.None, (active ? 2 : 1) * MaterialTheme.Metrics.Scale);
        if (state.Focused)
            ImGui.GetWindowDrawList().AddRect(min + new Vector2(MaterialTheme.Metrics.Scale), max - new Vector2(MaterialTheme.Metrics.Scale),
                Ink(theme.IsLcars ? MaterialLcars.Accent(2) : theme.Colors.Primary, state.Alpha), radius, ImDrawFlags.None, 2 * MaterialTheme.Metrics.Scale);
    }

    /// <summary>Enter immediately before a retained native field, after its width and height scopes.</summary>
    public static MaterialStyleScope NativeFieldStyle()
    {
        var scope = new MaterialStyleScope();
        if (!HasMaterial) return scope;
        var theme = MaterialTheme.Current;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
        PaintPanel(min, max, true, theme.IsSpatial ? theme.Colors.SurfaceContainer : theme.Colors.SurfaceContainerLowest,
            (theme.IsCyberpunk ? 3 : 6) * MaterialTheme.Metrics.Scale, ImGui.GetStyle().Alpha);
        scope.Color(ImGuiCol.FrameBg, Vector4.Zero);
        scope.Color(ImGuiCol.FrameBgHovered, MaterialColor.Alpha(theme.Colors.OnSurface, .06f));
        scope.Color(ImGuiCol.FrameBgActive, MaterialColor.Alpha(theme.Colors.OnSurface, .10f));
        scope.Style(ImGuiStyleVar.FrameRounding, (theme.IsCyberpunk ? 0 : 5) * MaterialTheme.Metrics.Scale);
        return scope;
    }

    internal static void PaintControl(in MaterialState state, Vector4 fill, float radius, ImDrawFlags corners, bool preserveFill)
    {
        if (!state.Visible) return;
        var theme = MaterialTheme.Current;
        var c = theme.Colors;
        var dl = ImGui.GetWindowDrawList();
        var s = MaterialTheme.Metrics.Scale;
        var min = state.Min; var max = state.Max; var alpha = state.Alpha;
        // Transparent text/outlined actions retain their transparent interiors.
        var filled = fill.W > 0;
        if (theme.IsGlassmorphism)
        {
            if (filled) GlassGlow(dl, min, max, radius, alpha, state.Pressed ? 1 : state.Hovered ? .9f : .7f, corners);
            dl.AddRectFilled(min, max, Ink(fill, alpha, preserveFill ? 1 : .68f), radius, corners);
            if (filled)
            {
                dl.AddRect(min, max, Ink(Vector4.Lerp(c.Primary, c.OnSurface, .35f), alpha, state.Pressed ? .95f : state.Hovered ? .85f : .65f), radius, corners);
                TopLight(dl, min, max, radius, c.OnSurface, alpha, .25f);
            }
        }
        else if (theme.IsSpatial)
        {
            if (filled && !state.Pressed) ContactShadow(dl, min, max - new Vector2(0, 2 * s), radius, alpha, corners);
            dl.AddRectFilled(min, max, Ink(fill, alpha), radius, corners);
            if (filled)
            {
                dl.AddRect(min, max, Ink(c.Outline, alpha, state.Hovered ? .6f : .3f), radius, corners);
                TopLight(dl, min, max, radius, state.Pressed ? c.Outline : Vector4.One, alpha, state.Pressed ? .18f : .85f);
            }
        }
        else if (theme.IsCyberpunk)
        {
            if (filled && !state.Pressed)
                dl.AddRectFilled(min + new Vector2(0, 2 * s), max + new Vector2(0, 2 * s), Ink(new(0, 0, 0, .24f), alpha), radius, corners);
            dl.AddRectFilled(min, max, Ink(fill, alpha), radius, corners);
            if (filled)
            {
                dl.AddRect(min, max, Ink(c.OnSurface, alpha, state.Pressed ? .32f : state.Hovered ? .26f : .16f), radius, corners);
                var cut = Math.Min(4 * s, Math.Min(max.X - min.X, max.Y - min.Y) * .25f);
                dl.PathClear();
                dl.PathLineTo(new(min.X + s, max.Y - cut));
                dl.PathLineTo(new(min.X + cut, max.Y - s));
                dl.PathLineTo(max - new Vector2(cut, s));
                dl.PathLineTo(max - new Vector2(s, cut));
                dl.PathStroke(Ink(c.Primary, alpha), ImDrawFlags.None, 2 * s);
            }
        }
        else dl.AddRectFilled(min, max, Ink(fill, alpha), radius, corners);
    }

    private static void GlassGlow(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, float alpha, float opacity,
        ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        var s = MaterialTheme.Metrics.Scale;
        var controls = MaterialTheme.Current.Colors.Primary;
        for (var layer = 4; layer > 0; layer--)
        {
            var spread = new Vector2(layer * .75f * s);
            dl.AddRect(min - spread, max + spread, Ink(controls, alpha, opacity * (5 - layer) * .035f),
                radius + spread.X, corners, 2 * s);
        }
    }

    private static void ContactShadow(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, float alpha,
        ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        var s = MaterialTheme.Metrics.Scale;
        for (var layer = 5; layer > 0; layer--)
        {
            var spread = new Vector2(layer * .7f * s, layer * .4f * s);
            dl.AddRectFilled(min - spread + new Vector2(0, 3 * s), max + spread + new Vector2(0, 3 * s),
                Ink(new(.09f, .16f, .23f, .035f), alpha), radius + layer * s, corners);
        }
    }

    private static void TopLight(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, Vector4 color, float alpha, float opacity, float thickness = 1)
    {
        var s = MaterialTheme.Metrics.Scale;
        if (max.X - min.X > 2 * radius)
            dl.AddLine(min + new Vector2(radius, s), new(max.X - radius, min.Y + s), Ink(color, alpha, opacity), thickness * s);
    }

    private static Vector4 Rgb(uint rgb) => new(MaterialColorSettings.Rgb(rgb), 1);
    /// <summary>Chooses the higher-contrast black or white caption for an opaque control fill.</summary>
    public static Vector4 ReadableInk(Vector4 surface) => MaterialColor.Contrast(surface, Vector4.One) >= MaterialColor.Contrast(surface, new(0, 0, 0, 1))
        ? Vector4.One : new(0, 0, 0, 1);
    private static uint Ink(Vector4 color, float alpha, float opacity = 1)
        => MaterialDraw.Color(new(color.X, color.Y, color.Z, color.W * opacity), alpha);
}
