using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapValueTracker.Patches
{
    [HarmonyPatch(typeof(RoundDirector))]
    public static class RoundDirectorPatches
    {
        private const float CompactHudWidth = 220f;
        private const float CompactHudHeight = 38f;
        private const float CompactHudFontSize = 21f;
        private const float CompactHudLineSpacing = -10f;
        private const float OpenPanelFontSize = 16f;
        private const float OpenPanelLineSpacing = -6f;
        private static readonly Color HudValueColor = new Color(0.7882f, 0.9137f, 0.902f, 1f);
        private static readonly Color HudLabelColor = new Color(0.7882f, 0.9137f, 0.902f, 0.86f);
        private static readonly Color HudPanelColor = new Color(0.035f, 0.05f, 0.06f, 0.3f);
        private static TextMeshProUGUI? openLabelsText;
        private static TextMeshProUGUI? openValuesText;
        private static GameObject? openPanel;
        private static Image? openPanelImage;
        private static ValueBreakdownSnapshot lastRenderedSnapshot;
        private static bool hasRenderedSnapshot;
        private static string lastOpenLabels = string.Empty;
        private static string lastOpenValues = string.Empty;
        private static string lastClosedMapText = string.Empty;
        private const float OpenPanelPaddingX = 10f;
        private const float OpenPanelPaddingY = 8f;
        private const float OpenPanelColumnGap = 16f;

        private static void SetContainerCoordinates(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetOverlayCoordinates(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
        }

        private static void ResetHudReferences()
        {
            MapValueTracker.textInstance = null;
            MapValueTracker.valueText = null;
            openPanel = null;
            openPanelImage = null;
            openLabelsText = null;
            openValuesText = null;
            hasRenderedSnapshot = false;
            lastOpenLabels = string.Empty;
            lastOpenValues = string.Empty;
            lastClosedMapText = string.Empty;
        }

        private static bool EnsureHud()
        {
            if (MapValueTracker.textInstance && MapValueTracker.valueText && openPanel && openPanelImage && openLabelsText && openValuesText)
            {
                return true;
            }

            ResetHudReferences();

            GameObject hud = GameObject.Find("Game Hud");
            GameObject haul = GameObject.Find("Tax Haul");
            if (hud == null || haul == null)
            {
                return false;
            }

            TMP_Text haulText = haul.GetComponent<TMP_Text>();
            if (haulText == null)
            {
                return false;
            }

            MapValueTracker.textInstance = new GameObject("Value HUD", typeof(RectTransform));
            MapValueTracker.textInstance.SetActive(false);
            MapValueTracker.textInstance.transform.SetParent(hud.transform, false);
            RectTransform containerRect = MapValueTracker.textInstance.GetComponent<RectTransform>();
            SetContainerCoordinates(containerRect);
            MapValueTracker.valueText = CreatePanelText("Compact Value Text", haulText, MapValueTracker.textInstance.transform, TextAlignmentOptions.TopRight, HudValueColor, 0.24f, 0.18f);

            RectTransform rect = MapValueTracker.valueText.rectTransform;
            SetOverlayCoordinates(rect, Config.Configuration.GetCompactOffset());
            rect.sizeDelta = new Vector2(CompactHudWidth, CompactHudHeight);
            MapValueTracker.valueText.fontSize = CompactHudFontSize;
            MapValueTracker.valueText.lineSpacing = CompactHudLineSpacing;

            openPanel = new GameObject("Open Map Panel", typeof(RectTransform), typeof(CanvasRenderer));
            openPanel.transform.SetParent(MapValueTracker.textInstance.transform, false);
            openPanelImage = openPanel.AddComponent<Image>();
            openPanelImage.color = HudPanelColor;
            openPanelImage.raycastTarget = false;

            RectTransform panelRect = openPanelImage.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = Config.Configuration.GetOpenMapOffset();
            panelRect.sizeDelta = new Vector2(178f, 86f);

            openLabelsText = CreatePanelText("Open Map Labels", haulText, openPanel.transform, TextAlignmentOptions.TopLeft, HudLabelColor, 0.2f, 0.16f);
            openValuesText = CreatePanelText("Open Map Values", haulText, openPanel.transform, TextAlignmentOptions.TopRight, HudValueColor, 0.2f, 0.16f);

            MapValueTracker.LogDebug("Created Value HUD through RoundDirector patch.");
            return true;
        }

        private static Material CreateHudMaterial(Material sourceMaterial, float underlayDilate, float underlaySoftness)
        {
            Material material = new Material(sourceMaterial);
            if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.72f));
            }

            if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
            {
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            }

            if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
            {
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.18f);
            }

            if (material.HasProperty(ShaderUtilities.ID_UnderlayDilate))
            {
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, underlayDilate);
            }

            if (material.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            {
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, underlaySoftness);
            }

            return material;
        }

        private static TextMeshProUGUI CreatePanelText(string name, TMP_Text haulText, Transform parent, TextAlignmentOptions alignment, Color color, float underlayDilate, float underlaySoftness)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = haulText.font;
            text.fontSharedMaterial = CreateHudMaterial(haulText.fontSharedMaterial, underlayDilate, underlaySoftness);
            text.color = color;
            text.fontSize = OpenPanelFontSize;
            text.enableWordWrapping = false;
            text.alignment = alignment;
            text.horizontalAlignment = alignment == TextAlignmentOptions.TopRight
                ? HorizontalAlignmentOptions.Right
                : HorizontalAlignmentOptions.Left;
            text.verticalAlignment = VerticalAlignmentOptions.Top;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = true;
            text.lineSpacing = OpenPanelLineSpacing;
            return text;
        }

        private static string ColorTag(Color color)
        {
            return ColorUtility.ToHtmlStringRGBA(color);
        }

        private static string FormatLabelValueLine(string label, string value)
        {
            string labelColor = ColorTag(HudLabelColor);
            string valueColor = ColorTag(HudValueColor);
            return $"<size=76%><color=#{labelColor}>{label.ToUpperInvariant()}</color></size>  <color=#{valueColor}>{value}</color>";
        }

        private static string FormatCurrency(float value)
        {
            return "$" + value.ToString("N0");
        }

        private static string FormatHaul(int currentHaul, int haulGoal)
        {
            return "$" + SemiFunc.DollarGetString(currentHaul) + " / $" + SemiFunc.DollarGetString(haulGoal);
        }

        private static string BuildOpenMapLabels()
        {
            if (Config.Configuration.IsFullBreakdown())
            {
                return "MAP\nCARTS\nHAULER\nREMAINING";
                // return "MAP\nCARTS\nHAULER\nEXTRACTION\nREMAINING";
            }

            return "MAP\nREMAINING";
            // return "MAP\nREMAINING\nHAUL";
        }

        private static string BuildOpenMapValues(ValueBreakdownSnapshot snapshot)
        {
            if (Config.Configuration.IsFullBreakdown())
            {
                return string.Join("\n",
                    FormatCurrency(snapshot.MapValue),
                    FormatCurrency(snapshot.CartsValue),
                    FormatCurrency(snapshot.HaulerValue),
                    FormatCurrency(snapshot.RemainingValue));
                    // FormatCurrency(snapshot.ExtractionValue),
            }

            return string.Join("\n",
                FormatCurrency(snapshot.MapValue),
                FormatCurrency(snapshot.RemainingValue));
                // FormatHaul(snapshot.CurrentHaul, snapshot.HaulGoal));
        }

        private static string BuildClosedMapText(ValueBreakdownSnapshot snapshot)
        {
            return FormatLabelValueLine(snapshot.PrimaryLabel, FormatCurrency(snapshot.PrimaryValue));
        }

        private static void UpdateOpenPanelLayout()
        {
            if (openPanelImage == null || openLabelsText == null || openValuesText == null)
            {
                return;
            }

            openLabelsText.ForceMeshUpdate();
            openValuesText.ForceMeshUpdate();

            float labelsWidth = Mathf.Ceil(openLabelsText.preferredWidth);
            float valuesWidth = Mathf.Ceil(openValuesText.preferredWidth);
            float labelsHeight = Mathf.Ceil(openLabelsText.preferredHeight);
            float valuesHeight = Mathf.Ceil(openValuesText.preferredHeight);
            float contentHeight = Mathf.Max(labelsHeight, valuesHeight);

            RectTransform panelRect = openPanelImage.rectTransform;
            panelRect.sizeDelta = new Vector2(
                OpenPanelPaddingX * 2f + labelsWidth + OpenPanelColumnGap + valuesWidth,
                OpenPanelPaddingY * 2f + contentHeight);

            RectTransform labelsRect = openLabelsText.rectTransform;
            labelsRect.anchorMin = new Vector2(0f, 1f);
            labelsRect.anchorMax = new Vector2(0f, 1f);
            labelsRect.pivot = new Vector2(0f, 1f);
            labelsRect.anchoredPosition = new Vector2(OpenPanelPaddingX, -OpenPanelPaddingY);
            labelsRect.sizeDelta = new Vector2(labelsWidth, contentHeight);

            RectTransform valuesRect = openValuesText.rectTransform;
            valuesRect.anchorMin = new Vector2(1f, 1f);
            valuesRect.anchorMax = new Vector2(1f, 1f);
            valuesRect.pivot = new Vector2(1f, 1f);
            valuesRect.anchoredPosition = new Vector2(-OpenPanelPaddingX, -OpenPanelPaddingY);
            valuesRect.sizeDelta = new Vector2(valuesWidth, contentHeight);
        }

        private static void HideHud()
        {
            GameObject? hudRoot = MapValueTracker.textInstance;
            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
            else
            {
                ResetHudReferences();
            }

            hasRenderedSnapshot = false;
        }

        private static void UpdateCompactHud(ValueBreakdownSnapshot snapshot)
        {
            if (MapValueTracker.valueText == null)
            {
                return;
            }

            RectTransform compactRect = MapValueTracker.valueText.rectTransform;
            SetOverlayCoordinates(compactRect, Config.Configuration.GetCompactOffset());
            MapValueTracker.valueText.fontSize = CompactHudFontSize;
            string nextText = BuildClosedMapText(snapshot);
            if (!string.Equals(lastClosedMapText, nextText))
            {
                MapValueTracker.valueText.SetText(nextText);
                lastClosedMapText = nextText;
            }
        }

        private static void UpdateOpenMapHud(ValueBreakdownSnapshot snapshot)
        {
            if (openPanelImage == null || openLabelsText == null || openValuesText == null)
            {
                return;
            }

            RectTransform panelRect = openPanelImage.rectTransform;
            SetOverlayCoordinates(panelRect, Config.Configuration.GetOpenMapOffset());
            string labels = BuildOpenMapLabels();
            string values = BuildOpenMapValues(snapshot);
            bool contentChanged = !string.Equals(lastOpenLabels, labels) || !string.Equals(lastOpenValues, values);
            if (contentChanged)
            {
                openLabelsText.SetText(labels);
                openValuesText.SetText(values);
                lastOpenLabels = labels;
                lastOpenValues = values;
                UpdateOpenPanelLayout();
            }
        }

        [HarmonyPatch("ExtractionCompleted")]
        [HarmonyPostfix]
        public static void ExtractionComplete()
        {
            if (!SemiFunc.RunIsLevel() || !MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.Logger.LogDebug("Extraction Completed!");
            MapValueTracker.CheckForItems();
            MapValueTracker.Logger.LogDebug("Checked after Extraction. Val is " + MapValueTracker.totalValue);
        }

        [HarmonyPatch("HaulCheck")]
        [HarmonyPostfix]
        public static void HaulCheckPostfix()
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.MarkDirty();
        }

        [HarmonyPatch("ExtractionCompletedAllRPC")]
        [HarmonyPostfix]
        public static void ExtractionCompletedAllPostfix()
        {
            if (!MapValueTracker.IsRuntimeEnabled())
            {
                return;
            }

            MapValueTracker.MarkDirty();
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdateHud()
        {
            if (!MapValueTracker.IsRuntimeEnabled() || !SemiFunc.RunIsLevel() || RoundDirector.instance == null)
            {
                HideHud();
                return;
            }

            if (!EnsureHud())
            {
                return;
            }

            ValueBreakdownSnapshot snapshot = MapValueTracker.GetSnapshot();
            bool visible = snapshot.ShowCompactHud || snapshot.ShowMapPanel;
            if (!visible)
            {
                HideHud();
                return;
            }

            bool mapOpen = snapshot.MapOpen;
            if (MapValueTracker.valueText == null || openPanel == null || openPanelImage == null || openLabelsText == null || openValuesText == null)
            {
                return;
            }

            openPanel.SetActive(mapOpen);
            MapValueTracker.valueText.gameObject.SetActive(!mapOpen);

            bool snapshotChanged = !hasRenderedSnapshot || !snapshot.Equals(lastRenderedSnapshot);
            if (mapOpen)
            {
                if (snapshotChanged || !openPanel.activeSelf)
                {
                    UpdateOpenMapHud(snapshot);
                }
            }
            else
            {
                if (snapshotChanged || !MapValueTracker.valueText.gameObject.activeSelf)
                {
                    UpdateCompactHud(snapshot);
                }
            }

            MapValueTracker.textInstance?.SetActive(true);
            lastRenderedSnapshot = snapshot;
            hasRenderedSnapshot = true;
        }
    }
}
