---
name: aethertek-ui
description: Build and evolve AethertekUI or redesign FFXIV-related C# ImGui applications from approved mockups, with multilingual localization and relative colour themes. Use for library components, layouts, the showcase, consumer integration, and UX deployment readiness; use the optional Dalamud adapter only in Dalamud hosts.
---

# AethertekUI adoption

Resolve this repository from the skill location (two directories above this
folder), currently `Z:\aethertekUI`. Read its `AGENTS.md` and the consumer's
instructions. This skill guides implementation; it does not authorize edits to
an unselected plugin, client installation, live-game testing or publication.
AutoParty is skipped in the foundation delivery.

## Projects and references

- `src/AethertekUI/AethertekUI.csproj`: core controls, drawing, data helpers,
  cached dark themes and OKLCH palettes; namespace `AethertekUI`. Existing
  component names remain `MaterialTheme`, `MaterialButton`, etc.
- `src/AethertekUI.Dalamud/AethertekUI.Dalamud.csproj`: optional
  `MaterialWindowHost`, host scaling and consumer-owned `MaterialGlassRenderer`;
  namespace `AethertekUI.Dalamud`.
- `src/AethertekUI.Demo/`: AethertekUI Showcase, command `/aethertekui`;
  eight native-rendered mock-data pages, including the sizing lab.
- `samples/ConsumerContract/ConsumerUi.cs`: compiled independent example of
  consumer-owned windows, configuration, semantic colors and overlays.

Prefer local `ProjectReference` entries to core, plus the adapter when needed.
For a sibling consumer repository, the core path is typically
`../aethertekUI/src/AethertekUI/AethertekUI.csproj`; resolve the actual path
relative to the consuming csproj. Do not copy library source into plugins.

Core targets .NET 10 / Windows x64 and references `Dalamud.Bindings.ImGui`.
Compatible FFXIV-related C# ImGui applications can use it when they supply that
binding and its matching native runtime/context. Another ImGui binding is not a
drop-in replacement. Reserve the adapter for Dalamud hosts; core does not own
Dalamud services or a renderer.

## Window and control scopes

Cache a `MaterialTheme` or `MaterialThemeController` outside Draw. Rebuild
palettes on seed changes; density, opacity and motion settings reuse them.
Place `MaterialTheme.Push` around the complete native Begin/End lifecycle,
including End when Begin returns false. For Dalamud WindowSystem, wrap
`WindowSystem.Draw` using `MaterialWindowHost.Draw` or `BeginFrame`; a theme
started inside `Window.Draw` is too late for outer-window styling.

`MaterialStyleMode.Compatible` is the default and preserves host layout.
Use `Material` explicitly for a new design that needs full library geometry,
or `ColorsOnly` / `BeginFrame(applyMetrics: false)` for retained overlay geometry.
Scopes are UI-thread/context-bound, lexical and LIFO. Do not copy or double
dispose them, or switch contexts before disposal. Preserve balanced native
Begin/End, popup, table, child, font and ID scopes.

For explicitly selected rounded outer chrome on a retained `ColorsOnly` layout,
enter `MaterialWindowChrome.Push()` after the theme and before native Begin,
keeping it active through End. It uses the current theme's already-scaled outer
radius and restores the prior window rounding without changing padding, spacing,
native IDs or saved geometry. The optional adapter's
`Draw(windows, applyMetrics, roundedCorners)` overload forwards the same scope.
Consumer PreDraw styles may override it. Native Begin forces rounding to zero
for owned platform viewports and docked windows; this style scope alone cannot
prove their rendered corners.

For selected owned-viewport corners and animated minimize in a Dalamud Window,
own one `MaterialWindowMotion` per window. Call
`Prepare(this, reducedMotion: existingPreference, roundedCorners: true)` last in
PreDraw, `DrawChrome()` first in Draw, and `Restore(this)` first in PostDraw,
after native End even when collapsed. Keep the theme and corner style scope
around the full lifecycle. Reference and ship the optional adapter DLL.
Preserve existing hook work and branch coverage, including font loading/error
windows. Enter the saved theme and chrome scope before a font-readiness early
return; linking the same status helper into public and private hosts does not
supply its caller theme. Keep borrowed body-font and layout scopes after readiness.
Prepare composes prior native size callbacks and temporarily suppresses
later managed constraint setters; Restore returns the owner's exact constraints.

For direct native Begin/End, own one `MaterialWindowFold` and one
`MaterialWindowDecorations`. Pass a cached `decorations.Prepare` delegate to the
fold's five-argument PreDraw overload after setting existing size constraints.
Call `decorations.Paint()` first in visible content and again after End, then
`fold.PostDraw()`. Paint waits until the owner leaves the native window stack;
keep the after-End call for expanded windows too. Front-inserting decoration
commands before native table/channel merges corrupts index offsets and clip
assignments, as ADS's header/actions/state tables demonstrated. The early call
retains the owner's font measurement for resize grips. Paint replaces only
matched native decoration meshes in that
owner's draw list, retaining emitted opacity, title controls, scrollbar grabs,
input and global styles. It does not paint a shared viewport background.
Docked windows and native popup/child/tooltip windows retain native chrome.

Native collapse arrows and title double-clicks keep their IDs. Existing
NoCollapse/NoTitleBar flags and disabled theme motion retain native behavior.
Inside Draw, size observers that save or repair geometry use
`motion.GetLogicalSize()` or `fold.GetLogicalSize()` to see expanded pixel size
during animation. Keep explicit resize actions and original position/save paths.
Verify actual owner hooks, expanded/collapsed/restored corner pixels, resize
grips, constraints, IDs and saves at both densities/scales. The offline injected
owned-viewport branch is diagnostic evidence; game/GPU acceptance stays separate.

For a selected appearance rollout, reuse the consumer?s existing configuration.
Keep colour, language and compact selection available in Settings, with independent
main-window visibility flags defaulting on. Preserve saved legacy opacity values
when replacing an older alpha path; apply complete-window opacity once instead
of multiplying it again through background styles. Add only the needed preference
fields and verify old configurations retain the new defaults and unrelated values.

Trace the consumer's actual storage before checking appearance persistence:
BFE uses ECommons EzConfig, MOGTOME uses its own System.Text.Json options,
Nav uses Version/Payload envelopes, and MOM/public loaders merge existing JSON.
Public-loader preference merges must respect their release dependency boundary.
DDuck's public host uses System.Text.Json and the existing MOM-style atomic
replacement to retain the shared root identity, version and private/unknown JSON;
its development/private module retains native typed saves. A Newtonsoft envelope
introduced a forbidden public-host reference. Verify packages against the actual
public Release loader as well as the build reference; retain the dependency guard.
A generic Dalamud serialization check cannot stand in for those paths.
Keep existing seeded-collection reload behavior separate from new appearance
regressions; compare against native legacy restore or the existing pure normalizer,
and retain any rejected whole-object result. AutoParty Administration preserves
its script-free CSP, so static opacity is supported and timed fade is disabled;
Lifecycle Lab supports focus fade through its existing page preferences.


For selected complete-window transparency, own one `MaterialWindowOpacity` per
top-level window tree. Call `Apply(windowName, opacity, enabled, autoFade,
fadedOpacity, unfocusedDelaySeconds)` once under its theme after the final native
End and motion/decorations restore, before Render, including collapsed windows.
`WindowSystem.Windows` returns `IWindow`; use that interface for per-window
opacity registry keys. A dictionary keyed by concrete `Window` rejects the
collection's elements, including in the separately compiled private build.
Fractions clamp to .1–1. Defaults are 1 normal, .5 unfocused, auto-fade enabled
and a 10-second delay. Focus in owned children/popups restores normal opacity
immediately; native application focus events retain lost-focus state across idle
frames. Reopening resets the delay. Theme motion and reduced motion control the
transition; fade never raises opacity above the selected normal value.
The component multiplies emitted owner/child/popup/tooltip vertex alpha, covering
native chrome, custom meshes and image tints while retaining RGB, UVs, indices,
geometry, IDs and styles. Shared foreground/viewport drawings and independent
windows retain their own opacity. Avoid separate child instances or another
style/image alpha multiplier on that tree. SurfaceOpacity alone changes themed
backgrounds. Consumers own translated toggles/settings and their selected save
path. Verify native focus loss/gain, delay, raw image alpha, collapsed title,
native actions and saved geometry; standalone results leave host/GPU acceptance
separate.

For a borderless native child, pass `ImGuiWindowFlags.AlwaysUseWindowPadding`
when the design needs inset content; a pushed `WindowPadding` alone is ignored
by that child. Footballer also needed the job-details cursor aligned explicitly
with its character-name column. Check actual child padding, narrow-to-wide
reflow and the complete default-width card row, including a retained scrollbar.

Use Compact density for compact desktop designs, and
`MaterialControls.Push(MaterialControlContext.Toolbar)` or `Dense` for local
rows. `MaterialTable.PushControls()` aligns native and custom table editors.
Enter a control scope after selecting its font. Pass host global scale once.

For tight editable grids, use `MaterialTable.PushTightRows()` after any control
sizing scope and before native `BeginTable`, retaining it through `EndTable`.
The approved ADS Rules pattern uses cell padding `(scale, 0)`: no vertical gap
between editors and one logical pixel beside column dividers. Keep readable
padding inside each editor; let the tallest editor determine the row height.
Do not remove font/frame padding or impose an oversized minimum row height.
`PushTightRows` retains editor geometry and restores styles on disposal. Existing
table defaults remain available; adopt tight rows explicitly for editable grids.
Keep native table/column/editor IDs, inline actions, scrolling, selection, Undo
and explicit Save behavior. Budget dropdown width for its complete translated
value, arrow, internal padding and inline help. Show one Advanced tool group at
a time when several groups crowd the grid. Verify repeated populated rows,
adjacent editor bounds, native editing/actions and scope restoration in both
densities at 100%/150% scale with larger text. The Sizing lab demonstrates five
consecutive rows; ADS English/Russian populated previews established the pattern.

For selected smaller native buttons, check the caller's zero-padding and Dense
state before shaping. Push Toolbar before `MaterialText.PushLineHeight`, or
measure the final caption/icon height explicitly after capturing eligibility.
A later padding scope otherwise erases shaped-height reservation and can enlarge
SmallButtons. Keep original IDs, positive widths, full-width slots and compound
image/action tiles; verify actual wrapper ink, native presses and restoration.
For an explicit-height wrapper that also calls `PushLineHeight`, capture the
Toolbar padding before that reservation. Size the native button from the measured
caption/icon height plus that original padding once. Using the later padded value
with the measured caption double-counts shaped growth, as FrenRider, LootGoblin,
MOGTOME and VMX demonstrated. Preserve any requested positive height as a minimum
and verify actual font roles, large text and both densities/scales.
Explicit component sizes are logical units; measured metrics, native ImGui
coordinates and `MaterialActionTile.Measure` results are already pixels.
Zero/negative sizing conventions vary by component; consult
[adaptive sizing](../../docs/ADAPTIVE_SIZING.md) before choosing fixed widths.

Retain native ImGui editing, focus, navigation, clipping, sorting and disabled
behavior. Consumers configure input/navigation and route any controller events.

First-use guide offers use ordinary titled nonmodal windows. Keep the parent
interactive without dimming or an input-blocking backdrop; neither
`BeginPopupModal` nor `MaterialDialog` meets this requirement. Preserve each
offer's native identity, translations, consumed/visited distinction and owner
cleanup. Escape dismisses only the focused offer. Pair every native `Begin`
with `End`, including collapsed windows, and retire an offer when the parent
opens its guide directly. Verify actual parent interaction and close/save state
at both densities; changing only the dim colour does not establish nonmodal behavior.

For icon-only Cards/Table switches, the `MaterialSegmented.Draw` overload with
`showSelectionCheck: false` retains the selected option's icon; `cornerRadius`
uses logical units. `MaterialChip.FilterChip` also accepts
`MaterialControlAppearance` and an optional selected-check flag for emphasized
filters. The original overloads keep their defaults and native IDs. The gallery's
secondary tab and independent consumer show these opt-in controls.

Set an auto-sized window's required width before native Begin, using the
consumer's PreDraw hook where available. Calling SetWindowSize from Draw can
leave that frame's work rectangle narrow and feed a collapsed width back into
the next frame, as FrenRider Mini demonstrated. Verify the actual window flags,
reopening and density changes without supplying a fixed diagnostic size.

For a retained auto-fit wizard with wrapped content, constrain the native
auto-fit width to the width selected before Begin. Krangler otherwise measured
its content using a different width and clipped the final navigation row. Reserve
the complete fractional lower edge of the last native controls before auto-fit;
verify settling, short-viewport scrolling and fresh reopening with original IDs.
Its fourteen-locale, two-density, 100%/150% checks retained a stable subpixel
scroll remainder while all button ink remained visible.
TTSL Setup separately needed its existing 560-logical-unit width constrained
before Begin: at 150% scale, unconstrained auto-fit fed wrapped content back
into a progressively narrower window. Its fourteen-locale stage/reopen checks
retain automatic height, draft edits and original navigation identities.

