using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AethertekUI.Demo;

internal sealed partial class Showcase
{
    private bool notifications=true,dependency,dependentSetting,reduceMotion;
    private int behavior,route,delay=250;
    private string preset="Evening route";
    private bool appearanceCompact=true,showCompact,showTransparency,showLanguage=true,appearanceTransparency=true,appearanceFade=true;
    private int appearanceOpacity=100,appearanceFadedOpacity=50;
    private int appearanceHighlightContrast=0;
    private float appearanceFadeDelay=10;
    private string appearanceLanguage="en";
    private static readonly string[] Routes=["Nearest node","Lowest cost","Preferred location"];
    private void RebuildTheme()
    {
        theme = new(seed, colorChoices, (MaterialDensity)density)
            { SurfaceOpacity = opacity, HighlightContrastPercent = appearanceHighlightContrast };
        theme.Motion.Enabled = !reduceMotion;
        appliedColorChoices = colorChoices;
    }

    private void DrawColourEditor(string id)
    {
        ImGui.PushID(id);
        try
        {
            MaterialText.Text("Highlight contrast");
            ImGui.SetNextItemWidth(Math.Max(120 * MaterialTheme.Metrics.Scale, ImGui.GetContentRegionAvail().X));
            ImGui.SliderInt("##highlight-contrast", ref appearanceHighlightContrast, 0, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp);
            ImGui.TextWrapped("Makes controls and selection states more distinct. Applies to all themed windows in this app.");
        }
        finally { ImGui.PopID(); }
        if (MaterialAppearanceSelector.DrawColors(id, ref colorChoices, ref seed,
            new("Colour", "Language", "Teal", "Blue", "Pink", "Custom RGB"),
            new("Auto", "Picker", "DEFAULT", "Background, controls and text follow your theme.", "Theme colour",
                "Choose each colour independently.", "Background", "Controls", "Text", "Aa  Preview text", "Control",
                "Switch to Picker to set each colour yourself.", "Switch back to Auto to use the automatic theme.")
                {
                    Lcars = "LCARS", LcarsHelp = "Choose the LCARS background and three panel bar colours.",
                    Glassmorphism = "Glassmorphism", Spatial = "Spatial", Cyberpunk = "Cyberpunk",
                    LcarsBar1 = "Bar colour 1", LcarsBar2 = "Bar colour 2", LcarsBar3 = "Bar colour 3",
                    Reset = "Reset", ResetColors = "Reset colours", Animation = "Animation", AnimationSpeed = "Movement speed (x)",
                    WaveColors = "Wave colours", GlassWave1 = "Wave colour 1", GlassWave2 = "Wave colour 2", GlassWave3 = "Wave colour 3",
                    ResetWaveColors = "Reset wave colours",
                    BackgroundMode = "Background mode", Waves = "Waves", Lights = "Lights", Image = "Image",
                    LightEffect = "Light effect", AmbientGlows = "Ambient glows", SweepingBeams = "Sweeping beams", PointerGlow = "Mouse-following glow",
                    ImageFile = "Image file", BrowseImage = "Browse...", LoadImage = "Load image",
                    ImageHelp = "The image fills the window. Blur (%) applies to the image.",
                    LightColors = "Light colours", GlassLight1 = "Light colour 1", GlassLight2 = "Light colour 2", GlassLight3 = "Light colour 3",
                    ResetLightColors = "Reset light colours", BackgroundBlur = "Background blur", BlurPercent = "Blur (%)",
                }, DefaultColorChoices, DefaultSeed, () => BrowseBackgroundImage?.Invoke())) RebuildTheme();
        DrawScreenshotPause();
        if (BackgroundRenderer?.ImageProblem is { } problem) MaterialText.Text(problem);
    }

    private void DrawScreenshotPause()
    {
        if (colorChoices.Mode == MaterialColorMode.Glassmorphism && colorChoices.GlassBackground == MaterialGlassBackground.Image)
        {
            MaterialText.Text("Image backgrounds remain visible to screenshots.");
            return;
        }
        var paused = BackgroundRenderer?.ScreenshotsPaused == true;
        ImGui.BeginDisabled(!paused && BackgroundRenderer?.CanPauseForScreenshots != true);
        try
        {
            if (ImGui.Checkbox("Pause backdrop for screenshots", ref paused))
                BackgroundRenderer?.TrySetScreenshotsPaused(paused);
        }
        finally { ImGui.EndDisabled(); }
    }

