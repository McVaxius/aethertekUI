# Component API contract

## Shared managed fonts (optional Dalamud adapter)

`AethertekUI.Dalamud.MaterialFontAtlas.CreateFont(IFontAtlas atlas,
MaterialFontWeight weight, float sizePx, ushort[] glyphRanges, string language)`
returns a consumer-owned `IFontHandle`. Weights Regular/Semibold/Bold are
400/600/700. Callers retain their role sizes, ranges, locale, readiness/error
checks, subscriptions and disposal; no existing font owner is replaced globally.

The adapter owns the unmodified Selawik 1.01 assets and OFL notice under
`Fonts/Selawik`. Preserve this directory beside its DLL in final packages.
Each rebuild retains native CJK regional/language/game-symbol merges and supplies
missing U+202F as actual blank narrow spacing without changing the source text.
Native font data is atlas-owned and ranges are pinned through building; no font
pointer is retained across rebuilds. Keep strict required-glyph checks, including
all roles/rebuilds. Managed-host readiness and game/GPU rendering remain separate
from offline glyph verification; optional shaping availability remains caller-owned.

## Adaptive sizing (v0.3)

See [Adaptive sizing and host compatibility](ADAPTIVE_SIZING.md) for units, ownership and upgrade behavior.

| API | Purpose |
|---|---|
| MaterialStyleMode.Compatible / Material / ColorsOnly | Preserve host layout by default; opt into full metrics or colors alone |
| MaterialWindowChrome.Push() | Explicit outer-window rounding using the active theme's pixel metrics; retains host padding, spacing, IDs and geometry |
| MaterialWindowDecorations.Prepare / Paint | Owned-viewport decoration replacement; compose preparation with the final native sizing callback, call first in Draw and after End; painting waits for the owner's final End |
| MaterialWindowFold.PreDraw / PostDraw | Consumer-owned transition of one native window's existing collapse controls; keeps expanded size, IDs and native resize behavior |
| MaterialWindowFold.GetLogicalSize() | Expanded pixel geometry for observers inside that window's Draw during a transition |
| MaterialWindowOpacity.Apply / CurrentOpacity | One owner-tree alpha multiplier after final End; native focus delay, immediate focus restoration, and consumer-owned preferences |
| AethertekUI.Dalamud.MaterialWindowMotion.Prepare / DrawChrome / Restore | Bridge for one Dalamud Window; prepares fold and optional owned-viewport chrome before Begin, restores body styles first in Draw, then paints chrome and restores managed constraints after End |
| AethertekUI.Dalamud.MaterialAppearanceTitleBar.Register / RefreshVisibility / DrawPopups | Standard Language, Compact, Transparency, Colour and Krangle titlebar actions; consumer-owned callbacks, localization and privacy state |
| MaterialColorMode.Auto / Picker / Lcars / Glassmorphism / Spatial / Cyberpunk | Values 0–5; added styles preserve legacy ordinals, independent palettes and semantic error roles |
| MaterialColorSettings.GetPalette / WithPalette / DefaultPalette | Three RGB roles per Picker/new style; LCARS and Glass waves have separate saved roles |
| MaterialAppearanceSelector.DrawColors | Native colour/hex editor; optional translated labels expose styles, LCARS bars, resets and Glass motion/waves |
| MaterialSkin.BackdropStyle / Backdrop / PanelFrame / Heading | Shared material geometry; suppress the adopted native background before Begin, then paint the backdrop first and frames behind controls |
| MaterialSkin.NativeButtonStyle / NativeFieldStyle / PaintButton / TransparentButtonFace | Retained native wrappers keep original actions/IDs while adopting selected materials |
| MaterialSkin.ReadableInk | Higher-contrast black/white caption for an opaque native control fill, including hover/active selection |
| MaterialSkin.TransparentTabFace / PaintTab | Retained native Cyberpunk tabs keep IDs and scrolling while painting chamfered plates behind captions |
| MaterialLcars.Header / PanelHeading / PanelFrame / Footer / NativeButtonStyle | Shared elbow/rail/panel geometry and retained native-button styling; caller retains IDs and scopes |
| MaterialAppearanceTitleBar.SetLcarsIcons / MaterialLcars.ControlBlue / MaterialIcon.Chocobo | Opt-in titlebar glyphs, blue control accent and a bird silhouette; original icon ordinals and actions retained |
| MaterialTheme.Feedback / MaterialLcars.Feedback | Host-owned activation feedback; disabled controls remain silent |
| MaterialUiSoundPlayer.TryPlay / Stop / Dispose | Optional original Click/Toggle/Preview PCM on one owned Windows waveOut handle; consumer owns mute and lifetime |
| MaterialSettings.Columns / Section | Compact semantic groups; measured pixel minimum controls wide-to-narrow reflow while retaining caller IDs |
| MaterialSettings.Field / Section(label,widthPixels) / Columns(...,divider) | Wrapped label/editor rows, bounded headings and opt-in paired columns; pixel widths and consumer-owned native callbacks |
| MaterialSelection.Checkbox / Radio explicit-width overloads | Wrapped display captions with the original native label-derived ID |
| MaterialSwitch.Row / MeasureRow | Wrapped label, optional help and trailing switch/state; exact native ID and shared pixel measurement |
| MaterialInput.SliderTrack | Thin linear track and circular thumb over native slider input; logical width, unchanged idle values and retained native navigation |
| MaterialAppearanceTab.Draw | Standard four-group Appearance layout with translated headings and consumer-owned controls/configuration |
| MaterialAppearanceSelector.DrawPalette | Inline preset swatches and existing custom-colour popup; returns whether the consumer draft changed |
| MaterialControls.Push(context) | Match native and custom control heights using current font, density and scale |
| MaterialControlContext.Regular / Toolbar / Dense | Local sizing intent independent of theme density |
| MaterialControls.Push(metrics) | Explicit already-scaled pixel metrics; parameterless nested scopes inherit, explicit contexts remeasure |
| MaterialTheme(colors, density) | Exact immutable scheme with initialization-time role assignments; palette/seed constructors remain available |
| MaterialControlAppearance | Consumer fill, foreground, border, and nonnegative logical corner radius |
| MaterialAppearanceSelector.Draw | Swatch/presets/RGB picker plus keyed language dropdown; separate AccentChanged / LanguageChanged flags |
| MaterialTable.PushControls() | Native table styling plus Dense editor sizing |
| MaterialTable.PushTightRows(alternateRows) | Adjacent editor rows, one logical pixel of horizontal cell padding; retains editor sizing |
| MaterialComboLayout | Independent popup width and visible row limit; optional native LeadingIcon in the field |
| MaterialActionTile.Measure / MaterialTileSizing | Content-aware wrapping and grid measurement; explicit fixed-height option |