Measure wrapped warning children against their actual inner width, including
native padding, border and scrollbar reservation; round the full padded height
upward. DhogNav's narrow Japanese warning otherwise measured as one line but
rendered two. Use the same reservation when budgeting the enclosing card and
its table footer, and verify the final native child content and scroll range.

## LCARS appearance and UI sounds

LCARS is the third `MaterialColorMode`, after unchanged Auto (0) and Picker (1).
Opt a consumer into the third radio with the existing `MaterialColorLabels`
constructor plus `Lcars` and `LcarsHelp` init properties. Localize those captions
through that consumer. Older callers retain their two-choice editor. Selecting
LCARS preserves the saved Picker RGB values and semantic error colours. Its
Background and three bar colours are saved independently; the defaults retain
black/orange/lilac/periwinkle styling. Supply all three `LcarsBar1`–`LcarsBar3`
labels to expose those fields. Captions derive readable black/white foregrounds
from the selected colours.

Create/cache the theme through the existing colour-settings path and use
`theme.IsLcars` to select `MaterialLcars.Header`, `PanelHeading`, `PanelFrame`
and `Footer`. These draw actual elbows, segmented rails and panel bars; a palette
alone is not this skin. Pass translated headings and the current density/scale.
Headings wrap at their measured available width. Paint `PanelFrame` in the
owner's existing background channel, keeping original tables, native IDs, fonts,
controls and actions. `NativeButtonStyle()` adapts retained native buttons with
dark outlined faces and contrasting captions; its accent overload supplies a
specific border colour while respecting explicit semantic fills. Enter it after
control metrics and keep its returned style scope lexical. `ControlBlue` supplies
the approved switch/slider accent; pastel panels remain separate. Reserve the
twelve-pixel PanelFrame band outside content so it cannot cover controls.
`MaterialCanvas.Color` already applies native style alpha;
do not multiply it again.

UI sounds are optional, default muted, and consumer-owned. Cache one
`MaterialUiSoundPlayer` per UI owner and attach `theme.Feedback` when creating
or rebuilding that cached theme. Core hit controls notify only on activation;
retained native wrappers can call `MaterialLcars.Feedback(activated, cue)`.
Do not double-notify nested wrappers, beep on every numeric-drag frame, or let
disabled controls submit sounds. A preview should emit one Preview cue rather
than both Click and Preview.

The player's Click/Toggle/Preview tones are original bounded PCM; no external
pack or dependency is needed. `TryPlay` is explicit and `Error` describes an
unavailable output. Share one saved mute setting between Settings and the
native titlebar. Stop the owned player on mute or leaving LCARS, and dispose it
on unload; do not reset it every Draw frame or stop process-wide sound. The
player owns its waveOut handle/buffer and is used on the UI thread. Offline tests
capture feedback and inspect PCM without opening a real audio device.

FrenRider is the first LCARS pilot. Preserve its existing Main/Mini/Settings/
auxiliary actions; further consumers need their own selected adoption. Follow
the existing publish-before-consumer GitHub Actions guidance when publication
is authorized: verify the new library commit is available, then pin and publish
the consumer. Native CPU renders and tests remain distinct from game/GPU and
audible-output acceptance.

Bind the existing Krangle action using the titlebar overload that accepts its
toggle and tooltip; the older four-action overload cannot show that button.
Call `SetLcarsIcons(theme.IsLcars)` on the retained adapter to select globe,
monitor and droplet glyphs without changing priorities, IDs or callbacks.
Verify the complete Settings owner including selected navigation: LCARS uses
readable black/white text on the chosen second bar colour (lilac by default).
Retained selection wrappers can reuse `MaterialSkin.ReadableInk(actualFill)`
after native submission to select caption ink for the actual hovered/active fill.
For retained selectable captions, keep the native vertical item bounds when
clipping; a nominal line-height clip can trim the original font's descenders.
Consumer-owned pastel status text can lose contrast on Spatial's light surfaces.
Use the existing selected Text role for such text; retain saved palettes and
semantic fill colours.

## Shared Glassmorphism, Spatial and Cyberpunk materials

These are additional modes, preserving Auto=0, Picker=1 and Lcars=2:
Glassmorphism=3, Spatial=4, Cyberpunk=5. Supply all three translated style-name
init properties on `MaterialColorLabels` to expose them. Each owns independent
Background/Controls/Text RGB choices in `MaterialColorSettings`; use
`GetPalette`/`WithPalette` for Picker and the new styles. Do not merge palettes
or treat a recoloured Picker as equivalent to the approved material geometry.

Use the `DrawColors` overload accepting consumer `defaults` and
`defaultAccent` before optional minimum width. Supply translated `Reset` and
`ResetColors` labels for per-role/current-palette resets, retaining other
styles. Auto reset uses the supplied existing seed, not the library seed.
Glass saves `GlassBackground` as `MaterialGlassBackground.Waves`, `Lights` or
`Image`, `GlassLights` as `MaterialGlassLights.AmbientGlows`, `SweepingBeams` or
`PointerGlow`, and the selected `GlassImagePath`. Retain the independent
`GlassWave1Rgb`–`GlassWave3Rgb` and `GlassLight1Rgb`–`GlassLight3Rgb` values when
switching modes; Background/Controls/Text palette resets retain these choices.
Supply `BackgroundMode`, `Waves`, `Lights` and `Image` labels to expose the shared
background selector, then `LightEffect`, `AmbientGlows`, `SweepingBeams` and
`PointerGlow` for all three light effects. Wave editors use `WaveColors`,
`GlassWave1`–`GlassWave3` and `ResetWaveColors`; light editors use `LightColors`,
`GlassLight1`–`GlassLight3` and `ResetLightColors`. `Animation` and `AnimationSpeed`
apply to Waves and Lights: animation defaults on, speed is bounded to
0.1–10x/default 1x, and all three colours remain editable with motion off.

Image editing uses `ImageFile`, `BrowseImage`, `LoadImage` and `ImageHelp` labels.
Use the `DrawColors` overload with consumer `defaults`, `defaultAccent`, then
`Action browseImage` before optional minimum width to invoke the consumer's
existing file picker. Browse alone neither loads nor requests persistence. Draw
the picker independently of the appearance popup; commit its selected path
through the consumer's existing save path. Load commits the edited path, while
the renderer's per-frame `ConfigureImage` owns texture readiness. Translate these
labels, image status and screenshot controls through the consumer's existing
catalogs; preserve its native picker lifecycle and localization boundaries.
Keep drafts outside Draw; save completed edits via the existing configuration,
then rebuild the cached theme at the next frame boundary.

Enter `MaterialSkin.BackdropStyle()` around the adopted owner's native Begin,
dispose it after Begin, then call `MaterialSkin.Backdrop()` first in visible
content under the existing full Begin/End theme. Use `popup: true` around
BeginPopup, disposing before content so nested native pickers retain their
backgrounds. The optional titlebar adapter handles its appearance popups.
Call Backdrop before tables or draw-channel splits; an existing splitter is
rejected before mutation so the owner's native channels remain intact.
This replaces one native background without stacking SurfaceOpacity. Without
an adopted background renderer, Auto, Picker and LCARS retain native backgrounds.
Standard surfaces and controls adopt the
selected geometry: Glass ribbons/frosted panes/coloured glow, opaque raised and
inset Spatial surfaces, or graphite Cyberpunk plates with cyan/ice tabs and
relative magenta details. `Heading` and `PanelFrame` support retained layouts;
paint frames in the existing background channel, reserving LCARS rail space.
Retain explicit appearances, radii, semantic fills and original native IDs.
`Heading` retains supplied casing; localize capitalization in the consumer.
For retained Cyberpunk tabs, submit native `BeginTabItem` under
`MaterialSkin.TransparentTabFace()`, dispose that scope, then call
`MaterialSkin.PaintTab(min, max, selected, capturedAlpha)` in the caller's
background channel while the native tab bar remains active. Preserve native
IDs, bar metrics, captions and EndTabItem ownership; intersect caption clips
with the bar's scrolling region too. `MaterialTabs` and the existing gallery
demonstrate the same chamfered tab geometry.

For native wrappers, `NativeButtonStyle` sets material metrics; draw the native
button under `TransparentButtonFace`, end that face scope and paint
`PaintButton` behind the caption in the caller's background channel. Capture
disabled alpha before ending its scope and pass it to the painter. Use
`PaintButton(min, max, alpha, cornerRadiusPixels)` for a retained explicit
radius in already-scaled pixels; the required fourth argument is never scaled
again and must be finite and nonnegative. Both painters clamp to half the
smaller button extent. The original three-argument overload keeps alpha third
and its scaled 7/3/6 logical-unit LCARS/Cyberpunk/other-material defaults.
Push the same pixel `FrameRounding` after `NativeButtonStyle` when retaining
native face/navigation geometry. The sizing lab demonstrates that pattern. Enter
`NativeFieldStyle` immediately before one single-row native input, after its
width/height scopes. Keep scopes lexical and complete-window opacity applied
once after End. Glass retains bounded procedural Waves and Lights over a ready
Glasswork backdrop, or as ordinary paint while glass is unavailable; theme
reduced motion freezes their animation too.

