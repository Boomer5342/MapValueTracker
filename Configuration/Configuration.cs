using BepInEx.Configuration;
using System.Globalization;
using UnityEngine;

namespace MapValueTracker.Config
{
    internal static class Configuration
    {
        private const string HideFromRepoConfig = "HideFromREPOConfig";
        private static readonly Vector2 DefaultHudOffset = new Vector2(-10f, 125f);

        public static ConfigEntry<bool> AlwaysOn = null!;
        public static ConfigEntry<string> ClosedMapDisplayMode = null!;
        public static ConfigEntry<string> OpenMapDisplayMode = null!;
        public static ConfigEntry<bool> SyncHudPositions = null!;
        public static ConfigEntry<string> UiPositionPreset = null!;
        public static ConfigEntry<string> CustomOffsetX = null!;
        public static ConfigEntry<string> CustomOffsetY = null!;
        public static ConfigEntry<string> OpenMapPositionPreset = null!;
        public static ConfigEntry<string> OpenMapCustomOffsetX = null!;
        public static ConfigEntry<string> OpenMapCustomOffsetY = null!;
        public static ConfigEntry<bool> HideAfterFinalExtraction = null!;
        public static ConfigEntry<float> RefreshIntervalSeconds = null!;
        public static ConfigEntry<bool> RuntimeEnabled = null!;
        public static ConfigEntry<bool> DebugLogging = null!;

        public static void Init(ConfigFile config)
        {
            config.SaveOnConfigSet = false;

            AlwaysOn = config.Bind(
                "Closed Map HUD",
                "Show Closed Map HUD",
                true,
                new ConfigDescription("Show the compact valuables readout while the map is closed.")
            );
            ClosedMapDisplayMode = config.Bind(
                "Closed Map HUD",
                "Closed Map Display",
                "Remaining",
                new ConfigDescription(
                    "Choose which value the closed-map HUD shows.",
                    new AcceptableValueList<string>("Map", "Remaining"))
            );
            OpenMapDisplayMode = config.Bind(
                "Open Map HUD",
                "Open Map Display",
                "Summary",
                new ConfigDescription(
                    "Choose whether the map-open HUD shows a summary or the full breakdown.",
                    new AcceptableValueList<string>("Summary", "FullBreakdown"))
            );
            SyncHudPositions = config.Bind(
                "Open Map HUD",
                "Match Closed Map Position",
                true,
                new ConfigDescription("Use the same position for the open-map HUD as the closed-map HUD.")
            );
            UiPositionPreset = config.Bind(
                "Closed Map Position",
                "Position Preset",
                "Default",
                new ConfigDescription(
                    "Choose the closed-map HUD position.",
                    new AcceptableValueList<string>("Default", "Custom"))
            );
            CustomOffsetX = config.Bind(
                "Closed Map Position",
                "Custom Offset X",
                DefaultHudOffset.x.ToString(CultureInfo.InvariantCulture),
                new ConfigDescription("Horizontal offset in pixels for the closed-map HUD. Negative moves left, positive moves right.")
            );
            CustomOffsetY = config.Bind(
                "Closed Map Position",
                "Custom Offset Y",
                DefaultHudOffset.y.ToString(CultureInfo.InvariantCulture),
                new ConfigDescription("Vertical offset in pixels for the closed-map HUD. Positive moves up, negative moves down.")
            );
            OpenMapPositionPreset = config.Bind(
                "Open Map Position",
                "Position Preset",
                "Default",
                new ConfigDescription(
                    "Choose the open-map HUD position when Match Closed Map Position is off.",
                    new AcceptableValueList<string>("Default", "Custom"))
            );
            OpenMapCustomOffsetX = config.Bind(
                "Open Map Position",
                "Custom Offset X",
                DefaultHudOffset.x.ToString(CultureInfo.InvariantCulture),
                new ConfigDescription("Horizontal offset in pixels for the open-map HUD when Match Closed Map Position is off.")
            );
            OpenMapCustomOffsetY = config.Bind(
                "Open Map Position",
                "Custom Offset Y",
                DefaultHudOffset.y.ToString(CultureInfo.InvariantCulture),
                new ConfigDescription("Vertical offset in pixels for the open-map HUD when Match Closed Map Position is off.")
            );
            HideAfterFinalExtraction = config.Bind(
                "Advanced",
                "Hide After Final Extraction",
                true,
                new ConfigDescription("Hide the valuables HUD after the last extraction is finished.")
            );
            RefreshIntervalSeconds = config.Bind(
                "Advanced",
                "Refresh Interval Seconds",
                1f,
                new ConfigDescription(
                    "How often the valuables totals refresh during a run.",
                    new AcceptableValueRange<float>(0.1f, 5f))
            );
            RuntimeEnabled = config.Bind(
                "Debug",
                "Disable",
                true,
                new ConfigDescription("Enable or disable all Map Value Tracker Plus logic.")
            );
            DebugLogging = config.Bind(
                "Internal",
                "Debug Logging",
                false,
                new ConfigDescription("Enable extra debug logging for HUD creation and placement.", null, HideFromRepoConfig)
            );

            config.Save();
            config.SaveOnConfigSet = true;
        }

        public static bool IsClosedModeRemaining()
        {
            return ClosedMapDisplayMode.Value == "Remaining";
        }

        public static bool IsFullBreakdown()
        {
            return OpenMapDisplayMode.Value == "FullBreakdown";
        }

        public static Vector2 GetCompactOffset()
        {
            return ResolveOffset(UiPositionPreset.Value, CustomOffsetX.Value, CustomOffsetY.Value);
        }

        public static Vector2 GetOpenMapOffset()
        {
            if (SyncHudPositions.Value)
            {
                return GetCompactOffset();
            }

            return ResolveOffset(OpenMapPositionPreset.Value, OpenMapCustomOffsetX.Value, OpenMapCustomOffsetY.Value);
        }

        private static Vector2 ResolveOffset(string preset, string xText, string yText)
        {
            if (preset != "Custom")
            {
                return DefaultHudOffset;
            }

            return new Vector2(ParseCoordinate(xText, DefaultHudOffset.x), ParseCoordinate(yText, DefaultHudOffset.y));
        }

        private static float ParseCoordinate(string value, float fallback)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
        }
    }
}
