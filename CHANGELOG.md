## - 1.2.2

### Added
- Added a `Hauler` line to the full breakdown.

### Changed
- Removed the `Extraction` line from the full breakdown.
- Removed the `Haul` line from the summary breakdown.

## - 1.2.0

### Added
- Added a `Disable` config toggle for live in-session performance testing.

### Changed
- Reworked value tracking to use cached game state instead of routine scene-wide scans.
- Switched cart tracking to registered cart state instead of per-refresh cart discovery.
- Reduced HUD text and layout churn by only updating when displayed values change.

## - 1.1.0

### Added
- Added a compact closed-map HUD and an expanded map-open HUD.
- Added REPOConfig-friendly in-game settings support.
- Added MenuLib and REPOConfig package dependencies.

### Changed
- Reworked the valuables HUD to better fit the current REPO UI.
- Reduced the config surface to focus on the current display, layout, and positioning options.
- Updated the default HUD position.

## - 1.0.3

### Changed
- Improved general performance.

## - 1.0.2

### Changed
- Updated config handling.
- Switched breakdown refresh timing to seconds-based configuration.

## - 1.0.1

### Added
- Added configurable refresh intervals and display toggles.

### Changed
- Improved performance with cart component caching and reflection caching.

## - 1.0.0

### Added
- Initial fork release as Map Value Tracker Plus.
- Added breakdown lines for carts, extraction, and remaining value.
- Added config options for the expanded value breakdown.