The showcase Settings and five-action titlebar popup demonstrate the same
shared preferences. FrenRider is the accepted pilot; this library update does
not authorize conversions of other consumers. Preserve their native storage,
public/private merge boundaries and localization. Local native checks and
diagnostic renders remain separate from game/GPU acceptance. See the
[API contract](../../docs/COMPONENT_API.md#shared-colour-modes-and-material-rendering)
for labels and lifecycle.

For selected actual glass, use the shared `MaterialGlassRenderer` in the Dalamud
adapter. It implements core `IMaterialBackgroundRenderer`; core receives only
ImGui/value types and never acquires Dalamud services, textures or a device.
Consumers retain configuration, subscriptions and renderer disposal. The legacy
`MaterialWindowBlur.Configure` helper only enables host-native blur; it does not
provide the approved Glasswork rendering path. Keep native blur disabled on
windows using Glasswork so two implementations do not stack.

Own one renderer for the selected UI owner, constructed from
`IDalamudPluginInterface` and `IPluginLog`. Each UI frame, call
`ConfigureImage(textureProvider, imageEnabled, savedPath)`, then `BeginFrame()`
before recording windows. Keep a lexical
`MaterialSkin.PushBackgroundRenderer(renderer, blurEnabled, frost, opacity: 1)`
around their complete themed Begin/End lifecycle. Enter `BackdropStyle` around
Begin and call `Backdrop` first in visible content, before tables/channel splits.
Pass `null` to the renderer scope when a nested owner must suspend the outer
renderer. After all selected windows are recorded, call
`EndFrame((blurEnabled || imageEnabled) && completed)` in `finally`; use false on
disabled, no-window and failed-draw paths so capture and affinity are released.
Complete the host's render callbacks before the next
`BeginFrame`. Call `ReleaseResources` on the consumer's existing resource-release
path and `Dispose` on unload; the renderer creates no event subscriptions.

`DrawBackground(drawList, min, max, rounding, tint, frost, windowBackground)`
reports readiness. For `windowBackground: true`, pass the current window draw
list and full `GetWindowPos()` through position plus `GetWindowSize()`, including
the title, before any channel split. It prepends native callbacks behind chrome.
Custom skins call this shared renderer explicitly at their Background surface
and retain ordinary paint when it returns false; an enabled toggle alone does
not prove a fresh capture or image. Core `Backdrop` handles this readiness and
retains normal fallback until glass succeeds. Preserve the selected Background
RGB as glass tint; preserve native title/actions, IDs, clips and render state.

Blur changes Background surfaces only. `PanelFrame` treats retained panels as
structural backgrounds; normal control painters retain their foreground fills.
After shared `Backdrop` succeeds for the current root/context/frame,
`MaterialSkin.BackgroundFill(color)` can reveal an explicitly identified
structural fill, and `MaterialSkin.StructuralBackgroundStyle()` can reveal
retained child/table backgrounds. These helpers preserve ordinary fills until
that root is ready. Keep their scope local to structure; never clear button,
input, checkbox, slider, semantic/status or other foreground colours globally.
Custom renderer callers retain the same successful-root guard in their own skin.

Use `HasBackgroundBlur(mode)` and `WithBackgroundBlur(mode, enabled)` for each
style's saved choice. Only Glassmorphism defaults on; Auto, Picker, LCARS, Spatial
and Cyberpunk default off. `BackgroundBlurPercent` is continuous Glasswork Frost,
clamped to 0–100 and initially 75%; pass it divided by 100. Frost is independent
of whole-window opacity and does not change the host's native global strength.
Keep the saved complete-window opacity/fade path applied once after End. Supply
renderer opacity only when that existing path requires it for native callback
output; do not multiply background/image alpha again as another transparency
preference. Supply translated `BackgroundBlur` and `BlurPercent` labels to expose
the per-style toggle and shared Frost control.

Enable image mode only for the selected Glass Image background. Its
`ConfigureImage` call every frame borrows the current
`ITextureProvider.GetFromFile` wrap; never retain/dispose that borrowed wrap in
consumer code. `ImageMode`, `ImageAvailable` and `ImageProblem` describe its state.
`DrawPlainImage` supplies cover-cropped unblurred drawing; blurred images use the
same material/callback path without desktop acquisition or capture exclusion.
Switching image mode/path releases the prior owned source resources. Keep image
loading and consumer file-picker ownership in the adapter/consumer, outside core.

Live detached glass captures the desktop behind only the supplied owned platform
windows and temporarily excludes those windows from OS screenshots/recordings.
Screenshot pause starts OFF and requires an explicit user toggle in both Colour
and Transparency controls, bound to the same renderer state. Read
`ScreenshotsPaused`/`CanPauseForScreenshots` and use `TrySetScreenshotsPaused`;
disable an unavailable pause request while allowing an active pause to resume.
Requests commit at a frame boundary. A successful pause stops desktop acquisition,
retains frozen monitor images and verifies screenshot-visible window affinity.
Resume restores exclusion and waits for a fresh excluded capture before live
glass returns. Image mode needs no desktop screenshot pause. Always restore
capture affinity on disabled, closed-window, release and unload paths.

Ship `AethertekUI.dll`, `AethertekUI.Dalamud.dll`, `Glasswork.dll`,
`Glasswork.Glass.dll` and `Glasswork.Backend.dll`, retaining the Glasswork MIT
notice supplied under the adapter's `Notices/Glasswork.MIT.txt`. The adapter owns
the approved `Glasswork.Backend` package reference; keep its compile/runtime
assets transitive. Reference the host's `TerraFX.Interop.Windows.dll` with
`Private=false`; do not bundle a second host TerraFX DLL. CPU/native tests and
source promotion do not establish current GPU, detached-window or screenshot
acceptance; retain mcvaxius's separate host acceptance boundary.

Optional titlebar `RegisterWindowControls` callbacks expose styled/native UI and
movement/resize lock. Keep a valid `MaterialStyleMode.Native` theme scope for
plain controls, suppress consumer material painting/chrome, and retain window
visibility/actions/fonts. Apply each consumer-owned `MaterialWindowLock` last
in PreDraw and Restore first in PostDraw, before motion restoration. Do not use
IsPinned for this lock: native presets can overwrite it and pinning also changes
Escape behavior. Consumers own independent saved choices and slash commands.
## Grouped settings and Appearance standard

Use `MaterialText.SingleLine` for summary captions that must stay within one
row. It fits a grapheme-safe ellipsis to the available width and restores inherited
wrapping. Choose the summary's intended entity before bounding its text; keep full
rosters and diagnostic payloads in existing detail surfaces, not giant tooltips.
When an approved design summarizes accounts, collapse repeated session observations
in presentation using existing identity rules, without altering transport records.
Keep unresolved identities and conflicting live sessions explicit. Check long
captions over several frames, both densities/scales and larger text.

For the approved three-colour editor, use `MaterialAppearanceSelector.DrawColors`
in both the native-titlebar Colour popup and Settings. Show Auto and Picker as
radio options, with Auto as the default. Auto retains the consumer's existing
automatic palette, seed and preset/custom RGB controls exactly. Picker stores
independent Background, Controls and Text RGB choices in consumer-owned
`MaterialColorSettings`; retain them when switching back to Auto. Apply the
settings to the existing automatic scheme with `settings.Apply(scheme)`, or use
the colour-settings `MaterialTheme` constructor/`MaterialThemeSettings.Colors`.
Do not replace a consumer's established Auto palette with the library default.
Picker keeps its chosen base RGB values exact, derives related surface roles,
and preserves semantic error/status colours. Do not silently adjust user colours
to enforce contrast. Keep all windows and popups on the same choice.

Keep the editor's colour/seed drafts outside Draw. `DrawColors` updates the
drafts during native editing and returns true only for mode/preset changes or
completed RGB edits. Save through the consumer's existing global appearance
owner at that boundary, and rebuild the cached theme at the next frame boundary
when the draft changes. Supply translated `MaterialColorLabels`, measure the
longest row and preserve the original Auto `DrawPalette` ID root. The showcase
Settings and native-titlebar preview share this editor; FrenRider is the first
selected product. Other consumers still need their own selected integration,
native/save checks and user-controlled host acceptance.

Bind cached editor drafts to the actual configuration owner as well as its saved
values. MOGTOME can switch account profiles with identical appearance values;
refresh the draft at frame and editor entry when that owner changes, and save
through its existing per-account route. For public/private hosts sharing JSON,
extend the native merge/load/copy/save paths together and preserve unknown data,
contract identity and separate language ownership. Check actual compiled hosts;
stubbed release guards do not prove appearance persistence. Apply Picker settings
to nested alternate themes too, and keep fixed decorative-fill shortcuts from
overriding a manually selected Background.

A selected browser companion uses its native appearance controls and storage
instead of referencing ImGui. AutoParty implements Auto/Picker with the same
three RGB roles in its header and Settings, retaining existing GET/hash
preferences and the administration page's script-free CSP. Browser checks and
synthetic save callbacks do not establish live host or game acceptance.

Use the approved grouped Appearance layout through `MaterialAppearanceTab.Draw`:
Theme & Language and Main window controls on the left; Window opacity and When
unfocused on the right. Pass translated `MaterialAppearanceSections` labels and
consumer-owned callbacks. `minimumColumnPixels` is measured in current-font pixels;
include the complete translated controls, units, help and conditional sync actions.
Two columns appear only when both fit; narrower windows stack complete groups.
Use `MaterialSettings.Section` for compact semantic headings and
`MaterialAppearanceSelector.DrawPalette` for inline colour swatches and the retained
custom-colour popup. The showcase Settings page demonstrates the standard.

A theme scope changes colours, metrics and chrome; it does not rebuild a
consumer's layout. For an approved redesign, replace the selected view's actual
row/card composition and bind each control to its existing value and compound
change/save handler. An opaque `DrawNode` callback retains its original label
placement and control order. Do not accept that callback inside newly painted
panels as evidence that the approved layout was implemented. Inspect the actual
native rows and rendered geometry against the approved board.

Dispatch retained widgets from an explicit set of supported field/value types,
not presentation attributes alone: BMR's `GroupDetails` also decorates an enum
array. Preserve the original custom/unknown renderers and `DrawCustom` route.

For selected wrapping checkbox/radio captions, use the explicit pixel-width
overloads:

```csharp
if (MaterialSelection.Checkbox(nativeLabel, display, ref enabled, rowWidthPixels))
    ExistingChangedHandler(enabled);
if (MaterialSelection.Radio(nativeLabel, display, option, ref selected, rowWidthPixels))
    ExistingChangedHandler(selected);
MaterialSettings.Field(nativeLabel, display, editorWidthPixels,
    label => ImGui.InputInt(label, ref count, 1), rowWidthPixels);
MaterialSettings.Section(displayHeading, rowWidthPixels);
if (MaterialSwitch.Row(nativeLabel, display, ref enabled, onText, offText,
    rowWidthPixels, helpText)) ExistingChangedHandler(enabled);
```

The new selection overloads submit a native full-row hit target with exactly
`ImGui.GetID(nativeLabel)`, wrap the display caption within the measured row
and return its change. They retain native keyboard/navigation, inherited disabled
state and alpha, and mark changes edited. Unlike the older component overloads,
they add no component ID scope. Retain the exact original label, including
`##`/`###` suffixes, and preserve its original parent root when moving it into
a presentation table/card. The old overloads and their IDs remain unchanged.

`MaterialSwitch.Row` uses that same native-ID contract for a wrapped label with
a trailing shared switch track and fixed ON/OFF text reservation. Measure complete
rows with `MaterialSwitch.MeasureRow(display, onText, offText, widthPixels, help)`
at the same font/control scope used for drawing; paired cards can then reserve
the taller group's actual wrapped height. Optional help appears as an inline
`(?)` marker and on whole-row hover through a native tooltip; pressing that marker
leaves the setting unchanged. The library owns no translated labels or state.
For paired cards, opt into `MaterialSettings.Columns(..., divider: false)`.
It removes the separator and outer cell inset, uses the current theme gap between
the columns, retains caller row spacing, and restores both caller IDs and styles. The four-argument Columns
overload retains its existing separator, padding and reflow threshold.

For the approved thin linear slider, use `MaterialInput.SliderTrack` beside the
retained numeric editor. Its width is logical; native slider input, focus,
disabled state and its existing component ID scope remain intact. Idle saved
values outside the range stay unchanged. Keep logarithmic ranges on their native
editor and route completed changes through the consumer's original handler.

`MaterialSettings.Field` reserves a wrapped label on the left and the caller's
native editor on the right, stacking when the row cannot fit both. Widths are
pixels already measured at the active font/host scale;
never scale them again. Its callback receives an empty label under
`PushOverrideID(originalNativeId)`. Submit exactly one editor with that supplied
label and no extra ID scope; its `GetID("")` and native scalar step-button IDs
then match the original widget. The callback retains native editing, flags,
bounds, enum choices and value ownership; the consumer runs the original save
or compound handler on the returned change. This contract does not relocate
an arbitrary callback's hard-coded caption or extra controls. Split/rebind that
selected consumer row explicitly. Native last-item data remains the editor's
afterward, so existing hover/help checks still target it.

The Sizing lab's appearance-titlebar preview includes a retained-settings example
demonstrating wrapped captions, a trailing switch,
radio row and left-label/right-editor bindings. Focused native checks cover
all six materials, both densities and 100%/150% scale, including exact native
identities, lower-caption/switch clicks, keyboard activation, disabled state, scalar
step buttons, narrow stacking and restoration after a consumer callback throws.
These checks establish control behavior; the adopting consumer still needs
actual board-layout and host-font verification.

Keep related toggle/value/unit controls on one row, use native tooltips for longer
help, and retain subordinate indentation. Do not recreate a generic
Label/Value/Help/Sync grid or reserve empty help/sync columns. Preserve every existing
setting, conditional action, native ID, profile/global boundary and save handler.
These presentation components own no configuration or automation. Verify actual
wide/narrow renders, native edits/actions and scope restoration; a concept image or
successful build alone does not establish visual fidelity or game acceptance.

Inside settings subframes, budget the complete row against that child's available
width: translated label, editor minimum, units, reset action and help. Keep them
inline when they fit. Put default/override state on the existing reset action's
tooltip, including its disabled inherited state; retain the original action ID
and mutation/confirmation handlers. Preserve semantic section headings.

A cursor to the right of a window's origin does not establish inline overflow:
table cells, indentation and groups also start there. `MaterialLayout.FitNextItemWidth`
must only move an exhausted native `SameLine` continuation, never a fresh cell or
indented row. Size numeric columns from the same editor minimum and scale as their
wrappers, and keep hidden-label editors free of separate label items. One
`TableNextRow` per item does not prove that its editors share a baseline. Check
several populated rows inside nested/indented children, both densities, larger
fonts and 100%/150% scale; preserve the existing true-overflow wrapping check.

## Native titlebar actions

For ImGui windows with native titlebars, place Language, Compact, Transparency,
Colour and Krangle shortcuts alongside existing window actions. New and adopted
UIs show Krangle by default on main, mini and auxiliary windows. Use titlebar
icons with localized tooltips. Compact and Krangle toggle directly on left click;
Language, Transparency and Colour open their existing editors in popups. Retain
full appearance controls in existing Settings. Body-header shortcuts require an
explicitly selected exception. Retain existing custom title strips for titleless
windows and existing browser layouts; this standard does not require new chrome.

For the approved one-line status rollout, use the separately named
`MaterialWindowHeader.PaintTitleWithStatus(window, visibleTitle, texture,
textureSize, status, extraRightWidth, showCloseButton)`. Its
`MaterialTitlebarStatus(Text, ShortText, Tooltip, Color)` is in `AethertekUI`.
Keep the original `PaintTitle` signature for existing/reflected callers and use
it when the consumer's Show titlebar status preference is off. Consumers own the
approved saved Show/Detailed preferences, translations and existing runtime data;
do not add collection, synchronization or new counters for presentation fields.

Keep Status and Detailed status in Settings > Appearance, with both off by
default. A consumer with main and Mini windows keeps independent saved choices
for each window in its existing configuration. Detailed status remains saved
when Status is disabled; disable its editor until Status is enabled. Derive
both views from the same existing workflow snapshots, prioritize the active
parent workflow, and preserve character-name privacy in text and tooltips.
For QC's `NoTitleBar` main window, paint within its existing custom title bounds
and reserve the timer and action cells; the native title painter skips that
flag. QSTComp Mini uses the after-End native status painter. Retain complete
tooltip details and one final opacity application for either route.
Capture a retained titlebar timer's paint values in the body, keep its hover
warning under that owner, and emit its text after the native title repaint.
Caption-glyph removal can otherwise erase matching timer letters. Preserve
the captured font, size, colour and actual timer reservation.

When borrowing a replacement font after End, capture
the native font with `ImGui.GetFont()` before the borrow and pass it through the
`PaintTitle`/`PaintTitleWithStatus` overload accepting `nativeCaptionFont`.
Botology's private-atlas regression showed that scanning only the paint font and
IO.Fonts can leave every original caption glyph visible. The original overloads
remain available; the independent consumer demonstrates the explicit font route.
The shared painter retains icon/title/version first, then a semantic dot and
quieter measured status in the existing remaining rectangle. Full text gives way
to ShortText, then a grapheme-safe ellipsis; no meaningful fit emits no status ink.
The existing shaped-text scope applies to measurements and rendering. Supply
complete localized tooltip details even when the title text is concise or clipped.
Hovering the non-button title region shows those details while expanded or
collapsed without an item/hit ID. Only for that tooltip, a balanced second owner
Begin/End retains native ancestry for the existing opacity pass. Preserve the
after-End/motion-restore/before-opacity lifecycle and existing action reservations;
do not grow the window or move its native title controls.

For Dalamud windows following this placement rule, use
`AethertekUI.Dalamud.MaterialAppearanceTitleBar.Register(window, labels,
drawLanguage, toggleCompact, drawTransparency, drawColour, toggleKrangle,
krangleTooltip, visibility)` for new and adopted UIs. `toggleKrangle` is an
`Action`; `krangleTooltip` is a `Func<string>` read on hover for the localized
current On/Off state. The fifth UserSecret icon invokes the existing saved
privacy toggle directly on left click. Reuse the consumer's existing masking
paths and actual privacy semantics, including privacy in status text/tooltips.
The library owns no privacy configuration, service, global preference or name
detector and does not automatically redact arbitrary strings. VERMAXION is the
selected reference integration, replacing its hand-built duplicate with the
shared action. Other consumers still require selected wiring and a rebuild;
a library update cannot retrofit every plugin.

Own the returned adapter per existing Window and register once after the owner's
other title actions. Supply translated `MaterialAppearanceTitleBarLabels` and
existing editor/save callbacks. The original overload ending in
`drawColour, visibility = null` retains its four-action compatibility contract.
Compact toggles the existing setting immediately; Language, Transparency and
Colour are separate manual popups. Move existing body-header shortcuts into these
actions unless an explicit exception is selected, retaining the grouped Appearance
tab and existing account/profile persistence. Do not introduce another preference.

Pass existing main-window visibility flags with the optional
`Func<MaterialAppearanceTitleBarVisibility>` callback. Its four positional fields
and constructor remain unchanged; Colour defaults true when the three existing
flags are supplied. The non-positional `Krangle` bool init property defaults true;
use `visibility with { Krangle = false }` only for an explicitly selected exception.
Visibility does not force the saved privacy state on. Mini and auxiliary windows
can omit the callback to show all five actions. Call `RefreshVisibility()` before
native Begin so hidden controls do not reserve title space. Five descriptors are
created once and reused by the standard overload, four by the compatibility path.
Refresh only attaches/removes those descriptors, preserving original custom
descriptors, priorities and relative native indices. The current host sorts
priorities descending before assigning IDs; new appearance priorities sit below
the existing minimum. The endpoint XML's contrary priority description is not
the current rendering contract.

Call `DrawPopups()` after native End and motion restore, before complete-window
opacity, including collapsed frames. A central host may instead call it after
`WindowSystem.Draw`, consistently in that same ID scope every frame. Popup IDs
include the owner's WindowName/hidden identity, and pending opens are consumed
before drawing. Only while opening/showing a popup, the adapter enters a second
balanced owner Begin/End with no body or decoration draws. This native parent
scope keeps popup focus and meshes in the owner's existing opacity tree, including
collapsed owners. Preserve the current title repaint, motion and opacity lifecycle.
Verify original custom indices through main visibility changes, left-click-only
actions, Krangle's live localized state tooltip and existing save/masking paths,
and collapsed native popup edits. Native popup interaction tests do not prove
managed IconFont rendering or consumer/game acceptance.

For a selected Dalamud titlebar rollout, register fixed `Window.TitleBarButtons`
once on the existing window owner. Preserve existing registrations and their
priority order: the native host derives custom-button IDs from that order.
Use consumer-owned callbacks that accept left clicks, re-read current eligibility
and call the same compound handler as the retained body action. Translate the
current action/reason tooltip; native title buttons have no disabled property,
so an unavailable action must remain inert. These callbacks run while collapsed,
outside body Draw and its local font/text scopes; keep the consumer's full host
theme/localization/shaping scopes around that route.

Keep every selected main body control except appearance shortcuts covered by
the placement rule above. Relocate other mini navigation shortcuts only when
explicitly selected, retaining mini run controls, information and contextual
item menus. A popup belonging to the body keeps its original ID scope: request
it from the title callback and open it from that scope when Draw runs, expanding
the owner only when the selected action requires it.

Dalamud scales `IconOffset` logical coordinates once. Reserve current native
font size plus item spacing per visible icon, together with close, optional
right-collapse and host settings controls. Consumer title repaints must clip
before that reservation. Preserve existing title-helper signatures used by
reflection; use a separately named owner-aware helper when needed. Keep motion,
corners, opacity, window IDs and saved geometry unchanged.

Keep the owner's `MaterialWindowMotion` lifecycle around native Begin/End.
The host reserves font size plus inner spacing beside Close; larger horizontal
frame padding can intercept the first custom button. `Prepare` retains that
limit for left-collapse layouts. Native right Collapse and Close are only one
font width apart, while each hit rectangle includes twice `FramePadding.X`;
a positive padding limit still overlaps them. For right-collapse layouts,
`Prepare` temporarily uses zero X when Close or adjacent custom actions are
present, including custom-action windows that hide Close. `DrawChrome` restores
the original X before body controls, and `Restore` handles collapsed frames.
Keep both calls in their original order; Y and the caller's styles remain intact.
Direct native Begin/End owners with Close and right Collapse need the same
zero-X scope around Begin only, restored before body drawing. IconOffset changes
icon ink, not hit regions. Check the native Close/collapse boundary and first
custom cell's right edge with actual hover transitions and clicks, including
collapsed windows and fractional scales; center presses alone missed I494.
Model native button bounds from the title rectangle inset by `WindowBorderSize`.
Mouse coordinates are floored; native button positions remain fractional.

The independent ConsumerContract example shares its retained Enabled handler
with one native title shortcut. Verify actual native pointer routing, collapsed
callbacks, fresh blockers and title/control bounds at both densities/scales.
Calling a descriptor delegate directly proves callback logic only. An offline
probe of the installed host's unavailable-icon fallback exercises native hit
routing but does not prove managed IconFont readiness, icon ink or game rendering.

For approved real-image branding, consumers acquire their existing packaged icon
through the current texture provider. Public/private modules resolve the icon
beside the running public host assembly, including LocalDev, rather than beside
an encrypted/private module. Core borrows the handle through render submission.
`MaterialCanvas.DrawImage(drawList, texture, textureSize, min, max, tint)` takes
pixel bounds, fits/centers the original aspect ratio and changes no layout/ID.
Reserve the existing scaled box unconditionally; missing textures draw nothing.
Keep the logical `Image` overload and existing helper signatures intact.

Check the runtime ZIP separately from installer image sidecars. The installed
DalamudPackager 15 default removes `images/icon.png` from the runtime ZIP and
copies it beside the ZIP for installer use; a `DalamudPackagerFile` item alone
does not change that task. A file getter needs its image inside the runtime
package at the same DLL-relative path used by direct and custom-folder builds.
Retain existing nested installer assets when adding a runtime root copy.
Compare each actual image with its source bytes; an old ZIP or a successful
direct build cannot establish current package placement.

Call `MaterialWindowHeader.PaintTitle(native, visibleTitle, texture, textureSize,
extraRightWidth, showCloseButton)` after final native End and motion/decorations
Restore, before complete-window opacity. Supply current native CalcFontSize-based
custom/host button reservation in pixels; the API budgets native Close/collapse.
Add one font-size image box plus inner gap to the retained minimum title width.
The API masks original glyph alpha instead of painting over native backgrounds;
it retains native controls when borrowing a different font or size. Geometry
comes from Begin's stored InnerRect decoration height, subtracting the menu offset
and splitting title/menu height when MenuBar is set. Native title helpers instead
recalculate from the current font and are unsafe after a font change. Keep Begin's
FramePadding.Y through repaint; motion may restore X. Include unequal-size fonts
and MenuBar in expanded/collapsed checks.
Use a nonintersecting owner title clip. Native collapse applies on the next Begin;
check that frame before asserting its result. Focused native checks cover image
aspect/bounds, unavailable-image spacing, separate paint fonts, expanded/collapsed
actions, native collapse/close and one opacity multiplier at both densities and
fractional scale. These checks do not establish host/GPU texture acceptance.

## Identity, localization and resources

Use only `mcvaxius` or `dhoggpt` for user attribution. Never include the
user's personal name in authored UI text, changelogs, commits, documentation,
comments, reports, or agent output. Inspect affected text before delivery
and report any identity matches without repeating the private spelling.

Keep IDs independent of translated labels. Use persistent model keys in
`PushID`, explicit component `id` arguments, `MaterialOption` keys and reordered
`MaterialSegment.Key` values. Index segment IDs suit fixed lists only. Raw
native controls can separate translated text and identity using `###stable-id`.
Preserve existing internal ImGui IDs during refactors and rebranding.
When replacing native Combo option submission, inspect the binding overloads.
This host's managed PushID(index) selects a pointer hash; native Combo uses a
four-byte integer. MOGTOME retained its original nine option IDs by calling
ImGuiNative.PushID(index). Preserve existing pointer-ID loops elsewhere; changing
those scopes merely to resemble C++ changes their established identities.

IDs include their parent window, table and tree scopes. Moving an established
control into a presentation child or table can change its ID even when its label
is unchanged; preserve the prior ID root when retaining that control.
Native tab bars also supply an ID scope. A tab item's NoPushId flag suppresses
only that item's scope; derive diagnostic identities from the remaining bar scope.
Appending `###original` to a translated label does not preserve the hash of an
existing plain `original` label. For those controls, pass the original raw label
to the native widget and paint the translation from its bounds, sizing for the
translated text. Retain native arrows, focus and disabled appearance as well.
Account/profile editors keep their viewed
selection separate from the runtime active account/character; browsing settings
must not redirect automation. Key choices by persistent IDs even when displayed
names are obfuscated or duplicate, and retain in-progress text edits across frames.
Keep account/character selection visible and separate from settings categories.
At narrow widths, label the profile picker explicitly. Follow a different profile
from selection through its editor and save path; check both account and character
identity before exposing runtime-only actions. Inspect runtime/bridge callers
before changing an active-account helper into an editing-account helper.

A horizontally scrollable native window can expand its work rectangle to prior
content width on later frames. Wrap explanatory paragraphs to the visible pane,
accounting for horizontal scroll and narrower native columns, while retaining
single-line controls and real content overflow. Footballer's Polish Settings
otherwise clipped complete notes despite using TextWrapped. Verify after content
expansion over several frames, checking complete paragraph height and glyph ink.
A helper that pushes wrap position zero overrides its caller's explicit wrap limit.
DDuck public's failure footer uses translated native text under the visible-width
limit; an outer wrap scope around its retained helper left that limit ineffective.
Measure this limit from the native InnerClipRect so scrollbar space is included.

Keep ordinary control and profile labels on one line. Measure translated text,
icons, gaps and padding; move whole controls to another row instead of squeezing
them into a letter-width cell. For native wrappers, use
`MaterialLayout.FitNextItemWidth(requestedPixels, minimumPixels)` before sizing
the widget. Its widths are already pixels; preserve a measured readable field
minimum and provide horizontal scrolling or a wider pane when that minimum
exceeds the pane. Capture pending field widths before drawing a separate label,
which consumes native NextItemWidth. Retain existing helper IDs when changing
wrapping; do not derive a new control identity from the resized label.
Numeric fields must budget the readable editor plus both retained native step
buttons and their gaps. Round native field minima upward: ImGui's item-width
calculation floors fractional pixels. Verify presses across the original and
wrapped widgets, including the subcontrol IDs and release, rather than checking
only an idle numeric group's ID. ImGui may report that group's idle ID as zero.
Inspect the installed binding's argument defaults before wrapping: its current
`InputInt` defaults to zero steps. Preserve that behavior and reserve step-button
space only when the caller actually enables steps; interaction fixtures must
pass explicit steps when testing those buttons.
For a native checkbox with an empty visible label, reserve only its rounded-up
square: there is no label gap. HFH's extra gap pushed rule checkboxes off their
regular row. Reserve adjacent actions before sizing a retained editor, as its
name field and K action required.
For retained native columns, budget the scrolling content from the sum of each
actual initial width and translated minimum, taking the larger for each column.
HFH's minimum-only budget left its German Remove column squeezed after wider
earlier defaults; its strict thirteen-caption native scroll check reproduced the
failure and passed after the complete width budget was restored. Keep existing
column initialization and resize identities; do not reset user widths to fit art.
Keep retained data sections rendered when a small viewport leaves little height;
reserve a usable native table minimum and let the containing window scroll.
HealBot Watch otherwise omitted all retained targets at its new default size.
Budget table content from translated headings and displayed row values, then
use native horizontal scrolling while retaining existing column resize IDs.
Its stretch Name column otherwise collapsed to eight pixels.
For retained two-column detail rows, compare measured translated label widths with
that column's allocated share. ADS keeps its reference ratio when labels fit,
otherwise requests a fixed native label minimum including bearing inset and cell
padding. Labels stay single-line; explanatory values wrap within their own column.
Check actual glyph ink as well as advance widths at the native column clip.
For native captions at a table's left boundary, reserve the leading glyph's
negative bearing at the active font/scale and include it in the width budget.
DAD's Chinese Task caption otherwise lost a fraction of its leading glyph.
Compare text origins or baselines when checking row alignment: accents naturally
have different ink tops. Retain complete-ink and native-clip assertions.
Give label/value telemetry tables local row padding instead of inheriting padding
intended for action or party rows. TTSL's inherited padding inflated its metric
cards, while its automatically sized label column wrapped Position at the default
width. Measured translated label minima and local metric padding corrected both
without changing the Party table's explicit row heights or native table IDs.
A cursor inset alone leaves a retained panel using its enclosing content width.
Confine the native work/content bounds and clip to the panel's inner padding,
keep the prior ID root, and restore those bounds/scopes before sibling content.
TTSL's heading separators crossed card edges until this boundary was supplied;
verify caller restoration, native actions and complete ink at narrow sizes.

Measure and wrap translated canvas placeholders at the retained font, centering
complete glyph ink within the usable square and restoring canvas text state.
TTSL's 96-pixel empty radar clipped its fixed-x caption; fourteen-locale
checks of both embedded browser pages verify the correction. A missing origin draws no origin marker;
retain populated-point coordinates and prove both branches independently.

Natural and fill-width library buttons now reflow while preserving their full label;
positive explicit sizes retain caller control. Keep the current assembly version
visible in main title bars while preserving native window IDs. Distinguish the
runtime character from the selected settings profile.
For a selected native companion, derive its displayed build version from the
existing project version authority and retain its window class/control IDs.
TTSL's existing CMake build reads the managed project's `Version` for its native
caption instead of maintaining a second release number.

FrenRider's translated tabs require SetNextItemWidth for the complete label and
native FittingPolicyScroll when shrinking would clip it. With retained close
controls, also budget the original raw caption's native geometry. HFH's shorter
Korean caption otherwise crowded the close hit area at 150 percent; reserving
the larger original/translated width retained native navigation and full ink.
Check tab-body hover/press separately from the native close control.
Padding changed only
inside BeginTabItem did not preserve translated widths. Verify selected-tab ink
after native scrolling settles within a finite frame limit. Its selectable rows
also showed that a clip based on nominal line height or text advance can crop
fractional-height descenders and glyph bearings; retain native window/table clips
for those single-row labels. All fourteen locales passed the corrected paths.
Custom combo previews also need room for a glyph's negative left bearing inside
their existing padding. ChokeAbo and Dheacon reproduced clipped leading T ink in
Vietnamese and Turkish when the preview clip began exactly at the text origin.
The combo now admits the existing preceding gap, preserving the text origin,
arrow boundary, field dimensions and native IDs; the native regression checks
both preview routes, densities and scales with a supplied negative bearing.
BFE's scrolling tab strip contributed its complete width to a horizontally
scrollable parent's content size and expanded the next frame's work rectangle.
Bound that tab strip's contribution and reflow viewport while preserving real
body overflow and restoring the native work rectangle after drawing. Clip custom
caption ink to the native scrolling rectangle and verify the complete selected
caption after scrolling settles. Set tab padding before BeginTabBar so its height
reserves the full tab frame. Hiding ImGuiCol.Text during the first BeginTabItem
also hid its native scroll arrows; repaint the retained caption in its actual tab
background while preserving arrow ink, focus and IDs. Check visible arrow
triangles and native adjacent-tab presses, as well as widths.

DhogGPT retains translucent tab fills by clearing only the original caption's
glyph alpha in the emitted vertex buffer, then drawing translated ink inside
the native scrolling bounds. Keep arrow/close geometry intact. Native tab bars
retain scroll positions after narrowing; verify current-frame bar identity and
visible arrow hit geometry through each draw command's clip and vertex offset.
Follow native settling and the actual configuration change when checking saves;
a display-only adjacent tab may legitimately save nothing and retain composer focus.

Paint a retained title outside the native body's clip rectangle; intersecting that
clip can make the entire version invisible. Reserve native close/collapse controls
and check actual title glyph vertices or a render, as ChokeAbo established.
The selector sizing guidance below applies to popup/Settings editors or
explicitly retained layouts; it does not prescribe body placement. The standard
appearance selector measures the selected language plus its arrow and leading
Globe mesh and padding, retaining that minimum in narrow panes. Reflow the entire
selector group within those editors or retained layouts; native callback overloads
still own their field sizing.
When an explicitly retained layout places existing actions between colour and
language, use MaterialAppearanceSelector.DrawAccent and DrawLanguage with the
same selector ID. They preserve the combined selector's accent and language IDs.
MaterialComboLayout.LeadingIcon uses native meshes; retain the field's borrowed
draw list while BeginCombo makes its popup current, so the icon stays in the field.
DrawAccent's explicit positive logical size supports approved larger swatches
while retaining the same selector/accent IDs and change events. Pass a Vector2
for a rectangular colour field with Palette mesh, swatch and chevron; the whole
field opens the existing popup. Use the float overload for a square swatch.
Both preserve the accent/button identity, preset/custom events and disabled state.
The rectangular Palette respects the current pixel `IconSize`, bounded by field
height and the gap before its swatch. Footballer's approved larger field needed
explicit 30/32 logical icon sizes at compact/regular density; multiply by host
scale once when supplying metrics and verify the actual rendered icon ink.
Configure the
language field's height through an explicit MaterialControls.Push(metrics)
scope; the existing selector inherits those scaled pixel metrics.

DrawAccent's fixed Teal/Blue/Pink preset fills use opaque black labels.
Use MaterialColor.Contrast on the actual fill/foreground pair in idle,
hover and pressed states; MaterialButton's .08/.14 foreground layers can
lower contrast on press. The selector regression checks at least 4.5:1
with native rendered fill samples and glyph colours while retaining IDs,
caller sizes and separate change events.

Measure translated labels using the active host font. Budget pane, column,
button and popup widths for longer translations; use wrapping tiles where
appropriate and check clipped compact labels. Provide tooltips when necessary.
There is no universal fixed width that fits every language. The host supplies
and manages fonts with the required CJK glyphs; the library does not load fonts
or guarantee localization coverage. Test actual host fonts and display scales
when an integration is selected.

When selected typography work must retain the host atlas, a lexical
`SetWindowFontScale` scope can adjust presentation while restoring the caller's
`FontWindowScale`. Leave borrowed `ImFont.Scale` and `FontGlobalScale` unchanged.
Remeasure fields, cards and translated reflow under that window scale. DhogGPT's
passive headings reserve measured glyph ink rather than an oversized font line
box; reference-size English Main/ultra checks fit in both densities. Verify native
IDs and a non-default caller scale separately. Offline targeting must measure
the rendered caption and follow wrapped controls through native scrolling;
pre-Draw font metrics or large wheel jumps can miss a reachable control. Measure each group with its actual local font scale. DhogGPT's ultra preferences use regular-header typography in a separate top row, retaining the original root IDs and approved chat-language field widths instead of squeezing those fields to fit new selectors.

Consumers own plugin subscriptions, commands, windows, disposal, configuration
storage, game actions, sorting/filter rules and invalidation. Keep domain status
colors explicit; an accent change must not redefine game status meaning. Fonts
and textures are borrowed and must stay alive through render submission. Do not
dispose host resources from library helpers.

Ship `AethertekUI.dll` beside the plugin and `AethertekUI.Dalamud.dll` when used.
Keep host-owned `Dalamud.dll`, `Dalamud.Bindings.ImGui.dll` and `cimgui.dll` out
of plugin distributions. Dalamud SDK hosts resolve their normal references;
Microsoft.NET.Sdk hosts must resolve the same installed binding/distribution.
Package identities are `AethertekUI` and `AethertekUI.Dalamud`, version 0.3.2;
local package generation is separate from normal build verification.
For a protected module host, resolve core and adapter references to the exact
assemblies already loaded by the public host; retain its dependency allow-list.
Verify compatibility against the actual deployable host as well as its build-only
ABI reference. MOM's supplied failure showed that updating the private access
DLL/JSON pair leaves an outdated installed public loader untouched. Treat public
host replacement and its version choice as a separate release step; equal version
metadata cannot distinguish older and rebuilt binaries. Offline compatibility
verification does not authorize host installation or publication.

For DDuck, MOM and DhogNav private modules, ClientStructs is owned by the
running Dalamud host. Resolve it through the load context of
`typeof(IDalamudPluginInterface).Assembly` with only the requested version
cleared; explicitly check the remaining assembly identity, the compiled version
floor and the actual Dalamud directory before returning that host instance.
CLR binding does not enforce every identity check on a custom cross-context
return. Keep contract, core, adapter, embedded and other dependency checks exact.
Build the module and host/reference against the same current supported bundle;
do not download or ship another ClientStructs DLL or add recurring version pins
to code or launchers. The three private AGENTS.md files carry this session rule.
Use actual deployable-host resolver checks and authenticated package verification
as distinct evidence. Installed old public loaders still require the selected
user-controlled replacement path; local checks do not establish game acceptance.

## GitHub Actions provisioning

For every consumer adoption, determine how its CI and release builds will obtain
AethertekUI. A local sibling checkout does not exist on a fresh Actions runner.
Keep CI changes within mcvaxius's selected scope and obtain his persistent-machinery
choice before adding dependency provisioning or credentials.

Pin the AethertekUI checkout to a published library commit that contains the
consumer's required APIs, and update that existing ref when adopting new APIs.
Verify the dependency commit is available before releasing its consumer; local
sibling builds can hide a cross-repository publication race. MOM's failed run
selected the preceding library commit two seconds before shared Discord/Ko-fi
icons arrived. Inspect the Actions checkout revision and compiler error together;
matching 0.3.0 version metadata does not prove matching API surface. Keep the
existing read-only credential and do not add retries or publish a dependency
without the user's authorization.

When Actions provisioning is selected, use native `actions/checkout` steps with
the consumer and AethertekUI in sibling folders inside `GITHUB_WORKSPACE`.
Checkout paths cannot escape that workspace. Run the build from the consumer
folder and adjust artifact paths to include its checkout folder. Build the plugin
csproj directly; adopting AethertekUI does not imply running either repository's
test projects. Install an SDK compatible with the library's `global.json`
(currently 10.0.201 with `latestPatch` roll-forward). Pass the downloaded API 15
distribution as `DalamudLibPath` during
restore and build so Microsoft's SDK-based library project resolves the same
binding as the plugin. Restore for the same configuration, platform and runtime as the build; include
`Configuration=Release` when intermediate paths depend on configuration.
Stop on a failed restore. An old generic `win` lock target can conflict with
x64 on the pinned SDK: remove only that obsolete empty target through native
restore, inspect the lock diff, and retain every package version and local feed.
Keep one tag-release owner per repository so duplicate workflows do not race
to replace the same assets. Use the packaged manifest for untagged release
versions instead of a hard-coded version in YAML. Preserve native-server
assets and public-host/private-module boundaries; a UI adoption does not
convert protected release tools into an ordinary full-plugin release.
Verify the release ZIP includes `AethertekUI.dll` and the
adapter when used, while excluding host-owned assemblies.

A public consumer's `GITHUB_TOKEN` cannot read the private AethertekUI repository.
Prefer a separate read-only AethertekUI deploy key for each consumer, with its
private key stored only as that consumer repository's Actions secret. Botology
uses `AETHERTEKUI_DEPLOY_KEY` through the library checkout's `ssh-key` input.
Set `persist-credentials: false` so checkout removes the key before build steps.
Give the build's `GITHUB_TOKEN` only `contents: read`; grant `contents: write`
only to an already authorized release job. Never reuse a broad personal token,
commit a private key, or include it in logs or artifacts. Remove temporary local
key files after configuring GitHub. Windows SSH key generation needs a local
filesystem that supports key permission changes; a mounted drive can fail while
saving the key. Use a temporary local directory outside repositories, remove it
after provisioning, and verify read-only key metadata and the consumer secret
name without printing private material. A deploy key can be revoked on AethertekUI;
its matching consumer secret must then be replaced or removed.

Public viewers can see a secret's name in the workflow, but cannot read its
value or gain private-repository access merely by viewing or forking the consumer.
Repository secrets are available only to that repository's authorized workflows;
fork pull-request runs do not receive them. Workflow editors must be trusted:
code running with a secret can disclose it despite log masking. Do not run
untrusted pull-request code with this credential or add `pull_request_target`
access as part of adoption. Shipping the compiled library DLL makes that binary
public; it does not publish the source repository or its deploy key.

## Bound UI revision rounds

For each selected UI change, use at most three implementation-and-check rounds.
Then stop visual adjustments and leave further refinement to user feedback.
Keep verification focused on the selected defect; retain failed and untested
results honestly. User feedback can authorize another bounded change.

## Evolve through approved designs

Before implementation, measure the approved reference's content envelope,
region bounds and gaps, typography roles/weights, representative colours, artwork
and state assumptions. Inventory main, mini, compact, public introduction and
auxiliary windows from current source before preparing mockups. Confirm which
items are editable, read-only or disabled; do not invent actions from sample art.
Keep those inputs in the consumer's existing presentation
constants and affected documentation. Map every selected window and popup to its
approved design or explicitly retained layout. New mockups are not implied for
retained windows. Native chrome, added selectors, real data and agreed semantic
changes are specific differences; do not use blanket masks to hide discrepancies.

Implement the selected approved mockup faithfully: layout, proportions,
density, hierarchy, interaction and status semantics. When it exposes a missing
component or layout capability, improve AethertekUI and demonstrate the addition
in the showcase. Do not reshape the approved design to fit the current controls.
Retain the MD3-inspired dark/OKLCH foundation while adding capabilities required
by actual integrations. Keep game behavior in the consumer.

## Standard UX redesign and deployment

For every mcvaxius-approved AethertekUI UX redesign, include the standard languages below and
whole-theme colour selection as standard features unless mcvaxius explicitly changes
that scope. Carry them into any separately authorized deployment; they are not
Botology-only options. Keep implementation in the selected consumer and honor
explicit limits on builds, tests, packaging and client operations.

Full and compact modes are standard for every selected UI, including previously
adopted Botology and Footballer. Prepare compact mockups alongside regular
mockups before adoption; include mini windows and the website generated by a
companion application when selected. For windows with native titlebars, specify
appearance shortcut icons in the native titlebar in both regular and compact
mockups and their generation prompts; "header" alone is insufficient. Follow the
placement rule above, with a localized `Compact mode` tooltip for its direct
titlebar toggle. Preserve existing compact-setting IDs when reusing a control.
Share its density choice across the consumer's windows and retained layouts.
Reuse existing preferences; the consumer owns any approved persistence through
its current save path.

Compact mode reduces padding, gaps, row/card height and title prominence while
keeping labels readable and actions, semantics, optional columns and saved
choices available. Use wrapping and scrolling when space is limited. Do not
treat a separate mini window as the main window's compact mode, shrink the whole
window with a global scale transform, or reset user widths to fit the mockup.
Keep fonts/glyphs, all advertised languages and colour selectors usable in both modes.

Share colour and language selections across every window and popup, retaining
their full editors in existing Settings. Follow the appearance shortcut
placement rule above; use `MaterialAppearanceSelector` in the existing
popup/Settings editors with teal/blue/pink presets and a custom RGB picker.
Use these stable codes and native dropdown names:

| Code | Dropdown name |
| --- | --- |
| `en` | English |
| `de` | Deutsch |
| `fr` | Français |
| `es` | Español |
| `it` | Italiano |
| `ru` | Русский |
| `ja` | 日本語 |
| `ko` | 한국어 |
| `zh-Hans` | 简体中文 |
| `vi` | Tiếng Việt |
| `pt-BR` | Português (Brasil) |
| `id` | Bahasa Indonesia |
| `pl` | Polski |
| `tr` | Türkçe |

The approved cross-plugin expansion selects Vietnamese (`vi`, Tiếng Việt),
Brazilian Portuguese (`pt-BR`, Português (Brasil)), Indonesian (`id`, Bahasa
Indonesia), Polish (`pl`, Polski) and Turkish (`tr`, Türkçe). Add them to every
selected surface using its existing resource/configuration route. Verify accents,
all displayed authored messages, embedded catalogs, selection/persistence and
translated layouts before advertising support. Retain every existing locale and
persisted ordinal, including existing pt-BR/zh-Hant choices; do not duplicate an
already supported locale. Apply equal nonempty key/placeholder coverage to all
advertised languages, including these additions.
Hindi (`hi`) requires Devanagari shaping beyond glyph presence. The approved
Windows route uses `MaterialTextLayout`, consumer-owned `MaterialTextRenderer`
and optional `MaterialTextHost(ITextureProvider)`; no third-party shaping package
is selected. Scope the full window/status draw path, including font-status early
returns. Route measurement and painting together through `MaterialText.Measure`,
`AddText` and `Text`. A scope alone does not intercept raw ImGui labels/tooltips
or consumer wrappers. Material buttons, action tiles and text/search fields use
this route; consumer catalogs and remaining controls still need integration.
Displaying a usable हिन्दी menu caption requires shaping in every locale.
Retain native glyph checks for existing scripts and validate authored Devanagari
through shaping before excluding its glyphs from atlas validation.

For retained native wrappers, `MaterialText.BeginCombo` and `Selectable` preserve
their supplied native labels/IDs, arrows and popup lifecycle. Raw
`MaterialText.ReplaceNativeCaption` repaints a consumer's retained native label
after submission. Capture the starting vertex and font before the control, and
pass only its caption bounds; an editor's value rectangle must stay outside those
bounds, even when the value contains the same glyphs as its label. The sizing lab
and independent consumer demonstrate this separate external-caption route.
`MaterialText.TreeNode`/`TableHeader` preserve the existing identity root and accept
an optional shaped display caption while retaining the original native label.
Keep caller-owned `TreePop` and reserve shaped table-header row height before
native submission. The raw tree retains existing vertical padding around shaped
ink, including unframed rows; complete-ink checks exposed top clipping at 150%.
Text/tooltip helpers and explicit-font `AddText` share the shaped route; `Text` respects native wrap
positions. `MaterialShapedInput.SingleLine` is public for existing wrappers,
including an empty label under the owner's `PushOverrideID`. Reserve its line
height with `MaterialText.PushLineHeight(value, hint)` instead of changing IDs.
Keep combo preview clipping at the full native frame: clipping at increased
FramePadding cuts shaped vertical ink and negative bearings, as the native Hindi
selector checks demonstrated. Retain arrow reservation and parent-window clips.
Single-line editors admit leading glyph overhang into the existing native frame
padding when unscrolled, without moving the text origin, pointer or caret. Keep
the padded clip during horizontal scrolling. Nav's Hindi Blacklist hint exposed
a clipped leading pixel at 150 percent; hint/value regressions cover both
densities/scales alongside native caret, IME and horizontal-scroll checks.
Do not compensate by adding text spaces or shifting the editor origin.

`renderer.CheckGlyphs(authoredText, fontSize)` checks actual shaped glyph runs
without bitmap/texture uploads. Validate each original font-role size separately
and retain atlas checks for all non-Devanagari characters. A text scope alone
does not make the atlas contain Hindi or prove consumer readiness.

Allow native DirectWrite fallback when the preferred family is absent; retain
actual glyph/cluster/ink validation. Do not reject a whole ordinary-language
generation solely because its optional Hindi menu name cannot be shaped. Remove
only that appended menu caption from mandatory required text, never catalog or
user-authored/raw values. Inside the existing generation check, probe that caption
with `TryCheckGlyphs(optionalText, fontSize, out error)` at every retained role.
It uploads no texture and adds no availability cache. Disable only the stable-key
Hindi option with ASCII `Hindi (unavailable)` when coverage fails.

Keep a saved Hindi selection explicit: use native ASCII loading/error status and
an accessible `Use English` action through the consumer's existing save/admission
route. Never silently rewrite the locale or drop required shaped text. Retain
failed mandatory coverage until a genuinely new generation can be checked; native
handles being temporarily unready must not erase its error. Readiness and font
files remain different from DirectWrite system-family availability under Wine.
`MaterialText.Selectable` accepts an ASCII display override without changing the
original native label/ID, including disabled entries. Native absent-family fallback
and unavailable-glyph rejection pass on Windows; Linux/Wine acceptance stays open.

Pass the active scaled font size once and reserve the shaped natural line height
before drawing fields. Native IDs, focus, clipboard and undo remain native;
Backspace dismantles conjuncts by codepoint and replacement paste retains native
undo steps. ImGui 1.88 ImWchar16 needs transient editor tokens for supplementary
and private-use values; tokens never enter saved strings. Retain proxy routing
after the last Hindi character is removed. Host-truncated supplementary keyboard
input, password fields and game-rendered DTR/SeString text remain outside the
demonstrated editor bridge.

Use `MaterialShapedInput.Multiline(nativeId, ref value, maxLength, pixelSize, flags)`
for a retained multiline editor; `MaterialInput.Multiline` routes its own field
through the same bridge. Preserve the original native ID and source bytes,
including CRLF/lone-CR, supplementary characters and private-use values. The
native child retains both scroll axes, clipboard/undo and capacity limits while
shaped line geometry supplies pointer selection, vertical navigation and IME
position. Keep protocol, JSON and encrypted values opaque. Actual host IME
acceptance remains separate from the standalone native checks.

Declare a mixed tab bar's visible captions before native Begin with
`MaterialTabs.Begin(id, captions, flags)`, or reserve their shaped line height
around the owner's `BeginTabBar`. Increasing padding only inside a tab cannot
increase the bar-owned height. Preserve explicit caller sizes and native IDs.
For retained tabs with an original raw label and shaped display caption, declare
its measured width before native BeginTabItem, preserving an explicit caller
width. Include the native padding once and clip painting to the complete item
rectangle; padding pushed only inside a tab left DDuck captions too narrow.
Reuse MaterialText.Selectable for shaped native labels and their leading bearings.
Mixed-caption diagnostics must inspect native-font ink for retained terms that
do not require shaping, rather than requiring a shaped texture for every caption.

For retained checkbox labels with shaped ink, round the caption reservation upward
and retain the native-label minimum when adjusting ItemInnerSpacing; clamp the
adjusted spacing nonnegative. DhogGPT's shorter Hindi Enabled caption otherwise
lost four right pixels at the native item clip. Preserve its original label/ID
and keep existing spacing on unshaped paths. Diagnostic combo-preview lookup must
include the consumer's deliberate leading-space layout and still check complete ink.

Tree/tab scope constructors restore native pushes when shaped uploads fail;
consumer wrappers must also close an open popup and restore their clips/IDs in
`finally`. Filter atlas input with rune-aware `MaterialText.NativeGlyphText`
only after validating authored Devanagari through the shaped glyph runs.

Renderer scopes are context-bound and LIFO. Borrow layouts for the current frame
only; keep textures alive through Render even after an explicit EndFrame. Clear
and dispose outside the frame after submission. Diagnostic rendering resolves
actual RGBA uploads, tint and clipping. Native group bounds round fractional
extents, so editing/IME fixtures obtain origins from drawn quads or native input
bounds. `/aethertekui hindi` previews this without changing existing showcase
pages. Keep consumer rollout and game/GPU/IME acceptance separate; advertise Hindi
only after the selected surfaces pass.

Translate all consumer-authored windows, headings, controls, tooltips and popup
text. C# consumers use keyed resources embedded in the assembly and native .NET
resource loading. Selected C++ or browser companions use their native resource
mechanisms and controls; do not introduce a managed dependency just for these
features. Cache the selected resource set. Keep key coverage equal across all
advertised languages; English fallback must not conceal unfinished translations.
Check every localized value for nonempty text wherever English is nonempty.
.NET compiles empty resource values, so matching keys and null-only lookup
guards can still leave visible controls blank. Reject empty translations in
the existing consumer resource check and inspect actual localized output.
Compare the exact key sets, including existing non-English-only keys, rather
than counts alone. Resolve every advertised locale's embedded resource set;
a selector entry does not prove that selecting or restoring that locale works.
The .NET resource compiler treats key names case-insensitively. Use one canonical
key for status spellings that differ only by case, and normalize the UI lookup
without changing runtime messages or native IDs. Check key uniqueness and format
placeholders as well as coverage. Keep private source phrases in the approved
translation environment; external translation services require explicit approval.
Preserve plugin names, external
catalog content, command tokens and logs. Format UI numbers and dates using the
selected locale without changing process-wide culture.
Include the selected culture's `NumberFormat.NumberGroupSeparator` in required
glyph text when displayed typed values use grouping. French `N0` uses U+202F,
which can be absent from resource strings and baseline font ranges. ChokeAbo,
LootGoblin, TTSL and ADS verified this correction across their advertised catalogs
and every original French font role, preserving role heights/files/merge order.
Check formatted samples against production ranges as well as required text;
digits already covered by baseline ranges are not missing product glyphs.
Translate authored interpolation arguments explicitly. Keep names, IDs and
already formatted service arguments raw, including leading zeroes; apply locale
number formatting to typed UI values instead of reparsing arbitrary text.
Generic label/value renderers need an explicit raw-value path as well. DAD's
saved preset named Regular party was translated by its KeyValue helper. Translate
the caption and provenance-known empty-selection prompt separately, then render
the exact saved name; verify label collisions in both wide and stacked layouts.
When a metadata lookup also returns an authored fallback, expose the unresolved
state separately for UI formatting. TTSL retains its public telemetry fallback
while translating Main's missing-area label; resolved names resembling that
fallback and published account identifiers remain opaque.
Match complete authored status wrappers before splitting lines so captured
LF/CRLF provider or exception detail stays intact. VerMAXion collection also
requires source-specific composition for intuition/spectral state, readiness,
cleanup and return suffixes. Use the known outcome and native supply-name
boundaries, including names containing colons; keep provider-owned detail opaque
even when it equals a UI label, and avoid translating the completed output twice.
Verify configuration round trips using the real host's serialization policy.
A seeded collection can be appended by Newtonsoft during reload: VerMAXion's
saved task order needed replacement semantics to retain custom order. Offline
checks may call verified pure serializer helpers; they must not construct host
storage services or operate clients.
Check every reached enum member, including failure/readiness values, in the
resources; localizing an interpolation does not translate a missing Busy label.
Prefer templates with authored leading text to catch-all leading placeholders.
DDuck's distance template otherwise consumed an entire member/HP diagnostic;
verify the nested displayed string and its separate empty form through the
compiled helper, with raw abbreviations and padded numbers retained.
Keep punctuation-only envelopes such as `{0} {1}: {2}` out of global
service-template matching. DAD's leveling compiler uses that shape around raw
slot/character identities; localize it through the source-specific display
formatter. Parse joined compiler details only within that context and constrain
slot captures to the producer's slot grammar so a later slot cannot swallow an
earlier summary. Keep exception details raw even inside an authored wrapper.
HealBot Watch similarly needs exact UI template composition for its retained
states and an explicitly translated authored prefix. Its complete resources did
not make leading-placeholder states reachable through the generic label lookup.
Trace composed statuses through execution/proxy getters as well as direct UI
calls. ADS verified an ownership wrapper around a Higher/Lower service summary
and retry/blocked detail. Check displayed row outcomes alongside templates.
Generated literal/wildcard message templates can use `RegexOptions.NonBacktracking`
to avoid timeouts from ambiguous adjacent captures, as DDuck's compiled status
checks verified. Check full, empty, nested, padded and LF/CRLF captures after the
change. Keep source-specific lookaround parsers separate: DAD's leveling patterns
contain lookaheads and cannot use that option.
Verify captured values through the consumer's installed runtime, not just match
success. ADS's named fields lost leading-zero and terminal-newline detail with
the non-backtracking route; ordinary anchored matching retained it. Keep the
established finite match timeout when restoring ordinary regex (20 ms in ADS),
and verify that bound alongside the exact raw captures.
For a catalog whose literal/wildcard templates repeatedly time out during Draw,
consider matching pre-split ordinal literal segments. LootGoblin verified this
route with ordered templates, raw/empty numbered arguments, repeated placeholders
and the retained final-newline behavior. Check actual displayed messages and
native window states; a literal matcher does not replace a parser whose grammar
needs alternation, lookarounds or other regular-expression semantics.
Before constructing or running a source-specific lookaround matcher, check a fixed
authored literal that every successful match must contain. DAD's joined leveling
blocker otherwise entered unrelated expensive patterns despite its prefix gate.
Retain template order, lookaheads, capture grammar, raw values and timeout failures;
the prefilter skips only patterns whose necessary literal is absent.
For literal/wildcard status catalogs, skip candidates whose authored first-line
prefix does not match before entering the existing bounded regex. TTSL's
already-localized Publisher display otherwise timed out in Settings Draw.
Preserve candidate order and the existing timeout, and verify every reached
template with raw captures. Absolute ordinary-regex anchors retain terminal
LF/CRLF details; TTSL checks those captures rather than only match success.
For large status catalogs, create a retained matcher only after its authored
leading literal applies. DDuck's eager non-backtracking construction exceeded
the diagnostic memory limit after the atlas was ready; deferring unused matchers
kept native selector runs below 521 MiB. Retain template order, raw captures and
LF/CRLF behavior; use a conservative first-line prefix before the existing pattern.
When hiding a native field label to retain its original ID, remove that label's
layout contribution as well as its ink. DhogGPT's measured field/card checks
caught invisible label widths expanding content bounds and scroll ranges.
Trace displayed service statuses through their getters and helper-returned
messages; resource parity alone does not prove that those statuses are covered.
When matching existing service-message templates, preserve empty arguments and
their numbered identities. Match templates before trimming whitespace: a trailing
space can represent an empty final argument. Check an actual displayed diagnostic as well as key
and placeholder parity; complete resource files can still fail to translate it.
If a producer trims an empty final detail, keep a reachable resource for that
shortened form as well as the complete template. DAD's completion summary
established this case. Translate nested captures only at positions traced to
authored role/module labels or summary/safety getters; arbitrary names, counts
and IDs remain opaque. Verify both full and empty forms through the compiled
helper, rather than changing the producer's runtime message to suit UI matching.

Footballer and MOM exposed terminal-newline failures in the installed runtime:
`$` can leave a final LF outside a capture, while a `NonBacktracking` pattern
ending in `\z` matched that input but returned empty argument groups. Its
bounded ordinary matcher with `\A`/`\z` retains both the name and complete
LF/CRLF detail. Test actual captured values, including an empty argument and a
terminal newline, before selecting a regex engine; a successful match alone is
insufficient. MOM's fifteen-locale startup checks also verify raw empty/padded
details and nested readiness wrappers with the bounded ordinary matcher.
Keep this runtime-specific evidence distinct from other hosts.

Structured diagnostic events need their producer schema as well as resource
coverage. AutoParty wake events use `state:wake:<phase>:<summary>`; split at
the first payload colon, then translate phase and summary separately. Preserve
colons and empty text inside the summary. In composed status rows, explicitly
translate authored enum/conditional/summary arguments before formatting; keep
owner, participant and character identities raw. DAD Mini established this
distinction through actual compiled helper checks.
Multiline status matching must tolerate the producer’s LF/CRLF literal line
breaks without normalizing captured raw details. BFE verified both prefix forms,
empty arguments, padded values and exact line endings inside the capture. XML
readers can normalize resource-value line endings; account for that in source
comparisons while checking raw helper arguments separately.

Reuse the consumer's existing configuration and save path. Prefer `UiLanguage`
(default `en`) and `UiAccentRgb` (default the approved reference accent), or reuse
equivalent preferences already present. Old configurations receive missing
defaults without resetting other values or changing their version. Commit
custom RGB on edit completion, not every draw; apply resource changes at the
next frame boundary. Consumers own translations, preferences, fonts and textures;
do not add a shared settings service.

Use relative OKLCH colours for the whole theme: backgrounds, panels, fields,
table surfaces, borders, foregrounds, gradients and neutral badges. Preserve
each measured role's lightness and relative hue/chroma; gamut-map the result and
choose contrasting foregrounds for filled accent controls. Use the existing
`MaterialColor` conversions, `GamutMap` and `Contrast`; OKLCH hue is in radians.
Keep the approved default colours exact. Grey seeds produce neutral decorative
roles; dark and pale seeds must retain a usable light/dark hierarchy. Keep assessment, ignored,
unavailable, update and DTR status meanings and external brand colours explicit.
Changing only primary controls while leaving decorative slate colours fixed is
incomplete. Build and cache the palette on preference changes, outside Draw.

Fonts must cover the selected resource set and every native dropdown name,
including CJK names when English is selected. Build and check the same required
text set, including English fallback strings and untranslated UI symbols such
as native favorites and support labels. A symbol-face merge adds only the glyphs
requested by its ranges. Prioritize the selected locale's
CJK glyphs and retain Latin/Cyrillic coverage. In Dalamud, use managed atlas
handles and explicitly merge the host's `DalamudAsset.NotoSansCjkRegular` when
needed (verified bundled TTC faces: Japanese 0, Korean 1, Simplified Chinese 2,
Traditional Chinese 3). These bundled faces have identical glyph coverage;
merge one face per role, selecting the active locale's regional forms (Japanese
0 for other locales), rather than merging every CJK face. Preserve required
ranges, font sizes, symbol merges and host-language coverage. Read the
collection's PostScript face names and recheck coverage when the host asset
changes; do not guess its face order. Do not pass neutral
codes such as `ja`, `ko` or `zh-Hans` to `AttachWindowsDefaultFont`: unsupported
cultures can silently add nothing. Check actual managed-handle readiness, load
errors and glyph coverage after atlas rebuilds. Dispose consumer-owned handles;
never distribute Windows fonts. Temporary host fonts cannot qualify for visual
acceptance.

