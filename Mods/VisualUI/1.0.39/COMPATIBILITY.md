# Compatibility and chart extensions

## Runtime target

- Editor plugin: InFalsus Studio Windows Mono 1.0.10, `Assembly-CSharp.dll` SHA-256 `C4C3D6118A19F9BCB1F7D33B29C340DB0F33580655744473B23187E9D3C4E417`.
- Community player target for the chart extension: InFalsus shell `inf-mod6`, version 1.0.6, build 25797904. A public source repository/tag for that shell was not supplied.
- Editor-side integration and source regression records exist for the plugin build. Loading and playing timinggroup charts in the target game shell, including judgment behavior, have **not been verified** by this commit.

## Extension syntax

The optional plugin recognizes these nonofficial chart statements:

```text
timinggroup(name):
    ... notes and timing events ...
groupend()
hidegroup(time_ms, 0_or_1)
```

`timinggroup(name,noinput):` and `timinggroup(name,noclip):` are also accepted. Names contain 1–64 Unicode letters/digits, `_` or `-`, and cannot begin with a digit. Groups cannot nest or reuse a name; the `chart(...)` header belongs in the base section. Group markers delimit following source lines through `groupend()`.

All groups share absolute song time. Each group has its own event stream. A non-base group inherits base `track` and `bpm` events of each kind until it defines its first local event of that kind; after that, that kind follows the local stream. `track` controls scroll speed; `bpm` controls beat-grid and count estimates. SkyArea `groupid` remains a separate visual/head-connection value and does not encode timinggroup membership.

`noinput` notes remain visible but are excluded from editor preview judgment, autocursor hits, hit sounds, and counts; the editor preview clips them at its judgment line. `noclip` also disables judgment and continues rendering past the line. These describe editor behavior. Their gameplay semantics in a player are not certified here. `hidegroup` applies to the current timinggroup: `1` hides its note visibility and `0` shows it; the latest event at or before the selected absolute time applies. Visibility does not itself suppress judgment or sound.

The external tools operate within the selected timinggroup. The three default tools and their precise selection/undo behavior are described in the [tool overview](ExternalTools/README.md) and individual tool READMEs.

## Official compatibility and export

The official editor/game format does not implement `timinggroup`, `noinput`, `noclip`, or `hidegroup` semantics. Without the optional plugin, the original editor has no timinggroup editor. With the plugin installed, Save and Save As preserve the extension statements as chart text; this version does not flatten them, remove them, or block saving as an official-format conversion. Do not treat a saved extended chart as playable by an official build. Use a player known to support the exact extension, and validate gameplay separately.

Installation is the explicit opt-in for the editor extension. Once installed, the plugin automatically recognizes the statements when present; it does not silently enable a per-chart official mode or migrate a chart on open. This commit does not add a compatibility toggle or an official-export conversion path.

## Other visual boundary

The editor's optional out-of-bounds Sky grid is an authoring/view feature. It is not a guarantee that all modified players support every out-of-bounds coordinate. No out-of-bounds Alt/grid adaptation beyond the current editor option is claimed here.