All controls are immediate-mode; booleans return activation or value change. Consumers own state and lifecycle.

### Retained settings layouts

The explicit-width selection overloads are
`Checkbox(nativeLabel, display, ref bool value, float widthPixels, bool disabled = false)`
and `Radio<T>(nativeLabel, display, T option, ref T value, float widthPixels, bool disabled = false)`.
They wrap the caption inside a native full-row hit target whose identity is exactly
`ImGui.GetID(nativeLabel)`. Keep the original label, including its `##`/`###`
suffix, and its parent ID root when moving the control into a presentation card.
They return changes and retain keyboard navigation, inherited disabled state,
alpha and edited status. The older overloads retain their component ID scopes.

`MaterialSettings.Field(nativeLabel, display, float editorWidthPixels,
Func<string,bool> drawEditor, float widthPixels = -1)` places a wrapped label to
the left of one native editor, stacking when both cannot fit. Its callback receives
an empty label under the original native ID: submit the editor using that label,
without adding another ID scope. Native scalar step-button identities and the
editor's last-item data remain available. A nonpositive row width uses available
content width; the editor width must be positive.

```csharp
var width = ImGui.GetContentRegionAvail().X;
if (MaterialSelection.Checkbox("Enabled##existing", enabledCaption, ref enabled, width))
    ExistingEnabledHandler(enabled);
if (MaterialSettings.Field("Delay##existing", delayCaption, 140 * MaterialTheme.Metrics.Scale,
    label => ImGui.SliderInt(label, ref delay, 0, 1000), width))
    ExistingDelayHandler(delay);
```

`MaterialSwitch.Row(nativeLabel, display, ref bool value, onLabel, offLabel,
float widthPixels, string? help = null, bool disabled = false)` uses the same
native-ID contract, with the shared track and stable ON/OFF text reservation on
the right. Optional help adds `(?)` and a whole-row hover tooltip; clicking the
marker leaves the value unchanged. `MeasureRow(display, onLabel, offLabel,
widthPixels, help)` returns the exact row size under the current font/control
scope, allowing paired cards to reserve their taller group's wrapped height.

All these widths and returned sizes are already-scaled pixels. The consumer
retains values, enum options, bounds, localization, compound handlers and saving.
For a thin linear range beside a retained numeric editor, use
`MaterialInput.SliderTrack(id, ref value, minimum, maximum, width, disabled)`.
Its explicit width is logical, like the other `MaterialInput` methods. It retains
the normal `id`/`##value` native slider scope and leaves out-of-range saved values
unchanged until edited. Keep the original numeric editor and its handlers for
precise entry; logarithmic ranges retain their existing native editor.
An arbitrary callback with a hard-coded caption must be rebound explicitly; a
theme scope does not rearrange its layout.

`MaterialSettings.Section(label, widthPixels)` and
`MaterialLcars.PanelHeading(title, accent, widthPixels)` constrain wrapped headings.
`MaterialSettings.Columns(id, minimumColumnPixels, left, right, divider: false)`
uses the theme gap between paired columns, omits the divider/outer cell inset and
retains caller row spacing. The four-argument Columns overload preserves its
original divider, padding, spacing and reflow threshold; the original Section
and PanelHeading overloads retain their geometry.

### Shared colour modes and material rendering

