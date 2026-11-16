using Dalamud.Interface.ManagedFontAtlas;

namespace AethertekUI.Dalamud;

public enum MaterialFontWeight { Regular = 400, Semibold = 600, Bold = 700 }

/// <summary>Shared bundled text faces and native merges; callers own handles and readiness checks.</summary>
public static class MaterialFontAtlas
{
    public static IFontHandle CreateFont(IFontAtlas atlas, MaterialFontWeight weight,
        float sizePx, ushort[] glyphRanges, string language)
    {
        var assetRoot = Path.GetDirectoryName(typeof(MaterialFontAtlas).Assembly.Location);
        if (string.IsNullOrEmpty(assetRoot))
            throw new InvalidOperationException("Supply the host plugin asset directory when the font adapter is loaded from memory.");
        return CreateFont(atlas, weight, sizePx, glyphRanges, language, assetRoot);
    }

    public static IFontHandle CreateFont(IFontAtlas atlas, MaterialFontWeight weight,
        float sizePx, ushort[] glyphRanges, string language, string assetRoot)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(glyphRanges);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        if (!Path.IsPathFullyQualified(assetRoot))
            throw new ArgumentException("The plugin asset directory must be absolute.", nameof(assetRoot));
        if (!float.IsFinite(sizePx) || sizePx <= 0) throw new ArgumentOutOfRangeException(nameof(sizePx));
        var file = weight switch
        {
            MaterialFontWeight.Regular => "selawk.ttf",
            MaterialFontWeight.Semibold => "selawksb.ttf",
            MaterialFontWeight.Bold => "selawkb.ttf",
            _ => throw new ArgumentOutOfRangeException(nameof(weight)),
        };
        var path = Path.Combine(assetRoot, "Fonts", "Selawik", file);
        return atlas.NewDelegateFontHandle(toolkit => toolkit.OnPreBuild(build =>
        {
            build.NewImAtlas.TexDesiredWidth = 4096;
            build.NewImAtlas.TexDesiredHeight = 4096;
            build.Font = build.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx, GlyphRanges = glyphRanges });
            build.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkRegular, new SafeFontConfig
            {
                SizePx = sizePx, MergeFont = build.Font, GlyphRanges = glyphRanges,
                FontNo = language switch { "ko" => 1, "zh-Hans" or "zh-CN" => 2, "zh-Hant" or "zh-TW" => 3, _ => 0 },
            });
            build.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig { SizePx = sizePx, MergeFont = build.Font });
            build.AddGameSymbol(new SafeFontConfig { SizePx = sizePx, MergeFont = build.Font });
        }).OnPostBuild(CompleteSpacing));
    }

    private static unsafe void CompleteSpacing(IFontAtlasBuildToolkitPostBuild build)
    {
        var font = build.Font;
        if (font.FindGlyphNoFallback('\u202F') != null) return;
        // U+202F is invisible narrow nonbreaking spacing, absent from Selawik 1.01 and native CJK.
        // Keep the original text/codepoint; supply its blank, one-fifth-em advance in every role/rebuild.
        font.AddGlyph(null, '\u202F', 0, 0, 0, 0, 0, 0, 0, 0, 0, font.FontSize / 5);
        build.BuildLookupTable(font);
    }
}
