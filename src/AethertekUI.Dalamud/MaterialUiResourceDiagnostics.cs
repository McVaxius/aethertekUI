using System.Diagnostics;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;

namespace AethertekUI.Dalamud;

/// <summary>Opt-in resource samples for consumers that own an isolated atlas and UI resources.</summary>
public static class MaterialUiResourceDiagnostics
{
    public static unsafe string DescribeAtlas(IFontAtlasBuildToolkit toolkit, ITextureProvider textures, int fontHandles)
    {
        var atlas = toolkit.NewImAtlas;
        int alphaPages = 0, rgbaPages = 0, sharedPages = 0;
        for (var index = 0; index < atlas.Textures.Size; index++)
        {
            var page = atlas.Textures[index];
            if (!page.TexID.IsNull) sharedPages++;
            else if (page.TexPixelsRGBA32 != null) rgbaPages++;
            else if (page.TexPixelsAlpha8 != null) alphaPages++;
        }

        // Match the managed host upload format for new pages. Shared pages are excluded from the estimate.
        var alphaBytes = alphaPages == 0 ? 0 : textures.IsDxgiFormatSupported(115) ? 2 : 4;
        var pixels = (long)atlas.TexWidth * atlas.TexHeight;
        var textureBytes = pixels * (rgbaPages * 4L + alphaPages * alphaBytes);
        var rasterBytes = pixels * (rgbaPages * 4L + alphaPages);
        var alphaFormat = alphaBytes == 2 ? "B4G4R4A4" : "BGRA8";
        return FormattableString.Invariant(
            $"fontHandles={fontHandles} atlas={atlas.TexWidth}x{atlas.TexHeight} pages={atlas.Textures.Size} alphaPages={alphaPages} alphaUpload={alphaFormat} rgbaPages={rgbaPages} sharedPages={sharedPages} rasterBytes={rasterBytes} estimatedFontTextureBytes={textureBytes}");
    }

    public static string ProcessMemory()
    {
        using var process = Process.GetCurrentProcess();
        return FormattableString.Invariant(
            $"managedBytes={GC.GetTotalMemory(false)} privateBytes={process.PrivateMemorySize64} workingSetBytes={process.WorkingSet64}");
    }
}
