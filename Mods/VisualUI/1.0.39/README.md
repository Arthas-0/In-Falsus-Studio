# Visual & UI 1.0.39

This directory contains the source-only checkpoint for the optional Visual & UI BepInEx plugin used with **InFalsus Studio Windows Mono 1.0.10**. It combines the current native visual and editor UI features, timinggroup authoring, performance improvements, and three default external timing tools.

The source repository does not distribute the compiled DLL, BepInEx runtime, extracted game bundles, meshes, fonts, personal configuration, or the integrated ZIP. The separately prepared local package is `InFalsusStudio_VisualUI_1.0.39.zip`; this source snapshot is not a standalone installer. See [build instructions](BUILDING.md) and [compatibility details](COMPATIBILITY.md).

The bundled external tools are exactly:

- **GradientTiming**: create or replace a `track` or `bpm` gradient in the current timinggroup. Its blackline mode creates head-hidden width-1 SkyAreas in a `noinput` group by default.
- **DeleteTiming**: remove explicit `track`/`bpm` events inside the two timestamps selected by paragraph range selection, for the current timinggroup.
- **CopyTiming**: copy explicit current-group `track`/`bpm` events to a specified start time while keeping their spacing and values.

Tool scripts and manifests live in [ExternalTools](ExternalTools/README.md). Add-on tool folders can be installed beside them in the editor-root `ExternalTools` directory; this version's source checkpoint contains only the three defaults above.

The latest editor checkpoint restores the editor's original key-label rendering and removes the side-note enhancement guides. Other existing features in this plugin version remain. The exact code is split into `Source/` and compiled as one assembly named `InfStudioVisualUI.dll`.

## Target and activation

The plugin targets the Windows Mono editor build whose `Assembly-CSharp.dll` SHA-256 is `C4C3D6118A19F9BCB1F7D33B29C340DB0F33580655744473B23187E9D3C4E417`. The plugin is opt-in through installation. When installed, timinggroup syntax is recognized automatically; there is no separate per-chart compatibility-mode toggle. The chart is not rewritten merely by opening it, and edits remain subject to the editor's explicit Save action.

Timinggroup gameplay compatibility is a separate question from editor support. The community target recorded for the chart extension is InFalsus shell **`inf-mod6`, version 1.0.6, build 25797904**. Its player-side behavior has not been validated by this source checkpoint; see [COMPATIBILITY.md](COMPATIBILITY.md).