For the selected suite CJK rollout, set `build.NewImAtlas.TexDesiredWidth`
and `TexDesiredHeight` to 4096 inside each existing managed `OnPreBuild`
callback, including rebuilds. Preserve original roles, heights, ranges, merge
targets, symbol/host-language merges and handle lifecycle. Reuse linked
public-root builders in private modules instead of duplicating them. MOM retains
its seven standard roles and two existing body handles at their density-specific
heights. BFE demonstrated this host-packing workaround; 4096 is a measured
starting size, not a universal atlas-size guarantee.

The installed TTC's PostScript name table was checked on 2026-10-07 and confirms
JP=0, KR=1, SC=2, TC=3, despite a conflicting order in the endpoint API XML.
Check the actual asset when it changes. String-code consumers recognize both
`zh-Hans`/`zh-CN` and `zh-Hant`/`zh-TW`; enum consumers retain their native
language selection.

Capturing and executing production PreBuild delegates verifies original/rebuild
configuration, role counts, merge order and disposal. Use the consumer-owned
required-text set when it constructs ranges internally; use supplied text for
callers that accept explicit ranges. A bounded native bake proves only the
checked glyph set. These
checks do not prove complete displayed-text coverage, managed readiness, Hindi
shaping, language/scale rebuild acceptance or game/GPU rendering. Keep finite
time and memory bounds and leave those host acceptance criteria explicit.

