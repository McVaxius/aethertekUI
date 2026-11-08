using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace AethertekUI;

/// <summary>A host-created texture. The renderer owns and disposes its supplied resource.</summary>
public sealed class MaterialTextTexture : IDisposable
{
    private IDisposable? owner;
    public ImTextureID Id { get; }
    public MaterialTextTexture(ImTextureID id, IDisposable owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (id.Handle == 0) throw new ArgumentException("A text texture requires a valid handle.", nameof(id));
        Id = id;
        this.owner = owner;
    }
    public void Dispose() => Interlocked.Exchange(ref owner, null)?.Dispose();
}

/// <summary>Owns Windows-shaped layouts and host textures; enter a scope around the owning window tree.</summary>
public sealed unsafe class MaterialTextRenderer : IDisposable
{
    [ThreadStatic] private static Scope? currentScope;
    private readonly Func<MaterialTextBitmap, MaterialTextTexture> upload;
    private readonly Dictionary<Key, Entry> entries = [];
    private nint context;
    private int activeScopes;
    private bool disposed;
    public string FontFamily { get; }
    public string Locale { get; }
    internal static MaterialTextRenderer? Current => currentScope?.Renderer;

    public MaterialTextRenderer(Func<MaterialTextBitmap, MaterialTextTexture> upload,
        string fontFamily = "Nirmala UI", string locale = "hi-IN")
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        this.upload = upload;
        FontFamily = fontFamily;
        Locale = locale;
    }

    public Scope Push()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var nativeContext = (nint)ImGui.GetCurrentContext().Handle;
        if (nativeContext == 0) throw new InvalidOperationException("Enter a text scope in its ImGui context.");
        if (context != 0 && context != nativeContext)
            throw new InvalidOperationException("A text renderer belongs to one ImGui context.");
        context = nativeContext;
        var result = new Scope(this, currentScope, nativeContext);
        currentScope = result;
        activeScopes++;
        return result;
    }

    /// <summary>Borrowed renderer-owned layout. Do not dispose it or retain it beyond the current frame.</summary>
    public MaterialTextLayout GetLayout(string text, float fontSize, float wrapWidth = 0)
        => GetEntry(text, fontSize, wrapWidth).Layout;

    /// <summary>Validates authored Devanagari separately from a consumer's retained native atlas checks.</summary>
    public void CheckGlyphs(IEnumerable<string> text, float fontSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (var value in text.Where(MaterialText.RequiresShaping).Distinct())
        {
            using var layout = new MaterialTextLayout(value, fontSize, fontFamily: FontFamily, locale: Locale);
            layout.CheckGlyphs();
        }
    }

    /// <summary>Probes optional shaped text without uploading textures or caching availability.</summary>
    public bool TryCheckGlyphs(IEnumerable<string> text, float fontSize, out string error)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            CheckGlyphs(text, fontSize);
            error = string.Empty;
            return true;
        }
        catch (Exception failure) when (failure is InvalidOperationException
            or System.Runtime.InteropServices.ExternalException or DllNotFoundException
            or EntryPointNotFoundException or BadImageFormatException)
        {
            error = failure.Message;
            return false;
        }
    }

    internal void Draw(ImDrawListPtr drawList, Vector2 position, uint color, string text, float fontSize, float wrapWidth = 0)
    {
        if (text.Length == 0 || (color >> 24) == 0) return;
        var entry = GetEntry(text, fontSize, wrapWidth);
        if (entry.Texture is null)
        {
            var bitmap = entry.Layout.Rasterize();
            if (bitmap.Width == 0 || bitmap.Height == 0) return;
            entry.Texture = upload(bitmap) ?? throw new InvalidOperationException("The text uploader returned no texture.");
            entry.Offset = bitmap.Offset;
            entry.RasterSize = new(bitmap.Width, bitmap.Height);
        }
        var start = position + entry.Offset;
        drawList.AddImage(entry.Texture.Id, start, start + entry.RasterSize,
            Vector2.Zero, Vector2.One, color);
    }

    private Entry GetEntry(string text, float fontSize, float wrapWidth)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        if (!float.IsFinite(fontSize) || fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (!float.IsFinite(wrapWidth) || wrapWidth < 0) throw new ArgumentOutOfRangeException(nameof(wrapWidth));
        var nativeContext = ImGui.GetCurrentContext();
        if (context != 0 && (nint)nativeContext.Handle != context)
            throw new InvalidOperationException("Use text resources in their original ImGui context.");
        var frame = nativeContext.Handle == null ? 0 : ImGui.GetFrameCount();
        var key = new Key(text, fontSize, wrapWidth);
        if (!entries.TryGetValue(key, out var entry))
        {
            // Never dispose textures already referenced by this frame's draw lists.
            while (entries.Count >= 256)
            {
                // Oldest entry not used this frame; ties keep the first in enumeration order.
                Key oldestKey = default;
                Entry? oldest = null;
                foreach (var (candidateKey, candidate) in entries)
                    if (candidate.LastFrame != frame && (oldest is null || candidate.LastFrame < oldest.LastFrame))
                    { oldestKey = candidateKey; oldest = candidate; }
                if (oldest is null) break;
                entries.Remove(oldestKey);
                oldest.Dispose();
            }
            entry = new(new MaterialTextLayout(text, fontSize,
                wrapWidth == 0 ? float.PositiveInfinity : wrapWidth, FontFamily, Locale));
            entries.Add(key, entry);
        }
        entry.LastFrame = frame;
        return entry;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (activeScopes != 0) throw new InvalidOperationException("Clear text resources outside drawing scopes.");
        var nativeContext = ImGui.GetCurrentContext();
        if (nativeContext.Handle != null && context != 0)
        {
            if ((nint)nativeContext.Handle != context)
                throw new InvalidOperationException("Release text resources in their original ImGui context.");
            if (nativeContext.WithinFrameScope || (nativeContext.FrameCountRendered != nativeContext.FrameCount
                && entries.Values.Any(entry => entry.Texture is not null && entry.LastFrame == nativeContext.FrameCount)))
                throw new InvalidOperationException("Release text textures after native frame submission.");
        }
        foreach (var entry in entries.Values) entry.Dispose();
        entries.Clear();
    }
    public void Dispose()
    {
        if (disposed) return;
        if (activeScopes != 0) throw new InvalidOperationException("Dispose the text renderer after its drawing scopes.");
        Clear();
        disposed = true;
    }

    private readonly struct Key(string text, float size, float width) : IEquatable<Key>
    {
        private readonly string text = text;
        private readonly float size = size, width = width;
        public bool Equals(Key other) => size.Equals(other.size) && width.Equals(other.width)
            && string.Equals(text, other.text, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(string.GetHashCode(text, StringComparison.Ordinal), size, width);
    }
    private sealed class Entry(MaterialTextLayout layout) : IDisposable
    {
        public MaterialTextLayout Layout { get; } = layout;
        public Vector2 Offset;
        public Vector2 RasterSize;
        public MaterialTextTexture? Texture;
        public int LastFrame;
        public void Dispose() { Texture?.Dispose(); Layout.Dispose(); }
    }

    public sealed class Scope : IDisposable
    {
        private MaterialTextRenderer? owner;
        private readonly Scope? previous;
        private readonly nint nativeContext;
        internal MaterialTextRenderer? Renderer => owner;
        internal Scope(MaterialTextRenderer owner, Scope? previous, nint nativeContext)
        { this.owner = owner; this.previous = previous; this.nativeContext = nativeContext; }
        public void Dispose()
        {
            if (owner is null) return;
            if (currentScope != this || (nint)ImGui.GetCurrentContext().Handle != nativeContext)
                throw new InvalidOperationException("Text scopes must be disposed in their original context and order.");
            currentScope = previous;
            owner.activeScopes--;
            owner = null;
        }
    }
}

