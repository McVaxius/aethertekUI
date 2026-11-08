# AethertekUI

A C# immediate-mode UI library for FFXIV-related applications:
MD3-inspired dark surfaces, compact desktop controls, cached OKLCH seed-derived
themes, scoped styles and native ImGui interaction. Version **0.3.1**.
The core targets .NET 10 / Windows x64 and uses Dalamud.Bindings.ImGui; the
optional adapter integrates with Dalamud API 15 hosts.

The optional adapter's stateless `XaItemOwnership` decoder reads current-character
XADB item responses (versions 1/2) and recorded armoire/dresser responses (version
1). ADS and MOGTOME retain their IPC subscriptions and native game checks. The
decoder supplies current-character recorded ownership without expiring older
copies. `QueryStates` returns Owned for recorded copies and Missing otherwise,
including unrecorded or unavailable storage; a missing current-character identity
stays Unknown. Operators are responsible for opening storage and refreshing XADB.
`Resolve` gives native inventory/registration and recorded copies precedence over
Missing. `QueryStocks` returns quantities across recorded bags, equipped gear,
armoury, both normal and premium saddlebag pages, retainers, armoire and dresser.
`XaItemStock.CountWithLiveInventory` replaces readable recorded containers with
fresh native counts, including zero, and retains unreadable storage. Duplicate
armoire/dresser results from the two IPC channels count once; legacy inventory
rows without locations form a lower bound rather than adding another native copy.
It owns no cache, retry, configuration or game action.

`MaterialWindowOpacity` fades an owned window's complete native and custom
content after its final End, with a focus delay and consumer-owned preferences.
The window-motion showcase and independent consumer demonstrate its lifecycle;
see [the component contract](docs/COMPONENT_API.md).

Borrowed plugin images can use aspect-preserving `MaterialCanvas.DrawImage` in
pixel bounds and `MaterialWindowHeader.PaintTitle` after End, before opacity.
The window-motion showcase and compiled consumer demonstrate image ownership
and missing-image spacing. Native font fallback retains shaped glyph validation;
optional Hindi menu probes can leave ordinary locales usable while selected
Hindi failure offers explicit consumer-owned recovery. Host/Wine/game acceptance
remains separate from local native tests.

The approved grouped Appearance standard uses `MaterialAppearanceTab.Draw` with
consumer-owned callbacks for Theme & Language, Main window controls, Window opacity
and When unfocused. It uses two columns when both measured groups fit and stacks
complete groups at narrow widths. `MaterialAppearanceSelector.DrawPalette` supplies
inline colour swatches; the Settings showcase demonstrates the layout. Existing
configuration, native identities and save behavior stay with the consumer.

The shared Colour editor supports **Auto** (the default), **Picker**, **LCARS**,
**Glassmorphism**, **Spatial** and **Cyberpunk**. The three new styles each retain
their own Background, Controls and Text choices. LCARS exposes Background and
three bar colours. Glass adds smooth coloured waves, three independent wave
colours, default-on animation and a 0.1–10x speed control (default 1x). Per-colour,
current-palette and wave resets retain the other choices. The overload accepting
consumer defaults and an Auto accent resets to that consumer's actual defaults.
Existing callers keep their original editor unless they supply the new labels.

`MaterialColorSettings` applies these preferences to the host's existing scheme,
preserving Auto/Picker behavior and semantic error roles. `MaterialSkin` provides
Glass ribbons, frosted panes and coloured edge glow; opaque raised/inset Spatial
surfaces; and graphite Cyberpunk plates with cyan/ice tabs and relative magenta
details. Standard controls and surfaces receive material geometry; retained
native wrappers can use its painting/style helpers without changing their IDs.
`MaterialLcars` supplies configurable elbows, rails and panel chrome. Settings
and the native-titlebar showcase share the editor. Hosts own drafts, persistence,
translated labels and complete-window opacity. FrenRider remains the accepted
pilot; other consumers require their selected adoption and host acceptance.

`MaterialTheme.Feedback` optionally routes actual control activations to an
owner's default-muted `MaterialUiSoundPlayer`. Its original short PCM tones use
an owned Windows waveOut handle, with no external assets or dependencies.
Settings and titlebar mute share the consumer's existing configuration/save path.
Library version is 0.3.1. See the [usage skill](skills/aethertek-ui/SKILL.md)
for lifecycle details.