`MaterialColorSettings` is an immutable, consumer-owned record. Auto=0, Picker=1
and Lcars=2 retain their ordinals; Glassmorphism=3, Spatial=4 and Cyberpunk=5 are
additions. `GetPalette(mode)`, `WithPalette(mode, background, controls, text)` and
`DefaultPalette(mode)` support Picker and the three new styles. LCARS uses
`LcarsBackgroundRgb` and `LcarsBar1Rgb`–`LcarsBar3Rgb`. Glass uses
`GlassAnimation` (true), `GlassAnimationSpeed` (normalized to 0.1–10, default 1)
and `GlassWave1Rgb`–`GlassWave3Rgb`; waves remain editable while motion is off.
`Apply(automaticScheme)` retains supplied semantic errors and Auto behavior.

The original `DrawColors(id, ref choices, ref accent, palette, labels,
minimumWidth)` remains supported. The added overload accepts
`MaterialColorSettings defaults, Vector3 defaultAccent` before `minimumWidth`.
Use that overload to reset to the consumer's actual defaults. Both update live
drafts, returning true for completed edits, mode changes and resets. Persist via
the existing consumer save path and refresh its cached theme next frame.

Supply translated `MaterialColorLabels.Glassmorphism`, `Spatial` and `Cyberpunk`
together to expose the three additions. Supply `Lcars`/`LcarsHelp` and all three
`LcarsBar1`–`LcarsBar3` captions for the four-role LCARS editor. `Reset` and
`ResetColors` enable per-role and current-palette resets; `Animation`,
`AnimationSpeed`, `WaveColors`, `GlassWave1`–`GlassWave3` and `ResetWaveColors`
enable the Glass controls. Resets preserve other styles and separate wave
choices. Legacy callers retain their choices, field IDs and layout.

Theme the complete native Begin/End lifecycle. Enter
`MaterialSkin.BackdropStyle()` around the adopted owner's native Begin, then
dispose it before drawing content. Use `popup: true` around BeginPopup instead;
disposing immediately after Begin restores native backgrounds for nested
colour/combo popups. Call `MaterialSkin.Backdrop()` first in visible content.
Call it before tables or draw-channel splits; it rejects an existing splitter
before mutation. Its bounded background merge preserves native chrome above it.
The pair replaces one native background rather than stacking its opacity.
Auto/Picker and LCARS retain their native backgrounds. The optional titlebar
adapter handles this pair for its own manual appearance popups.
`MaterialSurface` and default shaped controls automatically use the selected
material. Retained layouts paint `PanelFrame(min, max, accent, inset)` in their
existing background channel, behind controls. LCARS frames require the existing
twelve-logical-pixel rail reservation. Explicit appearances and corner radii
remain caller-owned.

For retained native buttons, enter `NativeButtonStyle`, capture disabled alpha,
draw the original button under `TransparentButtonFace`, then dispose that face
scope before calling `PaintButton` in the background channel. Pass captured
alpha when the disabled scope has ended. This retains native hit testing,
caption, focus and original IDs. `PaintButton(min, max, alpha = null)` retains
the scaled material radius: 7 logical units for LCARS, 3 for Cyberpunk and 6
for other materials, clamped to half the smaller button extent. Use
`PaintButton(min, max, alpha, cornerRadiusPixels)` for an explicit already-scaled
pixel radius; it is not scaled again and must be finite and nonnegative.
The fourth argument is required; the third argument remains captured alpha.
After `NativeButtonStyle`, push the same pixel radius as `FrameRounding` when
the native face/navigation geometry must match. The sizing lab reuses its
retained native action with this explicit radius. `NativeFieldStyle` paints one
single-row field immediately before its native input, after width/height scopes. Keep styles
lexical and apply complete-window opacity once after End. Glass ribbons and
glow are bounded draw-list geometry; they do not blur the game framebuffer.
Theme reduced motion also freezes the Glass backdrop.
`Heading` retains supplied casing; consumers own translated capitalization.

### Native appearance and Krangle titlebar actions

The optional Dalamud adapter retains the original four-action overload and adds
an overload for the standard fifth action:

```csharp
public static MaterialAppearanceTitleBar Register(Window owner,
    Func<MaterialAppearanceTitleBarLabels> labels,
    Action drawLanguage, Action toggleCompact,
    Action drawTransparency, Action drawColour,
    Func<MaterialAppearanceTitleBarVisibility>? visibility = null);

public static MaterialAppearanceTitleBar Register(Window owner,
    Func<MaterialAppearanceTitleBarLabels> labels,
    Action drawLanguage, Action toggleCompact,
    Action drawTransparency, Action drawColour,
    Action toggleKrangle, Func<string> krangleTooltip,
    Func<MaterialAppearanceTitleBarVisibility>? visibility = null);
```

Both return a consumer-owned `MaterialAppearanceTitleBar`. Labels retain their
four positional fields: `Language`, `Compact`, `Transparency` and `Colour`.
`MaterialAppearanceTitleBarVisibility` retains the same four positional boolean
fields and constructor; `Krangle` is a non-positional `bool` init property that
defaults true. An existing visibility value can explicitly hide the fifth action
with `visibility with { Krangle = false }`.

