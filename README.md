This mod shows total value of the valuables on the map and updates in real-time as things break, spawn in, or are extracted. The display is on the right side of the screen a little below the extraction goal.

Is ALWAYS ON and visible by default! 

To pull up map value only when pressing the Map button (Tab by default), set 'AlwaysOn' and 'UseValueRatio' to false.

When the map is open, it can show a breakdown for:
- Carts value
- Extraction value
- Remaining value (Map minus Carts/Extraction)

Can be set to only show the initial map's value and NOT update in real time by setting StartingValueOnly to true.

Configuration variables:
- AlwaysOn set to true will keep the value always on the HUD. Overrides any other setting like UseValueRatio.
- StartingValueOnly set to true will keep the Map Value fixed to the level's initially generated value. Will not update value in real time from breaking items, killing enemies, or extracting loot. Should not be used with UseValueRatio set to true.
- UseValueRatio set to true will only show the map value when your remaining map value is some ratio, 'ValueRatio', of the current extraction goal. Needs 'AlwaysOn' and 'StartingValueOnly' set to false to be usable.
- ValueRatio is the ratio of Map value to extraction goal. Ex: Configure 'AlwaysOn' to false, 'StartingValueOnly' to false, 'UseValueRatio' to true, and 'ValueRatio' to 2.0 to have it appear when remaining map value is 2x the current extraction goal.
- UIPosition is a drop down of UI Position presets along the right side of the screen. Set to Custom and modify CustomPositionCoords to use custom coordinates.
- CustomPositionCoords is the X and Y position of the UI element. Requires UIPosition to be set to Custom. 0.0,0.0 is bottom right corner. Default UIPosition is 0.0,225.0.
- ShowBreakdownOnMap enables the breakdown lines when the map is open.
- ShowCartsValue toggles the carts value line.
- ShowExtractionValue toggles the extraction value line.
- ShowRemainingValue toggles the remaining value line.
- BreakdownUpdateIntervalFrames controls how often the breakdown values refresh while the map is open.
- CartRescanIntervalFrames controls how often the scene is rescanned for cart components.
- EnableCartComponentCaching enables/disables caching of cart components.
- EnableCartReflectionCaching enables/disables caching of cart reflection metadata.
- ReplaceHudMapWithRemaining replaces the always-on HUD Map line with Remaining.

Credits:
- Original mod: Tansinator - Map Value Tracker (https://github.com/tansinator/MapValueTracker)