The 2026-10-23 FrenRider follow-up aligns LCARS native controls with the artwork:
dark outlined buttons, blue switches/sliders, substantial panel wraps and an
explicit Chocobo vector icon. The titlebar adapter's opt-in `SetLcarsIcons`
selects globe/monitor/droplet glyphs while retaining native actions and IDs.
Consumers still bind their existing Krangle toggle through the five-action
registration overload. Full-owner navigation verification covers selected ink.

`MaterialAppearanceTitleBar.Register` supplies the standard native-titlebar actions:
Language, Compact, Transparency, Colour and Krangle. New and adopted UIs show
Krangle by default on main, mini and auxiliary windows. Use the overload with
`toggleKrangle` and `krangleTooltip` after `drawColour`, before optional visibility.
Its UserSecret icon toggles the consumer's existing saved privacy setting on left
click; its consumer-localized tooltip reads the current On/Off state when hovered.
Consumers retain their masking rules; arbitrary strings are not automatically
redacted. The original four-action overload remains a compatibility path.

`MaterialAppearanceTitleBar.RegisterWindowControls` optionally appends styling and movement/resize lock callbacks without changing existing registration signatures or defaults. Consumers own the saved choices, localized tooltips and slash commands. `MaterialStyleMode.Native` keeps native host colors/styles while retaining component context; `MaterialWindowLock.Apply` runs last in PreDraw and `Restore` first in PostDraw, preserving original flags and pin preferences. The appearance preview demonstrates all seven actions.

The approved FrenRider glass implementation is shared through the optional adapter's `MaterialGlassRenderer`. It uses Xeldar's actual Glasswork renderer for game and detached desktop backdrops. The consumer owns the renderer, per-frame image configuration, BeginFrame/EndFrame, resize-resource release and disposal. Keep `MaterialSkin.PushBackgroundRenderer` around native Begin/End, use `BackdropStyle` around Begin and call `Backdrop` before content or table splits. Explicit Background surfaces use glass; foreground controls retain their fills. Ordinary background paint remains until the backend is ready. `BackgroundFill` and `StructuralBackgroundStyle` reveal structural child/table backgrounds only after that root rendered successfully.

The shared Colour editor can expose saved **Waves**, **Lights** and **Image** modes with translated optional labels. Lights provides ambient glows, sweeping beams and mouse-following glow, with separate colours and the existing motion controls. The Browse overload invokes the consumer's native file picker; the consumer loads/saves the selected path and preserves it on cancel. An image supplies the glass backdrop with centered cover cropping; **Blur (%)** blurs the selected image. Image mode needs no desktop capture. The showcase uses the same editor and an actual file picker in its Dalamud host.

Blur defaults on only for Glassmorphism, with **75% Frost**; the other styles default off. Whole-window opacity remains separate. **Pause backdrop for screenshots defaults off** and is a temporary renderer choice, exposed in Colour and Transparency. Live detached capture excludes its own windows from OS capture to avoid feedback; explicitly pausing freezes the clean backdrop, verifies screenshot visibility and keeps Glasswork/UI drawing. Image-backed windows remain capturable independently. Resume requires a fresh clean capture. Keep all three Glasswork DLLs and the MIT notice with the adapter. `MaterialWindowBlur` remains a separate legacy native-host helper; it does not reproduce this renderer.

Register once after existing title actions, supplying translated labels and
existing editor/save callbacks. Compact also toggles directly; Language,
Transparency and Colour open manual popups. Pass existing main-window visibility
flags through the optional `MaterialAppearanceTitleBarVisibility` callback;
its four positional fields are retained and its non-positional `Krangle` init
property defaults true. Auxiliary windows can omit the callback to show all five.
Call `RefreshVisibility()` before native Begin and `DrawPopups()` after End and
motion restore, before opacity, including collapsed frames. A central host can
call it consistently after WindowSystem.Draw. Original custom descriptors and
priorities are retained. The Sizing lab's appearance preview demonstrates the
shared Settings values in a native titlebar.

VERMAXION is the selected reference integration, reusing its existing privacy
setting and masking paths through the shared action. Each other consumer needs
selected wiring and a rebuild; a library update cannot retrofit every plugin.
The library adds no privacy configuration, service or global preference.

`MaterialWindowHeader.PaintTitleWithStatus` adds an optional one-line status to
the same retained title painter. Supply a `MaterialTitlebarStatus` with full and
short text, complete hover details and an optional semantic dot colour. The
icon/title/version keep their space first; the remaining width selects full text,
short text or a grapheme-safe ellipsis, omitting the status when it cannot fit.
Hover details remain available on the non-button title area while collapsed.
Call after native End and motion restore, before opacity, with the existing
custom-button reservation. Consumers own status data and saved visibility/detail
preferences; `PaintTitle` retains its original signature and behavior.

