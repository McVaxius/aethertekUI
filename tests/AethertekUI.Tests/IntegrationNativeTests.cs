using System.Numerics;
using AethertekUI.Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Xunit;

namespace AethertekUI.Tests;

public unsafe class IntegrationNativeTests
{
    [Fact]
    public void ReleasingRasterizedFontInputPreservesNativeDrawing()
    {
        using var host = new NativeTestContext();
        var atlas = ImGui.ImFontAtlas();
        try
        {
            var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguisb.ttf");
            var font = atlas.AddFontFromFileTTF(fontPath, 22);
            byte* pixels = null;
            int width = 0, height = 0;
            atlas.GetTexDataAsRGBA32(0, &pixels, &width, &height);
            var index = atlas.TextureIndexCommon;
            Assert.InRange(index, 0, atlas.Textures.Size - 1);
            atlas.SetTexID(index, new ImTextureID((nint)71));
            Assert.True(MaterialUiResourceDiagnostics.OwnedFontInputBytes(atlas) > 0);

            MaterialFontAtlas.ReleaseRasterizedInputs(atlas);
            MaterialFontAtlas.ReleaseRasterizedInputs(atlas);
            Assert.Equal(index, atlas.TextureIndexCommon);
            Assert.Equal(0L, MaterialUiResourceDiagnostics.OwnedFontInputBytes(atlas));

            host.Frame(() =>
            {
                ImGui.PushFont(font);
                try
                {
                    ImGui.Begin("Released source font", ImGuiWindowFlags.NoSavedSettings);
                    try
                    {
                        var drawList = ImGui.GetWindowDrawList();
                        var before = drawList.VtxBuffer.Size;
                        ImGui.TextUnformatted("Ready");
                        Assert.True(drawList.VtxBuffer.Size > before);
                    }
                    finally { ImGui.End(); }
                }
                finally { ImGui.PopFont(); }
            });
        }
        finally { ImGui.Destroy(atlas); }
    }

