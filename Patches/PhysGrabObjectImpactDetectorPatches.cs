using HarmonyLib;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector))]
    internal static class PhysGrabObjectImpactDetectorPatches
    {
        [HarmonyPatch("BreakRPC")]
        [HarmonyPostfix]
        private static void BreakRpcPostfix(float valueLost, PhysGrabObjectImpactDetector? __instance, bool _loseValue)
        {
            if (!_loseValue || !MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            ValuableObject? valuableObject = __instance?.GetComponent<ValuableObject>();

            MapValueTracker.RegisterOrRefreshValuable(valuableObject);
            MapValueTracker.Logger.LogDebug("Updated tracked value after break. Lost: " + valueLost + ", total: " + MapValueTracker.totalValue);
        }

        [HarmonyPatch(typeof(PhysGrabObject), "DestroyPhysGrabObjectRPC")]
        [HarmonyPostfix]
        private static void DestroyPhysGrabObjectPostfix(PhysGrabObject __instance)
        {
            if (!SemiFunc.RunIsLevel() || !MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            PhysGrabCart? cart = __instance.GetComponent<PhysGrabCart>();
            if (cart != null)
            {
                MapValueTracker.UnregisterCart(cart);
            }

            ValuableObject? valuableObject = __instance.GetComponent<ValuableObject>();
            if (valuableObject == null)
            {
                return;
            }

            float current = MapValueTracker.GetValuableCurrent(valuableObject);
            MapValueTracker.UnregisterValuable(valuableObject);
            MapValueTracker.Logger.LogDebug("Removed destroyed valuable from tracking: " + valuableObject.name + " (" + current + "). Total: " + MapValueTracker.totalValue);
        }
    }
}
