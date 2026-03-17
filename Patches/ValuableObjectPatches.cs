using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(ValuableObject))]
    static class ValuableObjectPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(ValuableObject __instance)
        {
            //__instance.gameObject.AddComponent<MyOnDestroy>();
            //MapValueTracker.Logger.LogDebug("Added OnDestroy");
        }
        [HarmonyPatch("DollarValueSetRPC")]
        [HarmonyPostfix]
        static void DollarValueSet(ValuableObject __instance, float value)
        {
            MapValueTracker.Logger.LogDebug("Created Valuable Object! " + __instance.name + " Val: " + value);
            MapValueTracker.totalValue += value;
            //MapValueTracker.CheckForItems();
            MapValueTracker.Logger.LogDebug("After dollar value set Total Val: " + MapValueTracker.totalValue);
        }
        [HarmonyPatch("DollarValueSetLogic")]
        [HarmonyPostfix]
        static void DollarValueSetLogic(ValuableObject __instance)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                float current = MapValueTracker.GetValuableCurrent(__instance);
                MapValueTracker.Logger.LogDebug("Created Valuable Object! " + __instance.name + " Val: " + current);
                MapValueTracker.totalValue += current;
                //MapValueTracker.CheckForItems();
                MapValueTracker.Logger.LogDebug("After dollar value set Total Val: " + MapValueTracker.totalValue);
            }
        }
    }
}