On 2026-10-06, BFE's Debug/x64 build through `Z:\bfe.bat` passed with zero
warnings and errors after this correction; the launcher was left unchanged.
mcvaxius then reported that BFE loaded instantly in the user-controlled Account 1
test. This establishes initial managed-host load acceptance for BFE, not a
measured load time or acceptance for other consumers. Language changes, complete
glyph coverage and scale rebuilds were not separately reported as tested.

Segoe UI text faces omit some symbols used in existing tooltips, including
U+26AB (black circle). Merge the installed `seguisym.ttf` through the managed
atlas for required symbol coverage, alongside the selected CJK merges, in every
font role that can render those strings. Validate the actual required glyphs;
report the missing code point/role instead of suppressing the check. Font files
remain on the host and are never packaged.
Read/write source and resources as UTF-8. Windows PowerShell's default native
pipe encoding can replace non-Latin translations and diacritics with `?`; use
explicit UTF-8 transport and inspect the resulting characters.
For .NET diagnostic runners, set `Console.OutputEncoding` to UTF-8 before
capturing text and decode the log accordingly. DhogNav's Japanese captions
otherwise became question marks despite intact resource values and passing native
glyph checks. Distinguish diagnostic encoding loss from damaged product catalogs.
When integrating a saved translation payload, read it directly as UTF-8 and
compare the written values with that payload before mirroring shared phrases.
DAD's tail passed structural checks after PowerShell had changed its accented
text to question marks; Python UTF-8 mode cannot recover text already damaged
by the native pipe. Keep the intact payload as the input for the correction.
Matching keys and placeholders cannot detect encoding loss. Inspect actual
resource values for native characters and unexpected literal `?` replacements,
especially in CJK, Cyrillic and accented Latin resources, before claiming coverage.
Inspect unexpected single question marks as well as repeated ones: DAD's Duty
Finder help lost Latin accents while retaining valid keys and placeholders.
Compare written values with intact UTF-8 input; ordinary question punctuation
remains valid. Trace a short label's displayed caller when checking semantic
quality: combat Party means a group, Ready describes readiness, and unrelated
second lines can survive structural checks in colour presets and status labels.

