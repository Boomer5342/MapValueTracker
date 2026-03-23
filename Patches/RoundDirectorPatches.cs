using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text;
using HarmonyLib;
using MapValueTracker.Config;
using TMPro;
using UnityEngine;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(RoundDirector))]
    public static class RoundDirectorPatches
    {
        private static void SetCoordinates(RectTransform component)
        {
            switch (Configuration.UIPosition.Value) 
            {
                case Positions.Default:
                    component.pivot = new Vector2(1f, 1f);
                    component.anchoredPosition = new Vector2(1f, -1f);
                    component.anchorMin = new Vector2(0f, 0f);
                    component.anchorMax = new Vector2(1f, 0f);
                    component.sizeDelta = new Vector2(0f, 0f);
                    component.offsetMax = new Vector2(0f, 225f);
                    component.offsetMin = new Vector2(0f, 225f);
                    break;
                case Positions.LowerRight:
                    component.pivot = new Vector2(1f, 1f);
                    component.anchoredPosition = new Vector2(1f, -1f);
                    component.anchorMin = new Vector2(0f, 0f);
                    component.anchorMax = new Vector2(1f, 0f);
                    component.sizeDelta = new Vector2(0f, 0f);
                    component.offsetMax = new Vector2(0f, 125f);
                    component.offsetMin = new Vector2(0f, 125f);
                    break;
                case Positions.BottomRight:
                    component.pivot = new Vector2(1f, 1f);
                    component.anchoredPosition = new Vector2(1f, -1f);
                    component.anchorMin = new Vector2(0f, 0f);
                    component.anchorMax = new Vector2(1f, 0f);
                    component.sizeDelta = new Vector2(0f, 0f);
                    component.offsetMax = new Vector2(0f, 0f);
                    component.offsetMin = new Vector2(0f, 0f);
                    break;
                case Positions.Custom:
                    component.pivot = new Vector2(1f, 1f);
                    component.anchoredPosition = new Vector2(1f, -1f);
                    component.anchorMin = new Vector2(0f, 0f);
                    component.anchorMax = new Vector2(1f, 0f);
                    component.sizeDelta = new Vector2(0f, 0f);
                    component.offsetMax = Configuration.CustomPositionCoords.Value;
                    component.offsetMin = Configuration.CustomPositionCoords.Value;
                    break;
                default:
                    component.pivot = new Vector2(1f, 1f);
                    component.anchoredPosition = new Vector2(1f, -1f);
                    component.anchorMin = new Vector2(0f, 0f);
                    component.anchorMax = new Vector2(1f, 0f);
                    component.sizeDelta = new Vector2(0f, 0f);
                    component.offsetMax = new Vector2(0, 225f);
                    component.offsetMin = new Vector2(0f, 225f);
                    break;
            }
        }

        [HarmonyPatch("ExtractionCompleted")]
        [HarmonyPostfix]
        public static void ExtractionComplete()
        {
            if (!SemiFunc.RunIsLevel())
                return;

            MapValueTracker.Logger.LogDebug("Extraction Completed!");

            MapValueTracker.CheckForItems();

            MapValueTracker.Logger.LogDebug("Checked after Extraction. Val is " + MapValueTracker.totalValue);
        }

        [HarmonyPatch(typeof(RoundDirector), "Update")]
        [HarmonyPostfix]
        public static void UpdateUI()
        {
            int? currentGoal = Traverse.Create(RoundDirector.instance).Field("extractionHaulGoal").GetValue<int>();
            bool extractActive = Traverse.Create(RoundDirector.instance).Field("extractionPointActive").GetValue<bool>(); 
            bool allExtractionPointsCompleted = Traverse.Create(RoundDirector.instance).Field("allExtractionPointsCompleted").GetValue<bool>();

            if (!SemiFunc.RunIsLevel())
                return;

            if (MapValueTracker.textInstance == null)
            {
                GameObject hud = GameObject.Find("Game Hud");
                GameObject haul = GameObject.Find("Tax Haul");

                if (hud == null || haul == null)
                {
                    return;
                }

                TMP_FontAsset font = haul.GetComponent<TMP_Text>().font;
                MapValueTracker.textInstance = new GameObject();
                MapValueTracker.textInstance.SetActive(false);
                MapValueTracker.textInstance.name = "Value HUD";
                MapValueTracker.textInstance.AddComponent<TextMeshProUGUI>();

                MapValueTracker.valueText = MapValueTracker.textInstance.GetComponent<TextMeshProUGUI>();
                MapValueTracker.valueText.font = font;
                MapValueTracker.valueText.color = new Vector4(0.7882f, 0.9137f, 0.902f, 1);
                MapValueTracker.valueText.fontSize = 24f;
                MapValueTracker.valueText.enableWordWrapping = false;
                MapValueTracker.valueText.alignment = TextAlignmentOptions.BaselineRight;
                MapValueTracker.valueText.horizontalAlignment = HorizontalAlignmentOptions.Right;
                MapValueTracker.valueText.verticalAlignment = VerticalAlignmentOptions.Baseline;

                MapValueTracker.textInstance.transform.SetParent(hud.transform, false);

                RectTransform component = MapValueTracker.textInstance.GetComponent<RectTransform>();

                SetCoordinates(component);

                return;
            }
            if (MapValueTracker.valueText != null && ((currentGoal != null && currentGoal != 0) || !allExtractionPointsCompleted))
            {
                bool mapToggled = Traverse.Create(MapToolController.instance).Field("mapToggled").GetValue<bool>();

                RectTransform component = MapValueTracker.textInstance.GetComponent<RectTransform>();

                SetCoordinates(component);


                float mapValue = MapValueTracker.GetDisplayedMapValue();
                bool mapOpen = SemiFunc.InputHold(InputKey.Map) || mapToggled;
                bool needCarts = Configuration.ShowCartsValue.Value || Configuration.ShowRemainingValue.Value;
                bool needExtraction = Configuration.ShowExtractionValue.Value || Configuration.ShowRemainingValue.Value;
                float hudMapValue = mapValue;
                if (!mapOpen && Configuration.ReplaceHudMapWithRemaining.Value)
                {
                    MapValueTracker.UpdateBreakdownCache(false, true, true, true);
                    hudMapValue = Mathf.Max(0f, mapValue - MapValueTracker.cachedCartsValue - MapValueTracker.cachedExtractionValue);
                }

                string text = "Map: $" + hudMapValue.ToString("N0");

                // Only expand the breakdown when the map is open to reduce HUD noise.
                if (mapOpen && Configuration.ShowBreakdownOnMap.Value)
                {
                    MapValueTracker.UpdateBreakdownCache(mapOpen, false, needCarts, needExtraction);
                    float inCarts = 0f;
                    float inExtraction = 0f;

                    if (Configuration.ShowCartsValue.Value)
                    {
                        inCarts = MapValueTracker.cachedCartsValue;
                        text += "\nCarts: $" + inCarts.ToString("N0");
                    }

                    if (Configuration.ShowExtractionValue.Value)
                    {
                        inExtraction = MapValueTracker.cachedExtractionValue;
                        text += "\nExtraction: $" + inExtraction.ToString("N0");
                    }

                    if (Configuration.ShowRemainingValue.Value)
                    {
                        float remaining = Mathf.Max(0f, mapValue - inCarts - inExtraction);
                        text += "\nRemaining: $" + remaining.ToString("N0");
                    }
                }

                MapValueTracker.valueText.SetText(text);
                
                if (Configuration.AlwaysOn.Value)
                {
                    MapValueTracker.textInstance.SetActive(true);
                }
                else if (mapOpen)
                {
                    MapValueTracker.textInstance.SetActive(true);
                }
                else
                {
                    MapValueTracker.textInstance.SetActive(false);
                }
            }
            else
                MapValueTracker.textInstance.SetActive(false);
        }
    }
}