## Repository

```text
AethertekUI.sln
AGENTS.md                          current repository instructions
skills/aethertek-ui/SKILL.md        adoption and library evolution guidance
src/
  AethertekUI/                      Core, Drawing, Components, Data
  AethertekUI.Dalamud/              optional lifecycle-free WindowSystem adapter
  AethertekUI.Demo/                 AethertekUI Showcase, /aethertekui
samples/ConsumerContract/           independent integration example
tests/AethertekUI.Tests/           pure logic, native input and render tests
eng/                               existing local environment and checks
docs/                              API, sizing, integration and historical context
PROJECT_MISSION.md                 original machine-specific mission, historical
artifacts/                         ignored logs, test results and diagnostic previews
.cache/                            ignored repository-local tool caches
```

## Build and verify

Prerequisites: Windows x64, installed .NET SDK **10.0.201** (latest patch allowed),
and an installed Dalamud API 15 distribution. Core and adapter use
Microsoft.NET.Sdk; only the showcase uses Dalamud.NET.Sdk/15.0.0.

```powershell
Set-Location 'Z:\aethertekUI'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\Build.ps1 -Previews
```

The normal route builds the solution once in Debug/x64, runs the existing tests,
and builds/checks the independent project-reference consumer. The command sets
execution-policy bypass only for that child process, without changing machine or
user policy, and dot-sources `Enter-RepoEnv.ps1` before tooling runs. It does not pack
release artifacts or run legacy GBR tooling. Showcase build packaging is disabled;
its source manifest is copied beside its DLL. The existing environment keeps
DOTNET_CLI_HOME, NuGet caches, TEMP/TMP, APPDATA and LOCALAPPDATA under `.cache/`.

`Enter-RepoEnv.ps1` honors an existing `DalamudLibPath` or `DALAMUD_HOME`, otherwise
uses the installed XIVLauncher dev distribution. To choose another distribution,
set `$env:DalamudLibPath` before entering the environment. Tooling reads installed
host assemblies and does not install or control a client.

For targeted checks, open a child shell with
`powershell.exe -NoProfile -ExecutionPolicy Bypass`, then enter the environment:

```powershell
. .\eng\Enter-RepoEnv.ps1
dotnet build AethertekUI.sln -c Debug -p:Platform=x64 --disable-build-servers -m:1
dotnet test tests/AethertekUI.Tests -c Debug --no-build --no-restore
.\eng\Verify-Consumer.ps1
```

NuGet.Config clears inherited feeds; existing dependency lock files remain
tracked. NuGet auditing is disabled by the inherited build configuration.

## Use in a consumer

Prefer local project references, adjusting these paths relative to the csproj:

```xml
<ItemGroup>
  <ProjectReference Include="../aethertekUI/src/AethertekUI/AethertekUI.csproj" />
  <ProjectReference Include="../aethertekUI/src/AethertekUI.Dalamud/AethertekUI.Dalamud.csproj" />
</ItemGroup>
```

The adapter is optional and reserved for Dalamud hosts. Compatible C# ImGui
applications can use core when they supply the same managed binding and matching
native runtime/context. A different ImGui binding requires an explicit port.

```csharp
using AethertekUI;
using Dalamud.Bindings.ImGui;

// Cache this outside Draw.
private readonly MaterialTheme theme = new(new(.36f, .72f, .46f));
private bool enabled;

public void Draw(float hostScale)
{
    using var scope = MaterialTheme.Push(theme, hostScale, MaterialStyleMode.Material);
    if (ImGui.Begin("Example###example-window"))
        MaterialSwitch.Draw("enabled", "Enabled", ref enabled);
    ImGui.End();
}
```

Wrap the complete Begin/End lifecycle. For WindowSystem, use
`MaterialWindowHost.Draw(windows)` or a `BeginFrame` scope around `windows.Draw()`.
The default Compatible mode preserves host layout; Material applies full geometry.
Consumers own lifecycle, configuration, actions, domain colors, fonts and textures.
The appearance selector supports square swatches and rectangular colour fields
with a Palette mesh, current swatch and chevron, retaining the existing popup IDs.
Explicit control metrics also size that Palette within the field's available space.
Ship the library DLLs beside the plugin while keeping Dalamud assemblies host-owned.

