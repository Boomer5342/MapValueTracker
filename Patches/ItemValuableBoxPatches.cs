using HarmonyLib;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(ItemValuableBox))]
    internal static class ItemValuableBoxPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(ItemValuableBox __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.RegisterOrRefreshValuableBox(__instance);
        }

        [HarmonyPatch("UpdateValueRPC")]
        [HarmonyPostfix]
        private static void UpdateValueRpcPostfix(ItemValuableBox __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.RegisterOrRefreshValuableBox(__instance);
        }

        [HarmonyPatch("ExplodeValuableBox")]
        [HarmonyPostfix]
        private static void ExplodeValuableBoxPostfix(ItemValuableBox __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.RegisterOrRefreshValuableBox(__instance);
        }

        [HarmonyPatch("OnDisable")]
        [HarmonyPostfix]
        private static void OnDisablePostfix(ItemValuableBox __instance)
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.RegisterOrRefreshValuableBox(__instance);
        }

        [HarmonyPatch("OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroyPostfix(ItemValuableBox __instance)
        {
            MapValueTracker.UnregisterValuableBox(__instance);
        }
    }
}
