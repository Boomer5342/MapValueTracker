using HarmonyLib;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(ValuableObject))]
    internal static class ValuableObjectPatches
    {
        [HarmonyPatch("DollarValueSetRPC")]
        [HarmonyPostfix]
        private static void DollarValueSetRpcPostfix(ValuableObject __instance, float value)
        {
            LogValueCreated(__instance.name, value);
            MapValueTracker.totalValue += value;
            LogTotalValue("After dollar value set Total Val: ");
            MapValueTracker.MarkDirty(forceBreakdown: true);
        }

        [HarmonyPatch("DollarValueSetLogic")]
        [HarmonyPostfix]
        private static void DollarValueSetLogicPostfix(ValuableObject __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            float current = MapValueTracker.GetValuableCurrent(__instance);
            LogValueCreated(__instance.name, current);
            MapValueTracker.totalValue += current;
            LogTotalValue("After dollar value set Total Val: ");
            MapValueTracker.MarkDirty(forceBreakdown: true);
        }

        [HarmonyPatch("AddToDollarHaulListRPC")]
        [HarmonyPostfix]
        private static void AddToDollarHaulListPostfix()
        {
            MapValueTracker.MarkDirty(forceBreakdown: true);
        }

        [HarmonyPatch("RemoveFromDollarHaulListRPC")]
        [HarmonyPostfix]
        private static void RemoveFromDollarHaulListPostfix()
        {
            MapValueTracker.MarkDirty(forceBreakdown: true);
        }

        private static void LogValueCreated(string objectName, float value)
        {
            MapValueTracker.Logger.LogDebug("Created Valuable Object! " + objectName + " Val: " + value);
        }

        private static void LogTotalValue(string prefix)
        {
            MapValueTracker.Logger.LogDebug(prefix + MapValueTracker.totalValue);
        }
    }
}