Use the new overload for new and adopted main, mini and auxiliary windows, with
Krangle visible by default. Its `FontAwesomeIcon.UserSecret` action invokes
`toggleKrangle` directly on left click. `krangleTooltip` supplies the localized
current On/Off state when hovered. Compact also toggles directly; Language,
Transparency and Colour open manual popups. The old overload creates four actions
for compatibility. Visibility does not change the consumer's saved privacy state.

Register once after existing title actions. Call `RefreshVisibility()` before
native Begin and `DrawPopups()` after End and motion restore, before opacity,
including collapsed frames. Existing custom descriptors, priorities, window
identities and lifecycle remain consumer-owned. Reuse the consumer's existing
saved privacy toggle and masking rules: the adapter owns no privacy configuration,
service, global preference or name detector and does not redact arbitrary strings.
VERMAXION is the selected reference integration; other consumers need selected
wiring and rebuilds.

### Windows text shaping

`MaterialTextLayout(text, fontSize, maximumWidth, fontFamily, locale)` owns a
Windows DirectWrite layout. Sizes and returned hit-test coordinates are scaled
UI pixels; pass the active font size once. Positive infinity selects no wrapping.
`Size`, `Baseline`, `Clusters`, `HitTestPoint`, `HitTestPosition` and `HitTestRange`
share the same shaping. `Rasterize()` returns white RGBA pixels with grayscale
coverage in alpha and an overhang `Offset`; consumers retain their tint and clip.

Own `MaterialTextRenderer(upload, fontFamily, locale)` and enter its `Push()`
scope in the owning ImGui context. The uploader returns
`MaterialTextTexture(id, disposableOwner)`. Core owns those returned resources;
the Dalamud `MaterialTextHost(ITextureProvider)` supplies the raw-RGBA uploader.
Dispose/clear outside a native frame, after render submission. Scopes are lexical,
context-bound and LIFO, including when nested with the same owner. Do not retain
or dispose borrowed `GetLayout` results beyond the current frame.

`MaterialText.Measure`, `AddText` and `Text` use DirectWrite only for Devanagari
under that scope. Native text is otherwise unchanged. `Text` supplies the shaped
layout's item height and avoids uploading offscreen items. Existing material
buttons/action tiles and text/search fields share the route. Raw ImGui labels,
tooltips and consumer wrappers require explicit routing; a text scope alone
does not shape them.

For retained consumer wrappers, use `TextWrapped`, `TextColored`, `TextDisabled`,
`BulletText` and `SetTooltip` for authored text. `Text` honors the existing native
wrap position. `AddText` also accepts an explicit native font and pixel size for
retained title/header drawing. `BeginCombo` retains its label-derived ID, native
arrow and caller-owned popup; `Selectable` retains the native option label/ID.
`TreeNode(nativeLabel, flags)` preserves a raw tree's label-derived ID and arrow;
the caller retains `TreePop` ownership. It reserves existing vertical padding
around shaped ink, including unframed rows. `TableHeader(nativeLabel)` retains
native sort arrows and presses; reserve the shaped header row height before
calling it. Their optional shaped `display` caption retains the supplied native
label/ID. These wrappers do not add an ID scope.
Reserve the complete frame for shaped preview ink, including negative bearings.
`PushLineHeight(value, hint)` supplies a temporary shaped-only native frame-height
reservation. Use it around `MaterialShapedInput.SingleLine(originalNativeId,
hint, ref value, maxLength, flags)`; an existing `PushOverrideID` plus empty label
remains valid. Do not replace the consumer's native ID with a new library field ID.

`MaterialTextLayout.CheckGlyphs()` validates actual shaped runs without allocating
a bitmap. `MaterialTextRenderer.CheckGlyphs(authoredText, fontSize)` checks only
Devanagari strings and requires no host texture uploads. Keep the consumer's
original atlas checks for every non-Devanagari character; validate each retained
font-role size before allowing a Hindi catalog or language-selector entry.

`TryCheckGlyphs(optionalText, fontSize, out error)` probes optional shaped captions
without uploading textures or caching availability. Native DirectWrite fallback
may satisfy an absent requested family; actual shaped glyph coverage still gates
support. Keep mandatory selected-catalog/raw-text checks separate. A consumer can
disable an unavailable Hindi menu option with an ASCII display override while
retaining its key/native ID, and offer explicit saved-language recovery through
its existing save route. Windows fallback checks do not establish Wine support.

The single-line field retains its original native input ID and native undo,
focus and clipboard behavior, with shaped pointer/caret/selection/scroll/IME
geometry. Fields reserve Hindi's natural line height before painting their
background. Backspace retains native codepoint deletion, allowing conjuncts to
be dismantled. Replacement paste retains native undo steps. Password fields
keep the unchanged native path. In ImWchar16 hosts, transient editor tokens
preserve supplementary and private-use source values through clipboard and
undo while respecting original UTF-8 input limits; tokens are never returned
to consumers. Supplementary keyboard data already lost in the host input queue
is outside this bridge.

