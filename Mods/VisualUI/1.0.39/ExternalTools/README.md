# Default external tools

This version contains only these three tool folders:

- `GradientTiming`: generate a track or BPM gradient, or create head-hidden width-1 SkyAreas in a default `noinput` group.
- `DeleteTiming`: delete explicit current-group track/BPM events within two paragraph-selected timestamps, inclusive.
- `CopyTiming`: copy explicit current-group track/BPM events to a target start time, preserving event spacing and values.

All edits are one undoable in-memory batch and still require the editor's explicit Save. Delete and Copy do not affect inherited timing, chart headers, notes, other timinggroups, or the source range. For field definitions and caveats, read each tool's README.

These tools use the editor's optional nonofficial timinggroup support. They do not make the generated chart compatible with the official game.
