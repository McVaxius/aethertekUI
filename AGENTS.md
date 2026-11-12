# AethertekUI repository instructions

This is the local, private AethertekUI library at `Z:\aethertekUI`, version
0.3.2, targeting .NET 10 / Windows x64 and Dalamud API 15. Keep it local;
publication belongs to mcvaxius. Do not create or contact a Git remote.

Use only `mcvaxius` or `dhoggpt` when attributing this work to the user.
Never put the user's personal name in authored text, changelogs, commits,
documentation, UI copy, comments, reports, or agent output. Check affected
text before delivery; report identity matches without repeating the name.

Read [skills/aethertek-ui/SKILL.md](skills/aethertek-ui/SKILL.md) when developing
the library or adopting it in a compatible C# ImGui consumer. Preserve existing
Material component names, public signatures, behavior and internal ImGui IDs.
Implement approved mockups faithfully. Improve a missing library component or
layout capability and demonstrate it in the showcase when adoption requires it.
Update the usage skill when real integrations establish better practices.

Dot-source `eng/Enter-RepoEnv.ps1` before invoking toolchains. Keep tool caches,
temporary files, logs and generated outputs under the ignored `.cache/`,
`artifacts/`, `bin/` and `obj/` paths in this repository. Use installed SDK
10.0.201. The normal check is `eng/Build.ps1 -Previews`: one Debug/x64 solution
build, existing tests and the independent project-reference consumer check.

Use native ImGui interaction, semantic theme roles, stable IDs, balanced scopes
and bounded motion state. Consumers own lifecycle, configuration, game actions,
domain status colors, fonts and textures. Keep the MD3-inspired dark/OKLCH basis.

`PROJECT_MISSION.md`, GBR documents, prior verification reports, and
`eng/*Gbr*`, `eng/GbrUiProbe/`, `eng/Record-GbrOutputs.targets` are preserved
historical context from another machine. Their paths, permissions and recorded
results are not current instructions or verification. Leave these documents
intact; legacy GBR tooling is outside the normal development route. The retained
release artifact verifier is also separate from the normal build/test route.

This foundation delivery does not authorize plugin conversions (AutoParty is
skipped), external repository edits, client installation, live-game tests,
release packaging or publication. Do not inspect unpublished RSR implementation.
Later integrations require mcvaxius's selected plugin and approved design.
