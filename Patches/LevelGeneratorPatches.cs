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
            MapValueTracker.Logger.LogDebug("Generating Started. Resetting to zero.");
            MapValueTracker.ResetValues();
            MapValueTracker.Logger.LogDebug("Room generation started. Now val is " + MapValueTracker.totalValue);
        }

        [HarmonyPatch("GenerateDone")]
        [HarmonyPrefix]
        private static void GenerateDonePrefix()
        {
            MapValueTracker.Logger.LogDebug("Generating Started. Resetting to zero.");
            MapValueTracker.CheckForItems();
            MapValueTracker.Logger.LogDebug("Generation done. Now val is " + MapValueTracker.totalValue);
        }
    }
}
