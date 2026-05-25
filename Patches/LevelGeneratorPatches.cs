using HarmonyLib;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(LevelGenerator))]
    internal static class LevelGeneratorPatches
    {
        [HarmonyPatch("StartRoomGeneration")]
        [HarmonyPrefix]
        private static void StartRoomGenerationPrefix()
        {
            MapValueTracker.Logger.LogDebug("Resetting value tracker for a new level.");
            MapValueTracker.ResetValues();
            MapValueTracker.Logger.LogDebug("Tracker reset complete. Total value: " + MapValueTracker.totalValue);
        }

        [HarmonyPatch("GenerateDone")]
        [HarmonyPrefix]
        private static void GenerateDonePrefix()
        {
            if (MapValueTracker.IsRuntimeEnabled())
            {
                MapValueTracker.CheckForItems();
            }
            MapValueTracker.Logger.LogDebug("Level generation finished. Total value: " + MapValueTracker.totalValue);
        }
    }
}