`MaterialShapedInput.Multiline(nativeId, ref value, maxLength, pixelSize, flags)`
preserves the retained editor ID, native child/scrolling, clipboard, undo and
UTF-8 capacity. Shaped line geometry supplies pointer/caret/selection, vertical
navigation and per-line IME placement. CRLF/lone-CR and supplementary/private-use
source values survive the transient native proxy. `MaterialInput.Multiline`
uses the same route for its existing field. Game-rendered SeStrings and actual
host IME acceptance remain separate.

`MaterialTabs.Begin(id, captions, flags)` declares mixed bar captions before
native Begin to reserve their natural shaped height. Explicit widths/flags and
native tab IDs remain caller-owned. Tree/tab scope construction cleans up native
pushes on an upload failure; consumer-owned popups/clips require their own
`finally` cleanup. Use rune-aware `MaterialText.NativeGlyphText` for atlas input
after checking Devanagari through shaped glyph runs.

Enter `MaterialWindowChrome.Push()` after the theme and before native Begin,
and keep it active through End. It supplies outer-window rounding explicitly
for retained `ColorsOnly` layouts without changing that mode's existing contract.
The optional adapter also exposes `Draw(windows, applyMetrics, roundedCorners)`.
Native Begin resets owned-platform and docked window rounding to zero. These
style scopes alone therefore do not establish visible corners for those windows.

Own one `MaterialWindowFold` per direct native window. Prepare it last before
Begin and finish it after End, including when Begin returned false. Its returned
true value means the installed native constraints must not be overwritten by a
later host setter. Bounds passed to it are pixels. Keep the theme active through
the complete lifecycle. Native collapse arrows and title double-clicks retain
their identities; reduced motion and native opt-outs retain native behavior.

For a Dalamud Window, `MaterialWindowMotion.Prepare(this)` belongs last in its
PreDraw and `Restore(this)` first in PostDraw. The original overload retains
fold-only behavior. Select owned-viewport corners with the three required
arguments `Prepare(this, reducedMotion: false, roundedCorners: true)`, and call
`DrawChrome()` first in Draw. Restore paints expanded and collapsed chrome after End,
before returning transient animation geometry to its expanded state.
The bridge scales the owner's
managed constraints using the host scale and restores them after End. A managed
constraint assigned during Draw is retained. Geometry observers inside Draw can
use `GetLogicalSize()` while visible height changes; the fold also preserves an
explicit native SetWindowSize request made by an existing control during Draw.
Reference the optional adapter and ship its DLL with this integration.

Own one `MaterialWindowOpacity` for each top-level window tree. Under its theme,
call `Apply(windowName, opacity, enabled, autoFade, fadedOpacity, unfocusedDelaySeconds)`
once after the final native End and after motion/decorations restore, before Render.
Fractions clamp to .1–1; defaults are 1 normal, .5 unfocused, auto-fade enabled and
10 seconds without focus. Disabling restores original emitted alpha. Focus in an
owned child or popup restores normal opacity immediately. Native application focus
events remain effective across idle frames; reopening resets the delay. A fade
never increases opacity above the selected normal value. Theme motion and the
optional `reducedMotion` argument control the transition.

The component multiplies emitted root/child/popup/tooltip vertex alpha, including
native chrome, custom artwork and image tints. It preserves RGB, geometry, UVs,
indices, clips, textures, IDs and styles; unrelated top-level windows and shared
viewport/foreground drawing stay outside its ownership. Avoid separate child
instances or another style/image opacity multiplier on the same tree. Configuration,
main toggles, translated labels and saving remain consumer responsibilities.

For a direct native window, compose `MaterialWindowDecorations.Prepare` through
the fold's five-argument PreDraw overload; cache the delegate per owner.
Call `Paint()` first in visible content and again after End before PostDraw.
Painting waits until the owner leaves the native window stack. Keep the after-End
call on expanded windows too: inserting decoration commands before native tables
and draw channels finish merging corrupts their index offsets and clip assignments.
The early call retains the owner's font measurement for resize-grip geometry.
The replacement uses matched native decoration vertices and emitted colors in
the owner's draw list. It preserves host alpha/fades and global style stacks;
title controls, menu dividers, scrollbar grabs and native input remain intact.
Docked, child, popup and tooltip windows retain their native decoration paths.
Rendered owned-viewport regression is separate from live platform/GPU acceptance.

Appearance overloads of `MaterialButton.Draw`, `MaterialStatus.Badge`,
`MaterialTextField.SearchField`, and `MaterialCombo.Draw` retain native
interaction and existing IDs. Badge dimensions and the explicit
`MaterialSwitch.Draw(..., trackSize, ...)` overload use logical units. The new
search overload hides clear while empty; the old overload retains its slot.
The combo paints one outlined field/chevron and keeps native popup editing,
navigation and selection. `MaterialCanvas.Surface(min,max,top,bottom,radius)`
draws a rounded vertical gradient; coordinates and radius remain pixels.