/// <summary>Native text for existing scripts; Windows shaping for Devanagari under an owning text scope.</summary>
public static partial class MaterialText
{
    public static string NativeGlyphText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!RequiresShaping(text)) return text;
        var result = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
            if (!IsShapedCodePoint(rune.Value)) result.Append(rune.ToString());
        return result.ToString();
    }

    public static bool RequiresShaping(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return RequiresShaping(text.AsSpan());
    }

    /// <summary>Same result as enumerating runes: unpaired surrogates never count as shaped.</summary>
    internal static bool RequiresShaping(ReadOnlySpan<char> text)
    {
        // Every shaped code point is at or above U+0900; most UI text is entirely below it.
        var start = text.IndexOfAnyInRange('ऀ', '￿');
        if (start < 0) return false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c < 0x0900) continue;
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    if (IsShapedCodePoint(char.ConvertToUtf32(c, text[i + 1]))) return true;
                    i++;
                }
                continue;
            }
            if (IsShapedCodePoint(c)) return true;
        }
        return false;
    }

    private static bool IsShapedCodePoint(int value) => value is >= 0x0900 and <= 0x097f or >= 0x1cd0 and <= 0x1cff
        or >= 0xa8e0 and <= 0xa8ff or >= 0x11b00 and <= 0x11b5f;

    public static Vector2 Measure(string text, bool hideTextAfterDoubleHash = false, float wrapWidth = 0)
    {
        if (MaterialTextRenderer.Current is { } renderer)
        {
            var visible = hideTextAfterDoubleHash ? MaterialNativeCaption.VisibleSpan(text) : text.AsSpan();
            if (RequiresShaping(visible))
            {
                var shaped = visible.Length == text.Length ? text : MaterialNativeCaption.Visible(text);
                return renderer.GetLayout(shaped, ImGui.GetFontSize(), Math.Max(0, wrapWidth)).Size;
            }
        }
        // Native measuring hides text after "##" itself.
        return ImGui.CalcTextSize(text, hideTextAfterDoubleHash, wrapWidth);
    }

    public static void AddText(ImDrawListPtr drawList, Vector2 position, uint color, string text, float wrapWidth = 0)
    {
        if (MaterialTextRenderer.Current is { } renderer && RequiresShaping(text))
            renderer.Draw(drawList, position, color, text, ImGui.GetFontSize(), Math.Max(0, wrapWidth));
        else drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), position, color, text, wrapWidth);
    }

    public static void AddText(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position,
        uint color, string text, float wrapWidth = 0)
    {
        if (MaterialTextRenderer.Current is { } renderer && RequiresShaping(text))
            renderer.Draw(drawList, position, color, text, fontSize, Math.Max(0, wrapWidth));
        else drawList.AddText(font, fontSize, position, color, text, wrapWidth);
    }

    /// <summary>Draw one informational line within the available width, with a grapheme-safe ellipsis.</summary>
    public static void SingleLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var line = SingleLineCache.Get(text, width);
        ImGui.PushTextWrapPos(-1);
        try { Text(line); }
        finally { ImGui.PopTextWrapPos(); }
    }

    /// <summary>The visible line for SingleLine. Depends only on text, width, font and the shaping renderer.</summary>
    private static string FitSingleLine(string text, float width)
    {
        var line = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (Measure(line).X > width)
        {
            const string ellipsis = "...";
            var boundaries = StringInfo.ParseCombiningCharacters(line);
            var low = 0;
            var high = boundaries.Length;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                var end = middle == boundaries.Length ? line.Length : boundaries[middle];
                if (Measure(line[..end] + ellipsis).X <= width) low = middle;
                else high = middle - 1;
            }
            var length = low == boundaries.Length ? line.Length : boundaries[low];
            line = Measure(ellipsis).X <= width ? line[..length] + ellipsis : string.Empty;
        }
        return line;
    }

    /// <summary>
    /// Truncation runs a binary search of measured substrings; drawn every frame, that was the
    /// most expensive text call. Results are kept per text, width, font and renderer.
    /// </summary>
    private static class SingleLineCache
    {
        private const int Capacity = 512;
        [ThreadStatic] private static Dictionary<CacheKey, string>? lines;

        public static unsafe string Get(string text, float width)
        {
            var renderer = MaterialTextRenderer.Current;
            var font = ImGui.GetFont().Handle;
            // A rebuilt font may reuse its address; its glyph table and atlas are new allocations.
            var fontIdentity = new FontIdentity((nint)font, (nint)font->ContainerAtlas, (nint)font->Glyphs.Data, font->Glyphs.Size);
            var key = new CacheKey(text, width, fontIdentity, ImGui.GetFontSize(), renderer);
            lines ??= new(Capacity);
            if (lines.TryGetValue(key, out var line)) return line;
            line = FitSingleLine(text, width);
            if (lines.Count >= Capacity) lines.Clear();
            lines.Add(key, line);
            return line;
        }

        private readonly record struct FontIdentity(nint Font, nint Atlas, nint Glyphs, int GlyphCount);

        private readonly struct CacheKey(string text, float width, FontIdentity font, float fontSize, MaterialTextRenderer? renderer)
            : IEquatable<CacheKey>
        {
            private readonly string text = text;
            private readonly float width = width, fontSize = fontSize;
            private readonly FontIdentity font = font;
            private readonly MaterialTextRenderer? renderer = renderer;
            public bool Equals(CacheKey other) => width.Equals(other.width) && fontSize.Equals(other.fontSize)
                && font == other.font && ReferenceEquals(renderer, other.renderer)
                && string.Equals(text, other.text, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(string.GetHashCode(text, StringComparison.Ordinal),
                width, font, fontSize, renderer);
        }
    }

    public static void Text(string text, float wrapWidth = 0)
    {
        if (MaterialTextRenderer.Current is null || !RequiresShaping(text))
        {
            if (wrapWidth > 0) ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrapWidth);
            ImGui.TextUnformatted(text);
            if (wrapWidth > 0) ImGui.PopTextWrapPos();
            return;
        }
        var position = ImGui.GetCursorScreenPos();
        if (wrapWidth <= 0 && ImGuiP.GetCurrentWindow().DC.TextWrapPos >= 0)
            wrapWidth = ImGuiP.CalcWrapWidthForPos(position, ImGuiP.GetCurrentWindow().DC.TextWrapPos);
        var size = Measure(text, wrapWidth: wrapWidth);
        ImGui.Dummy(size);
        if (ImGui.IsItemVisible())
            AddText(ImGui.GetWindowDrawList(), position, ImGui.GetColorU32(ImGuiCol.Text), text, wrapWidth);
    }
}
