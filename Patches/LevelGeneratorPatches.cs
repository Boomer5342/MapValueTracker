using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(LevelGenerator))]
    static class LevelGeneratorPatches
    {
        [HarmonyPatch("StartRoomGeneration")]
        [HarmonyPrefix]
        public static void StartRoomGeneration()
        {
            MapValueTracker.Logger.LogDebug("Generating Started. Resetting to zero.");
            MapValueTracker.ResetValues();
            MapValueTracker.Logger.LogDebug("Room generation started. Now val is " + MapValueTracker.totalValue);
        }

        [HarmonyPatch("GenerateDone")]
        [HarmonyPrefix]
        public static void GenerateDonePostfix()
        {
            MapValueTracker.Logger.LogDebug("Generating Started. Resetting to zero.");
            MapValueTracker.CheckForItems();
            MapValueTracker.Logger.LogDebug("Generation done. Now val is " + MapValueTracker.totalValue);
        }
    }
}
