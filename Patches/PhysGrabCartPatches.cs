using HarmonyLib;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(PhysGrabCart))]
    internal static class PhysGrabCartPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(PhysGrabCart __instance)
        {
            MapValueTracker.RegisterCart(__instance);
        }

        [HarmonyPatch("ObjectsInCart")]
        [HarmonyPostfix]
        private static void ObjectsInCartPostfix()
        {
            if (SemiFunc.RunIsLevel() && MapValueTracker.IsRuntimeEnabled())
            {
                MapValueTracker.MarkDirty();
            }
        }
    }
}
