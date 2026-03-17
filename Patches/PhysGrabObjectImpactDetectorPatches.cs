using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector))]
    public static class PhysGrabObjectImpactDetectorPatches
    {

        [HarmonyPatch("BreakRPC")]
        [HarmonyPostfix]
        static void StartPostFix(float valueLost, PhysGrabObjectImpactDetector? __instance, bool _loseValue)
        {
            if (!_loseValue)
                return;

            MapValueTracker.Logger.LogDebug("BreakRPC - Current Value: " + MapValueTracker.totalValue);

            ValuableObject vo = __instance?.GetComponent<ValuableObject>();

            MapValueTracker.Logger.LogDebug("BreakRPC - Valuable Object current value: " + (vo == null ? 0f : MapValueTracker.GetValuableCurrent(vo)));
            MapValueTracker.Logger.LogDebug("BreakRPC - Value lost: " + valueLost);

            MapValueTracker.totalValue -= valueLost;

            MapValueTracker.Logger.LogDebug("BreakRPC - After Break Value: " + MapValueTracker.totalValue);
        }

        [HarmonyPatch(typeof(PhysGrabObject), "DestroyPhysGrabObjectRPC")]
        [HarmonyPostfix]
        public static void DestroyPhysGrabObjectPostfix(PhysGrabObject __instance)
        {
            if (SemiFunc.RunIsLevel())
            {
                var vo = __instance.GetComponent<ValuableObject>();
                if (vo == null)
                    return;
                MapValueTracker.Logger.LogDebug("Destroying (DPGO)!");
                float current = MapValueTracker.GetValuableCurrent(vo);
                float original = MapValueTracker.GetValuableOriginal(vo);
                MapValueTracker.Logger.LogDebug("Destroyed Valuable Object! " + vo.name + " Val: " + current);
                if (current < original * 0.15f) //Workaround for duplicate destroyed objects vs extraction destruction
                    MapValueTracker.totalValue -= 0;
                else 
                    MapValueTracker.totalValue -= current;
                MapValueTracker.Logger.LogDebug("After DPGO Map Remaining Val: " + MapValueTracker.totalValue);
            }
        }

        /*[HarmonyPatch(typeof(ExtractionPoint), "DestroyTheFirstPhysObjectsInHaulList")]
        [HarmonyPrefix]
        public static void DestroyTheFirstPhysObjectsInHaulList()
        {
            if (SemiFunc.RunIsLevel())
            {
                if (SemiFunc.IsMasterClientOrSingleplayer() && RoundDirector.instance.dollarHaulList.Count != 0)
                {
                    if (RoundDirector.instance.dollarHaulList[0] && RoundDirector.instance.dollarHaulList[0].GetComponent<PhysGrabObject>())
                    {
                        MapValueTracker.Logger.LogDebug("Destroying (DAPOISL)!");
                        MapValueTracker.Logger.LogDebug("Destroyed Valuable Object! " + RoundDirector.instance.dollarHaulList[0].name + " Val: " + (int)RoundDirector.instance.dollarHaulList[0].GetComponent<ValuableObject>().dollarValueCurrent);
                        MapValueTracker.totalValue -= (int)RoundDirector.instance.dollarHaulList[0].GetComponent<ValuableObject>().dollarValueCurrent;
                        MapValueTracker.Logger.LogDebug("After DAPOISL Map Remaining Val: " + MapValueTracker.totalValue);
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(ExtractionPoint), "DestroyAllPhysObjectsInHaulList")]
        [HarmonyPrefix]
        public static void DestroyAllPhysObjectsInHaulList()
        {
            if (SemiFunc.RunIsLevel())
            {
                if (SemiFunc.IsMasterClientOrSingleplayer())
                {
                    foreach (GameObject gameObject in RoundDirector.instance.dollarHaulList)
                    {
                        if (gameObject && gameObject.GetComponent<PhysGrabObject>())
                        {
                            MapValueTracker.Logger.LogDebug("Destroying (DAPOISL)!");
                            MapValueTracker.Logger.LogDebug("Destroyed Valuable Object! " + gameObject.name + " Val: " + (int)gameObject.GetComponent<ValuableObject>().dollarValueCurrent);
                            MapValueTracker.totalValue -= (int)gameObject.GetComponent<ValuableObject>().dollarValueCurrent;
                            MapValueTracker.Logger.LogDebug("After DAPOISL Map Remaining Val: " + MapValueTracker.totalValue);
                        }
                    }
                }
            }
        }*/
    }
}
