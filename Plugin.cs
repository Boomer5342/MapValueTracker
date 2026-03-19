using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using MapValueTracker.Config;
using System;
using System.Collections;
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
        public const string PLUGIN_VERSION = "1.0.1";

        public static new ManualLogSource Logger;
        private readonly Harmony harmony = new Harmony("MapValueTrackerPlus.REPO");

        public static MapValueTracker instance;
        public static GameObject textInstance;
        public static TextMeshProUGUI valueText;

        public static float totalValue = 0f;
        public static float totalValueInit = 0f;
        public static float cachedCartsValue = 0f;
        public static float cachedExtractionValue = 0f;
        private static int lastBreakdownFrame = -100000;
        private static bool lastMapOpen = false;
        private static int lastCartScanFrame = -100000;
        private static List<Component> cachedCartComponents = new List<Component>();
        private static bool cartsDirty = true;
        private static readonly Dictionary<Type, FieldInfo[]> cartFieldsCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, PropertyInfo[]> cartPropsCache = new Dictionary<Type, PropertyInfo[]>();

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
        /// Returns the map value to display based on config (live value or initial value).
        /// </summary>
        public static float GetDisplayedMapValue()
        {
            return Configuration.StartingValueOnly.Value ? totalValueInit : totalValue;
        }

        /// <summary>
        /// Sums the value currently staged for extraction (haul list).
        /// </summary>
        public static float ComputeValueInExtraction()
        {
            if (RoundDirector.instance == null)
                return 0f;

            GetExtractionSets(out HashSet<ValuableObject> extractionValuables, out _);

            float sum = 0f;
            foreach (ValuableObject vo in extractionValuables)
            {
                sum += GetValuableCurrent(vo);
            }

            return sum;
        }

        /// <summary>
        /// Sums the value of valuables inside any cart (including pocket carts).
        /// Excludes items that are already on extraction to avoid double counting.
        /// </summary>
        public static float ComputeValueInCarts()
        {
            GetExtractionSets(out HashSet<ValuableObject> extractionValuables, out HashSet<GameObject> extractionObjects);
            return ComputeValueInCarts(extractionValuables, extractionObjects);
        }

        private static void ComputeBreakdownValues(bool needCarts, bool needExtraction, out float cartsValue, out float extractionValue)
        {
            cartsValue = 0f;
            extractionValue = 0f;

            if (!needCarts && !needExtraction)
                return;

            GetExtractionSets(out HashSet<ValuableObject> extractionValuables, out HashSet<GameObject> extractionObjects);

            if (needExtraction)
            {
                foreach (ValuableObject vo in extractionValuables)
                {
                    extractionValue += GetValuableCurrent(vo);
                }
            }

            if (needCarts)
            {
                cartsValue = ComputeValueInCarts(extractionValuables, extractionObjects);
            }
        }

        private static float ComputeValueInCarts(HashSet<ValuableObject> extractionValuables, HashSet<GameObject> extractionObjects)
        {
            float sum = 0f;
            HashSet<ValuableObject> counted = new HashSet<ValuableObject>();
            List<Component> cartComponents = GetCachedCartComponents();

            for (int i = 0; i < cartComponents.Count; i++)
            {
                Component comp = cartComponents[i];
                if (comp == null)
                    continue;

                if (IsComponentInExtraction(comp, extractionObjects))
                    continue;

                // Reflection-based scan so we don't depend on cart type names beyond "*Cart*".
                CollectValuablesFromComponent(comp, counted);
            }

            foreach (ValuableObject vo in counted)
            {
                if (extractionValuables.Contains(vo))
                    continue;
                sum += GetValuableCurrent(vo);
            }

            return sum;
        }

        private static List<Component> GetCachedCartComponents()
        {
            if (!Configuration.EnableCartComponentCaching.Value)
            {
                cachedCartComponents.Clear();
                Component[] immediate = UnityEngine.Object.FindObjectsOfType<Component>();
                for (int i = 0; i < immediate.Length; i++)
                {
                    Component comp = immediate[i];
                    if (comp == null)
                        continue;

                    string typeName = comp.GetType().Name;
                    if (typeName.IndexOf("Cart", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cachedCartComponents.Add(comp);
                    }
                }
                return cachedCartComponents;
            }

            int interval = Math.Max(1, Configuration.CartRescanIntervalFrames.Value);
            int frame = Time.frameCount;

            if (!cartsDirty && (frame - lastCartScanFrame) < interval && cachedCartComponents.Count > 0)
                return cachedCartComponents;

            cachedCartComponents.Clear();
            Component[] components = UnityEngine.Object.FindObjectsOfType<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component comp = components[i];
                if (comp == null)
                    continue;

                string typeName = comp.GetType().Name;
                if (typeName.IndexOf("Cart", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cachedCartComponents.Add(comp);
                }
            }

            lastCartScanFrame = frame;
            cartsDirty = false;
            return cachedCartComponents;
        }

        /// <summary>
        /// Updates cached breakdown values on a frame interval while the map is open.
        /// If allowWhenClosed is true, it also updates while the map is closed.
        /// </summary>
        public static void UpdateBreakdownCache(bool mapOpen, bool allowWhenClosed, bool needCarts, bool needExtraction)
        {
            int interval = Math.Max(1, Configuration.BreakdownUpdateIntervalFrames.Value);
            int frame = Time.frameCount;
            bool force = mapOpen && !lastMapOpen;

            if (!mapOpen && !allowWhenClosed)
            {
                lastMapOpen = false;
                return;
            }

            if (!force && (frame - lastBreakdownFrame) < interval)
            {
                lastMapOpen = true;
                return;
            }

            ComputeBreakdownValues(needCarts, needExtraction, out cachedCartsValue, out cachedExtractionValue);
            lastBreakdownFrame = frame;
            lastMapOpen = mapOpen;
        }

        public static void MarkCartsDirty()
        {
            cartsDirty = true;
        }

        /// <summary>
        /// Builds sets of valuables and objects currently staged for extraction.
        /// </summary>
        private static void GetExtractionSets(out HashSet<ValuableObject> extractionValuables, out HashSet<GameObject> extractionObjects)
        {
            extractionValuables = new HashSet<ValuableObject>();
            extractionObjects = new HashSet<GameObject>();

            if (RoundDirector.instance == null)
                return;

            object haulListObj = Traverse.Create(RoundDirector.instance).Field("dollarHaulList").GetValue();
            if (haulListObj is not IEnumerable haulList)
                return;

            foreach (object item in haulList)
            {
                if (item == null)
                    continue;

                if (item is ValuableObject directVo)
                {
                    extractionValuables.Add(directVo);
                    continue;
                }

                GameObject go = item as GameObject;
                if (go == null && item is Component component)
                    go = component.gameObject;

                if (go == null)
                    continue;

                extractionObjects.Add(go);

                ValuableObject vo = go.GetComponent<ValuableObject>();
                if (vo != null)
                {
                    extractionValuables.Add(vo);
                }
            }
        }

        /// <summary>
        /// Determines if a component belongs to a GameObject that is in extraction.
        /// </summary>
        private static bool IsComponentInExtraction(Component comp, HashSet<GameObject> extractionObjects)
        {
            if (comp == null || extractionObjects == null || extractionObjects.Count == 0)
                return false;

            Transform current = comp.transform;
            int depth = 0;
            while (current != null && depth < 12)
            {
                if (extractionObjects.Contains(current.gameObject))
                    return true;

                current = current.parent;
                depth++;
            }

            return false;
        }

        /// <summary>
        /// Reflects over a component's fields and properties to find valuables.
        /// </summary>
        private static void CollectValuablesFromComponent(Component comp, HashSet<ValuableObject> counted)
        {
            if (comp == null || counted == null)
                return;

            Type type = comp.GetType();
            FieldInfo[] fields = GetCachedCartFields(type);
            for (int i = 0; i < fields.Length; i++)
            {
                object value = fields[i].GetValue(comp);
                CollectValuablesFromValue(value, counted);
            }

            PropertyInfo[] properties = GetCachedCartProps(type);
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
                    continue;
                object value = prop.GetValue(comp, null);
                CollectValuablesFromValue(value, counted);
            }
        }

        private static FieldInfo[] GetCachedCartFields(Type type)
        {
            if (!Configuration.EnableCartReflectionCaching.Value)
            {
                var uncached = AccessTools.GetDeclaredFields(type);
                FieldInfo[] direct = new FieldInfo[uncached.Count];
                for (int i = 0; i < uncached.Count; i++)
                    direct[i] = uncached[i];
                return direct;
            }

            if (cartFieldsCache.TryGetValue(type, out FieldInfo[] cached))
                return cached;

            var list = AccessTools.GetDeclaredFields(type);
            FieldInfo[] fields = new FieldInfo[list.Count];
            for (int i = 0; i < list.Count; i++)
                fields[i] = list[i];

            cartFieldsCache[type] = fields;
            return fields;
        }

        private static PropertyInfo[] GetCachedCartProps(Type type)
        {
            if (!Configuration.EnableCartReflectionCaching.Value)
            {
                var uncached = AccessTools.GetDeclaredProperties(type);
                PropertyInfo[] direct = new PropertyInfo[uncached.Count];
                for (int i = 0; i < uncached.Count; i++)
                    direct[i] = uncached[i];
                return direct;
            }

            if (cartPropsCache.TryGetValue(type, out PropertyInfo[] cached))
                return cached;

            var list = AccessTools.GetDeclaredProperties(type);
            PropertyInfo[] props = new PropertyInfo[list.Count];
            for (int i = 0; i < list.Count; i++)
                props[i] = list[i];

            cartPropsCache[type] = props;
            return props;
        }

        /// <summary>
        /// Attempts to extract valuables from a field/property value.
        /// </summary>
        private static void CollectValuablesFromValue(object value, HashSet<ValuableObject> counted)
        {
            if (value == null || counted == null)
                return;

            if (value is string)
                return;

            if (value is ValuableObject vo)
            {
                counted.Add(vo);
                return;
            }

            if (value is Component comp)
            {
                TryAddValuableFromGameObject(comp.gameObject, counted);
                return;
            }

            if (value is GameObject go)
            {
                TryAddValuableFromGameObject(go, counted);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    CollectValuablesFromValue(item, counted);
                }
            }
        }

        /// <summary>
        /// Adds a ValuableObject from a GameObject if present.
        /// </summary>
        private static void TryAddValuableFromGameObject(GameObject go, HashSet<ValuableObject> counted)
        {
            if (go == null || counted == null)
                return;

            ValuableObject vo = go.GetComponent<ValuableObject>();
            if (vo != null)
            {
                counted.Add(vo);
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

    public class MyOnDestroy : MonoBehaviour
    {
        void OnDestroy()
        {
            MapValueTracker.Logger.LogDebug("Destroying!");
            var vo = GetComponent<ValuableObject>();
            float val = MapValueTracker.GetValuableCurrent(vo);
            MapValueTracker.Logger.LogDebug("Destroyed Valuable Object! " + vo.name + " Val: " + val);
            MapValueTracker.totalValue -= val;
            MapValueTracker.Logger.LogDebug("Total Val: " + MapValueTracker.totalValue);
        }
    }


}
