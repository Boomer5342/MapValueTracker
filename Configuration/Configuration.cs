using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace MapValueTracker.Config
{
    public enum Positions
    {
        Default,
        LowerRight,
        BottomRight,
        Custom
    }

	internal class Configuration
    {
        public static ConfigEntry<bool> AlwaysOn;
        public static ConfigEntry<bool> StartingValueOnly;
        public static ConfigEntry<bool> UseValueRatio;
        public static ConfigEntry<float> ValueRatio;
        public static ConfigEntry<Positions> UIPosition;
        public static ConfigEntry<Vector2> CustomPositionCoords;
        public static ConfigEntry<bool> ShowBreakdownOnMap;
        public static ConfigEntry<bool> ShowCartsValue;
        public static ConfigEntry<bool> ShowExtractionValue;
        public static ConfigEntry<bool> ShowRemainingValue;
        public static ConfigEntry<int> BreakdownUpdateIntervalFrames;
        public static ConfigEntry<bool> ReplaceHudMapWithRemaining;

        public static void Init(ConfigFile config)
        {
            config.SaveOnConfigSet = false;

            AlwaysOn = config.Bind(
                "Default",
                "AlwaysOn",
                true,
                "Toggle to always display map value when an extraction goal is active. If false, use the menu key to pull up the tracker (Tab by default)."
            );
            StartingValueOnly = config.Bind(
                "Default",
                "StartingValueOnly",
                false,
                "Toggle to keep the Map Value fixed to the level's initially generated value. Will not update value in real time from breaking items, killing enemies, or extracting loot. Should not be used with UseValueRatio set to true."
            );
            UseValueRatio = config.Bind(
                "Default",
                "UseValueRatio",
                false,
                "Toggle to use value ratio to display Map Valuables. AlwaysOn must be false and this must be true to take effect."
            );
            ValueRatio = config.Bind(
                "Default",
                "ValueRatio",
                2.0f,
                "Ratio of Map Value to Extraction Goal. UseValueRatio must be true to take effect. Ex: 20k map value to 10k goal is 2.0 "
            );
            UIPosition = config.Bind(
                "UIPosition",
                "UIPosition",
                Positions.Default,
                "Preset Position of the Value Tracker UI element. Default is on the right side, below the extraction targets."
            );
            CustomPositionCoords = config.Bind(
                "UIPosition",
                "CustomPositionCoords",
                new Vector2(0, 0),
                "Custom X,Y coordates of the Value Tracker UI element. Bottom Right corner is 0,0. Default position is 0,225."
            );
            ShowBreakdownOnMap = config.Bind(
                "Breakdown",
                "ShowBreakdownOnMap",
                true,
                "When true, shows extra value lines (Carts/Extraction/Remaining) while the map is open."
            );
            ShowCartsValue = config.Bind(
                "Breakdown",
                "ShowCartsValue",
                true,
                "When true, shows the total value currently inside carts."
            );
            ShowExtractionValue = config.Bind(
                "Breakdown",
                "ShowExtractionValue",
                true,
                "When true, shows the total value currently staged in extraction."
            );
            ShowRemainingValue = config.Bind(
                "Breakdown",
                "ShowRemainingValue",
                true,
                "When true, shows remaining value (Map minus Carts/Extraction)."
            );
            BreakdownUpdateIntervalFrames = config.Bind(
                "Breakdown",
                "BreakdownUpdateIntervalFrames",
                10,
                "How often (in frames) the breakdown values refresh while the map is open. Lower is more accurate, higher is lighter."
            );
            ReplaceHudMapWithRemaining = config.Bind(
                "Breakdown",
                "ReplaceHudMapWithRemaining",
                false,
                "When true, the always-on HUD line shows Remaining value instead of Map value."
            );

            ClearOrphanedEntries(config);
            config.Save();
            config.SaveOnConfigSet = true;
        }

        static void ClearOrphanedEntries(ConfigFile cfg)
        {
            // Find the private property `OrphanedEntries` from the type `ConfigFile` //
            PropertyInfo orphanedEntriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            // And get the value of that property from our ConfigFile instance //
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg);
            // And finally, clear the `OrphanedEntries` dictionary //
            orphanedEntries.Clear();
        }
    }

}
