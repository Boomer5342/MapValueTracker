Map Value Tracker Plus adds a small valuables HUD that fits into REPO's existing UI.

- With the map closed, it can show a compact one-line readout.
- With the map open, it can switch to either a short summary or a full breakdown.

It also works with `REPOConfig`, so the main settings can be changed in game.

What it tracks:
- Total map value
- Remaining value
- Value in carts
- Value in haulers

Main settings:
- `Show Closed Map HUD` keeps the compact valuables line visible while the map is closed.
- `Closed Map Display` chooses whether the closed-map line shows `Map` or `Remaining`.
- `Open Map Display` switches the map-open HUD between `Summary` and `FullBreakdown`.
- `Match Closed Map Position` keeps the open-map HUD in the same spot as the closed-map HUD.
- `Position Preset`, `Custom Offset X`, and `Custom Offset Y` are available for both the closed-map and open-map HUD sections.
- `Hide After Final Extraction` hides the valuables HUD after the last extraction.
- `Refresh Interval Seconds` controls how often the breakdown refreshes during a run.

Credits:
- Based on Tansinator's original Map Value Tracker:
  https://github.com/tansinator/MapValueTracker
