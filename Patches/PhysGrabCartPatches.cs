using HarmonyLib;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(PhysGrabCart))]
    internal static class PhysGrabCartPatches
    {
        [HarmonyPatch("ObjectsInCart")]
        [HarmonyPostfix]
        private static void ObjectsInCartPostfix()
        {
            if (SemiFunc.RunIsLevel())
            {
                MapValueTracker.MarkDirty();
            }
        }
    }
}