Use stable IDs independent of translated labels, host-provided CJK fonts, and
content-aware sizing for longer translations. Component type names and signatures
remain Material-prefixed, and internal ImGui IDs are preserved. Read the
[usage skill](skills/aethertek-ui/SKILL.md),
[integration guide](docs/INTEGRATION_GUIDE.md),
[adaptive sizing guide](docs/ADAPTIVE_SIZING.md) and
[component API](docs/COMPONENT_API.md).

### Windows-shaped Hindi text

`MaterialTextRenderer` uses Windows DirectWrite and installed Nirmala UI for
Devanagari shaping, measurement and caret geometry. A consumer supplies the
texture uploader; Dalamud hosts can own `MaterialTextHost` from the optional
adapter. Enter its `Push()` scope around the window tree and dispose it outside
the native frame after render submission. No font-atlas mutation or third-party
shaping package is required.

Under that scope, `MaterialText.Measure`, `AddText` and `Text` route Devanagari
through the same shaped layout. Material buttons, action tiles and text/search
fields use that route; other retained native text/control wrappers still need
explicit integration before a consumer advertises Hindi. Non-Hindi text and
password fields retain their native path. Borrowed layouts are valid for the
current frame only.

Retained native wrappers can use `MaterialText.BeginCombo`/`Selectable` and the
text/tooltip helpers while keeping their original labels and IDs.
`MaterialText.TreeNode` and `TableHeader` also retain raw native identities and
arrows; callers own `TreePop` and table row-height reservation.
The public `MaterialShapedInput.SingleLine` bridge accepts that same native ID; reserve its
line height with `MaterialText.PushLineHeight`.
Single-line fields preserve leading Hindi ink within their existing frame padding
at zero scroll, retaining the original text origin and the padded scrolling clip.
Keep existing atlas glyph checks
for other scripts and use `renderer.CheckGlyphs` for authored Devanagari without
uploading textures. `MaterialShapedInput.Multiline` preserves an existing editor's
native ID, both scroll axes, clipboard/undo and CRLF/Unicode source values;
`MaterialInput.Multiline` uses the same route. Declare mixed tab captions with
`MaterialTabs.Begin(id, captions)` before native Begin to reserve their natural
height. Game-native DTR/chat still uses the host's text renderer.

The showcase command `/aethertekui hindi` opens an optional mock editing preview
without changing the existing eight pages. Native ImGui still owns the search
field's ID, focus and undo. Its ImWchar16 buffer uses transient proxy characters
to preserve supplementary source text and clipboard values; these proxies never
enter saved consumer strings. Supplementary keyboard input that the host has
already truncated cannot be reconstructed. Game/GPU and IME interaction require
user-controlled acceptance.

## Showcase and outputs

**AethertekUI Showcase**, command `/aethertekui`, contains eight mock-data pages:
Gallery, Automation, Gathering data, Dashboard, Settings, List editor,
Large catalog and Sizing lab. It includes a floating automation overlay.
No game actions run from the showcase.

The Sizing lab includes five **Tight editor rows** using
`MaterialTable.PushTightRows()`: zero vertical cell padding and a one-logical-pixel
column inset, with readable padding inside each editor. Enter the scope after
control sizing and retain it around native `BeginTable`/`EndTable`.

Debug outputs relative to this repository:

- Core: `src/AethertekUI/bin/Debug/net10.0-windows/AethertekUI.dll`.
- Adapter: `src/AethertekUI.Dalamud/bin/Debug/net10.0-windows/AethertekUI.Dalamud.dll`.
- Showcase: `src/AethertekUI.Demo/bin/Debug/AethertekUI.Demo.dll` and its JSON manifest.
- Consumer: `samples/ConsumerContract/bin/Debug/net10.0-windows/ConsumerContract.dll`,
  with both renamed library DLLs beside it.
- Logs/results: `artifacts/build-Debug.log`, `artifacts/test-Debug.log`,
  `artifacts/consumer-project.log`, `artifacts/TestResults/`.
- Diagnostic previews: `artifacts/previews/page-0.png` through `page-7.png`,
  plus existing density/scale and narrow-field previews.

Previews are CPU diagnostic renders of actual native ImGui meshes using a default
font and fixed background. They are not game screenshots or GPU acceptance.
Real game/CJK fonts, GPU compositing, controller routing and unload/reload remain
for later selected plugin integrations. See [known limitations](docs/KNOWN_LIMITATIONS.md)
and the [smoke-test guide](docs/PROTOTYPE_SMOKE_TEST.md).

## Evolution and provenance

2026-11-08 - 0.3.1: integrate the reviewed text/control allocation changes and shared glass vertex grid. Waves and Lights reuse the bounded emitter; the FrenRider pilot retains its custom sampling and appearance. Offline validation remains separate from game/GPU acceptance. Further existing-consumer updates are in progress.

