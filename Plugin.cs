using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using MapValueTracker.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MapValueTracker
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class MapValueTracker : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "MapValueTrackerPlus";
        public const string PLUGIN_NAME = "Map Value Tracker Plus";
        public const string PLUGIN_VERSION = "1.0.3";

        public static new ManualLogSource Logger;
        private readonly Harmony harmony = new Harmony("MapValueTrackerPlus.REPO");

        public static MapValueTracker instance;
        public static GameObject textInstance;
        public static TextMeshProUGUI valueText;

        public static float totalValue = 0f;
        public static float cachedCartsValue = 0f;
        public static float cachedExtractionValue = 0f;
        private static float lastBreakdownTime = -100000f;
        private static bool lastMapOpen = false;
        private static readonly FieldInfo cartHaulCurrentField = AccessTools.Field(typeof(PhysGrabCart), "haulCurrent");
        private static readonly FieldInfo cartItemsInCartField = AccessTools.Field(typeof(PhysGrabCart), "itemsInCart");
        private static bool cartFieldWarningLogged;

        public void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");

            if (instance == null)
            {
                instance = this;
            }

            Configuration.Init(Config);

            harmony.PatchAll();
        }

        public static void ResetValues()
        {
            if (!SemiFunc.RunIsLevel())
                totalValue = 0;

            Logger.LogDebug("In ResetValues()");

            Logger.LogDebug("Total Map Value: " + totalValue);
        }

        public static void CheckForItems(ValuableObject? ignoreThis = null)
        {
            if (!Traverse.Create(RoundDirector.instance).Field("allExtractionPointsCompleted").GetValue<bool>())
            {
                totalValue = 0f;
                List<ValuableObject> valuebleObjects = UnityEngine.Object.FindObjectsOfType<ValuableObject>().ToList();

                if (ignoreThis != null)
                {
                    valuebleObjects.Remove(ignoreThis);
                }
                for (int i = 0; i < valuebleObjects.Count; i++)
                {
                    totalValue += GetValuableCurrent(valuebleObjects[i]);
                }
                MapValueTracker.Logger.LogDebug("After CheckForItems Total Val: " + MapValueTracker.totalValue);
            }
        }

        /// <summary>
        /// Sums the value of valuables inside any cart.
        /// Excludes items that are already on extraction to avoid double counting.
        /// </summary>
        public static float ComputeValueInCarts()
        {
            GetExtractionSets(out HashSet<ValuableObject> extractionValuables);
            return ComputeValueInCarts(extractionValuables);
        }

        private static void ComputeBreakdownValues(bool needCarts, bool needExtraction, out float cartsValue, out float extractionValue)
        {
            cartsValue = 0f;
            extractionValue = 0f;

            if (!needCarts && !needExtraction)
                return;

            GetExtractionSets(out HashSet<ValuableObject> extractionValuables);

            if (needExtraction)
            {
                foreach (ValuableObject vo in extractionValuables)
                {
                    extractionValue += GetValuableCurrent(vo);
                }
            }

            if (needCarts)
            {
                cartsValue = ComputeValueInCarts(extractionValuables);
            }
        }

        private static float ComputeValueInCarts(HashSet<ValuableObject> extractionValuables)
        {
            PhysGrabCart[] carts = UnityEngine.Object.FindObjectsOfType<PhysGrabCart>();
            if (carts == null || carts.Length == 0)
                return 0f;

            if (extractionValuables == null || extractionValuables.Count == 0)
            {
                float fastSum = 0f;
                for (int i = 0; i < carts.Length; i++)
                {
                    if (TryGetCartHaulCurrent(carts[i], out int haulCurrent))
                        fastSum += haulCurrent;
                }
                return fastSum;
            }

            float sum = 0f;
            HashSet<ValuableObject> counted = new HashSet<ValuableObject>();
            for (int i = 0; i < carts.Length; i++)
            {
                PhysGrabCart cart = carts[i];
                if (!TryGetCartItemsInCart(cart, out List<PhysGrabObject> itemsInCart))
                    continue;

                for (int itemIndex = 0; itemIndex < itemsInCart.Count; itemIndex++)
                {
                    PhysGrabObject physObj = itemsInCart[itemIndex];
                    if (physObj == null)
                        continue;

                    ValuableObject vo = physObj.GetComponent<ValuableObject>();
                    if (vo == null)
                        continue;

                    if (!counted.Add(vo))
                        continue;

                    if (extractionValuables.Contains(vo))
                        continue;

                    sum += GetValuableCurrent(vo);
                }
            }

            return sum;
        }

        private static bool TryGetCartHaulCurrent(PhysGrabCart cart, out int value)
        {
            value = 0;
            if (cart == null)
                return false;

            if (cartHaulCurrentField == null)
            {
                LogMissingCartFieldWarning("haulCurrent");
                return false;
            }

            object raw = cartHaulCurrentField.GetValue(cart);
            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            if (raw is IConvertible convertible)
            {
                try
                {
                    value = Convert.ToInt32(convertible);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryGetCartItemsInCart(PhysGrabCart cart, out List<PhysGrabObject> items)
        {
            items = null;
            if (cart == null)
                return false;

            if (cartItemsInCartField == null)
            {
                LogMissingCartFieldWarning("itemsInCart");
                return false;
            }

            object raw = cartItemsInCartField.GetValue(cart);
            if (raw is List<PhysGrabObject> list)
            {
                items = list;
                return true;
            }

            return false;
        }

        private static void LogMissingCartFieldWarning(string fieldName)
        {
            if (cartFieldWarningLogged)
                return;

            cartFieldWarningLogged = true;
            Logger.LogWarning($"Unable to access PhysGrabCart.{fieldName}. Cart values will be treated as $0.");
        }

        /// <summary>
        /// Updates cached breakdown values on a time interval while the map is open.
        /// If allowWhenClosed is true, it also updates while the map is closed.
        /// </summary>
        public static void UpdateBreakdownCache(bool mapOpen, bool allowWhenClosed, bool needCarts, bool needExtraction)
        {
            float interval = Math.Max(0.1f, Configuration.BreakdownUpdateIntervalSeconds.Value);
            float now = Time.unscaledTime;
            bool force = mapOpen && !lastMapOpen;

            if (!mapOpen && !allowWhenClosed)
            {
                lastMapOpen = false;
                return;
            }

            if (!force && (now - lastBreakdownTime) < interval)
            {
                lastMapOpen = true;
                return;
            }

            ComputeBreakdownValues(needCarts, needExtraction, out cachedCartsValue, out cachedExtractionValue);
            lastBreakdownTime = now;
            lastMapOpen = mapOpen;
        }

        /// <summary>
        /// Builds sets of valuables and objects currently staged for extraction.
        /// </summary>
        private static void GetExtractionSets(out HashSet<ValuableObject> extractionValuables)
        {
            extractionValuables = new HashSet<ValuableObject>();

            if (RoundDirector.instance == null || RoundDirector.instance.dollarHaulList == null)
                return;

            List<GameObject> haulList = RoundDirector.instance.dollarHaulList;
            for (int i = 0; i < haulList.Count; i++)
            {
                GameObject go = haulList[i];
                if (go == null)
                    continue;

                ValuableObject vo = go.GetComponent<ValuableObject>();
                if (vo != null)
                    extractionValuables.Add(vo);
            }
        }

        /// <summary>
        /// Reads the current value from a ValuableObject across possible field/property names.
        /// </summary>
        public static float GetValuableCurrent(ValuableObject vo)
        {
            return TryGetFloat(vo,
                "dollarValueCurrent",
                "dollarValue",
                "value",
                "Value",
                "currentValue",
                "CurrentValue"
            );
        }

        /// <summary>
        /// Reads the original value from a ValuableObject across possible field/property names.
        /// </summary>
        public static float GetValuableOriginal(ValuableObject vo)
        {
            float original = TryGetFloat(vo,
                "dollarValueOriginal",
                "dollarValueStart",
                "originalValue",
                "OriginalValue",
                "baseValue",
                "BaseValue"
            );

            if (original <= 0f)
                original = GetValuableCurrent(vo);

            return original;
        }

        /// <summary>
        /// Reflection helper to read a float-like value from common field/property names.
        /// </summary>
        private static float TryGetFloat(object obj, params string[] names)
        {
            if (obj == null)
                return 0f;

            Type type = obj.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                var field = AccessTools.Field(type, name);
                if (field != null)
                {
                    object value = field.GetValue(obj);
                    if (TryConvertToFloat(value, out float result))
                        return result;
                }

                var prop = AccessTools.Property(type, name);
                if (prop != null)
                {
                    object value = prop.GetValue(obj, null);
                    if (TryConvertToFloat(value, out float result))
                        return result;
                }
            }

            return 0f;
        }

        /// <summary>
        /// Converts numeric objects to float without throwing.
        /// </summary>
        private static bool TryConvertToFloat(object value, out float result)
        {
            if (value == null)
            {
                result = 0f;
                return false;
            }

            if (value is float f)
            {
                result = f;
                return true;
            }
            if (value is int i)
            {
                result = i;
                return true;
            }
            if (value is double d)
            {
                result = (float)d;
                return true;
            }
            if (value is long l)
            {
                result = l;
                return true;
            }

            if (value is IConvertible)
            {
                try
                {
                    result = Convert.ToSingle(value);
                    return true;
                }
                catch
                {
                    result = 0f;
                    return false;
                }
            }

            result = 0f;
            return false;
        }

        // Intentionally no per-object cart check helper; cart value is computed from cart components.
    }
}