Before declaring the redesign ready for deployment, verify embedded resources,
library payloads, preference persistence, all-language layout/glyph coverage and
theme coverage across the selected windows and popups. Include presets, a custom
colour, grey/black/white selections and independent semantic status colours in
the selected verification route. Report deferred checks explicitly; follow the
separate functional, diagnostic and game acceptance evidence below. Deployment
readiness does not authorize installing, packaging or publishing anything.

Botology's first adoption establishes these practices:

- `MaterialColorScheme` roles can be assigned during initialization and stay
  immutable afterward. `MaterialTheme(colors, density)` accepts exact sampled
  roles. Apply the relative theme requirements above. A consumer that already
  owns a cached theme can wrap `WindowSystem.Draw` directly with
  `MaterialTheme.Push(theme, ImGuiHelpers.GlobalScale, mode)`; the optional
  adapter is not required for that route.
- For compact rectangular toolbars and status cells, use
  `MaterialButton.Draw(id, label, cornerRadius: 4, ...)` and
  `MaterialStatus.Badge(label, cornerRadius: 4, ...)`. Radius and explicit sizes
  use logical units; the original overloads retain pill shapes and native IDs.
  Generic Document, Database, Refresh, Chart, ExternalLink and Download icons
  use native meshes without font or texture ownership.
  ChocoboColourized's save, copy, support and feeding-order affordances also use
  `MaterialIcon.Save`, `Copy`, `Heart` and `List`. Reuse those native meshes in
  retained controls while preserving their IDs, actions and localized tooltips.
  HealBot's combat actions and priority rules use appended `Sword` (crossed blades) and `Star`
  meshes under the same ownership and identity rules.
  ChokeAbo's preset workspace and breeding affordances use appended `Folder` and
  `Egg` meshes; preserve their native controls and localized tooltips.
  ADS uses appended Wrench/DoorExit/Cube/Chest/Cart/Chat/Group/Warning/Plus;
  LootGoblin uses Pulse/Utensils/Crystal; DAD uses Calendar/Crown/Book/Sparkle/Bell/Terminal.
  FrenRider's account-wide enable action uses the appended Power mesh.
  LootGoblin's Current Run uses Target, the appended crosshair; Location stays a pin.
  BFE's area heading and support action use appended MapPin and HeartOutline.
  Community actions use appended Discord (66) and KoFi (67) meshes, retaining
  native actions and caller sizing/opacity. The shared Discord has transparent
  eye openings; Ko-fi retains its red heart. Check the smallest consumer size:
  antialias fringes can fill a tiny eye, and a Bezier handle's complete stroke can
  exceed its control-point bounds. Native 14/36-pixel checks cover both cases.
  Append required artwork without changing existing ordinals. Keep icon drawing
  independent of font and texture resources, with actions and identities in consumers.