2026-10-30: promote the user-approved FrenRider Glasswork implementation into the optional adapter, with a host-independent scoped Background contract in core. Add Waves/Lights/Image settings, native-picker integration, structural-background coverage and default-off screenshot pause; retain foreground rendering, native scopes and legacy editor behavior. Update the showcase and both usage skills. Library version remains 0.3.0; consumer publication is separate.

2026-10-29: add opt-in native/plain styling and movement/resize controls, preserve native scope colors and flags, and introduce optional native blur support with per-style choices. FrenRider is the pilot; viewport/GPU acceptance and wider consumer wiring remain pending. Version remains 0.3.0.

2026-10-18: make Krangle the fifth standard titlebar action for new and adopted
UIs, visible by default on main, mini and auxiliary windows. Reuse consumer-owned
privacy toggles, masking and localized state tooltips; retain the original
four-action API. VERMAXION is the selected reference integration; other consumers
still require selected wiring and rebuilds. Version remains 0.3.0.

2026-10-14: correct readable-width wrapping inside settings children and table
cells. Only an explicit native SameLine continuation may move to a fresh line;
indentation or a later column alone must not split an editor from its row.
Extend the existing native sizing regression for nested/indented settings and
repeated cell baselines, and record the row/reset-action standard in the usage
skill. Version remains 0.3.0.

2026-10-11: extend the existing retained title painter with optional measured
status and full hover details. Keep title/icon/version priority, native geometry
and action hit regions; support clipped/collapsed titles and the existing shaped
text and complete-window opacity routes. Version remains 0.3.0.

2026-10-11: add four manual appearance titlebar actions to the optional adapter,
retaining consumer settings, main-window visibility flags and existing native
actions. The native preview and independent popup tests cover collapsed editors,
owner isolation and visibility restoration. Library version remains 0.3.0;
host icon rendering and game acceptance require separate verification.

2026-10-09: carry the approved ADS Rules grid into `MaterialTable.PushTightRows()`
and the Sizing lab. Existing table defaults and editor dimensions are retained;
tight rows are an explicit table layout choice. Library version remains 0.3.0.
Refresh the reviewed Sizing lab baseline and the existing backpack-icon preview.

The optional Dalamud adapter includes `MaterialSupportLog` for a manual support
button. Pass the active plugin interface, the consumer's translator, its existing
folder-opening action and error logger. It snapshots the current client's log
into a ZIP under the plugin's `support-logs` folder while leaving the log writer
usable, then opens the output folder. Logs at or above 100 MiB show an inline
warning before export because Dalamud may have stopped recording recent activity.
Sharing and removal of old ZIPs remain manual; there is no automatic upload,
background logger, retry or cleanup service.

The five-argument `MaterialSupportLog.Draw` overload also accepts the consumer's
existing `ICommandManager`. It adds a separate **Open XA Slave log tools** button
only while XA Slave is loaded. The button issues `/xa mods` to open the provider's
existing Utility > XA Mods panel; it does not invoke the log cleaner. The original
four-argument exporter API, button identity, label and behavior are retained.

Implement approved plugin mockups faithfully. When a design exposes a component
or layout gap, improve AethertekUI and demonstrate the addition in the showcase.
Update the skill when real integrations establish better practices. AutoParty
remains skipped; this delivery establishes the foundation without plugin edits,
client installation, live-game tests, release packaging or publication.
Publication remains mcvaxius's responsibility; this repository has no remote.

Imported from the supplied `CombatReborn.MaterialUI.zip`, retaining its two
upstream Git commits and the newer working-tree source. The incoming state is
preserved in commit `1f65827` before rebranding. Original showcase attribution to
**CombatReborn** remains in project/manifest metadata. The implementation is
independent of GBR, ElliLib, RSR and game-specific plugin code. OKLab/OKLCH uses
[Bjorn Ottosson's published matrices](https://bottosson.github.io/posts/oklab/).
No root license was supplied; this conversion adds no licensing terms.

`PROJECT_MISSION.md`, GBR documents and prior verification reports remain intact
as historical context from another machine. Their paths, authorizations and test
claims do not apply to this repository. `eng/*Gbr*`, `eng/GbrUiProbe/` and
`eng/Record-GbrOutputs.targets` are legacy tooling outside the normal route.
`eng/Verify-Artifacts.ps1` retains the old release-artifact workflow separately;
normal checks do not invoke it. No GBR source is needed for the current build.