`MaterialAppearanceSelector.Draw(id, ref accent, ref language, options, labels,
languageWidth)` borrows the active font. Preset selection reports immediately;
custom RGB reports on edit completion. Consumers own the draft, localized
labels/native language options, semantic colours, configuration/save path and
next-frame theme/font changes. Palette and Globe icons are appended to the
existing enum. Map, Clock, Link, Calculator, Person, Shield, Monitor and Location are also appended for area,
elapsed-time, dependency, calculation, identity, privacy, display and saved-position controls; all use native meshes without
font or texture ownership. Send appends a paper-plane icon for message composers
without changing existing enum values. Save, Copy, Heart and List append native
meshes for saving, copying, support and ordered-list affordances; consumers keep
the action, stable control ID and localized tooltip.
Sword (crossed blades) and Star append native combat/action and rule-priority artwork, preserving
all prior enum values. Their meshes need no font or texture resources.
Folder and Egg append native workspace/preset and breeding artwork for ChokeAbo
with the same ownership and stable-identity rules.
Wrench, DoorExit, Cube, Chest, Cart, Chat, Group, Warning and Plus supply ADS's
approved action/status artwork. Pulse, Utensils and Crystal supply LootGoblin's
summaries and Alexandrite action. Calendar, Crown, Book, Sparkle, Bell and Terminal
supply DAD's schedule, guide and command artwork. These append after Egg, preserving
every earlier ordinal, and retain the same font-free drawing and identity rules.
Power appends at ordinal 62 for FrenRider's account-wide enable action; consumers
retain their existing callbacks, state and native control identity.
Target appends at ordinal 63 for LootGoblin's Current Run crosshair; the existing
Location pin and every preceding mesh and ordinal remain available.
MapPin (64) and HeartOutline (65) provide BFE's approved map-with-pin and outlined
support artwork, retaining the separate Map, Location and filled Heart meshes.
Discord (66) and KoFi (67) provide shared community artwork with transparent
Discord eyes and a red Ko-fi heart. They retain caller colour/opacity and restore
native draw-list flags; complete stroked geometry fits the icon square at 14/36 pixels.
See the sizing showcase and independent consumer example.
For an established native language control, use
`MaterialAppearanceSelector.Draw(id, ref accent, labels, drawLanguage)`.
The callback runs after the selector's ID scope is restored and returns whether
the language changed. The standard selector preserves the measured selected-label,
arrow and padding minimum at narrow widths; consumers reflow the whole header group.
The consumer keeps its original native Combo label,
option ordering/index IDs, localization and disabled state. Accent selection
and change flags retain the same behavior.
The standard language field contains its Globe mesh. When an approved header
places existing actions between colour and language, call
`DrawAccent(id, ref accent, labels)` and
`DrawLanguage(id, ref language, options, languageWidth)` using the same ID.
Both retain the combined selector's native identities and change semantics;
the consumer still owns placement and saving.

`DrawAccent(id, ref accent, labels, floatSize)` accepts a positive logical square
swatch size; the original overload retains 28 and its native identity.
The `Vector2` overload draws a complete rectangular colour field with a Palette
mesh, current swatch and chevron. Its dimensions are logical units, and the full
field opens the same popup with the same accent/button IDs and change events.
Its Palette uses the current pixel `MaterialControls.Metrics.IconSize`, bounded
by the field height and space before the swatch. Supply explicit metrics for a
larger approved icon without changing the field dimensions or native identity.
Language field height
uses the existing explicit `MaterialControls.Push(metrics)` scope; measure its
width with those same metrics rather than adding another selector implementation.

`MaterialComboLayout.LeadingIcon`
uses the current control metrics, and `MaterialIcons.Draw(icon, drawList, ...)`
borrows the field's draw list so its mesh stays in the field when a popup opens.
Use MaterialTheme.Push(theme, ImGuiHelpers.GlobalScale) around your window draw. Pass stable IDs separately from visible labels. Scope IDs further with ImGui.PushID for repeated domain objects.
Button variants share label, leading/trailing MaterialIcon, size and disabled support. Negative width fills available width; positive sizes are logical units; zero selects defaults.
`MaterialButton.Draw(id, label, cornerRadius, ...)` and `MaterialStatus.Badge(label, cornerRadius, ...)` accept an explicit nonnegative logical radius, clamped to the rendered bounds. Existing overloads keep their pill shapes. Document, Database, Refresh, Chart, ExternalLink and Download extend the texture-free icon set without changing prior enum values.
MaterialSegment<T> arrays are cached, passed as ReadOnlySpan, and bind ref T. Switch binds ref bool and animates by stable ID.
Surface/Card and Dialog scopes expose Visible. Always dispose child scopes even when Visible is false; only render child contents when visible. Dialog scopes call EndPopup only on a successful BeginPopupModal.
Tables remain native BeginTable/EndTable with a MaterialTable.PushStyle scope and optional row coloring. Native combo and slider behavior inherits the theme. Checkbox/radio helpers use stable native button IDs and custom visuals.

