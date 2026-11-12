using System.Numerics;
using AethertekUI.Dalamud;
using Dalamud.Bindings.ImGui;

namespace AethertekUI.Demo;

internal sealed partial class Showcase
{
    public bool IsOpen;
    public int Page;
    internal ImTextureID BorrowedImage;
    internal Vector2 BorrowedImageSize;
    internal MaterialDensity DemoDensity { set { density=(int)value; theme.Density=value; } }
    internal MaterialColorSettings DemoColors { set => colorChoices = value; }
    internal MaterialGlassRenderer? BackgroundRenderer { get; set; }
    internal Action? BrowseBackgroundImage { get; set; }
    internal MaterialColorSettings BackgroundSettings => colorChoices;
    internal bool HasOpenWindows => IsOpen || overlayOpen || windowMotionOpen || HindiPreviewOpen || appearanceWindow.IsOpen;
    internal void SelectBackgroundImage(string path) => colorChoices = colorChoices with { GlassImagePath = path };
    private static readonly Vector3 DefaultSeed = new(.36f,.72f,.46f);
    private static readonly MaterialColorSettings DefaultColorChoices = new();
    private Vector3 seed = DefaultSeed;
    private MaterialTheme theme = new(DefaultSeed);
    private MaterialColorSettings colorChoices = DefaultColorChoices;
    private MaterialColorSettings? appliedColorChoices;
    private float opacity=.94f;
    private int density;
    private bool expanded=true;
    private bool windowMotionOpen;
    private readonly MaterialWindowFold windowFold = new();
    private readonly MaterialWindowDecorations windowDecorations = new();
    private readonly MaterialWindowOpacity windowOpacity = new();
    private bool windowOpacityEnabled = true;
    private bool windowAutoFade = true;
    private float windowOpacityPercent = 100;
    private float windowFadePercent = 50;
    private float windowFadeDelay = 10;
    private readonly MaterialWindowFold showcaseFold = new();
    private readonly MaterialWindowDecorations showcaseDecorations = new();
    private readonly Action<ImGuiWindowPtr> prepareWindowDecorations;
    private readonly Action<ImGuiWindowPtr> prepareShowcaseDecorations;

    public Showcase()
    {
        prepareWindowDecorations = windowDecorations.Prepare;
        prepareShowcaseDecorations = showcaseDecorations.Prepare;
        appearanceWindow = new(this);
        appearanceWindows.AddWindow(appearanceWindow);
    }
    private string notice="Ready. All actions use mock state.";
    private static readonly string[] Pages=["Gallery","Automation","Gathering data","Dashboard","Settings","List editor","Large catalog","Sizing lab"];
    private static readonly MaterialIcon[] PageIcons=[MaterialIcon.Grid,MaterialIcon.Play,MaterialIcon.Table,MaterialIcon.Home,MaterialIcon.Settings,MaterialIcon.Edit,MaterialIcon.Table,MaterialIcon.Sliders];
    private static readonly MaterialHeaderAction[] HeaderActions=[new("density",MaterialIcon.Sliders,"Toggle expanded navigation"),new("close",MaterialIcon.Close,"Close showcase")];
    private static readonly MaterialSegment<int>[] DensityOptions=[new(0,"Compact"),new(1,"Standard")];
    private static readonly MaterialSegment<int>[] Modes=[new(0,"Auto"),new(1,"Manual"),new(2,"Off")];
    private static readonly MaterialSegment<int>[] FourOptions=[new(0,"All"),new(1,"Mining"),new(2,"Botany"),new(3,"Fishing")];
    private static readonly MaterialSegment<int>[] TwoOptions=[new(0,"Local"),new(1,"Shared")];
    private static readonly MaterialSegment<int>[] DisabledOptions=[new(0,"Ready"),new(1,"Unavailable",Disabled:true),new(2,"Off")];

