using System.Numerics;
using Dalamud.Bindings.ImGui;
using Xunit;

namespace AethertekUI.Tests;

public sealed class MaterialSkinNativeTests
{
    [Fact]
    public unsafe void RendererScopesRestoreOptionsAndRevealOnlySuccessfulCurrentRootsWithoutChangingForegrounds()
    {
        using var host = new NativeTestContext();
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings
            { Mode = MaterialColorMode.Glassmorphism, GlassAnimation = false }) { SurfaceOpacity = 1 };
        var renderer = new BackgroundRecorder { Ready = true, Opacity = .83f };
        var drawBackdrop = true;
        var rootAvailable = false;
        uint[] foreground = [];
        uint[] ReadColors(ImDrawListPtr list, int start)
        {
            var colors = new uint[list.VtxBuffer.Size - start];
            for (var i = 0; i < colors.Length; i++) colors[i] = list.VtxBuffer[start + i].Col;
            return colors;
        }
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new(500, 350));
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Renderer owner", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysVerticalScrollbar);
            var list = ImGui.GetWindowDrawList();
            var native = ReadColors(list, 0);
            var nativeIndices = list.IdxBuffer.Size;
            if (drawBackdrop)
            {
                MaterialSkin.Backdrop();
                Assert.Equal(ImGui.GetWindowPos(), renderer.Min);
                Assert.Equal(ImGui.GetWindowPos() + ImGui.GetWindowSize(), renderer.Max);
                Assert.True(renderer.WindowBackground);
                Assert.Equal(.75f, renderer.Frost);
                Assert.Equal(.42f, renderer.Tint.W);
                for (var i = 0; i < native.Length; i++) Assert.Equal(native[i], list.VtxBuffer[i].Col);
                Assert.All(ReadColors(list, native.Length), color =>
                    Assert.InRange(color >> 24, 0u, renderer.Ready ? 90u : 153u));
                if (renderer.Ready)
                    for (var i = 0; i < list.CmdBuffer.Size; i++)
                    {
                        var command = list.CmdBuffer[i];
                        if (command.ElemCount == 0 || command.IdxOffset < nativeIndices) continue;
                        Assert.True(command.ClipRect.Y >= ImGuiP.GetCurrentWindow().InnerRect.Min.Y);
                        Assert.True(command.ClipRect.Z <= ImGuiP.GetCurrentWindow().InnerRect.Max.X);
                    }
            }
            rootAvailable = MaterialSkin.BackgroundFill(Vector4.One) == Vector4.Zero;
            Assert.Equal(drawBackdrop && renderer.Ready, rootAvailable);
            var style = ImGui.GetStyle();
            var field = style.Colors[(int)ImGuiCol.FrameBg];
            var button = style.Colors[(int)ImGuiCol.Button];
            var child = style.Colors[(int)ImGuiCol.ChildBg];
            using (MaterialSkin.StructuralBackgroundStyle())
            {
                Assert.Equal(rootAvailable ? Vector4.Zero : child, style.Colors[(int)ImGuiCol.ChildBg]);
                Assert.Equal(field, style.Colors[(int)ImGuiCol.FrameBg]);
                Assert.Equal(button, style.Colors[(int)ImGuiCol.Button]);
            }
            Assert.Equal(child, style.Colors[(int)ImGuiCol.ChildBg]);
            using (MaterialTable.PushStyle())
                Assert.Equal(rootAvailable ? Vector4.Zero : theme.Colors.SurfaceContainerHigh, style.Colors[(int)ImGuiCol.TableHeaderBg]);
            using (var card = MaterialSurface.Card("structural child", new(260, 65)))
                if (card.Visible) Assert.Equal(rootAvailable, MaterialSkin.BackgroundFill(Vector4.One) == Vector4.Zero);
            var first = list.VtxBuffer.Size;
            MaterialButton.FilledButton("foreground", "Foreground", size: new(160, 32));
            ImGui.SetNextItemWidth(180);
            var value = 3;
            using (MaterialSkin.NativeFieldStyle()) ImGui.InputInt("##foreground-field", ref value);
            foreground = ReadColors(list, first);
            using (MaterialSkin.PushBackgroundRenderer(renderer, false, .1f, .25f))
            {
                Assert.Equal(.25f, renderer.Opacity);
                Assert.Equal(Vector4.One, MaterialSkin.BackgroundFill(Vector4.One));
            }
            Assert.Equal(.6f, renderer.Opacity);
            Assert.Equal(rootAvailable, MaterialSkin.BackgroundFill(Vector4.One) == Vector4.Zero);
            using (MaterialSkin.PushBackgroundRenderer(null, false, 0))
            {
                Assert.Equal(.6f, renderer.Opacity);
                Assert.Equal(Vector4.One, MaterialSkin.BackgroundFill(Vector4.One));
            }
            Assert.Equal(rootAvailable, MaterialSkin.BackgroundFill(Vector4.One) == Vector4.Zero);
            ImGui.End();

            ImGui.SetNextWindowPos(new(550, 0)); ImGui.SetNextWindowSize(new(350, 250));
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Unrelated renderer root", ImGuiWindowFlags.NoSavedSettings);
            Assert.Equal(Vector4.One, MaterialSkin.BackgroundFill(Vector4.One));
            if (drawBackdrop) MaterialSkin.Backdrop();
            ImGui.End();
            ImGui.Begin("Renderer owner", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysVerticalScrollbar);
            Assert.Equal(rootAvailable, MaterialSkin.BackgroundFill(Vector4.One) == Vector4.Zero);
            ImGui.End();
        }
        using (MaterialSkin.PushBackgroundRenderer(renderer, true, .75f, .6f))
        {
            host.Frame(Draw); host.Frame(Draw);
            var filledForeground = foreground;
            renderer.Ready = false;
            host.Frame(Draw);
            Assert.Equal(filledForeground, foreground);
            renderer.Ready = true;
            drawBackdrop = false;
            host.Frame(Draw);
            Assert.False(rootAvailable); // A prior frame never authorizes transparent children.
        }
        Assert.Equal(.83f, renderer.Opacity);
        Assert.Equal(Vector4.One, MaterialSkin.BackgroundFill(Vector4.One));
        AssertMeshIndices();
    }

    [Theory]
    [InlineData(MaterialGlassLights.AmbientGlows)]
    [InlineData(MaterialGlassLights.SweepingBeams)]
    [InlineData(MaterialGlassLights.PointerGlow)]
    public void LightsKeepIndependentColorsStillMotionAndTranslucentSuccessfulGlass(MaterialGlassLights effect)
    {
        using var host = new NativeTestContext();
        var settings = new MaterialColorSettings { Mode = MaterialColorMode.Glassmorphism,
            GlassBackground = MaterialGlassBackground.Lights, GlassLights = effect, GlassAnimation = false };
        var renderer = new BackgroundRecorder();
        var reducedMotion = false;
        var size = new Vector2(900, 650);
        uint[] colors = [];
        var indices = 0;
        void Draw()
        {
            var theme = new MaterialTheme(Vector3.UnitY, settings) { SurfaceOpacity = 1 };
            theme.Motion.Enabled = !reducedMotion;
            using var palette = MaterialTheme.Push(theme);
            using var background = MaterialSkin.PushBackgroundRenderer(renderer, true, .75f);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(size);
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Shared lights", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList(); var first = list.VtxBuffer.Size; var firstIndex = list.IdxBuffer.Size;
            MaterialSkin.Backdrop();
            indices = list.IdxBuffer.Size - firstIndex;
            colors = new uint[list.VtxBuffer.Size - first];
            for (var i = 0; i < colors.Length; i++) colors[i] = list.VtxBuffer[first + i].Col;
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        var still = colors;
        ImGui.GetIO().AddMousePosEvent(100, 140); host.Frame(Draw);
        Assert.Equal(still, colors);
        Assert.InRange(colors.Length, 33 * 25, 65 * 49);
        Assert.All(colors, color => Assert.Equal(255u, color >> 24));
        for (var light = 1; light <= 3; light++)
        {
            settings = light switch
            {
                1 => settings with { GlassLight1Rgb = 0xFF1828 },
                2 => settings with { GlassLight2Rgb = 0x31F020 },
                _ => settings with { GlassLight3Rgb = 0x3220FF },
            };
            host.Frame(Draw); Assert.False(still.SequenceEqual(colors)); still = colors;
        }
        settings = settings with { GlassWave1Rgb = 0x010203, GlassWave2Rgb = 0x040506, GlassWave3Rgb = 0x070809 };
        host.Frame(Draw); Assert.Equal(still, colors);
        settings = settings with { GlassAnimation = true }; reducedMotion = true;
        host.Frame(Draw); Assert.Equal(still, colors);
        reducedMotion = false;
        ImGui.GetIO().AddMousePosEvent(720, 500); host.Frame(Draw);
        Assert.False(still.SequenceEqual(colors));
        renderer.Ready = true;
        host.Frame(Draw);
        Assert.All(colors, color => Assert.InRange(color >> 24, 0u, 141u));
        renderer.Ready = false;
        size = new(8000, 5000); host.Frame(Draw);
        Assert.Equal(65 * 49, colors.Length); // Neighbouring cells share their corner vertices.
        Assert.Equal(64 * 48 * 6, indices); // Every opaque cell draws two triangles.
        Assert.All(colors, color => Assert.Equal(255u, color >> 24));
        AssertMeshIndices();
        renderer.Ready = true;
        host.Frame(Draw);
        Assert.Equal(65 * 49, colors.Length);
        // Fully transparent glass cells emit no triangles. The shared grid is row-major, 65 vertices a row.
        var visibleCells = 0;
        for (var row = 0; row < 48; row++)
            for (var column = 0; column < 64; column++)
            {
                var top = row * 65 + column;
                if (((colors[top] | colors[top + 1] | colors[top + 65] | colors[top + 66]) >> 24) != 0) visibleCells++;
            }
        Assert.InRange(visibleCells, 1, 64 * 48);
        Assert.Equal(visibleCells * 6, indices);
        Assert.All(colors, color => Assert.InRange(color >> 24, 0u, 141u));
        AssertMeshIndices();
    }

    [Fact]
    public unsafe void ImageBackdropUsesTheSuppliedImageWithoutProceduralOverlayAndKeepsUnavailableFallback()
    {
        using var host = new NativeTestContext();
        using var image = host.RegisterTexture([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255], 2, 2);
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings
            { Mode = MaterialColorMode.Glassmorphism, GlassBackground = MaterialGlassBackground.Image }) { SurfaceOpacity = 1 };
        var renderer = new BackgroundRecorder { ImageMode = true, Texture = image.Id };
        var blur = false;
        var covered = false;
        var vertices = 0;
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme);
            using var background = MaterialSkin.PushBackgroundRenderer(renderer, blur, .75f);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new(500, 350));
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Image owner", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground);
            var list = ImGui.GetWindowDrawList(); var first = list.VtxBuffer.Size;
            MaterialSkin.Backdrop();
            vertices = list.VtxBuffer.Size - first;
            covered = MaterialSkin.BackgroundFill(Vector4.One) == Vector4.Zero;
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        Assert.False(covered); Assert.Equal(0, renderer.BackgroundCalls);
        Assert.InRange(vertices, 4, 100); // Ordinary fallback, with no Waves or Lights mesh.
        renderer.ImageAvailable = true;
        host.Frame(Draw);
        Assert.True(covered); Assert.InRange(vertices, 8, 104);
        var plainCalls = renderer.PlainCalls;
        renderer.Ready = true; blur = true;
        host.Frame(Draw);
        Assert.True(covered); Assert.Equal(plainCalls, renderer.PlainCalls);
        Assert.True(renderer.BackgroundCalls > 0); Assert.Equal(4, vertices);
        var data = ImGui.GetDrawData();
        var imageCommands = 0;
        for (var li = 0; li < data.CmdListsCount; li++)
        {
            var list = new ImDrawListPtr(data.CmdLists[li]);
            for (var ci = 0; ci < list.CmdBuffer.Size; ci++)
                if (list.CmdBuffer[ci].TextureId.Handle == image.Id.Handle && list.CmdBuffer[ci].ElemCount > 0) imageCommands++;
        }
        Assert.True(imageCommands > 0);
        AssertMeshIndices();
    }

    private sealed class BackgroundRecorder : IMaterialBackgroundRenderer
    {
        public bool ImageMode { get; set; }
        public bool ImageAvailable { get; set; }
        public float Opacity { get; set; } = 1;
        internal bool Ready;
        internal int BackgroundCalls, PlainCalls;
        internal Vector2 Min, Max;
        internal Vector4 Tint;
        internal float Frost;
        internal bool WindowBackground;
        internal ImTextureID Texture;
        public bool DrawBackground(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
            Vector4 tint, float frost, bool windowBackground = false)
        {
            BackgroundCalls++; Min = min; Max = max; Tint = tint; Frost = frost; WindowBackground = windowBackground;
            if (Ready && ImageMode && ImageAvailable) drawList.AddImage(Texture, min, max);
            return Ready;
        }
        public bool DrawPlainImage(ImDrawListPtr drawList, Vector2 min, Vector2 max)
        {
            PlainCalls++;
            if (!ImageMode || !ImageAvailable) return false;
            drawList.AddImage(Texture, min, max);
            return true;
        }
    }

    [Fact]
    public void GlassRibbonsRespectStillMotionSpeedAndIndependentSavedWaves()
    {
        using var host = new NativeTestContext();
        var selected = new MaterialColorSettings { Mode = MaterialColorMode.Glassmorphism, GlassAnimation = false };
        var reducedMotion = false;
        var windowSize = new Vector2(900, 650);
        var windowPosition = Vector2.Zero;
        uint[] colors = [];
        Vector2[] positions = [];
        int vertices = 0, indices = 0;
        void Draw()
        {
            var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), selected);
            theme.Motion.Enabled = !reducedMotion;
            using var scope = MaterialTheme.Push(theme);
            ImGui.SetNextWindowPos(windowPosition); ImGui.SetNextWindowSize(windowSize);
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Glass ribbons", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList();
            var first = list.VtxBuffer.Size;
            var firstIndex = list.IdxBuffer.Size;
            MaterialSkin.Backdrop();
            vertices = list.VtxBuffer.Size - first;
            indices = list.IdxBuffer.Size - firstIndex;
            colors = new uint[vertices];
            positions = new Vector2[vertices];
            for (var i = 0; i < vertices; i++) (colors[i], positions[i]) = (list.VtxBuffer[first + i].Col, list.VtxBuffer[first + i].Pos);
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        var still = colors;
        host.Frame(Draw);
        Assert.Equal(still, colors);
        Assert.InRange(vertices, 49 * 33, 89 * 65);
        Assert.True(colors.Distinct().Count() > 1500); // Smooth corner interpolation rather than solid circles.
        // A still background keeps its colours while the window moves; its vertices follow the window.
        var resting = positions;
        windowPosition = new(37, 21);
        host.Frame(Draw);
        Assert.Equal(still, colors);
        Assert.All(positions.Zip(resting), pair =>
            Assert.True(Vector2.Distance(pair.First, pair.Second + windowPosition) < .01f));
        windowPosition = Vector2.Zero;
        host.Frame(Draw);
        Assert.Equal(still, colors);
        Assert.Equal(resting, positions);
        foreach (var wave in new[] { 1, 2, 3 })
        {
            selected = wave switch
            {
                1 => selected with { GlassWave1Rgb = 0xFF1828 },
                2 => selected with { GlassWave2Rgb = 0x31F020 },
                _ => selected with { GlassWave3Rgb = 0x3220FF },
            };
            host.Frame(Draw);
            Assert.False(still.SequenceEqual(colors));
            still = colors;
        }
        reducedMotion = true;
        selected = selected with { GlassAnimation = true };
        host.Frame(Draw);
        Assert.Equal(still, colors);
        reducedMotion = false;
        host.Frame(Draw);
        Assert.False(still.SequenceEqual(colors));
        var moving = colors;
        host.Frame(Draw);
        Assert.False(moving.SequenceEqual(colors));
        selected = selected with { GlassAnimationSpeed = 10 };
        host.Frame(Draw);
        Assert.True(colors.Zip(moving).Count(pair => pair.First != pair.Second) > vertices / 2);
        selected = selected with { GlassAnimation = false };
        host.Frame(Draw);
        Assert.Equal(still, colors);
        windowSize = new(8000, 5000);
        host.Frame(Draw);
        Assert.Equal(89 * 65, vertices); // Neighbouring cells share their corner vertices.
        Assert.Equal(88 * 64 * 6, indices); // Every opaque cell draws two triangles.
        AssertMeshIndices();
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f)]
    [InlineData(MaterialDensity.Compact, 1.5f)]
    [InlineData(MaterialDensity.Standard, 1f)]
    [InlineData(MaterialDensity.Standard, 1.5f)]
    public void SelectedMaterialsKeepActionsIdsGeometryAndNativeScopes(MaterialDensity density, float scale)
    {
        using var host = new NativeTestContext((int)(1000 * scale), (int)(750 * scale), scale);
        var mode = MaterialColorMode.Auto;
        var disabled = false;
        var actions = 0;
        var controlVertices = 0;
        var cardVertices = 0;
        uint id = 0;
        Vector2 min = default, max = default;
        void Draw()
        {
            var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), new MaterialColorSettings { Mode = mode }, density);
            using var palette = MaterialTheme.Push(theme, scale);
            ImGui.SetNextWindowPos(new(10, 10)); ImGui.SetNextWindowSize(new Vector2(800, 600) * scale);
            ImGui.Begin("Material actions", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList(); var first = list.VtxBuffer.Size;
            if (MaterialButton.FilledButton("run", "Run", size: new(160, 40), disabled: disabled)) actions++;
            controlVertices = list.VtxBuffer.Size - first;
            id = ImGui.GetCurrentContext().LastItemData.ID;
            min = ImGui.GetItemRectMin(); max = ImGui.GetItemRectMax();
            var style = ImGui.GetStyle();
            var rounding = style.FrameRounding;
            var border = style.FrameBorderSize;
            var button = style.Colors[(int)ImGuiCol.Button];
            var field = style.Colors[(int)ImGuiCol.FrameBg];
            using (MaterialSkin.NativeButtonStyle())
            {
                list.ChannelsSplit(2);
                try
                {
                    list.ChannelsSetCurrent(1);
                    using (MaterialSkin.TransparentButtonFace()) ImGui.Button("Retained action");
                    list.ChannelsSetCurrent(0);
                    MaterialSkin.PaintButton(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                }
                finally { list.ChannelsMerge(); }
            }
            ImGui.SetNextItemWidth(200 * scale);
            var value = 5;
            using (MaterialSkin.NativeFieldStyle()) ImGui.InputInt("##field", ref value);
            Assert.Equal(rounding, style.FrameRounding);
            Assert.Equal(border, style.FrameBorderSize);
            Assert.Equal(button, style.Colors[(int)ImGuiCol.Button]);
            Assert.Equal(field, style.Colors[(int)ImGuiCol.FrameBg]);
            var beforeCard = list.VtxBuffer.Size;
            using (var card = MaterialSurface.Card("card", new(300, 90)))
                if (card.Visible)
                {
                    cardVertices = list.VtxBuffer.Size - beforeCard + ImGui.GetWindowDrawList().VtxBuffer.Size;
                    MaterialText.Text("Native child content");
                }
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        var original = (id, min, max);
        var originalControlVertices = controlVertices;
        var originalCardVertices = cardVertices;
        foreach (var selected in Enum.GetValues<MaterialColorMode>())
        {
            mode = selected;
            host.Frame(Draw); host.Frame(Draw);
            Assert.Equal(original, (id, min, max));
            if (mode is MaterialColorMode.Glassmorphism or MaterialColorMode.Spatial)
            {
                Assert.True(controlVertices > originalControlVertices);
                Assert.True(cardVertices > originalCardVertices);
            }
            var before = actions;
            host.Click((min + max) * .5f, Draw);
            Assert.Equal(before + 1, actions);
            disabled = true;
            host.Click((min + max) * .5f, Draw);
            Assert.Equal(before + 1, actions);
            disabled = false;
            AssertMeshIndices();
        }
    }

    [Theory]
    [InlineData(MaterialColorMode.Glassmorphism)]
    [InlineData(MaterialColorMode.Spatial)]
    [InlineData(MaterialColorMode.Cyberpunk)]
    public void MaterialCardsPaintTheirOuterEdgesBehindNativeScrollbarsAndRetainedCaptions(MaterialColorMode mode)
    {
        using var host = new NativeTestContext(650, 420);
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = mode }) { SurfaceOpacity = 1 };
        Vector2 captionMin = default, captionMax = default, cardMin = default, cardMax = default;
        Vector4 caption = default;
        var scrollbarWidth = 0f;
        void Draw()
        {
            using var scope = MaterialTheme.Push(theme);
            ImGui.SetNextWindowPos(new(0, 0)); ImGui.SetNextWindowSize(new(600, 390));
            ImGui.Begin("Material ordering", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList();
            using (MaterialSkin.NativeButtonStyle())
            {
                caption = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
                list.ChannelsSplit(2);
                try
                {
                    list.ChannelsSetCurrent(1);
                    using (MaterialSkin.TransparentButtonFace()) ImGui.Button("Retained action", new(180, 36));
                    var textSize = ImGui.CalcTextSize("Retained action");
                    captionMin = ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() - textSize) * .5f;
                    captionMax = captionMin + textSize;
                    list.ChannelsSetCurrent(0);
                    MaterialSkin.PaintButton(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                }
                finally { list.ChannelsMerge(); }
            }
            using (var card = MaterialSurface.Begin("scrolling-card", new(300, 180), MaterialElevation.Low,
                       outline: true, radius: 0, flags: ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                cardMin = ImGui.GetWindowPos(); cardMax = cardMin + ImGui.GetWindowSize();
                scrollbarWidth = ImGui.GetStyle().ScrollbarSize;
                if (card.Visible)
                    for (var row = 0; row < 30; row++) ImGui.TextUnformatted("Scrollable native content");
            }
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw); host.Frame(Draw);
        var image = SoftwarePreview.Capture(host);
        Assert.True(CountInk(captionMin, captionMax, caption) > 5); // A face painted over the native text fails this.
        Assert.True(CountInk(new(cardMax.X - scrollbarWidth, cardMin.Y), cardMax, theme.Colors.OutlineVariant) > 5);
        var expectedFill = mode == MaterialColorMode.Glassmorphism
            ? Vector4.Lerp(theme.Colors.Background, theme.Colors.SurfaceContainerLow, .42f)
            : theme.Colors.SurfaceContainerLow;
        Assert.True(CountInk(cardMin + new Vector2(4, 2), cardMin + new Vector2(9, 3), expectedFill) >= 4);

        int CountInk(Vector2 min, Vector2 max, Vector4 expected)
        {
            var count = 0;
            for (var y = Math.Max(0, (int)min.Y); y < Math.Min(image.Height, (int)MathF.Ceiling(max.Y)); y++)
                for (var x = Math.Max(0, (int)min.X); x < Math.Min(image.Width, (int)MathF.Ceiling(max.X)); x++)
                {
                    var offset = (y * image.Width + x) * 4;
                    if (Math.Abs(image.Rgba[offset] - expected.X * 255) <= 8
                        && Math.Abs(image.Rgba[offset + 1] - expected.Y * 255) <= 8
                        && Math.Abs(image.Rgba[offset + 2] - expected.Z * 255) <= 8) count++;
                }
            return count;
        }
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f)]
    [InlineData(MaterialDensity.Compact, 1.5f)]
    [InlineData(MaterialDensity.Standard, 1f)]
    [InlineData(MaterialDensity.Standard, 1.5f)]
    public unsafe void CyberPanelsAndNativeTabsKeepChamferedMeshesFillsClipsAndCallerState(MaterialDensity density, float scale)
    {
        using var host = new NativeTestContext((int)(750 * scale), (int)(500 * scale), scale);
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = MaterialColorMode.Cyberpunk }, density)
            { SurfaceOpacity = .7f };
        var selectedFill = new Vector4(.57f, .12f, .08f, .8f);
        var hoverFill = new Vector4(.13f, .48f, .19f, .65f);
        var idleFill = new Vector4(.17f, .21f, .29f, .75f);
        var unfocusedSelectedFill = new Vector4(.08f, .17f, .57f, .55f);
        var unfocusedIdleFill = new Vector4(.31f, .16f, .27f, .45f);
        var labels = new[] { "First retained Cyberpunk###cyber-first", "Second retained Cyberpunk###cyber-second", "Third retained Cyberpunk###cyber-third" };
        var points = new Vector2[3];
        var selected = new bool[3];
        var disabled = false; var narrow = false; var request = 0; var firstHovered = false; var firstActive = false;
        var unfocusOwner = false; var rawBaseline = false; var verifiedUnfocusedRaw = false;
        var panels = 0; var tabs = 0;
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme, scale);
            using var ink = new MaterialStyleScope();
            ink.Style(ImGuiStyleVar.Alpha, .6f);
            ink.Color(ImGuiCol.Tab, idleFill); ink.Color(ImGuiCol.TabUnfocused, unfocusedIdleFill);
            ink.Color(ImGuiCol.TabActive, selectedFill); ink.Color(ImGuiCol.TabUnfocusedActive, unfocusedSelectedFill);
            ink.Color(ImGuiCol.TabHovered, hoverFill);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new Vector2(narrow ? 240 : 700, 450) * scale);
            ImGui.Begin("Cyber plate contracts", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList(); var context = ImGui.GetCurrentContext(); var style = ImGui.GetStyle();
            var caller = (ImGui.GetID("after-plates"), context.ColorStack.Size, context.StyleVarStack.Size, list.ClipRectStack.Size, style.FramePadding, style.Alpha);
            ImGui.BeginDisabled(disabled);
            using (var bar = MaterialTabs.Begin("retained-cyber-tabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                Assert.True(bar.Visible);
                for (var index = 0; index < labels.Length; index++)
                {
                    var id = ImGui.GetID(labels[index]); var first = list.VtxBuffer.Size;
                    var padding = style.FramePadding; var clips = list.ClipRectStack.Size;
                    var flags = request == index ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                    using var item = rawBaseline ? default(MaterialTabs.TabItemScope) : MaterialTabs.Item(labels[index], flags);
                    var visible = rawBaseline ? ImGui.BeginTabItem(labels[index], flags) : item.Visible;
                    try
                    {
                    selected[index] = visible;
                    Assert.Equal(id, ImGuiP.GetItemID());
                    Assert.Equal(padding, style.FramePadding); Assert.Equal(clips, list.ClipRectStack.Size);
                    Assert.Equal(idleFill, style.Colors[(int)ImGuiCol.Tab]);
                    Assert.Equal(selectedFill, style.Colors[(int)ImGuiCol.TabActive]);
                    Assert.Equal(hoverFill, style.Colors[(int)ImGuiCol.TabHovered]);
                    Assert.Equal(unfocusedIdleFill, style.Colors[(int)ImGuiCol.TabUnfocused]);
                    Assert.Equal(unfocusedSelectedFill, style.Colors[(int)ImGuiCol.TabUnfocusedActive]);
                    var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
                    points[index] = (min + max) * .5f;
                    if (index == 0) { firstHovered = ImGui.IsItemHovered(); firstActive = ImGui.IsItemActive(); }
                    var barState = context.CurrentTabBar;
                    var focused = (barState.Flags & (ImGuiTabBarFlags)ImGuiTabBarFlagsPrivate.IsFocused) != 0;
                    var fill = ImGui.IsItemHovered() ? hoverFill : visible
                        ? focused ? selectedFill : unfocusedSelectedFill : focused ? idleFill : unfocusedIdleFill;
                    var windowClip = ImGuiP.GetCurrentWindow().ClipRect;
                    var tabClipMin = new Vector2(Math.Max(Math.Max(min.X, barState.ScrollingRectMinX), windowClip.Min.X), Math.Max(min.Y, windowClip.Min.Y));
                    var tabClipMax = new Vector2(Math.Min(Math.Min(max.X, barState.ScrollingRectMaxX), windowClip.Max.X), Math.Min(max.Y, windowClip.Max.Y));
                    if (rawBaseline)
                    {
                        if (visible && tabClipMax.X > tabClipMin.X && tabClipMax.Y > tabClipMin.Y)
                        {
                            Assert.False(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));
                            Assert.True(focused); // A regular native bar retains its focused fill roles when its owner loses focus.
                            var expected = ImGui.ColorConvertFloat4ToU32(fill with { W = fill.W * style.Alpha });
                            var found = false;
                            for (var vertex = first; vertex < list.VtxBuffer.Size; vertex++)
                                if (list.VtxBuffer[vertex].Col == expected) { found = true; break; }
                            Assert.True(found, "Raw native unfocused-owner tab uses its bar-focused fill role");
                            verifiedUnfocusedRaw = true;
                        }
                        continue;
                    }
                    var mesh = CheckMesh(first, min, max, fill, style.Alpha, Math.Min(8 * scale, Math.Min(max.X - min.X, max.Y - min.Y) * .25f));
                    if (tabClipMax.X > tabClipMin.X && tabClipMax.Y > tabClipMin.Y)
                        Assert.True(mesh, $"Visible Cyber tab {index} must emit a plate: selected={visible}, hovered={ImGui.IsItemHovered()}, disabled={disabled}");
                    if (mesh)
                    {
                        tabs++;
                        AssertPlateClips(first, fill, tabClipMin, tabClipMax);
                    }
                    }
                    finally { if (rawBaseline && visible) ImGui.EndTabItem(); }
                }
            }
            ImGui.EndDisabled();
            var panelMin = new Vector2(35, 170) * scale; var panelMax = new Vector2(220, 310) * scale;
            var panelFirst = list.VtxBuffer.Size; var panelId = ImGuiP.GetItemID();
            var clipMin = panelMin + new Vector2(0, 8 * scale); var clipMax = panelMax - new Vector2(0, 8 * scale);
            list.PushClipRect(clipMin, clipMax, true);
            MaterialSkin.PanelFrame(panelMin, panelMax);
            Assert.Equal(panelId, ImGuiP.GetItemID());
            Assert.True(CheckMesh(panelFirst, panelMin, panelMax, theme.Colors.SurfaceContainerLow, .6f * theme.SurfaceOpacity, 6 * scale));
            AssertPlateClips(panelFirst, theme.Colors.SurfaceContainerLow, clipMin, clipMax);
            list.PopClipRect(); panels++;
            Assert.Equal(caller, (ImGui.GetID("after-plates"), context.ColorStack.Size, context.StyleVarStack.Size, list.ClipRectStack.Size, style.FramePadding, style.Alpha));
            ImGui.End();
            if (unfocusOwner)
            {
                ImGui.SetNextWindowPos(new Vector2(400, 350) * scale); ImGui.SetNextWindowSize(new Vector2(200, 70) * scale);
                ImGui.SetNextWindowFocus();
                ImGui.Begin("Cyber baseline focus owner", ImGuiWindowFlags.NoSavedSettings);
                ImGui.End();
            }

            bool CheckMesh(int first, Vector2 min, Vector2 max, Vector4 fill, float alpha, float cut)
            {
                var rgb = ImGui.ColorConvertFloat4ToU32(fill) & 0xFFFFFF;
                var face = new List<(Vector2 Position, uint Alpha)>();
                var colors = new HashSet<uint>();
                for (var i = first; i < list.VtxBuffer.Size; i++)
                {
                    var vertex = list.VtxBuffer[i];
                    if (vertex.Col >> 24 > 0) colors.Add(vertex.Col & 0xFFFFFF);
                    if ((vertex.Col & 0xFFFFFF) == rgb && vertex.Col >> 24 > 0) face.Add((vertex.Pos, vertex.Col >> 24));
                }
                if (face.Count == 0) return false; // The caller separately requires a mesh for every tab intersecting its native clips.
                Assert.InRange(face.Count, 8, 16);
                foreach (var color in new[] { theme.Colors.Primary, theme.Colors.Secondary, Vector4.Lerp(theme.Colors.OnSurface, theme.Colors.Primary, .35f) })
                    Assert.Contains(ImGui.ColorConvertFloat4ToU32(color) & 0xFFFFFF, colors);
                foreach (var vertex in face)
                {
                    Assert.InRange(vertex.Position.X, min.X - 1, max.X + 1);
                    Assert.InRange(vertex.Position.Y, min.Y - 1, max.Y + 1);
                }
                var top = face.Min(vertex => vertex.Position.Y);
                var tangent = face.Where(vertex => vertex.Position.Y <= top + .01f).Min(vertex => vertex.Position.X - min.X);
                Assert.InRange(tangent, cut - 1.25f, cut + 1.25f);
                Assert.DoesNotContain(face, vertex => vertex.Position.X < min.X + cut * .4f && vertex.Position.Y < min.Y + cut * .4f);
                var expected = 255 * fill.W * alpha;
                Assert.InRange(face.Max(vertex => vertex.Alpha), (uint)expected, (uint)expected + 1);
                return true;
            }

            void AssertPlateClips(int first, Vector4 fill, Vector2 min, Vector2 max)
            {
                var rgb = ImGui.ColorConvertFloat4ToU32(fill) & 0xFFFFFF;
                for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
                {
                    var command = list.CmdBuffer[commandIndex]; var plate = false;
                    for (var i = command.IdxOffset; i < command.IdxOffset + command.ElemCount; i++)
                    {
                        var vertex = (int)(list.IdxBuffer[(int)i] + command.VtxOffset);
                        if (vertex >= first && (list.VtxBuffer[vertex].Col & 0xFFFFFF) == rgb && list.VtxBuffer[vertex].Col >> 24 > 0) { plate = true; break; }
                    }
                    if (!plate) continue;
                    Assert.True(command.ClipRect.X >= min.X && command.ClipRect.Y >= min.Y);
                    Assert.True(command.ClipRect.Z <= max.X && command.ClipRect.W <= max.Y);
                }
            }
        }
        ImGui.GetIO().AddMousePosEvent(-100, -100); host.Frame(Draw); host.Frame(Draw); request = -1;
        Assert.True(selected[0]);
        ImGui.GetIO().AddMousePosEvent(points[0].X, points[0].Y); host.Frame(Draw); Assert.True(firstHovered);
        ImGui.GetIO().AddMouseButtonEvent(0, true); host.Frame(Draw); Assert.True(firstActive);
        ImGui.GetIO().AddMouseButtonEvent(0, false); host.Frame(Draw);
        disabled = true; host.Click(points[1], Draw); Assert.True(selected[0]); Assert.False(selected[1]); Assert.False(firstHovered);
        disabled = false; host.Click(points[1], Draw); host.Frame(Draw); Assert.True(selected[1]);
        narrow = true; request = 2; host.Frame(Draw); host.Frame(Draw); host.Frame(Draw);
        Assert.True(selected[2]); Assert.True(panels > 0 && tabs > 0);
        ImGui.GetIO().AddMousePosEvent(-100, -100); narrow = false; unfocusOwner = true; host.Frame(Draw); host.Frame(Draw);
        rawBaseline = true; host.Frame(Draw); host.Frame(Draw); Assert.True(verifiedUnfocusedRaw);
        rawBaseline = false; host.Frame(Draw); host.Frame(Draw);
        AssertMeshIndices();
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f)]
    [InlineData(MaterialDensity.Compact, 1.5f)]
    [InlineData(MaterialDensity.Standard, 1f)]
    [InlineData(MaterialDensity.Standard, 1.5f)]
    public void RetainedButtonPixelRadiiKeepGeometryAlphaAndNativeState(MaterialDensity density, float scale)
    {
        using var host = new NativeTestContext((int)(600 * scale), (int)(420 * scale), scale);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        var mode = MaterialColorMode.Lcars;
        var size = new Vector2(160, 40) * scale;
        float? radius = null;
        var disabled = false; var requestFocus = false; var focused = false; var validateInvalid = true;
        var actions = 0;
        uint id = 0;
        float capturedAlpha = 0;
        Vector2 min = default, max = default;
        var mesh = new List<(Vector2 Position, uint Color)>();
        var face = new List<(Vector2 Position, uint Alpha)>();
        var fill = new Vector4(.91f, .12f, .08f, 1);
        var fillRgb = ImGui.ColorConvertFloat4ToU32(fill) & 0xFFFFFF;
        void Draw()
        {
            var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = mode }, density);
            using var palette = MaterialTheme.Push(theme, scale);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new Vector2(500, 350) * scale);
            ImGui.Begin("Retained pixel radius", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList(); var style = ImGui.GetStyle();
            var original = (style.FrameRounding, style.FrameBorderSize, style.Alpha, style.Colors[(int)ImGuiCol.Button]);
            using (MaterialSkin.NativeButtonStyle())
            using (var ink = new MaterialStyleScope())
            {
                ink.Style(ImGuiStyleVar.FrameRounding, radius ?? 0);
                ink.Style(ImGuiStyleVar.Alpha, .5f);
                ink.Color(ImGuiCol.Button, fill); ink.Color(ImGuiCol.ButtonHovered, fill); ink.Color(ImGuiCol.ButtonActive, fill);
                list.ChannelsSplit(2);
                try
                {
                    list.ChannelsSetCurrent(1);
                    ImGui.BeginDisabled(disabled);
                    try
                    {
                        if (requestFocus) ImGui.SetKeyboardFocusHere();
                        using (MaterialSkin.TransparentButtonFace())
                            if (ImGui.Button("Original action###retained-radius", size)) actions++;
                        capturedAlpha = style.Alpha;
                    }
                    finally { ImGui.EndDisabled(); }
                    id = ImGuiP.GetItemID(); min = ImGui.GetItemRectMin(); max = ImGui.GetItemRectMax();
                    focused = ImGui.IsItemFocused();
                    var context = ImGui.GetCurrentContext();
                    var state = (id, context.ActiveId, context.NavId, min, max, ImGui.IsItemHovered(), ImGui.IsItemActive(), focused);
                    list.ChannelsSetCurrent(0);
                    var first = list.VtxBuffer.Size;
                    if (validateInvalid)
                    {
                        var indices = list.IdxBuffer.Size;
                        foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                            Assert.Throws<ArgumentOutOfRangeException>(() => MaterialSkin.PaintButton(min, max, capturedAlpha, invalid));
                        Assert.Equal(first, list.VtxBuffer.Size); Assert.Equal(indices, list.IdxBuffer.Size);
                        Assert.Equal(2, list.Splitter.Count); Assert.Equal(0, list.Splitter.Current);
                        validateInvalid = false;
                    }
                    if (radius is { } pixels) MaterialSkin.PaintButton(min, max, capturedAlpha, pixels);
                    else MaterialSkin.PaintButton(min, max, capturedAlpha);
                    Assert.Equal(state, (ImGuiP.GetItemID(), context.ActiveId, context.NavId, ImGui.GetItemRectMin(),
                        ImGui.GetItemRectMax(), ImGui.IsItemHovered(), ImGui.IsItemActive(), ImGui.IsItemFocused()));
                    Assert.Equal(.5f, style.Alpha); Assert.Equal(radius ?? 0, style.FrameRounding);
                    mesh.Clear(); face.Clear();
                    for (var i = first; i < list.VtxBuffer.Size; i++)
                    {
                        var vertex = list.VtxBuffer[i];
                        mesh.Add((vertex.Pos - min, vertex.Col));
                        if ((vertex.Col & 0xFFFFFF) == fillRgb && vertex.Col >> 24 > 0)
                            face.Add((vertex.Pos - min, vertex.Col >> 24));
                    }
                }
                finally { list.ChannelsMerge(); }
            }
            Assert.Equal(original, (style.FrameRounding, style.FrameBorderSize, style.Alpha, style.Colors[(int)ImGuiCol.Button]));
            ImGui.End();
        }
        void AssertRadius(float expected)
        {
            Assert.NotEmpty(face);
            var top = face.Min(vertex => vertex.Position.Y);
            var tangent = face.Where(vertex => vertex.Position.Y <= top + .01f).Min(vertex => vertex.Position.X);
            // Native antialiasing and its one-pixel extent clamp can move the opaque tangent inward.
            Assert.InRange(tangent, expected - 1.25f, expected + 1.25f);
            var expectedAlpha = 255 * capturedAlpha * (mode == MaterialColorMode.Glassmorphism ? .68f : 1);
            Assert.InRange(face.Max(vertex => vertex.Alpha), (uint)expectedAlpha, (uint)expectedAlpha + 1);
        }
        foreach (var selected in new[] { MaterialColorMode.Lcars, MaterialColorMode.Glassmorphism,
                     MaterialColorMode.Spatial, MaterialColorMode.Cyberpunk })
            foreach (var logicalSize in new[] { new Vector2(48, 24), new Vector2(24, 48), new Vector2(160, 40) })
            {
                mode = selected; size = logicalSize * scale; radius = null;
                ImGui.GetIO().AddMousePosEvent(-100, -100);
                host.Frame(Draw); host.Frame(Draw);
                var original = (id, min, max);
                var legacyMesh = mesh.ToArray();
                var defaultRadius = (mode == MaterialColorMode.Lcars ? 7 : mode == MaterialColorMode.Cyberpunk ? 3 : 6) * scale;
                AssertRadius(defaultRadius);
                radius = defaultRadius; host.Frame(Draw);
                Assert.Equal(legacyMesh, mesh.ToArray());
                foreach (var pixels in new[] { 0f, 10f, 1000f })
                {
                    radius = pixels; host.Frame(Draw);
                    AssertRadius(Math.Min(pixels, Math.Min(size.X, size.Y) * .5f));
                    Assert.Equal(original, (id, min, max));
                }
                radius = 10;
                var before = actions;
                host.Click((min + max) * .5f, Draw);
                Assert.Equal(before + 1, actions);
                disabled = true; host.Click((min + max) * .5f, Draw);
                Assert.Equal(before + 1, actions); AssertRadius(10);
                disabled = false; requestFocus = true;
                host.Frame(Draw); host.Frame(Draw); requestFocus = false;
                Assert.True(focused);
                ImGui.GetIO().AddKeyEvent(ImGuiKey.Space, true); host.Frame(Draw);
                ImGui.GetIO().AddKeyEvent(ImGuiKey.Space, false); host.Frame(Draw);
                Assert.Equal(before + 2, actions);
                AssertMeshIndices();
            }
    }

    [Theory]
    [InlineData(MaterialColorMode.Glassmorphism)]
    [InlineData(MaterialColorMode.Spatial)]
    [InlineData(MaterialColorMode.Cyberpunk)]
    public void MaterialBackgroundFillsRespectSurfaceOpacityAndApplyNativeAlphaOnce(MaterialColorMode mode)
    {
        using var host = new NativeTestContext();
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = mode }) { SurfaceOpacity = .4f };
        var backdropAlpha = new List<uint>();
        var panelAlpha = new List<uint>();
        void Draw()
        {
            using var scope = MaterialTheme.Push(theme);
            using var opacity = new MaterialStyleScope(); opacity.Style(ImGuiStyleVar.Alpha, .5f);
            ImGui.SetNextWindowSize(new(600, 400));
            var nativeBackground = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Material surface alpha", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            Assert.Equal(nativeBackground, ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg]);
            var list = ImGui.GetWindowDrawList(); var first = list.VtxBuffer.Size;
            MaterialSkin.Backdrop();
            backdropAlpha.Clear();
            for (var i = first; i < list.VtxBuffer.Size; i++)
                if (list.VtxBuffer[i].Col >> 24 > 0) backdropAlpha.Add(list.VtxBuffer[i].Col >> 24);
            first = list.VtxBuffer.Size;
            var min = ImGui.GetCursorScreenPos();
            MaterialSkin.PanelFrame(min, min + new Vector2(250, 100));
            var fill = ImGui.ColorConvertFloat4ToU32(theme.Colors.SurfaceContainerLow) & 0xFFFFFF;
            panelAlpha.Clear();
            for (var i = first; i < list.VtxBuffer.Size; i++)
                if ((list.VtxBuffer[i].Col & 0xFFFFFF) == fill && list.VtxBuffer[i].Col >> 24 > 0)
                    panelAlpha.Add(list.VtxBuffer[i].Col >> 24);
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        Assert.NotEmpty(backdropAlpha); Assert.NotEmpty(panelAlpha);
        Assert.All(backdropAlpha, value => Assert.InRange(value, 50u, 52u));
        var expected = 255 * .5f * .4f * (mode == MaterialColorMode.Glassmorphism ? .42f : 1);
        Assert.InRange(panelAlpha.Max(), (uint)expected, (uint)expected + 1);
        var partial = BodyPixel();
        theme.SurfaceOpacity = 0; host.Frame(Draw);
        var clear = BodyPixel();
        Assert.Equal(new byte[] { 26, 29, 33 }, clear);
        theme.SurfaceOpacity = 1; host.Frame(Draw);
        var full = BodyPixel();
        for (var channel = 0; channel < 3; channel++)
            Assert.InRange(partial[channel], clear[channel] + (full[channel] - clear[channel]) * .4f - 2,
                clear[channel] + (full[channel] - clear[channel]) * .4f + 2);

        byte[] BodyPixel()
        {
            var frame = SoftwarePreview.Capture(host);
            var offset = (150 * frame.Width + 450) * 4;
            return [frame.Rgba[offset], frame.Rgba[offset + 1], frame.Rgba[offset + 2]];
        }
    }

    [Fact]
    public void PopupBackdropStyleEndsAfterBeginAndNestedNativePopupsKeepTheirBackground()
    {
        using var host = new NativeTestContext();
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = MaterialColorMode.Glassmorphism });
        var opening = true;
        var nestedOpening = true;
        var nativeBackgroundVertices = 0;
        void Draw()
        {
            using var themeScope = MaterialTheme.Push(theme);
            ImGui.SetNextWindowSize(new(600, 400));
            ImGui.Begin("Popup material owner", ImGuiWindowFlags.NoSavedSettings);
            if (opening) { ImGui.OpenPopup("appearance"); opening = false; }
            var popupColor = ImGui.GetStyle().Colors[(int)ImGuiCol.PopupBg];
            bool visible;
            using (MaterialSkin.BackdropStyle(popup: true)) visible = ImGui.BeginPopup("appearance");
            Assert.Equal(popupColor, ImGui.GetStyle().Colors[(int)ImGuiCol.PopupBg]);
            if (visible)
            {
                MaterialSkin.Backdrop();
                ImGui.TextUnformatted("Appearance");
                if (nestedOpening) { ImGui.OpenPopup("native-colour"); nestedOpening = false; }
                if (ImGui.BeginPopup("native-colour"))
                {
                    var list = ImGui.GetWindowDrawList();
                    var packed = ImGui.GetColorU32(ImGuiCol.PopupBg);
                    nativeBackgroundVertices = 0;
                    for (var i = 0; i < list.VtxBuffer.Size; i++)
                        if (list.VtxBuffer[i].Col == packed && packed >> 24 > 0) nativeBackgroundVertices++;
                    ImGui.TextUnformatted("Native nested picker");
                    ImGui.EndPopup();
                }
                ImGui.EndPopup();
            }
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw); host.Frame(Draw); host.Frame(Draw);
        Assert.True(nativeBackgroundVertices >= 4);
    }

    [Theory]
    [InlineData(MaterialColorMode.Glassmorphism, 1f)]
    [InlineData(MaterialColorMode.Glassmorphism, 1.5f)]
    [InlineData(MaterialColorMode.Spatial, 1f)]
    [InlineData(MaterialColorMode.Spatial, 1.5f)]
    [InlineData(MaterialColorMode.Cyberpunk, 1f)]
    [InlineData(MaterialColorMode.Cyberpunk, 1.5f)]
    public unsafe void BackdropsFillPaddingBehindNativeChromeAndKeepTheirMaterialsThroughOwnedCornerPainting(MaterialColorMode mode, float scale)
    {
        using var host = new NativeTestContext((int)(700 * scale), (int)(520 * scale), scale);
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = mode, GlassAnimation = false,
            GlassWave1Rgb = 0xFF1818, GlassWave2Rgb = 0x1820FF, GlassWave3Rgb = 0x18FF20 }) { SurfaceOpacity = 1 };
        var decorations = new MaterialWindowDecorations();
        var fold = new MaterialWindowFold();
        var owned = new ImGuiSizeCallback(_ => ImGuiP.GetCurrentWindow().Handle->ViewportOwned = 1);
        var noTitle = true;
        var square = false;
        var noBackground = false;
        var presses = 0;
        uint actionId = 0;
        Vector2 action = default, min = default, max = default;
        var scrollbarWidth = 0f;
        var titleHeight = 0f;
        var titleColor = new Vector4(.8f, .2f, .1f, 1);
        void Draw()
        {
            using var themeScope = MaterialTheme.Push(theme, scale);
            using var chrome = MaterialWindowChrome.Push();
            using var ink = new MaterialStyleScope();
            if (square) ink.Style(ImGuiStyleVar.WindowRounding, 0);
            ink.Color(ImGuiCol.ScrollbarGrab, new(1, 1, 0, 1));
            ink.Color(ImGuiCol.TitleBg, titleColor); ink.Color(ImGuiCol.TitleBgActive, titleColor);
            ImGui.SetNextWindowSizeConstraints(new(100), new(1000), owned);
            fold.PreDraw("Material backdrop owner", null, null, true, decorations.Prepare);
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(500, 350) * scale);
            var flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysVerticalScrollbar;
            if (noTitle) flags |= ImGuiWindowFlags.NoTitleBar;
            if (noBackground) flags |= ImGuiWindowFlags.NoBackground;
            bool visible;
            using (MaterialSkin.BackdropStyle()) visible = ImGui.Begin("Material backdrop owner", flags);
            min = ImGui.GetWindowPos(); max = min + ImGui.GetWindowSize();
            scrollbarWidth = ImGui.GetStyle().ScrollbarSize;
            titleHeight = ImGuiP.TitleBarHeight(ImGuiP.GetCurrentWindow());
            if (visible)
            {
                MaterialSkin.Backdrop();
                decorations.Paint();
                if (ImGui.BeginTable("native-table", 2))
                {
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    if (ImGui.Button("Retained native action")) presses++;
                    actionId = ImGui.GetCurrentContext().LastItemData.ID;
                    action = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
                    ImGui.TableNextColumn(); ImGui.TextUnformatted("Native table content");
                    ImGui.EndTable();
                }
                ImGui.Dummy(new Vector2(100, 1000) * scale);
            }
            ImGui.End();
            decorations.Paint(); fold.PostDraw();
        }
        host.Frame(Draw); host.Frame(Draw); host.Frame(Draw);
        var image = SoftwarePreview.Capture(host);
        Assert.Equal(new byte[] { 26, 29, 33 }, Pixel(min + Vector2.One * scale));
        Assert.NotEqual(new byte[] { 26, 29, 33 }, Pixel(new(min.X + 4 * scale, (min.Y + max.Y) * .5f)));
        var colors = new HashSet<uint>();
        for (var y = 1; y < 7; y++)
            for (var x = 1; x < 7; x++)
            {
                var rgb = Pixel(min + (max - min) * new Vector2(x / 8f, y / 8f));
                colors.Add((uint)(rgb[0] << 16 | rgb[1] << 8 | rgb[2]));
            }
        if (mode == MaterialColorMode.Glassmorphism) Assert.True(colors.Count > 20);
        else if (mode == MaterialColorMode.Spatial) Assert.True(colors.Count > 5);
        var yellow = 0;
        for (var y = (int)(min.Y + 8 * scale); y < max.Y - 8 * scale; y++)
            for (var x = (int)(max.X - scrollbarWidth + 3 * scale); x < max.X - 3 * scale; x++)
            {
                var rgb = Pixel(new(x, y));
                if (rgb[0] > 250 && rgb[1] > 250 && rgb[2] < 5) yellow++;
            }
        Assert.True(yellow > 5);
        var originalId = actionId;
        host.Click(action, Draw);
        Assert.Equal(1, presses); Assert.Equal(originalId, actionId);
        noTitle = false;
        host.Frame(Draw); host.Frame(Draw);
        image = SoftwarePreview.Capture(host);
        var title = Pixel(min + new Vector2((max.X - min.X) * .8f, titleHeight * .5f));
        Assert.InRange(title[0], 202, 206); Assert.InRange(title[1], 49, 53); Assert.InRange(title[2], 24, 28);
        noTitle = true; square = true;
        host.Frame(Draw); host.Frame(Draw);
        image = SoftwarePreview.Capture(host);
        Assert.False(Pixel(min + new Vector2(2 * scale)).SequenceEqual(new byte[] { 26, 29, 33 }));
        noBackground = true;
        host.Frame(Draw); host.Frame(Draw);
        image = SoftwarePreview.Capture(host);
        Assert.Equal(new byte[] { 26, 29, 33 }, Pixel(new(min.X + 4 * scale, (min.Y + max.Y) * .5f)));
        AssertMeshIndices();

        byte[] Pixel(Vector2 point)
        {
            var offset = ((int)point.Y * image.Width + (int)point.X) * 4;
            return [image.Rgba[offset], image.Rgba[offset + 1], image.Rgba[offset + 2]];
        }
    }

    [Fact]
    public unsafe void BackdropRejectsAnActiveCallerSplitterWithoutChangingItsChannels()
    {
        using var host = new NativeTestContext();
        var theme = new MaterialTheme(Vector3.UnitY, new MaterialColorSettings { Mode = MaterialColorMode.Glassmorphism });
        host.Frame(() =>
        {
            using var scope = MaterialTheme.Push(theme);
            ImGui.SetNextWindowSize(new(500, 350));
            using (MaterialSkin.BackdropStyle()) ImGui.Begin("Existing caller channels", ImGuiWindowFlags.NoSavedSettings);
            var list = ImGui.GetWindowDrawList();
            list.ChannelsSplit(2);
            try
            {
                list.ChannelsSetCurrent(1);
                Assert.Throws<InvalidOperationException>(() => MaterialSkin.Backdrop());
                Assert.Equal(2, list.Splitter.Count);
                Assert.Equal(1, list.Splitter.Current);
                ImGui.TextUnformatted("Retained caller channel");
            }
            finally { list.ChannelsMerge(); }
            ImGui.End();
        });
        AssertMeshIndices();
    }

    [Theory]
    [InlineData(MaterialColorMode.Glassmorphism)]
    [InlineData(MaterialColorMode.Spatial)]
    [InlineData(MaterialColorMode.Cyberpunk)]
    public void ExplicitSemanticFillAndDisabledAlphaSurviveTheNativeHitScope(MaterialColorMode mode)
    {
        using var host = new NativeTestContext();
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), new MaterialColorSettings { Mode = mode });
        var appearance = new MaterialControlAppearance(new(.91f, .12f, .08f, .8f), new(.9f, .95f, 1, 1), new(.12f, .88f, .15f, 1), 0);
        var fillAlphas = new List<uint>();
        var borderAlphas = new List<uint>();
        float expectedAlpha = 0;
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme);
            using var style = new MaterialStyleScope();
            style.Style(ImGuiStyleVar.Alpha, .5f);
            ImGui.SetNextWindowSize(new(600, 400));
            ImGui.Begin("Semantic material", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList(); var first = list.VtxBuffer.Size;
            expectedAlpha = .5f * ImGui.GetStyle().DisabledAlpha;
            MaterialButton.Draw("error", "Error", appearance, new(160, 40), disabled: true);
            Assert.Equal(.5f, ImGui.GetStyle().Alpha);
            var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
            var fill = ImGui.ColorConvertFloat4ToU32(appearance.Fill) & 0xFFFFFF;
            var edge = ImGui.ColorConvertFloat4ToU32(appearance.Border) & 0xFFFFFF;
            var facePoints = new List<Vector2>();
            fillAlphas.Clear(); borderAlphas.Clear();
            for (var i = first; i < list.VtxBuffer.Size; i++)
            {
                var vertex = list.VtxBuffer[i];
                if ((vertex.Col & 0xFFFFFF) == fill)
                {
                    fillAlphas.Add(vertex.Col >> 24);
                    facePoints.Add(vertex.Pos);
                    Assert.InRange(vertex.Pos.X, min.X, max.X);
                    Assert.InRange(vertex.Pos.Y, min.Y, max.Y);
                }
                if ((vertex.Col & 0xFFFFFF) == edge) borderAlphas.Add(vertex.Col >> 24);
            }
            Assert.Contains(min, facePoints); Assert.Contains(max, facePoints);
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        Assert.NotEmpty(fillAlphas); Assert.NotEmpty(borderAlphas);
        Assert.InRange(fillAlphas.Max(), (uint)(255 * .8f * expectedAlpha), (uint)(255 * .8f * expectedAlpha) + 1);
        Assert.InRange(borderAlphas.Max(), (uint)(255 * expectedAlpha), (uint)(255 * expectedAlpha) + 1);
    }

    [Fact]
    public void LcarsChromeUsesOnlyTheThreeSavedBarsAndReadableCaptionInk()
    {
        using var host = new NativeTestContext();
        var settings = new MaterialColorSettings { Mode = MaterialColorMode.Lcars, LcarsBackgroundRgb = 0xE0E9ED,
            LcarsBar1Rgb = 0x13272E, LcarsBar2Rgb = 0xF0D720, LcarsBar3Rgb = 0x8E15C0 };
        var theme = new MaterialTheme(Vector3.One, settings);
        Assert.Equal(Vector4.One, MaterialSkin.ReadableInk(theme.Colors.Primary));
        Assert.Equal(new Vector4(0, 0, 0, 1), MaterialSkin.ReadableInk(theme.Colors.Secondary));
        var colors = new HashSet<uint>();
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme);
            ImGui.SetNextWindowSize(new(700, 500));
            ImGui.Begin("Saved LCARS bars", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
            var list = ImGui.GetWindowDrawList(); var first = list.VtxBuffer.Size;
            MaterialLcars.Header("Operator");
            for (var i = 0; i < 4; i++) MaterialLcars.PanelHeading("Panel", i);
            MaterialLcars.Footer("Ready");
            colors.Clear();
            for (var i = first; i < list.VtxBuffer.Size; i++) colors.Add(list.VtxBuffer[i].Col & 0xFFFFFF);
            Assert.Equal(theme.Colors.Primary, MaterialLcars.Orange);
            Assert.Equal(theme.Colors.Secondary, MaterialLcars.Lilac);
            Assert.Equal(theme.Colors.Tertiary, MaterialLcars.Periwinkle);
            Assert.Equal(MaterialLcars.Accent(0), MaterialLcars.Accent(3));
            ImGui.End();
        }
        host.Frame(Draw); host.Frame(Draw);
        foreach (var bar in new[] { theme.Colors.Primary, theme.Colors.Secondary, theme.Colors.Tertiary })
            Assert.Contains(ImGui.ColorConvertFloat4ToU32(bar) & 0xFFFFFF, colors);
        Assert.Contains(0xFFFFFFu, colors); Assert.Contains(0u, colors);
        Assert.DoesNotContain(ImGui.ColorConvertFloat4ToU32(MaterialLcars.Amber) & 0xFFFFFF, colors);
        Assert.NotEqual(theme.Colors.Primary, MaterialLcars.Orange); // Default preview outside the selected theme.
    }

    private static unsafe void AssertMeshIndices()
    {
        var data = ImGui.GetDrawData();
        for (var li = 0; li < data.CmdListsCount; li++)
        {
            var list = new ImDrawListPtr(data.CmdLists[li]);
            for (var ci = 0; ci < list.CmdBuffer.Size; ci++)
            {
                var command = list.CmdBuffer[ci];
                Assert.True(command.IdxOffset + command.ElemCount <= list.IdxBuffer.Size);
                for (var i = command.IdxOffset; i < command.IdxOffset + command.ElemCount; i++)
                    Assert.True(list.IdxBuffer[(int)i] + command.VtxOffset < list.VtxBuffer.Size);
            }
        }
    }
}