- `MaterialControlAppearance` supplies fill, foreground, border and logical
  radius to button, badge, search and combo overloads. Badges accept logical
  dimensions; switches accept an explicit logical track size. Rounded gradient
  `MaterialCanvas.Surface` keeps pixel coordinates/radius. Explicit
  `MaterialControls.Push(metrics)` values are already scaled pixels;
  parameterless nested scopes inherit them, explicit contexts remeasure, and
  theme/control scope disposal restores the prior state. Showcase and compiled
  consumer examples accompany demonstrated reusable gaps.
- Measure retained table columns from translated headings and actual row text.
  Native fixed-column requests are content widths; budget cell padding and
  borders separately in the scrolling table's inner width. ChocoboColourized's
  narrow Timers otherwise starved its stretch column despite a wide inner table.
  Keep ordinary cells on one line and verify native scrolling reaches the final
  column. Bound the table height to its rows and viewport instead of filling a
  mostly empty retained surface; preserve IDs, raw names and model data.
- Reserve footer height before sizing a scrolling table. Preserve optional
  columns and allow horizontal scrolling instead of resetting saved preferences.
  Assign native column user IDs from stable column identities, consume native
  sorting specs, and include a persistent row-key tie-breaker. Sort captured
  versions numerically and keep unavailable metadata sortable.
