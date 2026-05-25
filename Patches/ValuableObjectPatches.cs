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
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            LogValueCreated(__instance.name, value);
            MapValueTracker.RegisterOrRefreshValuable(__instance);
            LogTotalValue("Total value after setup: ");
        }

        [HarmonyPatch("DollarValueSetLogic")]
        [HarmonyPostfix]
        private static void DollarValueSetLogicPostfix(ValuableObject __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            float current = MapValueTracker.GetValuableCurrent(__instance);
            LogValueCreated(__instance.name, current);
            MapValueTracker.RegisterOrRefreshValuable(__instance);
            LogTotalValue("Total value after setup: ");
        }

        [HarmonyPatch("AddToDollarHaulList")]
        [HarmonyPostfix]
        private static void AddToDollarHaulListDirectPostfix(ValuableObject __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.MarkDirty();
        }

        [HarmonyPatch("AddToDollarHaulListRPC")]
        [HarmonyPostfix]
        private static void AddToDollarHaulListPostfix(ValuableObject __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.MarkDirty();
        }

        [HarmonyPatch("RemoveFromDollarHaulList")]
        [HarmonyPostfix]
        private static void RemoveFromDollarHaulListDirectPostfix(ValuableObject __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.MarkDirty();
        }

        [HarmonyPatch("RemoveFromDollarHaulListRPC")]
        [HarmonyPostfix]
        private static void RemoveFromDollarHaulListPostfix(ValuableObject __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.MarkDirty();
        }

        private static void LogValueCreated(string objectName, float value)
        {
            MapValueTracker.Logger.LogDebug("Tracking valuable: " + objectName + " (" + value + ")");
        }

        private static void LogTotalValue(string prefix)
        {
            MapValueTracker.Logger.LogDebug(prefix + MapValueTracker.totalValue);
        }
    }
}
