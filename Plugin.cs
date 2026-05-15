using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MapValueTracker.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace MapValueTracker
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class MapValueTracker : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "MapValueTrackerPlus";
        public const string PLUGIN_NAME = "Map Value Tracker Plus";
        public const string PLUGIN_VERSION = "1.1.0";
        private const float SnapshotRefreshResetTime = -100000f;

        public static new ManualLogSource Logger = null!;
        private readonly Harmony harmony = new Harmony("MapValueTrackerPlus.REPO");

        public static MapValueTracker? instance;
        public static GameObject? textInstance;
        public static TextMeshProUGUI? valueText;

        public static float totalValue;

        private static readonly FieldInfo cartHaulCurrentField = AccessTools.Field(typeof(PhysGrabCart), "haulCurrent");
        private static readonly FieldInfo cartItemsInCartField = AccessTools.Field(typeof(PhysGrabCart), "itemsInCart");
        private static bool cartFieldWarningLogged;
        private static bool snapshotDirty = true;
        private static bool forceBreakdownRefresh = true;
        private static float lastSnapshotRefreshTime = SnapshotRefreshResetTime;
        private static bool lastMapOpen;
        private static ValueBreakdownSnapshot currentSnapshot;

        public void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");

            if (instance == null)
            {
                instance = this;
            }

            Configuration.Init(Config);

            harmony.PatchAll();
            MarkDirty(forceBreakdown: true);
        }

        public static void ResetValues()
        {
            if (!SemiFunc.RunIsLevel())
            {
                totalValue = 0f;
            }

            Logger.LogDebug("In ResetValues()");
            Logger.LogDebug("Total Map Value: " + totalValue);
            MarkDirty(forceBreakdown: true);
        }

        public static void CheckForItems(ValuableObject? ignoreThis = null)
        {
            if (RoundDirector.instance == null)
            {
                totalValue = 0f;
                MarkDirty(forceBreakdown: true);
                return;
            }

            if (!Traverse.Create(RoundDirector.instance).Field("allExtractionPointsCompleted").GetValue<bool>())
            {
                totalValue = 0f;
                List<ValuableObject> valuableObjects = new List<ValuableObject>(UnityEngine.Object.FindObjectsOfType<ValuableObject>());

                if (ignoreThis != null)
                {
                    valuableObjects.Remove(ignoreThis);
                }

                for (int i = 0; i < valuableObjects.Count; i++)
                {
                    totalValue += GetValuableCurrent(valuableObjects[i]);
                }

                Logger.LogDebug("After CheckForItems Total Val: " + totalValue);
            }

            MarkDirty(forceBreakdown: true);
        }

        public static void MarkDirty(bool forceBreakdown = false)
        {
            snapshotDirty = true;
            if (forceBreakdown)
            {
                forceBreakdownRefresh = true;
                lastSnapshotRefreshTime = SnapshotRefreshResetTime;
            }
        }

        internal static ValueBreakdownSnapshot GetSnapshot()
        {
            bool mapOpen = IsMapOpen();
            float now = Time.unscaledTime;
            bool shouldRefresh = ShouldRefreshSnapshot(mapOpen, now);

            if (shouldRefresh)
            {
                currentSnapshot = BuildSnapshot(mapOpen);
                lastSnapshotRefreshTime = now;
                snapshotDirty = false;
                forceBreakdownRefresh = false;
            }

            lastMapOpen = mapOpen;
            return currentSnapshot;
        }

        private static ValueBreakdownSnapshot BuildSnapshot(bool mapOpen)
        {
            ValueBreakdownSnapshot snapshot = new ValueBreakdownSnapshot
            {
                MapOpen = mapOpen
            };

            if (!IsRunActive())
            {
                return snapshot;
            }

            int currentGoal = GetRoundDirectorInt("extractionHaulGoal");
            bool allExtractionPointsCompleted = GetRoundDirectorBool("allExtractionPointsCompleted");
            bool hideAfterCompletion = Configuration.HideAfterFinalExtraction.Value && allExtractionPointsCompleted;
            bool hasVisibleReason = currentGoal != 0 || !allExtractionPointsCompleted;

            ComputeBreakdownValues(out float cartsValue, out float extractionValue);

            snapshot.MapValue = Mathf.Max(0f, totalValue);
            snapshot.CartsValue = Mathf.Max(0f, cartsValue);
            snapshot.ExtractionValue = Mathf.Max(0f, extractionValue);
            snapshot.RemainingValue = Mathf.Max(0f, snapshot.MapValue - snapshot.CartsValue - snapshot.ExtractionValue);
            snapshot.HaulGoal = Mathf.Max(0, currentGoal);
            snapshot.CurrentHaul = Mathf.Max(0, GetRoundDirectorInt("currentHaul"));
            snapshot.HideAfterCompletion = hideAfterCompletion;
            snapshot.UseRemainingForPrimary = Configuration.IsClosedModeRemaining();
            snapshot.IsVisible = hasVisibleReason && !hideAfterCompletion;
            snapshot.ShowCompactHud = snapshot.IsVisible && !mapOpen && Configuration.AlwaysOn.Value;
            snapshot.ShowMapPanel = snapshot.IsVisible && mapOpen;

            return snapshot;
        }

        public static bool IsMapOpen()
        {
            bool mapToggled = false;
            if (MapToolController.instance != null)
            {
                try
                {
                    mapToggled = Traverse.Create(MapToolController.instance).Field("mapToggled").GetValue<bool>();
                }
                catch
                {
                    mapToggled = false;
                }
            }

            return SemiFunc.InputHold(InputKey.Map) || mapToggled;
        }

        private static bool IsRunActive()
        {
            return SemiFunc.RunIsLevel() && RoundDirector.instance != null;
        }

        private static bool ShouldRefreshSnapshot(bool mapOpen, float now)
        {
            float interval = Math.Max(0.1f, Configuration.RefreshIntervalSeconds.Value);
            return snapshotDirty
                || forceBreakdownRefresh
                || mapOpen != lastMapOpen
                || (now - lastSnapshotRefreshTime) >= interval;
        }

        private static int GetRoundDirectorInt(string fieldName)
        {
            return RoundDirector.instance == null
                ? 0
                : Traverse.Create(RoundDirector.instance).Field(fieldName).GetValue<int>();
        }

        private static bool GetRoundDirectorBool(string fieldName)
        {
            return RoundDirector.instance != null
                && Traverse.Create(RoundDirector.instance).Field(fieldName).GetValue<bool>();
        }

        private static void ComputeBreakdownValues(out float cartsValue, out float extractionValue)
        {
            cartsValue = 0f;
            extractionValue = 0f;

            HashSet<ValuableObject> extractionValuables = GetExtractionValuables();

            foreach (ValuableObject vo in extractionValuables)
            {
                extractionValue += GetValuableCurrent(vo);
            }

            cartsValue = ComputeValueInCarts(extractionValuables);
        }

        public static float ComputeValueInCarts()
        {
            return ComputeValueInCarts(GetExtractionValuables());
        }

        private static float ComputeValueInCarts(HashSet<ValuableObject> extractionValuables)
        {
            PhysGrabCart[] carts = UnityEngine.Object.FindObjectsOfType<PhysGrabCart>();
            if (carts == null || carts.Length == 0)
            {
                return 0f;
            }

            if (extractionValuables == null || extractionValuables.Count == 0)
            {
                float fastSum = 0f;
                for (int i = 0; i < carts.Length; i++)
                {
                    if (TryGetCartHaulCurrent(carts[i], out int haulCurrent))
                    {
                        fastSum += haulCurrent;
                    }
                }

                return fastSum;
            }

            float sum = 0f;
            HashSet<ValuableObject> counted = new HashSet<ValuableObject>();
            for (int i = 0; i < carts.Length; i++)
            {
                PhysGrabCart cart = carts[i];
                if (!TryGetCartItemsInCart(cart, out List<PhysGrabObject> itemsInCart))
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < itemsInCart.Count; itemIndex++)
                {
                    PhysGrabObject physObj = itemsInCart[itemIndex];
                    if (physObj == null)
                    {
                        continue;
                    }

                    ValuableObject vo = physObj.GetComponent<ValuableObject>();
                    if (vo == null || !counted.Add(vo) || extractionValuables.Contains(vo))
                    {
                        continue;
                    }

                    sum += GetValuableCurrent(vo);
                }
            }

            return sum;
        }

        private static bool TryGetCartHaulCurrent(PhysGrabCart cart, out int value)
        {
            value = 0;
            if (cart == null)
            {
                return false;
            }

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
            items = null!;
            if (cart == null)
            {
                return false;
            }

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
            {
                return;
            }

            cartFieldWarningLogged = true;
            Logger.LogWarning($"Unable to access PhysGrabCart.{fieldName}. Cart values will be treated as $0.");
        }

        public static void LogDebug(string message)
        {
            if (Configuration.DebugLogging.Value)
            {
                Logger.LogDebug(message);
            }
        }

        private static HashSet<ValuableObject> GetExtractionValuables()
        {
            HashSet<ValuableObject> extractionValuables = new HashSet<ValuableObject>();

            if (RoundDirector.instance == null || RoundDirector.instance.dollarHaulList == null)
            {
                return extractionValuables;
            }

            List<GameObject> haulList = RoundDirector.instance.dollarHaulList;
            for (int i = 0; i < haulList.Count; i++)
            {
                GameObject go = haulList[i];
                if (go == null)
                {
                    continue;
                }

                ValuableObject vo = go.GetComponent<ValuableObject>();
                if (vo != null)
                {
                    extractionValuables.Add(vo);
                }
            }

            return extractionValuables;
        }

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
            {
                original = GetValuableCurrent(vo);
            }

            return original;
        }

        private static float TryGetFloat(object obj, params string[] names)
        {
            if (obj == null)
            {
                return 0f;
            }

            Type type = obj.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                FieldInfo field = AccessTools.Field(type, name);
                if (field != null)
                {
                    object value = field.GetValue(obj);
                    if (TryConvertToFloat(value, out float result))
                    {
                        return result;
                    }
                }

                PropertyInfo prop = AccessTools.Property(type, name);
                if (prop != null)
                {
                    object value = prop.GetValue(obj, null);
                    if (TryConvertToFloat(value, out float result))
                    {
                        return result;
                    }
                }
            }

            return 0f;
        }

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
    }
}