- A mockup's sample text is not a data contract. Bind source badges to actual
  provenance, installed cells to captured versions, and update cells to
  availability unless the host supplies a target version. Keep assessment
  colours with warnings and DTR controls with actual visibility state.
- A sibling `ProjectReference` supports local development, but a consumer-only
  checkout cannot build it. Identify library provisioning as a release
  prerequisite; do not change publication or CI as part of an approved local
  UI pass. Use `-p:Use_DalamudPackager=false` for Dalamud SDK build checks when
  release packaging is outside the selected scope.
- Repair existing consumer build launchers to enter the library's required
  environment, select its pinned SDK and build the plugin csproj directly.
  Propagate the build exit code; keep test execution in the explicitly selected
  verification route. Inspect all selected plugin's batch aliases, retain their
  existing development/public/private modes, and resolve retired wrappers to the
  maintained native tool. Use `%~dp0` paths and preserve the caller's directory.
  For AethertekUI, enter `eng/Enter-RepoEnv.ps1` before
  building project references so `DalamudLibPath` is supplied. With a lockfile
  containing DalamudPackager, restore normally and then build with `--no-restore`
  and `-p:Use_DalamudPackager=false` to avoid removing it from the lockfile.
  Already-restored packager targets can still run despite that build flag.
  Isolated `--artifacts-path` outputs do not isolate the source manifest used
  by DalamudPackager. Serialize builds with overlapping project-reference
  graphs while those targets are active; ADS and FrenRider checks reproduced
  an exclusive-lock failure on their shared FrenRider manifest.
  For compilation checks with SDK 10.0.201, use
  `-p:ImportProjectExtensionTargets=false` to exclude restored NuGet build
  targets. This SDK does not reference `ImportNuGetBuildTargets`; that older
  flag alone does not suppress its restored packager. Consumer-owned post-build
  targets still need inspection. Retain normal imports for release packaging.
  Test projects also need their normal test SDK targets: compile the plugin
  with suppression first, then build/run the test project with normal imports
  and `-p:BuildProjectReferences=false`, using the freshly built plugin.
  VERMAXION verified this route with its live aliased assembly and matching
  dependency bytes; no test harness or new dependency was needed.
- When wrapping a native toolbar, compare the last item's right edge with the
  content region's right edge before `SameLine`. After a widget advances to the
  next line, `GetContentRegionAvail()` alone describes that next line and can
  incorrectly keep every subsequent group on an overflowing row. Include the
  measured translated label, icon, track and gaps in each group width.
- Check the installed binding API before choosing newer ImGui helpers. ChokeAbo
  established that this host lacks `ImGuiChildFlags` and `AddEllipseFilled`: an
  autosizing native table can provide a balanced card scope, while a closed
  convex path draws the required ellipse. Retain the previous ID root when
  introducing the table. A copied helper from another binding is not build evidence.
- For an autosizing card built from a retained native table, measure cell content
  before `EndTable` and paint its rounded fill/border between
  `ImGuiP.TablePushBackgroundChannel()` and `TablePopBackgroundChannel()`.
  DhogNav's public introductions otherwise painted the measured background over
  the foreground. `PadOuterX` retains the horizontal cell inset; keep the previous
  ID root and balance the channel/table scopes. Verify translated content and
  safe native actions at narrow widths, both densities and supported scales.
- Inside native BeginGroup, a positive SameLine offset is relative to the group.
  Do not include the window-local cursor X again; DDuck Mini stock rows otherwise
  double-counted the left inset and exceeded the viewport. Use group-relative
  offsets there and retain window-local coordinates for SetCursorPosX. The fixed
  rows passed native action, target-edit and width checks in all fourteen locales.
- Offline fonts may differ from the live managed atlas and conceal a broken
  merge. A clean build or CPU render does not prove host font readiness. Give
  loading/error windows containing wrapped text an explicit width every frame,
  including after opening; retain their native IDs and balanced Begin/End.
- A selected server-rendered browser companion can retain a strict script-free
  CSP while loading a same-origin stylesheet. AutoParty administration uses
  native GET appearance controls, validated URL preferences and translated
  embedded resources, preserving those preferences in each POST action and its
  successful local redirect. Keep anti-forgery fields, exact Host/Origin gates,
  action bounds, escaped aliases and redacted identities intact. Verify actual
  compiled HTML/CSS and browser layout without starting production services;
  a presentation check does not prove a live administrative action.

Read [component API](../../docs/COMPONENT_API.md) for control signatures and
[integration guide](../../docs/INTEGRATION_GUIDE.md) for host/data boundaries.
Update this skill as real integrations establish better practices; record
demonstrated guidance, not speculative infrastructure.

## Local verification

Use installed SDK 10.0.201 and the repository-local environment:

```powershell
Set-Location 'Z:\aethertekUI'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\Build.ps1 -Previews
```

The child process dot-sources `Enter-RepoEnv.ps1` before tooling and leaves
machine/user execution policy unchanged. For direct toolchain commands, enter
that environment in the calling process first. The normal route builds Debug/x64,
runs existing library tests and checks the
independent consumer. Inspect `artifacts/previews/page-0.png` through
`page-7.png` as diagnostic CPU renders of actual native ImGui meshes. They use
a default font and fixed background; GPU compositing and game/CJK fonts require
later host acceptance. Offline hosts that merge CJK may produce multiple native
atlas textures. Refresh and assign each texture its own ID, then resolve the
draw command's actual texture when rendering; sampling texture zero for every
command corrupts otherwise valid glyph UVs. Sample RGBA texture colour as well
as alpha: a font-only alpha sampler turns coloured supplied images into white
blocks. Footballer exposed this in its job icons; the shared renderer now checks
opaque colour, tint and transparency. Earlier coloured-image diagnostic renders
need regeneration before visual acceptance. HealBot's Japanese helper diagnostic
verified this distinction. It does not establish managed-host readiness.
Give a standalone diagnostic atlas explicit dimensions suitable for its required
glyphs and roles, and record those dimensions with the result. BFE and
ChocoboColourized's complete Korean windows passed with their unchanged fonts,
ranges and merge order at 4096 by 4096, after default packing exceeded its bound.
Keep finite time and memory bounds; include measured host overhead when setting
the full-window bound. A successful standalone bake does not prove the managed
host's atlas configuration or readiness.
When a complete offline multi-role atlas exceeds its memory bound during font configuration,
check required glyphs in separate original-role processes and native interaction with
the actual role used by those controls. ADS's nine roles and four CJK merges used
763 MiB before baking; separate role checks stayed below 236 MiB. Retain the
required text, original heights and merge order, and record every process bound.
Separate glyph checks do not prove combined-atlas, whole-window or managed-host readiness.
Later ADS Main/Quick runs retain all nine original roles and merges at 4096x4096
within a finite 90-second/1536-MiB combined diagnostic bound. The fifteen-locale
matrix passes; root independently verifies English and Russian, with the latter
using 1316.922 MiB. Keep the earlier smaller-bound failure distinct and budget
measured native/render overhead. This still does not establish managed-host readiness.
For standalone checker references, inspect MSBuild's actual ReferencePath and
ResolvedFrom after ResolveAssemblyReferences. Retained ignored DLL outputs can
resolve through {CandidateAssemblyFiles} before an explicit HintPath; the
shared checker's search paths exclude those candidates. Use the normal build's
actual core output, and require identical core bytes in checker and product
before accepting results. A successful rebuild or equal file sizes is insufficient.
For a retained whole-window fixture, read the owner's actual flags after PreDraw
and add only the diagnostic NoSavedSettings flag. DAD Shopping and TTSL Main
otherwise inherited a horizontal scrollbar they do not use in production;
its prior-content width changed later work rectangles and produced false overflow.
Keep intentional scrolling helper fixtures separate from actual-window evidence.
Historical mission/GBR reports do not prove current
integration compatibility or grant permission to run legacy tooling.

Keep three kinds of evidence separate: functional native interaction checks,
diagnostic visual renders, and mcvaxius's game screenshot acceptance. Extend the
existing renderer/runners with supplied snapshots; do not construct the real
plugin or start game services in offline tests. Fix canvas, scale, data, locale,
fonts, timestamps, pointer and motion. Missing baselines/fonts fail. Compare
RGBA dimensions and channels with tolerance 2; emit expected/actual/difference
previews under ignored artifacts. Normal runs never overwrite baselines.

Compare actual rendered bounds, typography, colours and artwork with the
approved design before initializing baselines (Botology: ±2 logical pixels and
±6 RGB for representative surfaces; glyph antialiasing/raster grain excluded).
Generate baseline candidates separately, inspect them, resolve discrepancies,
then deliberately adopt them through a reviewable Git change. Prove the
comparator catches shifted regions, wrong fills and missing elements. Baselines
prove regression stability, not initial design fidelity or GPU verification.

Native rounded borders have separate horizontal and vertical extrema. Their
texture-antialiased vertices also include transparent atlas fringes. Measure the
connected stroke path or rendered ink; requiring one corner vertex at both
extrema can fail a correct scaled layout. Retain the reference tolerance and
prove that shifted regions still fail, as BFE's 100%/150% checks established.
Cover selected windows/popups, all locales, scale/size limits, accents and
functional states. Open interactive popups through their native controls and
check that they remain visible with usable bounds after the opening frame.
Choose click positions after measured native layout bounds have settled. When an
action caption also occurs in explanatory prose, find the action through its retained
native ID and check that occurrence's complete ink; DDuck public's Ko-fi caption
otherwise selected text in its community paragraph. Child
scrollbar changes can alter available width and move controls over several
frames; Botology's Indonesian popup check exposed this. Require stable geometry
with a finite frame limit that fails if layout does not settle.

Before inspecting a retained table after an exhaustive scrolling text sweep,
expose its row again and target wheel input inside `InnerRect` intersected with
`ClipRect`. DhogNav's fresh narrow workflow left its table above the viewport;
retain the scrollbar, row-access and edit/cancel/save requirements when correcting
that observation order.

Auto-sized popups containing wrapped content may need an explicit width each
frame; an appearing-only width can collapse later. Builds, test counts and
self-generated baselines alone do not establish completion. Deliver as
"locally verified; game visual acceptance
pending" until mcvaxius supplies and accepts the game screenshot; never auto-accept
or operate clients without explicit authorization.