    private void Settings()
    {
        if (theme.IsLcars) MaterialLcars.Header("AethertekUI · LCARS");
        else if (theme.IsGlassmorphism || theme.IsSpatial || theme.IsCyberpunk)
            MaterialSkin.Heading("AethertekUI · " + colorChoices.Mode);
        MaterialText.Text("Window appearance");
        MaterialAppearanceTab.Draw("groupedAppearance",new("THEME & LANGUAGE","MAIN WINDOW CONTROLS","WINDOW OPACITY","WHEN UNFOCUSED"),
            360*MaterialTheme.Metrics.Scale,() =>
        {
            DrawColourEditor("appearance");
            MaterialText.Text("Language");ImGui.SameLine();
            MaterialAppearanceSelector.DrawLanguage("appearance",ref appearanceLanguage,sizingLanguages);
            ImGui.Checkbox("Compact mode",ref appearanceCompact);
            if(MaterialSwitch.Draw("motion","Reduce motion",ref reduceMotion)) theme.Motion.Enabled=!reduceMotion;
        },() =>
        {
            ImGui.Checkbox("Compact visible on main window",ref showCompact);
            ImGui.Checkbox("Transparency visible on main window",ref showTransparency);
            ImGui.Checkbox("Language visible on main window",ref showLanguage);
        },() =>
        {
            ImGui.Checkbox("Transparency",ref appearanceTransparency);
            ImGui.BeginDisabled(!appearanceTransparency);
            try { MaterialText.Text("Opacity");ImGui.SameLine();ImGui.SetNextItemWidth(150*MaterialTheme.Metrics.Scale);ImGui.SliderInt("##appearanceOpacity",ref appearanceOpacity,10,100,"%d%%"); }
            finally { ImGui.EndDisabled(); }
        },() =>
        {
            ImGui.Checkbox("Auto-fade when unfocused",ref appearanceFade);
            ImGui.BeginDisabled(!appearanceTransparency);
            try
            {
                MaterialText.Text("Unfocused opacity");ImGui.SameLine();ImGui.SetNextItemWidth(150*MaterialTheme.Metrics.Scale);ImGui.SliderInt("##appearanceFaded",ref appearanceFadedOpacity,10,100,"%d%%");
                ImGui.BeginDisabled(!appearanceFade);
                try { MaterialText.Text("Unfocused delay");ImGui.SameLine();ImGui.SetNextItemWidth(90*MaterialTheme.Metrics.Scale);ImGui.InputFloat("##appearanceDelay",ref appearanceFadeDelay); }
                finally { ImGui.EndDisabled(); }
            }
            finally { ImGui.EndDisabled(); }
        });
        using(var automation=MaterialSurface.Card("automationSettings",new(-1,230)))
        {
            if(automation.Visible)
            {
                ImGui.TextUnformatted("Automation preferences");
                MaterialSwitch.Draw("notify","Notify when complete",ref notifications);
                MaterialSegmented.Draw<int>("behavior",Modes,ref behavior);
                ImGui.SetNextItemWidth(230*MaterialTheme.Metrics.Scale);
                if(ImGui.BeginCombo("Routing",Routes[route]))
                {
                    for(var i=0;i<Routes.Length;i++) if(ImGui.Selectable(Routes[i],route==i)) route=i;
                    ImGui.EndCombo();
                }
                ImGui.SetNextItemWidth(230*MaterialTheme.Metrics.Scale);
                ImGui.SliderInt("Delay (ms)",ref delay,0,1000);
                MaterialTextField.TextField("preset",ref preset,"Preset name",leading:MaterialIcon.Edit);
            }
        }
        using(var integrations=MaterialSurface.Card("integrations",new(-1,165)))
        {
            if(integrations.Visible)
            {
                ImGui.TextUnformatted("Optional integration / mock");
                MaterialSwitch.Draw("dependency","Dependency available",ref dependency);
                using var nested=MaterialSurface.Begin("nested",new(-1,68),MaterialElevation.High,padding:8,radius:12);
                if(nested.Visible)
                {
                    MaterialSwitch.Draw("dependent","Use integration",ref dependentSetting,!dependency);
                    ImGui.TextDisabled(dependency?"The mock dependency is available.":"Enable the mock dependency to change this setting.");
                }
            }
        }
    }
}