## Public families
| Type | Entry points | State / result |
|---|---|---|
| MaterialTheme | constructor(seed,density,generator), Push(theme,globalScale), Colors, SurfaceOpacity, Density, Motion | Cached immutable RGB scheme; opacity/density mutable |
| MaterialButton | FilledButton, FilledTonalButton, OutlinedButton, TextButton, Draw, IconButton | Activation bool; stable id separate from label |
| MaterialSegmented | Draw<T>(id, segments, ref value, width, disabled) | Value changed bool; options include Disabled and Icon |
| MaterialSegmented | Draw<T>(id, segments, ref value, width, disabled, showSelectionCheck, cornerRadius) | Opt-in icon-preserving view switch and logical corner radius; original overload/IDs remain unchanged |
| MaterialSwitch | Draw(id,label,ref value,disabled) | Changed bool; thumb interpolation |
| MaterialSwitch | Row(nativeLabel,display,ref value,onLabel,offLabel,widthPixels,help,disabled); MeasureRow | Changed bool / measured pixel size; original native ID, trailing track and optional help |
| MaterialSelection | Checkbox; Radio<T> | Changed bool; caller owns setting |
| MaterialSettings | Field; Section; Columns | Native editor change / semantic heading / paired groups; no data or configuration ownership |
| MaterialSurface | Begin; Card; ClickableCard; Color(elevation) | Ref-struct child scope / activation bool |
| MaterialChip | FilterChip; ActionChip | Ref bool selection / activation |
| MaterialChip | FilterChip(id, label, ref selected, appearance, disabled, icon, showSelectionCheck) | Explicit fill/foreground/border/radius; consumer can keep the filter caption without a selected check |
| MaterialTextField | SearchField; TextField | Changed bool; native string editing, flags, maxLength |
| MaterialNavigation | Rail; Item; ListItem | Child scope / activation |
| MaterialTabs | Begin; Item | Native tab scopes with Visible |
| MaterialActionTile | Draw | Activation, active/disabled flags, optional subtitle |
| MaterialStatus | Badge; LinearProgress; SectionHeader | Display only; optional consumer colors |
| MaterialDialog | Begin(id,ref open,flags) | Modal scope with Visible |
| MaterialWindowHeader | Draw(title,subtitle,actions,compact); PaintTitle(window,title,texture,textureSize,extraRightWidth,showCloseButton) | Header action index or -1; borrowed after-End title image/caption |
| MaterialTable | PushStyle(alternateRows); PushTightRows(alternateRows); RowState(selected,hovered,semanticBackground) | Style-only scopes; native tables remain caller-owned |
| MaterialIcons | Draw | Texture-free vector drawing; caller can use native images separately |

## Child scopes and composition
```csharp
using (var card = MaterialSurface.Card("card", new Vector2(-1, 120)))
{
    if (card.Visible)
    {
        MaterialStatus.SectionHeader("QUEUE");
        MaterialStatus.Badge("Running", domainRunningColor);
        MaterialStatus.LinearProgress(completion);
    }
}
```
Child scopes always call EndChild, even when BeginChild returns false. A clickable card is a separate single-action control; use an ordinary Card for interactive child contents. Surface padding and radius overrides are logical units. All zero/negative child sizes retain native semantics.

## Native table preservation
```csharp
using var tableStyle = MaterialTable.PushStyle();
if (ImGui.BeginTable("items", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
{
    // Column setup, sorting and clipping stay in the consumer.
    ImGui.TableNextRow();
    ImGui.TableNextColumn();
    if (ImGui.Selectable(itemName, selected, ImGuiSelectableFlags.SpanAllColumns))
        selected = true;
    MaterialTable.RowState(selected, ImGui.IsItemHovered());
    // Render remaining cells and consumer-owned semantic colors.
    ImGui.EndTable();
}
```
Apply the table scope once around the table, not per row. RowState may take a semantic background; otherwise do not call it on rows with domain-owned background colors.

For tight editable grids, replace `PushStyle()` with `PushTightRows()`. Enter it
after any `MaterialControls.Push(...)` or `MaterialTable.PushControls()` scope,
before `BeginTable`, and retain it through `EndTable`. Cell padding becomes
`(scale, 0)`: editors meet vertically and sit one logical pixel from column
dividers. Editor frame padding and font height remain intact; no fixed row height
is imposed. The tallest editor determines each row's height. A caller-supplied
minimum row height can still add space. All prior styles restore on disposal.

```csharp
using var editors = MaterialControls.Push(MaterialControlContext.Regular);
using var tightRows = MaterialTable.PushTightRows();
if (ImGui.BeginTable("existing-grid", 3, existingFlags))
{
    // Keep existing columns, editor IDs, native actions and scrolling.
    ImGui.TableNextRow(); // Let the editors determine the height.
    // Draw each cell with the existing editors.
    ImGui.EndTable();
}
```

## Modal lifecycle
```csharp
if (MaterialButton.FilledButton("open", "Open"))
{
    open = true;
    ImGui.OpenPopup("Confirm");
}
using var dialog = MaterialDialog.Begin("Confirm", ref open);
if (dialog.Visible)
{
    if (MaterialButton.FilledButton("confirm", "Confirm"))
    {
        ImGui.CloseCurrentPopup();
        open = false;
    }
}
```
OpenPopup and Begin must run at the same ImGui ID-stack level. Scope disposal handles only EndPopup; the consumer controls close/save/cancel.