    public void Draw(float globalScale = 1)
    {
        if(!HasOpenWindows) return;
        if (colorChoices != appliedColorChoices) RebuildTheme();
        theme.HighlightContrastPercent = appearanceHighlightContrast;
        using var glass = MaterialSkin.PushBackgroundRenderer(BackgroundRenderer,
            colorChoices.HasBackgroundBlur(colorChoices.Mode), colorChoices.BackgroundBlurPercent / 100f);
        {
        // The theme scope wraps Begin/End, so it also styles the outer window.
        using var scope=MaterialTheme.Push(theme,globalScale,MaterialStyleMode.Material);
        using var chrome=MaterialWindowChrome.Push();
        if(IsOpen)
        {
            var scale=MaterialTheme.Metrics.Scale;
            ImGui.SetNextWindowSize(new Vector2(1000,760)*scale,ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(780,500)*scale,new(float.MaxValue));
            showcaseFold.PreDraw("AethertekUI Showcase###crmaterial", null, null, true, prepareShowcaseDecorations);
            bool visible;
            using (MaterialSkin.BackdropStyle())
                visible = ImGui.Begin("AethertekUI Showcase###crmaterial",ref IsOpen,ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoSavedSettings);
            if(visible)
            {
                MaterialSkin.Backdrop();
                showcaseDecorations.Paint();
                var action=MaterialWindowHeader.Draw("AethertekUI Showcase","Compact surfaces. Native behavior. Mock data.",HeaderActions);
                if(action==0) expanded=!expanded;
                if(action==1) IsOpen=false;
                MaterialStatus.SectionHeader("SHOWCASE");
                using(var rail=MaterialNavigation.Rail("navigation",expanded))
                {
                    if(rail.Visible)
                    {
                        for(var i=0;i<Pages.Length;i++)
                        {
                            ImGui.PushID(i);
                            if(MaterialNavigation.Item("page",Pages[i],PageIcons[i],Page==i,expanded)) Page=i;
                            if(!expanded&&ImGui.IsItemHovered()) ImGui.SetTooltip(Pages[i]);
                            ImGui.PopID();
                        }
                        if(expanded)
                        {
                            ImGui.Spacing();
                            MaterialStatus.Badge("API 15 / .NET 10");
                            ImGui.Spacing();
                            ImGui.TextWrapped("Independent library. No game automation.");
                        }
                    }
                }
                ImGui.SameLine();
                using(var page=MaterialSurface.Begin("page",default,MaterialElevation.Lowest,padding:12))
                {
                    if(page.Visible)
                    {
                        switch(Page)
                        {
                            case 0: Gallery(); break;
                            case 1: AutomationPage(); break;
                            case 2: DataPage(); break;
                            case 3: Dashboard(); break;
                            case 4: Settings(); break;
                            case 5: Editors(); break;
                            case 6: LargeData(); break;
                            case 7: SizingLab(); break;
                        }
                    }
                }
            }
            ImGui.End();
            showcaseDecorations.Paint();
            showcaseFold.PostDraw();
        }
        if(overlayOpen) DrawOverlay();
        if(windowMotionOpen) DrawWindowMotion();
        if(HindiPreviewOpen) DrawHindiPreview();
        }
        if (appearanceWindow.IsOpen)
        {
            using var previewTheme = MaterialTheme.Push(theme, globalScale,
                appearanceStyling ? MaterialStyleMode.Material : MaterialStyleMode.Native);
            using var previewChrome = MaterialWindowChrome.Push();
            using var previewGlass = MaterialSkin.PushBackgroundRenderer(appearanceStyling ? BackgroundRenderer : null,
                appearanceStyling && colorChoices.HasBackgroundBlur(colorChoices.Mode), colorChoices.BackgroundBlurPercent / 100f);
            using var backdrop = MaterialSkin.BackdropStyle();
            appearanceWindows.Draw();
        }
    }
    private void DrawWindowMotion()
    {
        const string name="Window motion preview###aethertek-window-motion";
        var scale=MaterialTheme.Metrics.Scale;
        ImGui.SetNextWindowSize(new Vector2(420,260)*scale,ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(320,180)*scale,new Vector2(700,500)*scale);
        windowFold.PreDraw(name, null, null, false, prepareWindowDecorations);
        bool visible;
        using (MaterialSkin.BackdropStyle()) visible = ImGui.Begin(name,ref windowMotionOpen);
        if(visible)
        {
            if (BackgroundRenderer != null)
                BackgroundRenderer.Opacity = windowOpacityEnabled
                    ? ImGui.IsWindowAppearing() ? Math.Clamp(windowOpacityPercent, 10, 100) / 100f : windowOpacity.CurrentOpacity
                    : 1;
            MaterialSkin.Backdrop();
            windowDecorations.Paint();
            var imageOrigin = ImGui.GetCursorScreenPos();
            MaterialCanvas.DrawImage(ImGui.GetWindowDrawList(), BorrowedImage, BorrowedImageSize,
                imageOrigin, imageOrigin + new Vector2(36 * scale));
            ImGui.Dummy(new Vector2(36 * scale));
            ImGui.TextWrapped("Use the title arrow or double-click the title to minimize and restore this window.");
            ImGui.TextWrapped("Resize it, then minimize and restore again. The expanded size and native controls are retained.");
            var motion=theme.Motion.Enabled;
            if(ImGui.Checkbox("Animate minimize",ref motion))theme.Motion.Enabled=motion;
            ImGui.Checkbox("Window opacity",ref windowOpacityEnabled);
            ImGui.SliderFloat("Normal opacity",ref windowOpacityPercent,10,100,"%.0f%%");
            ImGui.Checkbox("Fade without focus",ref windowAutoFade);
            ImGui.SliderFloat("Unfocused opacity",ref windowFadePercent,10,100,"%.0f%%");
            ImGui.SliderFloat("Unfocused delay",ref windowFadeDelay,0,60,"%.0f seconds");
        }
        ImGui.End();
        windowDecorations.Paint();
        windowFold.PostDraw();
        MaterialWindowHeader.PaintTitle(ImGuiP.FindWindowByName(name), "Window motion preview",
            BorrowedImage, BorrowedImageSize);
        windowOpacity.Apply(name,windowOpacityPercent/100,windowOpacityEnabled,windowAutoFade,
            windowFadePercent/100,windowFadeDelay);
    }
    private void Appearance()
    {
        ImGui.SetNextItemWidth(180*MaterialTheme.Metrics.Scale);
        if(ImGui.ColorEdit3("Accent",ref seed,ImGuiColorEditFlags.NoInputs))
        {
            var motion=theme.Motion.Enabled;
            RebuildTheme();
            theme.Motion.Enabled=motion;
            notice="Accent palette regenerated.";
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200*MaterialTheme.Metrics.Scale);
        if(ImGui.SliderFloat("Opacity",ref opacity,.4f,1,"%.2f")) theme.SurfaceOpacity=opacity;
        if(MaterialSegmented.Draw<int>("density",DensityOptions,ref density,300)) theme.Density=(MaterialDensity)density;
    }
}
