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

            MapValueTracker.Logger.LogDebug("BreakRPC - Current Value: " + MapValueTracker.totalValue);
            MapValueTracker.Logger.LogDebug("BreakRPC - Valuable Object current value: " + (valuableObject == null ? 0f : MapValueTracker.GetValuableCurrent(valuableObject)));
            MapValueTracker.Logger.LogDebug("BreakRPC - Value lost: " + valueLost);

            MapValueTracker.RegisterOrRefreshValuable(valuableObject);

            MapValueTracker.Logger.LogDebug("BreakRPC - After Break Value: " + MapValueTracker.totalValue);
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

            MapValueTracker.Logger.LogDebug("Destroying (DPGO)!");
            float current = MapValueTracker.GetValuableCurrent(valuableObject);
            MapValueTracker.Logger.LogDebug("Destroyed Valuable Object! " + valuableObject.name + " Val: " + current);
            MapValueTracker.UnregisterValuable(valuableObject);

            MapValueTracker.Logger.LogDebug("After DPGO Map Remaining Val: " + MapValueTracker.totalValue);
        }
    }
}