## IDs, input and clipping
Use stable domain IDs for repeated items. MaterialSegment.Key supplies identity across reordering; omitted keys retain positional IDs for fixed lists. Labels in custom controls are display text and can change without changing the hit ID. Tabs follow native ImGui label/### conventions.
Standard keyboard/gamepad navigation requires the host's native navigation configuration. The library does not set it. Segments are separately focusable native button items; a full radio-group arrow/bumper model is future work.
Native inputs own text editing and flags, including ReadOnly. Search reserves a trailing clear slot rather than drawing over editable text. Practical minimum width is about 120 logical units. After SearchField, the last item is the field group; tooltips can be attached to it. Button/switch/tile calls preserve their hit item for native consumer tooltips/context menus.

## Lifetime and performance
Create themes and segment/action arrays on configuration/model changes, not each frame. A theme caches all palette colors; modifying SurfaceOpacity does not regenerate RGB. Use theme.Motion.Enabled=false for reduced motion.
Every style/child/tab/dialog scope is a ref struct. Do not copy, box, store or dispose them out of order. All calls belong on the owning ImGui UI thread. Native draw-list primitives are clipped by the host.

## Integration additions (v0.2)
| Type | Contract |
|---|---|
| MaterialThemeSettings / Controller | Serializable immutable settings; Apply validates, skips unchanged settings, regenerates only for RGB changes |
| MaterialWindowHost (optional adapter assembly) | Draw(WindowSystem,applyMetrics), explicit Draw(WindowSystem,applyMetrics,roundedCorners), or BeginFrame(applyMetrics); no subscriptions or config writes |
| MaterialTheme.Push(...,applyMetrics:false) | Native colors only; preserves existing ImGui geometry styles |
| MaterialOptions<T> / MaterialCombo.Draw | Cached keyed options, filtered on SetQuery/Replace; disabled options, native editing/selection |
| MaterialInput | Integer, Number, Slider, Multiline; stable IDs, native flags, logical size, disabled and bounds |
| MaterialLayout.FitNextItemWidth | Native pixel widths; moves an exhausted row and preserves the measured readable minimum. Negative fills; zero uses the minimum; consumer owns narrow-pane scrolling. |
| MaterialOverride.Switch | Nullable bool with Inherit/On/Off |
| MaterialFlags | Explicit ulong masks; preserves unrelated bits; Draw, IsSet and Set |
| MaterialTree.Node / MaterialContextMenu.Begin | Native scopes; Visible gates contents; lexical disposal |
| MaterialDragDrop.Source / TryAccept<T> | Copies unmanaged ID payload, validates type/size and waits for delivery |
| MaterialSplitPane.Divider | Host-owned first panel width, logical dimensions, mouse drag and clamps |
| MaterialDataView<T> | Explicit Rebuild(source,filter,comparer); old view retained if callback throws |
| MaterialSelectionSet<TKey> | Selection survives filtering and sorting; explicit prune on object deletion |
| MaterialClipper | Native uniform-row clipper; Step then Start..End, optional Include before first Step |
| MaterialSearchState | Optional FocusRequested, restores text focus following clear |
| MaterialSemantic | Text/Colors scopes preserve supplied colors; Feedback and Tooltip |
| MaterialCanvas | Pixel-space Color/Surface/DrawImage(drawList,texture,textureSize,min,max,tint); borrowed Image/ImageButton/GlyphButton with logical control sizes |
| MaterialNavigationMath.NextEnabled | Pure host-routed traversal; skips disabled options, optional wrap; no global input |

Options/models must be owned per control or deliberately shared; sharing a MaterialOptions instance also shares its query. Payload type names must distinguish incompatible layouts even when their unmanaged sizes match. Keep native table ID/column settings and host texture lifetimes. Full usage is compiled in samples/ConsumerContract; see INTEGRATION_GUIDE.md.

`DrawImage` centers an aspect-preserving image within pixel bounds and changes no
cursor or item ID. Missing/invalid textures draw nothing; the caller reserves
layout space. `PaintTitle` runs after final End and motion/decorations restoration,
before complete-window opacity. It masks only native caption glyphs and retains
native controls, background, geometry and IDs. If replacement text borrows a different font, capture
`ImGui.GetFont()` before that borrow and use the overload accepting
`nativeCaptionFont` after texture size (after status for `PaintTitleWithStatus`).
The original native font can belong to a separate host atlas outside IO.Fonts;
the explicit font preserves its glyph identity. Original overloads are unchanged.
Supply existing custom/host-button
reservation in pixels; native Close/collapse are budgeted separately. It always
reserves one native-font-size image box plus inner spacing, including when the
image is unavailable or the body is collapsed. The consumer owns the texture
through render submission and keeps its title minimum wide enough for this box.
Title geometry comes from the owner's retained Begin bounds, so a subsequently
borrowed font of a different size does not change native control spacing. Keep
the Begin frame-padding Y in scope through repaint; X may be restored by motion.