    [Fact]
    public void CapturedCaptionFontReplacesNativeInkFromASeparateHostAtlas()
    {
        using var host=new NativeTestContext();
        var atlas=ImGui.ImFontAtlas();
        try
        {
            var nativeFont=atlas.AddFontFromFileTTF(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"seguisb.ttf"),22);
            byte* pixels=null;int width=0,height=0;
            atlas.GetTexDataAsRGBA32(0,&pixels,&width,&height);atlas.SetTexID(0,new ImTextureID((nint)71));
            var theme=new MaterialTheme(new Vector3(.3f,.7f,.4f));
            void Draw()
            {
                using var palette=MaterialTheme.Push(theme,1,MaterialStyleMode.ColorsOnly);
                ImGui.GetStyle().WindowMenuButtonPosition=ImGuiDir.Right;
                ImGui.PushFont(nativeFont);
                ImGui.SetNextWindowPos(new(30,30));ImGui.SetNextWindowSize(new(680,200));
                var open=true;
                ImGui.Begin("Original###private-caption",ref open,ImGuiWindowFlags.NoSavedSettings);
                var window=ImGuiP.GetCurrentWindow();
                ImGui.End();ImGui.PopFont();
                var list=window.DrawList;
                var before=Enumerable.Range(0,list.VtxBuffer.Size).Select(i=>list.VtxBuffer[i]).ToArray();
                var identity=window.ID;var size=window.SizeFull;
                MaterialWindowHeader.PaintTitleWithStatus(window,"Replacement v1.0",default,Vector2.Zero,
                    new("Ready","Ready"),nativeFont,80);
                var changed=0;
                for(var i=0;i<before.Length;i++)
                {
                    Assert.Equal(before[i].Pos,list.VtxBuffer[i].Pos);Assert.Equal(before[i].Uv,list.VtxBuffer[i].Uv);
                    if(before[i].Col==list.VtxBuffer[i].Col) continue;
                    changed++;Assert.Equal(before[i].Col&0x00FFFFFF,list.VtxBuffer[i].Col);
                }
                Assert.Equal("Original".Length*4,changed);
                Assert.Equal(identity,window.ID);Assert.Equal(size,window.SizeFull);
            }
            host.Frame(Draw);host.Frame(Draw);
        }
        finally { ImGui.Destroy(atlas); }
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f, false)]
    [InlineData(MaterialDensity.Standard, 1.5f, true)]
    public void TitlebarStatusFitsFullShortEllipsisOrNothingWithoutChangingNativeGeometry(MaterialDensity density, float scale, bool collapsed)
    {
        using var host = new NativeTestContext((int)(1200 * scale), (int)(600 * scale), scale);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), density);
        var opacity = new MaterialWindowOpacity();
        const string name = "Native title###status-fit";
        const string branding = "Plugin v1.2.3";
        const string full = "Following [RSR] Duty: ADS";
        const string concise = "Following";
        var expected = full;
        var mode = 0;
        uint originalId = 0;
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            ImGui.GetStyle().WindowMenuButtonPosition = ImGuiDir.Right;
            var style = ImGui.GetStyle();
            var font = ImGui.GetFontSize();
            var gap = style.ItemInnerSpacing.X;
            var reserved = 3 * (font + gap);
            var available = mode switch
            {
                0 => ImGui.CalcTextSize(full).X + 2,
                1 => ImGui.CalcTextSize(concise).X + 2,
                2 => (ImGui.CalcTextSize("Fol...").X + ImGui.CalcTextSize("Foll...").X) * .5f,
                _ => ImGui.CalcTextSize("F...").X - 1,
            };
            var width = 2 * style.WindowBorderSize + 2 * style.FramePadding.X + 2 * (font + gap) + reserved
                + font + gap + ImGui.CalcTextSize(branding).X + 2 * gap + font * .26f + gap + available;
            ImGui.SetNextWindowPos(new Vector2(30, 30) * scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(width, 180 * scale));
            ImGui.SetNextWindowCollapsed(collapsed);
            var open = true;
            var shown = ImGui.Begin(name, ref open, ImGuiWindowFlags.NoSavedSettings);
            var window = ImGuiP.GetCurrentWindow();
            if (shown) ImGui.Button("Retained body action");
            ImGui.End();
            var size = window.SizeFull;
            var position = window.Pos;
            var lastId = ImGui.GetCurrentContext().LastItemData.ID;
            originalId = originalId == 0 ? window.ID : originalId;
            var list = window.DrawList;
            var first = list.VtxBuffer.Size;
            var quietColor = ImGui.GetColorU32(theme.Colors.OnSurfaceVariant);
            var dotColor = ImGui.GetColorU32(new Vector4(1, .1f, .2f, 1));
            MaterialWindowHeader.PaintTitleWithStatus(window, branding, default, Vector2.Zero,
                new(full, concise, "Full operational details", new Vector4(1, .1f, .2f, 1)), reserved);
            Assert.Equal(size, window.SizeFull);
            Assert.Equal(position, window.Pos);
            Assert.Equal(originalId, window.ID);
            Assert.Equal(lastId, ImGui.GetCurrentContext().LastItemData.ID);
            var statusVertices = Enumerable.Range(first, list.VtxBuffer.Size - first)
                .Where(index => list.VtxBuffer[index].Col == quietColor).ToArray();
            Assert.Equal(expected.Count(character => !char.IsWhiteSpace(character)) * 4, statusVertices.Length);
            var dotVertices = Enumerable.Range(first, list.VtxBuffer.Size - first)
                .Where(index => list.VtxBuffer[index].Col == dotColor).ToArray();
            Assert.Equal(expected.Length != 0, dotVertices.Length != 0);
            var limit = window.Pos.X + window.Size.X - window.WindowBorderSize - style.FramePadding.X
                - 2 * (font + gap) - reserved;
            Assert.All(statusVertices, index => Assert.True(list.VtxBuffer[index].Pos.X <= limit + 1));
            opacity.Apply(name, .5f, autoFade: false);
            Assert.All(statusVertices.Concat(dotVertices), index =>
                Assert.Equal(128u, list.VtxBuffer[index].Col >> 24));
        }
        foreach (var choice in new[] { (0, full), (1, concise), (2, "Fol..."), (3, "") })
        {
            mode = choice.Item1;
            expected = choice.Item2;
            host.Frame(Draw); host.Frame(Draw);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TitlebarStatusTooltipRetainsOwnerOpacityAndNativeTitleActions(bool collapsed)
    {
        using var host = new NativeTestContext(800, 500);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), MaterialDensity.Compact);
        var opacity = new MaterialWindowOpacity();
        const string name = "Retained title###status-tooltip";
        var forceCollapse = true;
        var open = true;
        var tooltipObserved = false;
        Vector2 titlePoint = default, collapsePoint = default, closePoint = default;
        ImGuiWindowPtr window = default;
        void Draw()
        {
            tooltipObserved = false;
            using var palette = MaterialTheme.Push(theme, 1, MaterialStyleMode.ColorsOnly);
            ImGui.GetStyle().WindowMenuButtonPosition = ImGuiDir.Right;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, ImGui.GetStyle().FramePadding.Y));
            ImGui.SetNextWindowPos(new Vector2(30, 30), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(400, 180), ImGuiCond.FirstUseEver);
            if (forceCollapse) ImGui.SetNextWindowCollapsed(collapsed);
            ImGui.Begin(name, ref open, ImGuiWindowFlags.NoSavedSettings);
            window = ImGuiP.GetCurrentWindow();
            var title = ImGuiP.TitleBarRect(window);
            var font = ImGui.GetFontSize();
            var gap = ImGui.GetStyle().ItemInnerSpacing.X;
            titlePoint = title.Min + new Vector2(100, (title.Max.Y - title.Min.Y) * .5f);
            collapsePoint = new Vector2(title.Max.X - 1.5f * font, titlePoint.Y);
            closePoint = new Vector2(title.Max.X - .5f * font, titlePoint.Y);
            ImGui.End();
            var rootCount = window.DrawList.VtxBuffer.Size;
            var size = window.SizeFull;
            var lastId = ImGui.GetCurrentContext().LastItemData.ID;
            // Deliberately leave no room for status ink: the full tooltip remains available.
            MaterialWindowHeader.PaintTitleWithStatus(window, "Retained v1.0", default, Vector2.Zero,
                new("Waiting for the party leader to finish the current action", "Waiting",
                    "Action: Waiting\nReason: Party leader\nRuns: 8 / 66"), 250);
            Assert.Equal(size, window.SizeFull);
            Assert.Equal(lastId, ImGui.GetCurrentContext().LastItemData.ID);
            var context = ImGui.GetCurrentContext();
            for (var index = 0; index < context.Windows.Size; index++)
            {
                var candidate = new ImGuiWindowPtr(context.Windows.Data[index]);
                if ((candidate.Flags & ImGuiWindowFlags.Tooltip) == 0 || candidate.LastFrameActive != ImGui.GetFrameCount()) continue;
                tooltipObserved = true;
                Assert.Equal(window, candidate.ParentWindowInBeginStack);
            }
            opacity.Apply(name, .5f, autoFade: false);
            if (tooltipObserved)
                for (var index = 0; index < context.Windows.Size; index++)
                {
                    var candidate = new ImGuiWindowPtr(context.Windows.Data[index]);
                    if ((candidate.Flags & ImGuiWindowFlags.Tooltip) == 0 || candidate.LastFrameActive != ImGui.GetFrameCount()) continue;
                    Assert.Contains(Enumerable.Range(0, candidate.DrawList.VtxBuffer.Size),
                        vertex => candidate.DrawList.VtxBuffer[vertex].Col >> 24 == 128);
                }
            Assert.True(window.DrawList.VtxBuffer.Size >= rootCount);
            ImGui.PopStyleVar();
        }
        host.Frame(Draw); host.Frame(Draw);
        forceCollapse = false;
        var nativeId = window.ID;
        ImGui.GetIO().AddMousePosEvent(titlePoint.X, titlePoint.Y);
        host.Frame(Draw); host.Frame(Draw);
        Assert.True(tooltipObserved);
        ImGui.GetIO().AddMousePosEvent(collapsePoint.X, collapsePoint.Y);
        host.Frame(Draw); host.Frame(Draw);
        Assert.False(tooltipObserved);
        host.Click(collapsePoint, Draw);
        host.Frame(Draw);
        Assert.Equal(!collapsed, window.Collapsed);
        Assert.Equal(nativeId, window.ID);
        host.Click(closePoint, Draw);
        Assert.False(open);
    }

    [Fact]
    public void TitlebarStatusUsesMeasuredShapedTextAndPreservesItsClipping()
    {
        using var host = new NativeTestContext(600, 300);
        using var renderer = new MaterialTextRenderer(bitmap =>
        {
            var texture = host.RegisterTexture(bitmap.Rgba, bitmap.Width, bitmap.Height);
            return new MaterialTextTexture(texture.Id, texture);
        });
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), MaterialDensity.Compact);
        const string status = "\u0939\u093F\u0928\u094D\u0926\u0940";
        void Draw()
        {
            using var shaped = renderer.Push();
            using var palette = MaterialTheme.Push(theme, 1, MaterialStyleMode.ColorsOnly);
            ImGui.SetNextWindowSize(new Vector2(440, 180));
            ImGui.Begin("Status shaping###status-shaped", ImGuiWindowFlags.NoSavedSettings);
            var window = ImGuiP.GetCurrentWindow();
            ImGui.End();
            var first = window.DrawList.VtxBuffer.Size;
            MaterialWindowHeader.PaintTitleWithStatus(window, "Plugin v1.0", default, Vector2.Zero,
                new(status, status, status), 60, showCloseButton: false);
            Assert.Contains(Enumerable.Range(0, window.DrawList.CmdBuffer.Size), index =>
                window.DrawList.CmdBuffer[index].TextureId.Handle >= 2 && window.DrawList.CmdBuffer[index].ElemCount > 0);
            var vertices = Enumerable.Range(first, window.DrawList.VtxBuffer.Size - first)
                .Where(index => window.DrawList.VtxBuffer[index].Col == ImGui.GetColorU32(theme.Colors.OnSurfaceVariant)).ToArray();
            Assert.Equal(4, vertices.Length);
            var size = renderer.GetLayout(status, ImGui.GetFontSize()).Size;
            Assert.True(size.X > 0);
            Assert.All(vertices, index => Assert.True(window.DrawList.VtxBuffer[index].Pos.X < window.Pos.X + window.Size.X - 60));
        }
        host.Frame(Draw); host.Frame(Draw);
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f)]
    [InlineData(MaterialDensity.Standard, 1.5f)]
    public void AppearanceTitlebarEditorsWorkAfterEndWhileCollapsedAndRemainOwnerScoped(MaterialDensity density, float scale)
    {
        using var host = new NativeTestContext((int)(1000 * scale), (int)(600 * scale), scale);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), density);
        var first = new AppearanceProbeWindow("First###appearance-first") { IsOpen = true };
        var second = new AppearanceProbeWindow("Second###appearance-second") { IsOpen = true };
        var saves = new int[2];
        var compact = 0;
        var rendered = new int[2];
        var centers = new Vector2[2];
        var popups = new ImGuiWindowPtr[2];
        var fade = new MaterialWindowOpacity();
        var focusCheck = false;
        var labels = new MaterialAppearanceTitleBarLabels("Language", "Compact", "Transparency", "Colour");
        var firstActions = MaterialAppearanceTitleBar.Register(first, () => labels,
            () => Editor(0), () => compact++, () => Editor(0), () => Editor(0));
        var secondActions = MaterialAppearanceTitleBar.Register(second, () => labels,
            () => Editor(1), () => compact++, () => Editor(1), () => Editor(1));

        void Editor(int owner)
        {
            rendered[owner]++;
            popups[owner] = ImGuiP.GetCurrentWindow();
            Assert.Equal(ImGuiP.FindWindowByName(owner == 0 ? first.WindowName : second.WindowName),
                popups[owner].ParentWindowInBeginStack);
            if (ImGui.Button("Save existing preference")) { saves[owner]++; ImGui.CloseCurrentPopup(); }
            centers[owner] = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
        }

        void Draw()
        {
            using var palette = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            ImGui.SetNextWindowPos(new Vector2(20, 20) * scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(400, 200) * scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowCollapsed(true);
            ImGui.Begin(first.WindowName, ImGuiWindowFlags.NoSavedSettings);
            ImGui.End();
            var firstNative = ImGuiP.FindWindowByName(first.WindowName);
            var rootVertices = firstNative.DrawList.VtxBuffer.Size;
            firstActions.DrawPopups();
            Assert.Equal(rootVertices, firstNative.DrawList.VtxBuffer.Size);
            var popup = popups[0];
            var alphas = !popup.IsNull && popup.LastFrameActive == ImGui.GetFrameCount()
                ? Enumerable.Range(0, popup.DrawList.VtxBuffer.Size).Select(index => popup.DrawList.VtxBuffer[index].Col >> 24).ToArray()
                : [];
            fade.Apply(first.WindowName, focusCheck ? 1 : .5f, autoFade: focusCheck,
                fadedOpacity: .2f, unfocusedDelaySeconds: 0, reducedMotion: true);
            if (!focusCheck)
                for (var index = 0; index < alphas.Length; index++)
                    Assert.Equal((uint)MathF.Round(alphas[index] * .5f), popup.DrawList.VtxBuffer[index].Col >> 24);
            ImGui.SetNextWindowPos(new Vector2(500, 20) * scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(400, 200) * scale, ImGuiCond.FirstUseEver);
            ImGui.Begin(second.WindowName, ImGuiWindowFlags.NoSavedSettings);
            ImGui.End();
            secondActions.DrawPopups();
        }

        host.Frame(Draw); host.Frame(Draw);
        var native = ImGuiP.FindWindowByName(first.WindowName);
        var originalId = native.ID;
        var originalSize = native.SizeFull;
        first.TitleBarButtons[0].Click!(ImGuiMouseButton.Right);
        host.Frame(Draw);
        Assert.Equal(new[] { 0, 0 }, rendered);
        first.TitleBarButtons[1].Click!(ImGuiMouseButton.Right);
        Assert.Equal(0, compact);
        first.TitleBarButtons[1].Click!(ImGuiMouseButton.Left);
        Assert.Equal(1, compact);

        // The descriptor selects the editor; native pointer presses verify its
        // after-End popup and save/close path. Managed icon rendering is separate.
        first.TitleBarButtons[0].Click!(ImGuiMouseButton.Left);
        host.Frame(Draw); host.Frame(Draw);
        Assert.True(rendered[0] > 0);
        Assert.Equal(0, rendered[1]);
        focusCheck = true;
        host.Frame(Draw);
        Assert.Equal(1, fade.CurrentOpacity);
        host.Click(centers[0], Draw);
        Assert.Equal(new[] { 1, 0 }, saves);
        Assert.True(native.Collapsed);
        Assert.Equal(originalId, native.ID);
        Assert.Equal(originalSize, native.SizeFull);

        foreach (var buttonIndex in new[] { 2, 3 })
        {
            first.TitleBarButtons[buttonIndex].Click!(ImGuiMouseButton.Left);
            host.Frame(Draw); host.Frame(Draw);
            host.Click(centers[0], Draw);
        }
        Assert.Equal(new[] { 3, 0 }, saves);

        second.TitleBarButtons[0].Click!(ImGuiMouseButton.Left);
        host.Frame(Draw); host.Frame(Draw);
        host.Click(centers[1], Draw);
        Assert.Equal(new[] { 3, 1 }, saves);
        Assert.True(native.Collapsed);
    }

    [Fact]
    public void AppearanceRegistrationRetainsExistingNativePriorityOrderAndDescriptors()
    {
        var window = new AppearanceProbeWindow("Existing title actions###appearance-order");
        var original = new[]
        {
            new TitleBarButton { Icon = FontAwesomeIcon.PowerOff, Priority = 3 },
            new TitleBarButton { Icon = FontAwesomeIcon.Cog, Priority = -2 },
        };
        window.TitleBarButtons.AddRange(original);
        var visible = new MaterialAppearanceTitleBarVisibility(true, true, true, true);
        var appearance = MaterialAppearanceTitleBar.Register(window, () => new("Language", "Compact", "Transparency", "Colour"),
            () => { }, () => { }, () => { }, () => { }, () => visible);
        var hostOrder = window.TitleBarButtons.OrderByDescending(button => button.Priority).ToArray();
        Assert.Same(original[0], hostOrder[0]);
        Assert.Same(original[1], hostOrder[1]);
        Assert.Equal(new[] { FontAwesomeIcon.Language, FontAwesomeIcon.CompressAlt, FontAwesomeIcon.Adjust, FontAwesomeIcon.Palette },
            hostOrder.Skip(2).Select(button => button.Icon));
        Assert.Equal(3, original[0].Priority);
        Assert.Equal(-2, original[1].Priority);
        var fixedDescriptors = hostOrder.Skip(2).ToArray();
        visible = new(true, false, false, true);
        appearance.RefreshVisibility();
        Assert.Equal(4, window.TitleBarButtons.Count);
        Assert.Same(original[0], window.TitleBarButtons[0]);
        Assert.Same(original[1], window.TitleBarButtons[1]);
        Assert.Same(fixedDescriptors[0], window.TitleBarButtons[2]);
        Assert.Same(fixedDescriptors[3], window.TitleBarButtons[3]);
        visible = new(true, true, true, true);
        appearance.RefreshVisibility();
        appearance.RefreshVisibility();
        hostOrder = window.TitleBarButtons.OrderByDescending(button => button.Priority).ToArray();
        Assert.Equal(6, hostOrder.Length);
        Assert.Same(original[0], hostOrder[0]);
        Assert.Same(original[1], hostOrder[1]);
        Assert.Equal(fixedDescriptors, hostOrder.Skip(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KrangleTitlebarActionSharesConsumerStateAndPreservesExistingControls(bool collapsed)
    {
        using var host = new NativeTestContext(1000, 500);
        var owners = new[]
        {
            new AppearanceProbeWindow("First###krangle-first") { IsOpen = true },
            new AppearanceProbeWindow("Second###krangle-second") { IsOpen = true },
        };
        var original = owners.Select(_ => new[]
        {
            new TitleBarButton { Icon = FontAwesomeIcon.PowerOff, Priority = 3 },
            new TitleBarButton { Icon = FontAwesomeIcon.Cog, Priority = -2 },
        }).ToArray();
        var privacy = false;
        var toggles = 0;
        var compact = 0;
        var editors = new int[3];
        var observed = new string[2];
        var tooltips = new string[2];
        var bodyIds = new uint[2];
        var visibility = new MaterialAppearanceTitleBarVisibility(true, true, true, true);
        var labels = new MaterialAppearanceTitleBarLabels("Language", "Compact", "Transparency", "Colour");
        var closeEditor = false;
        var actions = owners.Select((owner, index) =>
        {
            owner.TitleBarButtons.AddRange(original[index]);
            return MaterialAppearanceTitleBar.Register(owner, () => labels,
                () => Editor(0), () => compact++, () => Editor(1), () => Editor(2),
                () => { privacy = !privacy; toggles++; },
                () => tooltips[index] = privacy ? "Show character names" : "Krangle character names",
                () => visibility);
        }).ToArray();
        var fixedDescriptors = owners.Select(owner => owner.TitleBarButtons.OrderByDescending(button => button.Priority).ToArray()).ToArray();
        var priorIcons = new[] { FontAwesomeIcon.Language, FontAwesomeIcon.CompressAlt, FontAwesomeIcon.Adjust, FontAwesomeIcon.Palette };
        var hoverOwner = -1;

        void Editor(int index)
        {
            editors[index]++;
            if (closeEditor) ImGui.CloseCurrentPopup();
        }

        void Draw()
        {
            for (var index = 0; index < owners.Length; index++)
            {
                actions[index].RefreshVisibility();
                ImGui.SetNextWindowPos(new Vector2(20 + index * 480, 20), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSize(new Vector2(440, 200), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowCollapsed(collapsed);
                var shown = ImGui.Begin(owners[index].WindowName, ImGuiWindowFlags.NoSavedSettings);
                bodyIds[index] = ImGui.GetID("Retained body action");
                observed[index] = privacy ? "Private character" : "Visible character";
                if (shown)
                {
                    ImGui.TextUnformatted(observed[index]);
                    ImGui.Button("Retained body action");
                }
                ImGui.End();
                actions[index].DrawPopups();
                if (hoverOwner == index) fixedDescriptors[index][6].ShowTooltip!();
            }
        }

        host.Frame(Draw); host.Frame(Draw);
        var natives = owners.Select(owner => ImGuiP.FindWindowByName(owner.WindowName)).ToArray();
        var nativeIds = natives.Select(window => window.ID).ToArray();
        var positions = natives.Select(window => window.Pos).ToArray();
        var sizes = natives.Select(window => window.SizeFull).ToArray();
        var originalBodyIds = bodyIds.ToArray();
        foreach (var index in new[] { 0, 1 })
        {
            Assert.Equal(7, fixedDescriptors[index].Length);
            Assert.Same(original[index][0], fixedDescriptors[index][0]);
            Assert.Same(original[index][1], fixedDescriptors[index][1]);
            Assert.Equal(priorIcons, fixedDescriptors[index].Skip(2).Take(4).Select(button => button.Icon));
            Assert.Equal(FontAwesomeIcon.UserSecret, fixedDescriptors[index][6].Icon);
            Assert.Equal(new[] { 3, -2, -3, -4, -5, -6, -7 }, fixedDescriptors[index].Select(button => button.Priority));
            hoverOwner = index;
            host.Frame(Draw);
            Assert.Equal("Krangle character names", tooltips[index]);
        }
        hoverOwner = -1;

        // Use the production descriptors: this covers callbacks, independently of managed icon rendering.
        fixedDescriptors[0][6].Click!(ImGuiMouseButton.Right);
        Assert.False(privacy);
        Assert.Equal(0, toggles);
        fixedDescriptors[0][6].Click!(ImGuiMouseButton.Left);
        Assert.True(privacy);
        Assert.Equal(1, toggles);
        hoverOwner = 1;
        host.Frame(Draw);
        Assert.Equal(new[] { "Private character", "Private character" }, observed);
        Assert.Equal("Show character names", tooltips[1]);
        Assert.Equal(new[] { 0, 0, 0 }, editors);
        fixedDescriptors[1][6].Click!(ImGuiMouseButton.Left);
        Assert.False(privacy);
        Assert.Equal(2, toggles);
        hoverOwner = 0;
        host.Frame(Draw);
        Assert.Equal(new[] { "Visible character", "Visible character" }, observed);
        Assert.Equal("Krangle character names", tooltips[0]);
        hoverOwner = -1;

        // Visibility is re-read by the callback, even before the next refresh removes its descriptor.
        visibility = visibility with { Krangle = false };
        fixedDescriptors[0][6].Click!(ImGuiMouseButton.Left);
        Assert.Equal(2, toggles);
        host.Frame(Draw);
        foreach (var index in new[] { 0, 1 })
        {
            actions[index].RefreshVisibility();
            Assert.Equal(fixedDescriptors[index].Take(6), owners[index].TitleBarButtons.OrderByDescending(button => button.Priority));
            fixedDescriptors[index][6].Click!(ImGuiMouseButton.Left);
            fixedDescriptors[index][6].Click!(ImGuiMouseButton.Right);
        }
        Assert.False(privacy);
        Assert.Equal(2, toggles);
        visibility = visibility with { Krangle = true };
        foreach (var action in actions) { action.RefreshVisibility(); action.RefreshVisibility(); }
        host.Frame(Draw);
        foreach (var index in new[] { 0, 1 })
        {
            Assert.Equal(fixedDescriptors[index], owners[index].TitleBarButtons.OrderByDescending(button => button.Priority));
            Assert.Equal(nativeIds[index], natives[index].ID);
            Assert.Equal(positions[index], natives[index].Pos);
            Assert.Equal(sizes[index], natives[index].SizeFull);
            Assert.Equal(originalBodyIds[index], bodyIds[index]);
            Assert.Equal(collapsed, natives[index].Collapsed);
        }

        fixedDescriptors[0][3].Click!(ImGuiMouseButton.Right);
        Assert.Equal(0, compact);
        fixedDescriptors[0][3].Click!(ImGuiMouseButton.Left);
        Assert.Equal(1, compact);
        foreach (var descriptor in new[] { 2, 4, 5 })
        {
            fixedDescriptors[0][descriptor].Click!(ImGuiMouseButton.Right);
            host.Frame(Draw);
            Assert.Equal(new[] { 0, 0, 0 }, editors);
        }
        closeEditor = true;
        foreach (var descriptor in new[] { 2, 4, 5 })
        {
            fixedDescriptors[0][descriptor].Click!(ImGuiMouseButton.Left);
            host.Frame(Draw); host.Frame(Draw);
        }
        Assert.All(editors, count => Assert.True(count > 0));
        Assert.False(privacy);
        Assert.Equal(2, toggles);

        var legacy = new AppearanceProbeWindow("Legacy###krangle-legacy");
        MaterialAppearanceTitleBar.Register(legacy, () => labels, () => { }, () => { }, () => { }, () => { });
        Assert.Equal(priorIcons, legacy.TitleBarButtons.Select(button => button.Icon));
    }

    [Fact]
    public void OptionalWindowControlsPreserveExistingActionsAndUseLiveConsumerState()
    {
        using var host = new NativeTestContext();
        var window = new AppearanceProbeWindow("Optional controls###window-controls") { IsOpen = true };
        var original = new TitleBarButton { Icon = FontAwesomeIcon.PowerOff, Priority = 7 };
        window.TitleBarButtons.Add(original);
        var visibility = new MaterialAppearanceTitleBarVisibility(true, true, true, true);
        var styling = true;
        var locked = false;
        var styleSaves = 0;
        var lockSaves = 0;
        var styleTooltip = "";
        var lockTooltip = "";
        var actions = MaterialAppearanceTitleBar.Register(window, () => new("Language", "Compact", "Transparency", "Colour"),
            () => { }, () => { }, () => { }, () => { }, () => { }, () => "Privacy", () => visibility);
        var existing = window.TitleBarButtons.OrderByDescending(button => button.Priority).ToArray();
        Assert.Equal(6, existing.Length);
        actions.RegisterWindowControls(() => { styling = !styling; styleSaves++; },
            () => styleTooltip = styling ? "Use plain native styling" : "Enable styling",
            () => { locked = !locked; lockSaves++; },
            () => lockTooltip = locked ? "Unlock movement and resizing" : "Lock movement and resizing");
        var descriptors = window.TitleBarButtons.OrderByDescending(button => button.Priority).ToArray();
        Assert.Equal(existing, descriptors.Take(existing.Length));
        Assert.Equal(8, descriptors.Length);
        Assert.Equal(FontAwesomeIcon.Moon, descriptors[6].Icon);
        Assert.Equal(FontAwesomeIcon.Lock, descriptors[7].Icon);
        Assert.True(descriptors[6].Priority < existing[^1].Priority);
        Assert.True(descriptors[7].Priority < descriptors[6].Priority);
        descriptors[6].Click!(ImGuiMouseButton.Right);
        descriptors[7].Click!(ImGuiMouseButton.Right);
        Assert.True(styling); Assert.False(locked);
        Assert.Equal(0, styleSaves + lockSaves);
        descriptors[6].Click!(ImGuiMouseButton.Left);
        descriptors[7].Click!(ImGuiMouseButton.Left);
        Assert.False(styling); Assert.True(locked);
        Assert.True(window.IsOpen);
        Assert.Equal(1, styleSaves); Assert.Equal(1, lockSaves);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f));
        host.Frame(() =>
        {
            using var scope = MaterialTheme.Push(theme);
            descriptors[6].ShowTooltip!();
        });
        host.Frame(() =>
        {
            using var scope = MaterialTheme.Push(theme);
            descriptors[7].ShowTooltip!();
        });
        Assert.Equal("Enable styling", styleTooltip);
        Assert.Equal("Unlock movement and resizing", lockTooltip);
        visibility = visibility with { Styling = false, MovementLock = false };
        descriptors[6].Click!(ImGuiMouseButton.Left);
        descriptors[7].Click!(ImGuiMouseButton.Left);
        Assert.Equal(1, styleSaves); Assert.Equal(1, lockSaves);
        actions.RefreshVisibility();
        Assert.Equal(existing, window.TitleBarButtons.OrderByDescending(button => button.Priority));
        visibility = visibility with { Styling = true, MovementLock = true };
        actions.RefreshVisibility(); actions.RefreshVisibility();
        Assert.Equal(descriptors, window.TitleBarButtons.OrderByDescending(button => button.Priority));
        Assert.Throws<InvalidOperationException>(() => actions.RegisterWindowControls(() => { }, () => "Style", () => { }, () => "Lock"));
        Assert.Equal(descriptors, window.TitleBarButtons.OrderByDescending(button => button.Priority));
    }

    [Theory]
    [InlineData(ImGuiWindowFlags.None)]
    [InlineData(ImGuiWindowFlags.NoResize)]
    [InlineData(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize)]
    public void TemporaryWindowLockRestoresExactFlagsWithoutChangingPinState(ImGuiWindowFlags originalFlags)
    {
        var window = new AppearanceProbeWindow("Locked###temporary-window-lock") { Flags = originalFlags };
        var other = new AppearanceProbeWindow("Other###temporary-window-lock-other");
        var pinned = window.IsPinned;
        var windowLock = new MaterialWindowLock();
        windowLock.Apply(window, true);
        Assert.Equal(originalFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize, window.Flags);
        Assert.Equal(pinned, window.IsPinned);
        Assert.Throws<InvalidOperationException>(() => windowLock.Apply(other, true));
        Assert.Throws<InvalidOperationException>(() => windowLock.Restore(other));
        windowLock.Restore(window);
        Assert.Equal(originalFlags, window.Flags);
        Assert.Equal(pinned, window.IsPinned);
        windowLock.Apply(window, false);
        window.Flags |= ImGuiWindowFlags.NoScrollbar;
        windowLock.Restore(window);
        Assert.Equal(originalFlags | ImGuiWindowFlags.NoScrollbar, window.Flags);
    }

    [Fact]
    public void NativeThemeKeepsHostStyleAndRetainedControlsAndRestoresNestedPalettes()
    {
        using var host = new NativeTestContext();
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), new MaterialColorSettings { Mode = MaterialColorMode.Lcars });
        theme.SurfaceOpacity = .2f;
        var savedPalette = theme.Colors;
        var alternate = new MaterialTheme(new Vector3(.7f, .3f, .4f));
        host.Frame(() =>
        {
            var style = ImGui.GetStyle();
            var colors = Enumerable.Range(0, Enum.GetValues<ImGuiCol>().Max(role => (int)role))
                .Select(index => style.Colors[index]).ToArray();
            var padding = style.WindowPadding;
            var rounding = style.WindowRounding;
            using (var scope = MaterialTheme.Push(theme, 1.5f, MaterialStyleMode.Native))
            {
                Assert.Same(theme, MaterialTheme.Current);
                Assert.True(theme.IsNative);
                Assert.False(theme.IsLcars);
                Assert.False(theme.IsGlassmorphism);
                Assert.Equal(1.5f, MaterialTheme.Metrics.Scale);
                Assert.Equal(colors[(int)ImGuiCol.WindowBg], theme.Colors.Background);
                Assert.Equal(colors[(int)ImGuiCol.Button], theme.Colors.SecondaryContainer);
                Assert.Equal(colors[(int)ImGuiCol.Text], theme.Colors.OnSurface);
                Assert.Equal(savedPalette.Error, theme.Colors.Error);
                Assert.Equal(theme.Colors.Background, theme.SurfaceColor(theme.Colors.Background));
                for (var index = 0; index < colors.Length; index++) Assert.Equal(colors[index], style.Colors[index]);
                Assert.Equal(padding, style.WindowPadding);
                Assert.Equal(rounding, style.WindowRounding);
                var nativePalette = theme.Colors;
                using (var nested = MaterialTheme.Push(alternate)) Assert.False(MaterialTheme.Current.IsNative);
                Assert.Same(nativePalette, theme.Colors);
                Assert.True(MaterialTheme.Current.IsNative);
                ImGui.Begin("Native retained controls###native-theme", ImGuiWindowFlags.NoSavedSettings);
                var id = ImGui.GetID("retained-owner-id");
                MaterialButton.FilledButton("retained-button", "Action");
                Assert.Equal(id, ImGui.GetID("retained-owner-id"));
                ImGui.End();
            }
            Assert.Same(savedPalette, theme.Colors);
            Assert.True(theme.IsLcars);
            Assert.False(theme.IsNative);
            for (var index = 0; index < colors.Length; index++) Assert.Equal(colors[index], style.Colors[index]);
            Assert.Equal(padding, style.WindowPadding);
            Assert.Equal(rounding, style.WindowRounding);
            var windowHost = new MaterialWindowHost { StyleMode = MaterialStyleMode.Native };
            using (var scope = windowHost.BeginFrame(applyMetrics: false)) Assert.True(MaterialTheme.Current.IsNative);
        });
    }

    private sealed class AppearanceProbeWindow(string name) : Window(name)
    {
        public override void Draw() { }
    }

    [Theory]
    [InlineData(240, 0f, 1f, false)]
    [InlineData(350, .5f, 1f, true)]
    [InlineData(500, 1f, 1f, false)]
    [InlineData(240, 0f, 1.5f, true)]
    [InlineData(350, .5f, 1.5f, false)]
    [InlineData(500, 1f, 1.5f, true)]
    public void RepaintedClippedTitleMatchesAnInitiallyTransparentNativeCaption(int width, float alignment, float scale, bool collapsed)
    {
        using var host = new NativeTestContext((int)(800 * scale), (int)(500 * scale), scale);
        var paintFont = ImGui.GetIO().Fonts.AddFontDefault();
        paintFont.Scale = 1.65f;
        host.RefreshAtlas();
        ImGui.GetIO().Fonts.SetTexID(0, new ImTextureID((nint)1));
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), MaterialDensity.Compact);
        const string name = "MOGTOME v3.0.0.8 | A long native caption clipped by the retained window###clipped-title";
        var hideNative = false;
        Vector2 min = default, max = default;
        void Draw()
        {
            using var palette = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowTitleAlign, new Vector2(alignment, .5f));
            ImGui.GetStyle().WindowMenuButtonPosition = ImGuiDir.Right;
            ImGui.SetNextWindowPos(new Vector2(30, 30) * scale);
            ImGui.SetNextWindowSize(new Vector2(width, 200) * scale);
            ImGui.SetNextWindowCollapsed(collapsed);
            if (hideNative) ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            var open = true;
            ImGui.Begin(name, ref open, ImGuiWindowFlags.NoSavedSettings);
            var window = ImGuiP.GetCurrentWindow();
            var title = ImGuiP.TitleBarRect(window);
            var fontSize = ImGui.GetFontSize();
            min = title.Min + new Vector2(window.WindowBorderSize + ImGui.GetStyle().FramePadding.X, 1);
            max = title.Max - new Vector2(window.WindowBorderSize + ImGui.GetStyle().FramePadding.X
                + 2 * (fontSize + ImGui.GetStyle().ItemInnerSpacing.X), 1);
            ImGui.End();
            if (hideNative) ImGui.PopStyleColor();
            ImGui.PushFont(paintFont);
            try { MaterialWindowHeader.PaintTitle(window, "MOGTOME v3.0.0.8", default, Vector2.Zero, 180 * scale); }
            finally { ImGui.PopFont(); }
            ImGui.PopStyleVar();
        }
        host.Frame(Draw); host.Frame(Draw);
        var actual = SoftwarePreview.Capture(host);
        hideNative = true;
        host.Frame(Draw);
        var expected = SoftwarePreview.Capture(host);
        // Compare the real rendered caption area while retaining native actions.
        // This detects clipped glyph remnants, including borrowed-font repaints.
        for (var y = (int)MathF.Ceiling(min.Y); y < (int)MathF.Floor(max.Y); y++)
        for (var x = (int)MathF.Ceiling(min.X); x < (int)MathF.Floor(max.X); x++)
        for (var channel = 0; channel < 4; channel++)
        {
            var offset = (y * actual.Width + x) * 4 + channel;
            Assert.InRange(Math.Abs(actual.Rgba[offset] - expected.Rgba[offset]), 0, 2);
        }
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f, false)]
    [InlineData(MaterialDensity.Compact, 1.25f, true)]
    [InlineData(MaterialDensity.Standard, 1f, true)]
    [InlineData(MaterialDensity.Standard, 1.25f, false)]
    public void BorrowedTitleImageRetainsNativeControlsGeometryAndOpacity(MaterialDensity density, float scale, bool menuBar)
    {
        using var host = new NativeTestContext(1000, 600, scale);
        var paintFont = ImGui.GetIO().Fonts.AddFontDefault();
        paintFont.Scale = 1.65f;
        host.RefreshAtlas();
        using var image = host.RegisterTexture(Enumerable.Repeat((byte)255, 8 * 4 * 4).ToArray(), 8, 4);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), density);
        var opacity = new MaterialWindowOpacity();
        const string name = "Original retained title###borrowed-title";
        var imageReady = true;
        var collapsed = false;
        var forceCollapse = true;
        var open = true;
        var actions = 0;
        var bodyActions = 0;
        Vector2 actionCenter = default, bodyCenter = default, bodyOrigin = default, textOrigin = default;
        ImGuiWindowPtr window = default;
        uint actionId = 0, bodyId = 0;

        void Draw()
        {
            using var palette = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 5 * scale));
            ImGui.GetStyle().WindowMenuButtonPosition = ImGuiDir.Right;
            ImGui.SetNextWindowPos(new(30, 30), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(560, 330) * scale, ImGuiCond.FirstUseEver);
            if (forceCollapse) ImGui.SetNextWindowCollapsed(collapsed);
            var visible = ImGui.Begin(name, ref open, ImGuiWindowFlags.NoSavedSettings
                | (menuBar ? ImGuiWindowFlags.MenuBar : ImGuiWindowFlags.None));
            window = ImGuiP.GetCurrentWindow();
            var title = ImGuiP.TitleBarRect(window);
            var cursor = ImGui.GetCursorScreenPos();
            var fontSize = ImGui.GetFontSize();
            // Models an existing consumer-owned title action, including while collapsed.
            var actionPosition = new Vector2(title.Max.X - 3 * fontSize - 2 * ImGui.GetStyle().ItemInnerSpacing.X, title.Min.Y);
            var id = ImGui.GetID("title-action");
            if (actionId == 0) actionId = id;
            Assert.Equal(actionId, id);
            ImGui.PushClipRect(title.Min, title.Max, false);
            try { if (ImGuiP.CloseButton(id, actionPosition)) actions++; }
            finally { ImGui.PopClipRect(); }
            actionCenter = actionPosition + new Vector2(fontSize * .5f, (title.Max.Y - title.Min.Y) * .5f);
            if (visible)
            {
                bodyOrigin = cursor;
                id = ImGui.GetID("body-action");
                if (bodyId == 0) bodyId = id;
                Assert.Equal(bodyId, id);
                if (ImGui.Button("body-action")) bodyActions++;
                bodyCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
            }
            ImGui.End();
            var list = window.DrawList;
            var count = list.VtxBuffer.Size;
            var prior = Enumerable.Range(0, count).Select(i => list.VtxBuffer[i]).ToArray();
            var lastId = ImGui.GetCurrentContext().LastItemData.ID;
            ImGui.PushFont(paintFont);
            try
            {
                Assert.True(ImGuiP.CalcFontSize(window) > fontSize * 1.5f);
                MaterialWindowHeader.PaintTitle(window, "Translated retained title", imageReady ? image.Id : default,
                    new(8, 4), fontSize + 2 * ImGui.GetStyle().ItemInnerSpacing.X);
            }
            finally { ImGui.PopFont(); }
            Assert.Equal(lastId, ImGui.GetCurrentContext().LastItemData.ID);
            var hidden = 0;
            for (var i = 0; i < count; i++)
            {
                Assert.Equal(prior[i].Pos, list.VtxBuffer[i].Pos);
                Assert.Equal(prior[i].Uv, list.VtxBuffer[i].Uv);
                if (prior[i].Col == list.VtxBuffer[i].Col) continue;
                hidden++;
                Assert.Equal(prior[i].Col & 0x00ffffff, list.VtxBuffer[i].Col);
                Assert.InRange(list.VtxBuffer[i].Pos.Y, title.Min.Y - 1, title.Max.Y + 1);
            }
            Assert.True(hidden > 0, "Native caption was not replaced.");
            var imageVertices = new List<ImDrawVert>();
            var captionVertices = new List<ImDrawVert>();
            for (var c = 0; c < list.CmdBuffer.Size; c++)
            {
                var command = list.CmdBuffer[c];
                for (var i = command.IdxOffset; i < command.IdxOffset + command.ElemCount; i++)
                {
                    var index = (int)(list.IdxBuffer[(int)i] + command.VtxOffset);
                    if (index < count) continue;
                    var vertex = list.VtxBuffer[index];
                    if (command.TextureId == image.Id) imageVertices.Add(vertex);
                    else captionVertices.Add(vertex);
                }
            }
            Assert.Equal(imageReady, imageVertices.Count > 0);
            if (imageReady)
            {
                var min = new Vector2(imageVertices.Min(v => v.Pos.X), imageVertices.Min(v => v.Pos.Y));
                var max = new Vector2(imageVertices.Max(v => v.Pos.X), imageVertices.Max(v => v.Pos.Y));
                Assert.InRange((max.X - min.X) / (max.Y - min.Y), 1.999f, 2.001f);
                Assert.InRange(max.X - min.X, fontSize - .01f, fontSize + .01f);
                Assert.True(max.Y <= title.Max.Y && min.Y >= title.Min.Y);
                Assert.True(max.X < captionVertices.Min(v => v.Pos.X));
            }
            var currentTextOrigin = new Vector2(captionVertices.Min(v => v.Pos.X), captionVertices.Min(v => v.Pos.Y));
            if (textOrigin == default) textOrigin = currentTextOrigin;
            Assert.Equal(textOrigin, currentTextOrigin); // Missing texture keeps its space.
            opacity.Apply(name, .5f, autoFade: false);
            foreach (var vertex in imageVertices)
            {
                // Vertex snapshots are pre-opacity; the actual list below proves one multiplier.
                Assert.Equal(uint.MaxValue, vertex.Col);
            }
            if (imageReady)
                Assert.Contains(Enumerable.Range(count, list.VtxBuffer.Size - count),
                    i => (list.VtxBuffer[i].Col >> 24) == 128);
            ImGui.PopStyleVar();
        }

        host.Frame(Draw); host.Frame(Draw);
        var initialBody = bodyOrigin;
        var position = window.Pos;
        var size = window.SizeFull;
        host.Click(bodyCenter, Draw);
        Assert.Equal(1, bodyActions);
        Assert.Equal(initialBody, bodyOrigin);
        host.Click(actionCenter, Draw);
        Assert.Equal(1, actions);
        imageReady = false; host.Frame(Draw);
        Assert.Equal(position, window.Pos);
        Assert.Equal(size, window.SizeFull);
        collapsed = true; host.Frame(Draw); host.Frame(Draw);
        Assert.True(window.Collapsed);
        host.Click(actionCenter, Draw);
        Assert.Equal(2, actions);
        forceCollapse = false;
        var titleRect = ImGuiP.TitleBarRect(window);
        var font = ImGuiP.CalcFontSize(window);
        host.Click(new(titleRect.Max.X - 1.5f * font, titleRect.Min.Y + (titleRect.Max.Y - titleRect.Min.Y) * .5f), Draw);
        host.Frame(Draw); // Native collapse request applies on the next Begin.
        Assert.False(window.Collapsed);
        host.Click(new(titleRect.Max.X - .5f * font, titleRect.Min.Y + (titleRect.Max.Y - titleRect.Min.Y) * .5f), Draw);
        Assert.False(open);
        Assert.Equal(size, window.SizeFull);
        Assert.Equal(0, host.ReleasedTextures);
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f)]
    [InlineData(MaterialDensity.Compact, 1.5f)]
    [InlineData(MaterialDensity.Standard, 1f)]
    [InlineData(MaterialDensity.Standard, 1.5f)]
    public void OwnedViewportChromePreservesTablesAndNestedChannelsThroughOpacityToggle(MaterialDensity density, float scale)
    {
        using var host = new NativeTestContext((int)(1200 * scale), (int)(700 * scale), scale);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), density);
        var fold = new MaterialWindowFold();
        var decorations = new MaterialWindowDecorations();
        var opacity = new MaterialWindowOpacity();
        const string name = "Owned table chrome###owned-table-chrome";
        var decorated = false;
        var transparency = false;
        uint transparencyId = 0;
        var actionIds = new uint[6];
        var actionCenters = new Vector2[6];
        var presses = new int[6];
        Vector2 transparencyCenter = default, contentMin = default, contentMax = default;
        ImDrawListPtr list = default;
        var markers = new List<(string Name, int FirstVertex, Vector2 Min, Vector2 Max)>();
        var owned = new ImGuiSizeCallback(_ => ImGuiP.GetCurrentWindow().Handle->ViewportOwned = 1);

        void Marker(string label)
        {
            var min = ImGui.GetCursorScreenPos();
            var max = min + new Vector2(18, 12) * scale;
            markers.Add((label, list.VtxBuffer.Size, min, max));
            list.AddRectFilled(min, max, 0xff48e0c0);
            ImGui.Dummy(max - min);
        }

        void Draw()
        {
            using var palette = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            using var chrome = MaterialWindowChrome.Push();
            using var geometry = new MaterialStyleScope();
            geometry.Style(ImGuiStyleVar.WindowPadding, new Vector2(12) * scale);
            geometry.Style(ImGuiStyleVar.CellPadding, new Vector2(5, 4) * scale);
            markers.Clear();
            ImGui.SetNextWindowSizeConstraints(new(100), new(2000 * scale), owned);
            fold.PreDraw(name, null, null, true, decorated ? decorations.Prepare : null);
            ImGui.SetNextWindowPos(new Vector2(20) * scale);
            ImGui.SetNextWindowSize(new Vector2(980, 520) * scale);
            if (ImGui.Begin(name, ImGuiWindowFlags.NoSavedSettings))
            {
                if (decorated) decorations.Paint();
                list = ImGui.GetWindowDrawList();
                contentMin = ImGui.GetCursorScreenPos();
                if (ImGui.BeginTable("header", 2, ImGuiTableFlags.SizingStretchSame))
                {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("Header and subtitle remain visible");
                    Marker("header");
                    ImGui.TableNextColumn();
                    var id = ImGui.GetID("Transparency");
                    if (transparencyId == 0) transparencyId = id;
                    Assert.Equal(transparencyId, id);
                    ImGui.Checkbox("Transparency", ref transparency);
                    Assert.Equal(transparencyId, ImGuiP.GetItemID());
                    transparencyCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
                    ImGui.EndTable();
                }
                if (ImGui.BeginTable("actions", 6, ImGuiTableFlags.SizingStretchSame))
                {
                    for (var column = 0; column < 6; column++)
                    {
                        ImGui.TableNextColumn();
                        var label = $"Action {column}";
                        var id = ImGui.GetID(label);
                        if (actionIds[column] == 0) actionIds[column] = id;
                        Assert.Equal(actionIds[column], id);
                        if (ImGui.Button(label, new Vector2(-1, 32 * scale))) presses[column]++;
                        Assert.Equal(id, ImGuiP.GetItemID());
                        actionCenters[column] = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
                        Marker(label);
                    }
                    ImGui.EndTable();
                }
                if (ImGui.BeginTable("state", 4, ImGuiTableFlags.SizingStretchSame))
                {
                    for (var column = 0; column < 4; column++)
                    {
                        ImGui.TableNextColumn();
                        var start = ImGui.GetCursorScreenPos();
                        var width = ImGui.GetContentRegionAvail().X;
                        var splitter = ImGui.ImDrawListSplitter();
                        splitter.Split(list, 2);
                        try
                        {
                            splitter.SetCurrentChannel(list, 1);
                            ImGui.SetCursorScreenPos(start + new Vector2(6) * scale);
                            if (ImGui.BeginTable("bounds", 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings,
                                new Vector2(width - 12 * scale, 0)))
                            {
                                ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthFixed, width - 12 * scale);
                                ImGui.TableNextColumn();
                                ImGui.TextUnformatted($"State {column}");
                                Marker($"State {column}");
                                ImGui.EndTable();
                            }
                            var bottom = ImGui.GetCursorScreenPos().Y + 6 * scale;
                            splitter.SetCurrentChannel(list, 0);
                            list.AddRectFilled(start, new(start.X + width, bottom), 0xff403020);
                            ImGui.SetCursorScreenPos(new(start.X, bottom));
                            ImGui.Dummy(new(width, 0));
                        }
                        finally { splitter.Merge(list); splitter.Destroy(); }
                    }
                    ImGui.EndTable();
                }
                contentMax = new(contentMin.X + ImGui.GetContentRegionAvail().X, ImGui.GetCursorScreenPos().Y);
            }
            ImGui.End();
            if (decorated) decorations.Paint();
            fold.PostDraw();
            opacity.Apply(name, .5f, transparency, autoFade: false, reducedMotion: true);

            // A table merge must keep each emitted marker under the clip of its own cell.
            foreach (var marker in markers)
            {
                var references = 0;
                for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
                {
                    var command = list.CmdBuffer[commandIndex];
                    for (var index = command.IdxOffset; index < command.IdxOffset + command.ElemCount; index++)
                    {
                        var vertex = list.IdxBuffer[(int)index] + command.VtxOffset;
                        if (vertex < marker.FirstVertex || vertex >= marker.FirstVertex + 4) continue;
                        references++;
                        Assert.True(command.ClipRect.X <= marker.Min.X && command.ClipRect.Y <= marker.Min.Y
                            && command.ClipRect.Z >= marker.Max.X && command.ClipRect.W >= marker.Max.Y,
                            $"{marker.Name} geometry uses another cell's clip {command.ClipRect}; expected {marker.Min}..{marker.Max}; decorations={decorated}, transparency={transparency}.");
                    }
                }
                Assert.True(references == 6, $"{marker.Name} has {references} indexed vertices instead of 6; decorations={decorated}.");
            }
        }

        void CheckInk()
        {
            ImGui.GetIO().AddMousePosEvent(-100, -100);
            decorated = false;
            host.Frame(Draw); host.Frame(Draw);
            var expected = SoftwarePreview.Capture(host);
            decorated = true;
            host.Frame(Draw); host.Frame(Draw);
            var actual = SoftwarePreview.Capture(host);
            for (var y = (int)MathF.Ceiling(contentMin.Y); y < (int)MathF.Floor(contentMax.Y); y++)
            for (var x = (int)MathF.Ceiling(contentMin.X); x < (int)MathF.Floor(contentMax.X); x++)
            for (var channel = 0; channel < 4; channel++)
            {
                var offset = (y * actual.Width + x) * 4 + channel;
                Assert.True(Math.Abs(expected.Rgba[offset] - actual.Rgba[offset]) <= 2,
                    $"Table ink changed at ({x},{y}) channel {channel}: native={expected.Rgba[offset]}, decorated={actual.Rgba[offset]}, transparency={transparency}.");
            }
        }

        CheckInk();
        for (var toggle = 0; toggle < 2; toggle++)
        {
            host.Click(transparencyCenter, Draw);
            Assert.Equal(toggle == 0, transparency);
            CheckInk();
        }
        for (var column = 0; column < 6; column++)
        {
            host.Click(actionCenters[column], Draw);
            Assert.Equal(1, presses[column]);
        }
    }

    [Theory]
    [InlineData(MaterialDensity.Compact, 1f)]
    [InlineData(MaterialDensity.Compact, 1.5f)]
    [InlineData(MaterialDensity.Standard, 1f)]
    [InlineData(MaterialDensity.Standard, 1.5f)]
    public void OwnedViewportChromeRoundsRenderedCornersAndPreservesNativeControls(MaterialDensity density, float scale)
    {
        using var host = new NativeTestContext(800, 600, scale);
        var theme = new MaterialTheme(new Vector3(.3f, .7f, .4f), density);
        var fold = new MaterialWindowFold();
        var decorations = new MaterialWindowDecorations();
        var useDecorations = false;
        var simulateOwned = true;
        var collapsed = false;
        var open = true;
        uint nativeId = 0, actionId = 0;
        var presses = 0;
        var cornerRadius = 0f;
        Vector2 actionCenter = default;
        ImGuiWindowPtr window = default;
        // Exercise the installed native Begin's owned-viewport branch without a platform backend.
        // The flag is injected after viewport selection; this is not live platform acceptance.
        var owned = new ImGuiSizeCallback(data => ImGuiP.GetCurrentWindow().Handle->ViewportOwned = (byte)(simulateOwned ? 1 : 0));
        void Draw()
        {
            var context = ImGui.GetCurrentContext();
            using var themeScope = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            using var chrome = MaterialWindowChrome.Push();
            cornerRadius = MaterialTheme.Metrics.OuterRadius;
            var colors = context.ColorStack.Size;
            ImGui.SetNextWindowSizeConstraints(new(100, 50), new(1000, 1000), owned);
            if (useDecorations) fold.PreDraw("Owned chrome###owned-chrome", null, null, true, decorations.Prepare);
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(320, 240) * scale);
            ImGui.SetNextWindowCollapsed(collapsed);
            ImGui.SetNextWindowBgAlpha(.4f);
            var titleColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TitleBg];
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.7f, .2f, .3f, .6f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.8f, .2f, .3f, .6f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(.6f, .2f, .3f, .6f));
            var visible = ImGui.Begin("Owned chrome###owned-chrome", ref open, ImGuiWindowFlags.HorizontalScrollbar);
            window = ImGuiP.GetCurrentWindow();
            try
            {
                Assert.Equal(simulateOwned, window.ViewportOwned);
                Assert.Equal(simulateOwned ? 0 : cornerRadius, window.WindowRounding);
                if (useDecorations && visible) decorations.Paint();
                if (nativeId == 0) nativeId = window.ID;
                Assert.Equal(nativeId, window.ID);
                Assert.Equal(new Vector2(320, 240) * scale, window.SizeFull);
                if (visible)
                {
                    var id = ImGui.GetID("retained-action");
                    if (actionId == 0) actionId = id;
                    Assert.Equal(actionId, id);
                    if (ImGui.Button("retained-action")) presses++;
                    actionCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
                    ImGui.Dummy(new Vector2(1000, 900) * scale);
                }
            }
            finally
            {
                // Match Dalamud's title override pop before End, including collapsed frames.
                ImGui.PopStyleColor(3);
                ImGui.End();
                if (useDecorations) { decorations.Paint(); fold.PostDraw(); }
            }
            Assert.Equal(colors, context.ColorStack.Size);
            Assert.Equal(titleColor, ImGui.GetStyle().Colors[(int)ImGuiCol.TitleBg]);
            Assert.True(open);
        }
        host.Frame(Draw); host.Frame(Draw);
        var square = SoftwarePreview.Capture(host);
        Assert.False(IsBackground(square, window.Pos + Vector2.One));
        useDecorations = true;
        host.Frame(Draw); host.Frame(Draw);
        var rounded = SoftwarePreview.Capture(host);
        Assert.True(window.ScrollbarX);
        Assert.True(window.ScrollbarY);
        AssertCorners(rounded);
        host.Click(actionCenter, Draw);
        Assert.Equal(1, presses);
        collapsed = true;
        host.Frame(Draw); host.Frame(Draw);
        Assert.True(window.Collapsed);
        AssertCorners(SoftwarePreview.Capture(host));
        collapsed = false;
        host.Frame(Draw); host.Frame(Draw);
        Assert.False(window.Collapsed);
        AssertCorners(SoftwarePreview.Capture(host));

        // Compare the corrected owned-window grip with native rounded decoration ink.
        // An undersized convex-fill path can clear corners while corrupting the grip.
        useDecorations = false;
        simulateOwned = false;
        host.Frame(Draw); host.Frame(Draw);
        var nativeRounded = SoftwarePreview.Capture(host);
        SoftwarePreview.Save(Path.Combine("Z:/aethertekUI/artifacts/window-motion-platform", $"grip-native-{density}-{scale}.png"), host);
        useDecorations = true;
        simulateOwned = true;
        host.Frame(Draw); host.Frame(Draw);
        var corrected = SoftwarePreview.Capture(host);
        SoftwarePreview.Save(Path.Combine("Z:/aethertekUI/artifacts/window-motion-platform", $"grip-corrected-{density}-{scale}.png"), host);
        var gripExtent = (int)MathF.Ceiling(60 * scale);
        var right = (int)(window.Pos.X + window.Size.X);
        var bottom = (int)(window.Pos.Y + window.Size.Y);
        for (var y = bottom - gripExtent; y < bottom; y++)
        for (var x = right - gripExtent; x < right; x++)
        for (var channel = 0; channel < 4; channel++)
        {
            var pixel = (y * corrected.Width + x) * 4 + channel;
            var referencePixel = (y * nativeRounded.Width + x) * 4 + channel;
            Assert.True(Math.Abs(corrected.Rgba[pixel] - nativeRounded.Rgba[referencePixel]) <= 2,
                $"Resize region ({x},{y}) channel {channel}: corrected {corrected.Rgba[pixel]}, native {nativeRounded.Rgba[referencePixel]}");
        }

        // Background and menu fills can be identical native rectangles. Match each
        // separately so an already-cleared background cannot hide the later menu.
        void DrawMenuOnly()
        {
            using var themeScope = MaterialTheme.Push(theme, scale, MaterialStyleMode.ColorsOnly);
            using var chrome = MaterialWindowChrome.Push();
            using var geometry = new MaterialStyleScope();
            geometry.Style(ImGuiStyleVar.WindowBorderSize, 0f);
            geometry.Style(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(320 * scale, ImGui.GetFontSize() + 2 * ImGui.GetStyle().FramePadding.Y));
            ImGui.SetNextWindowSizeConstraints(Vector2.Zero, new(1000), owned);
            fold.PreDraw("Menu-only owned chrome", null, null, true, decorations.Prepare);
            if (ImGui.Begin("Menu-only owned chrome", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.MenuBar |
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)) decorations.Paint();
            window = ImGuiP.GetCurrentWindow();
            ImGui.End();
            decorations.Paint();
            fold.PostDraw();
        }
        host.Frame(DrawMenuOnly); host.Frame(DrawMenuOnly);
        var menuFrame = SoftwarePreview.Capture(host);
        Assert.Equal(MathF.Floor(ImGuiP.MenuBarHeight(window)), window.Size.Y);
        Assert.True(IsBackground(menuFrame, window.Pos + Vector2.One));
        Assert.False(IsBackground(menuFrame, window.Pos + new Vector2(window.Size.X / 2, window.Size.Y / 2)));

        void AssertCorners(SoftwarePreview.Frame frame)
        {
            var min = window.Pos;
            var max = min + window.Size;
            Assert.True(IsBackground(frame, min + Vector2.One));
            Assert.True(IsBackground(frame, new(max.X - 2, min.Y + 1)));
            Assert.True(IsBackground(frame, new(min.X + 1, max.Y - 2)));
            Assert.True(IsBackground(frame, max - new Vector2(2)));
            Assert.False(IsBackground(frame, min + new Vector2(cornerRadius + 2, 2)));
            Assert.False(IsBackground(frame, max - new Vector2(cornerRadius + 2, 3)));
            Assert.Equal(cornerRadius, window.WindowRounding);
        }
        static bool IsBackground(SoftwarePreview.Frame frame, Vector2 point)
        {
            var i = ((int)point.Y * frame.Width + (int)point.X) * 4;
            return frame.Rgba[i] == 26 && frame.Rgba[i + 1] == 29 && frame.Rgba[i + 2] == 33;
        }
    }

    [Fact]
    public void ExplicitWindowChromePreservesRetainedLayoutAndRestoresOuterStyle()
    {
        using var context = new NativeTestContext();
        var style = ImGui.GetStyle();
        var rounding = style.WindowRounding;
        var padding = style.WindowPadding;
        var framePadding = style.FramePadding;
        var spacing = style.ItemSpacing;
        foreach (var density in new[] { MaterialDensity.Compact, MaterialDensity.Standard })
        foreach (var scale in new[] { 1f, 1.5f })
        {
            var chromeTheme = new MaterialTheme(new Vector3(.3f, .7f, .4f), density);
            void Draw()
            {
                using var themeScope = MaterialTheme.Push(chromeTheme, scale, MaterialStyleMode.ColorsOnly);
                Assert.Equal(rounding, style.WindowRounding);
                using (var chrome = MaterialWindowChrome.Push())
                {
                    ImGui.SetNextWindowSize(new Vector2(400, 260));
                    ImGui.Begin("Window chrome###retained-chrome", ImGuiWindowFlags.NoSavedSettings);
                    try
                    {
                        Assert.Equal(MaterialTheme.Metrics.OuterRadius, ImGuiP.GetCurrentWindow().WindowRounding);
                        Assert.Equal(padding, style.WindowPadding);
                        Assert.Equal(framePadding, style.FramePadding);
                        Assert.Equal(spacing, style.ItemSpacing);
                        var retainedId = ImGui.GetID("retained-action");
                        ImGui.Button("retained-action");
                        Assert.Equal(retainedId, ImGuiP.GetItemID());
                        Assert.Equal(new Vector2(400, 260), ImGui.GetWindowSize());
                    }
                    finally { ImGui.End(); }
                }
                Assert.Equal(rounding, style.WindowRounding);
            }
            context.Frame(Draw);
            context.Frame(Draw);
        }
        Assert.Equal(rounding, style.WindowRounding);
    }

    [Theory] [InlineData(14f)] [InlineData(36f)]
    public void ConsumerActionIconsKeepDistinctMeshesBoundsAndOpacity(float size)
    {
        using var context=new NativeTestContext();
        MaterialIcon[] icons=[MaterialIcon.Save,MaterialIcon.Copy,MaterialIcon.Heart,MaterialIcon.List,MaterialIcon.Sword,MaterialIcon.Star,MaterialIcon.Folder,MaterialIcon.Egg,
            MaterialIcon.Wrench,MaterialIcon.DoorExit,MaterialIcon.Cube,MaterialIcon.Chest,MaterialIcon.Cart,MaterialIcon.Chat,
            MaterialIcon.Group,MaterialIcon.Warning,MaterialIcon.Plus,MaterialIcon.Pulse,MaterialIcon.Utensils,MaterialIcon.Crystal,
            MaterialIcon.Calendar,MaterialIcon.Crown,MaterialIcon.Book,MaterialIcon.Sparkle,MaterialIcon.Bell,MaterialIcon.Terminal,MaterialIcon.Power,MaterialIcon.Target,
            MaterialIcon.MapPin,MaterialIcon.HeartOutline,MaterialIcon.Discord,MaterialIcon.KoFi,MaterialIcon.Backpack];
        Assert.Equal(35,(int)MaterialIcon.Send);
        for(var i=0;i<icons.Length;i++) Assert.Equal(36+i,(int)icons[i]);
        icons=[..icons,MaterialIcon.Location];
        var shapes=new List<Vector2[]>();
        void Draw()=>Window(()=>
        {
            shapes.Clear();
            var list=ImGui.GetWindowDrawList();
            var flags=list.Flags;
            var cursor=ImGui.GetCursorScreenPos();
            var item=ImGui.GetID("retained-action");
            foreach(var icon in icons)
            {
                var origin=new Vector2(40+(size+20)*shapes.Count,80);
                var firstVertex=list.VtxBuffer.Size;var firstIndex=list.IdxBuffer.Size;
                MaterialIcons.Draw(icon,origin,size,new(.3f,.5f,.8f,1),.4f);
                Assert.True(list.IdxBuffer.Size>firstIndex);
                var shape=new Vector2[list.VtxBuffer.Size-firstVertex];
                var maximumAlpha=0u;
                for(var i=0;i<shape.Length;i++)
                {
                    var vertex=list.VtxBuffer[firstVertex+i];
                    var position=vertex.Pos-origin;
                    Assert.True(float.IsFinite(position.X)&&float.IsFinite(position.Y));
                    Assert.True(position.X>=0&&position.X<=size&&position.Y>=0&&position.Y<=size,
                        $"{icon} vertex {i} escaped {size}px bounds: {position}");
                    maximumAlpha=Math.Max(maximumAlpha,vertex.Col>>24);
                    shape[i]=position/size;
                }
                Assert.InRange(maximumAlpha,101u,103u);
                if(icon==MaterialIcon.Discord)
                {
                    foreach(var eye in new[] { new Vector2(.35f,.54f),new Vector2(.65f,.54f) })
                    {
                        var point=origin+eye*size;
                        for(var i=firstIndex;i<list.IdxBuffer.Size;i+=3)
                        {
                            var a=list.VtxBuffer[list.IdxBuffer[i]].Pos;
                            var b=list.VtxBuffer[list.IdxBuffer[i+1]].Pos;
                            var c=list.VtxBuffer[list.IdxBuffer[i+2]].Pos;
                            static float Edge(Vector2 from,Vector2 to,Vector2 point)
                                =>(to.X-from.X)*(point.Y-from.Y)-(to.Y-from.Y)*(point.X-from.X);
                            if(MathF.Abs(Edge(a,b,c))<.0001f) continue;
                            var ab=Edge(a,b,point);var bc=Edge(b,c,point);var ca=Edge(c,a,point);
                            Assert.False((ab>=0&&bc>=0&&ca>=0)||(ab<=0&&bc<=0&&ca<=0),
                                $"Discord eye {eye} is covered by triangle {(a-origin)/size}, {(b-origin)/size}, {(c-origin)/size}; colors {list.VtxBuffer[list.IdxBuffer[i]].Col:X8}/{list.VtxBuffer[list.IdxBuffer[i+1]].Col:X8}/{list.VtxBuffer[list.IdxBuffer[i+2]].Col:X8}.");
                        }
                    }
                }
                if(icon==MaterialIcon.KoFi)
                {
                    var heart=ImGui.ColorConvertFloat4ToU32(new Vector4(1,.29f,.33f,.4f));
                    Assert.Contains(Enumerable.Range(firstVertex,shape.Length).Select(i=>list.VtxBuffer[i].Col),color=>color==heart);
                }
                Assert.All(shapes,previous=>Assert.False(previous.SequenceEqual(shape)));
                shapes.Add(shape);
                Assert.Equal(cursor,ImGui.GetCursorScreenPos());
                Assert.Equal(item,ImGui.GetID("retained-action"));
                Assert.Equal(flags,list.Flags);
            }
        });
        context.Frame(Draw);context.Frame(Draw);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void SearchableComboFiltersCachedOptionsUsingNativeTextEntry(bool appearance)
    {
        using var context=new NativeTestContext();
        var entries=new MaterialOption<int>[500];
        for(var i=0;i<entries.Length;i++) entries[i]=new($"key-{i}",i,$"Recipe {i:000}");
        var options=new MaterialOptions<int>(entries);
        var selected=0;Vector2 combo=default;
        void Draw()=>Window(()=>
        {
            if(appearance) MaterialCombo.Draw("recipes",options,ref selected,new MaterialControlAppearance(theme.Colors.Surface,theme.Colors.OnSurface,theme.Colors.Outline),width:300);
            else MaterialCombo.Draw("recipes",options,ref selected,width:300);
            if(options.Query.Length==0) combo=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())*.5f;
        });
        context.Frame(Draw);context.Frame(Draw);context.Click(combo,Draw);
        context.Frame(Draw);context.Frame(Draw);
        foreach(var ch in "499") ImGui.GetIO().AddInputCharacter(ch);
        context.Frame(Draw);
        Assert.Equal("499",options.Query);
        Assert.Equal(1,options.Count);
        Assert.Equal(499,options[0].Value);
    }
    [Fact]
    public void SplitterMovesAndBorrowedIconHelpersRender()
    {
        using var context=new NativeTestContext();
        var width=180f;Vector2 divider=default;
        void Draw()=>Window(()=>
        {
            ImGui.Dummy(new(width,80));ImGui.SameLine(0,0);
            MaterialSplitPane.Divider("split",ref width,600,100,100,80);
            divider=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())*.5f;
            MaterialCanvas.GlyphButton("glyph","+",ImGui.GetFont());
            MaterialCanvas.ImageButton("texture",new ImTextureID((nint)1),new(32));
            MaterialCanvas.Image(new ImTextureID((nint)1),new(32));
        });
        context.Frame(Draw);context.Frame(Draw);
        ImGui.GetIO().AddMousePosEvent(divider.X,divider.Y);context.Frame(Draw);
        ImGui.GetIO().AddMouseButtonEvent(0,true);context.Frame(Draw);
        ImGui.GetIO().AddMousePosEvent(divider.X+40,divider.Y);context.Frame(Draw);
        ImGui.GetIO().AddMouseButtonEvent(0,false);context.Frame(Draw);
        Assert.InRange(width,219,221);
    }
    private readonly MaterialTheme theme=new(new Vector3(.3f,.7f,.4f));
    private void Window(Action content, bool applyMetrics=true)
    {
        using var scope=MaterialTheme.Push(theme,1,applyMetrics);
        ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(new(700,650));
        ImGui.Begin("Integration tests",ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoTitleBar);
        content();
        ImGui.End();
    }
    [Fact]
    public void ColorsOnlyScopePreservesHostMetricsAndSemanticOverridesRestore()
    {
        using var context=new NativeTestContext();
        context.Frame(()=>
        {
            var padding=ImGui.GetStyle().WindowPadding;
            var rounding=ImGui.GetStyle().FrameRounding;
            var text=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            using(var scope=MaterialTheme.Push(theme,1.5f,false))
            {
                Assert.Equal(padding,ImGui.GetStyle().WindowPadding);
                Assert.Equal(rounding,ImGui.GetStyle().FrameRounding);
                var domain=new Vector4(1,.2f,.1f,1);
                using(var semantic=MaterialSemantic.Text(domain))
                    Assert.Equal(domain,ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
                Assert.Equal(theme.Colors.OnSurface,ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
            }
            Assert.Equal(text,ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        });
    }
    [Fact]
    public void SearchClearRestoresTypingFocus()
    {
        using var context=new NativeTestContext();
        var query="ore";var state=new MaterialSearchState();Vector2 clear=default;
        void Draw()=>Window(()=>
        {
            MaterialTextField.SearchField("search",ref query,width:300,state:state);
            clear=new(ImGui.GetItemRectMax().X-14,(ImGui.GetItemRectMin().Y+ImGui.GetItemRectMax().Y)*.5f);
        });
        context.Frame(Draw);context.Frame(Draw);
        context.Click(clear,Draw);
        Assert.Empty(query);
        context.Frame(Draw);context.Frame(Draw);
        ImGui.GetIO().AddInputCharacter('x');context.Frame(Draw);
        Assert.Equal("x",query);
    }
    [Fact]
    public void IntegerClampsAndDisabledEditorDoesNotChange()
    {
        using var context=new NativeTestContext();
        var count=3;var disabled=false;Vector2 plus=default;
        void Draw()=>Window(()=>
        {
            MaterialInput.Integer("quantity",ref count,1,4,width:200,disabled:disabled);
            plus=new(ImGui.GetItemRectMax().X-8,(ImGui.GetItemRectMin().Y+ImGui.GetItemRectMax().Y)*.5f);
        });
        context.Frame(Draw);context.Frame(Draw);
        context.Click(plus,Draw);Assert.Equal(4,count);
        context.Click(plus,Draw);Assert.Equal(4,count);
        count=2;disabled=true;context.Click(plus,Draw);Assert.Equal(2,count);
    }
    [Fact]
    public void NativeClipperSubmitsVisibleRowsAndRequestedSelectionOnly()
    {
        using var context=new NativeTestContext();
        var submitted=0;var requested=false;
        void Draw()=>Window(()=>
        {
            using var surface=MaterialSurface.Card("rows",new(300,200));
            if(!surface.Visible)return;
            submitted=0;requested=false;
            using var clipper=new MaterialClipper(10_000,ImGui.GetTextLineHeightWithSpacing());
            clipper.Include(9000);
            while(clipper.Step())
                for(var i=clipper.Start;i<clipper.End;i++)
                { submitted++;requested|=i==9000;ImGui.TextUnformatted("Uniform row"); }
        });
        context.Frame(Draw);context.Frame(Draw);
        Assert.InRange(submitted,1,40);Assert.True(requested);
    }
    [Fact]
    public void ExtendedWidgetsSupportNestedScopesAndReadOnlyText()
    {
        using var context=new NativeTestContext();
        var text="Keep this text";var number=1f;ulong flags=1;bool? inherited=null;
        var options=new MaterialOptions<int>([new("a",0,"First"),new("b",1,"Second")]);var selected=0;
        void Draw()=>Window(()=>
        {
            MaterialCombo.Draw("combo",options,ref selected);
            ImGui.SetNextItemOpen(true);
            using(var tree=MaterialTree.Node("tree","Folder"))
                if(tree.Visible)
                {
                    MaterialInput.Multiline("notes",ref text,new(300,80),flags:ImGuiInputTextFlags.ReadOnly);
                    MaterialInput.Number("number",ref number,0,10,width:200);
                    MaterialInput.Slider("slider",ref number,0,10,width:200);
                    MaterialOverride.Switch("override",ref inherited,300);
                    MaterialFlags.Draw("flags",[new("a","One",1),new("b","Two",2)],ref flags);
                    using var menu=MaterialContextMenu.Begin("context");
                    if(menu.Visible) ImGui.TextUnformatted("Menu");
                }
        });
        context.Frame(Draw);context.Frame(Draw);
        Assert.Equal("Keep this text",text);
        Assert.Null(inherited);
    }
    [Fact]
    public void DragDropCopiesTypedPayloadAndDeliversOnlyOnRelease()
    {
        using var context=new NativeTestContext();
        Vector2 source=default,target=default;var result=-1;
        void Draw()=>Window(()=>
        {
            ImGui.Button("Source",new(120,30));
            source=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())*.5f;
            var key=42;MaterialDragDrop.Source("CR_TEST",in key,"Moving");
            ImGui.Button("Target",new(120,30));
            target=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())*.5f;
            if(MaterialDragDrop.TryAccept<int>("CR_TEST",out var received)) result=received;
        });
        context.Frame(Draw);context.Frame(Draw);
        ImGui.GetIO().AddMousePosEvent(source.X,source.Y);context.Frame(Draw);
        ImGui.GetIO().AddMouseButtonEvent(0,true);context.Frame(Draw);
        ImGui.GetIO().AddMousePosEvent(source.X+15,source.Y);context.Frame(Draw);
        ImGui.GetIO().AddMousePosEvent(target.X,target.Y);context.Frame(Draw);context.Frame(Draw);
        Assert.Equal(-1,result);
        ImGui.GetIO().AddMouseButtonEvent(0,false);context.Frame(Draw);context.Frame(Draw);
        Assert.Equal(42,result);
    }
}
