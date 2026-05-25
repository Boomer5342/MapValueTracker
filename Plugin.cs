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
        public const string PLUGIN_VERSION = "1.2.1";
        private const float SnapshotRefreshResetTime = -100000f;

        public static new ManualLogSource Logger = null!;
        private readonly Harmony harmony = new Harmony("MapValueTrackerPlus.REPO");

        public static MapValueTracker? instance;
        public static GameObject? textInstance;
        public static TextMeshProUGUI? valueText;

        public static float totalValue;

        private static readonly FieldInfo? cartHaulCurrentField = AccessTools.Field(typeof(PhysGrabCart), "haulCurrent");
        private static readonly FieldInfo? roomVolumeCheckInExtractionPointField = AccessTools.Field(typeof(RoomVolumeCheck), "inExtractionPoint");
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo?>> cachedMembers = new Dictionary<Type, Dictionary<string, MemberInfo?>>();
        private static readonly Dictionary<ValuableObject, float> trackedValuables = new Dictionary<ValuableObject, float>();
        private static readonly Dictionary<ItemValuableBox, float> trackedValuableBoxes = new Dictionary<ItemValuableBox, float>();
        private static readonly HashSet<PhysGrabCart> trackedCarts = new HashSet<PhysGrabCart>();
        private static bool cartFieldWarningLogged;
        private static bool snapshotDirty = true;
        private static bool fullResyncRequested = true;
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
            Configuration.RuntimeEnabled.SettingChanged += (_, _) => HandleRuntimeEnabledChanged();

            harmony.PatchAll();
            MarkDirty();
        }

        public static bool IsRuntimeEnabled()
        {
            return Configuration.RuntimeEnabled.Value;
        }

        public static void HandleRuntimeEnabledChanged()
        {
            if (IsRuntimeEnabled())
            {
                RequestFullResync();
            }
            else
            {
                currentSnapshot = default;
                snapshotDirty = false;
                lastSnapshotRefreshTime = SnapshotRefreshResetTime;
            }
        }

        public static void ResetValues()
        {
            trackedValuables.Clear();
            trackedValuableBoxes.Clear();
            trackedCarts.Clear();
            totalValue = 0f;
            currentSnapshot = default;
            RequestFullResync();
            Logger.LogDebug("Reset tracker state.");
        }

        public static void CheckForItems()
        {
            RebuildTrackedState();
        }

        public static void RequestFullResync()
        {
            fullResyncRequested = true;
            MarkDirty();
        }

        public static void MarkDirty()
        {
            snapshotDirty = true;
        }

        public static void RegisterCart(PhysGrabCart? cart)
        {
            if (cart == null || !IsRuntimeEnabled())
            {
                return;
            }

            trackedCarts.Add(cart);
            MarkDirty();
        }

        public static void UnregisterCart(PhysGrabCart? cart)
        {
            if (cart == null)
            {
                return;
            }

            trackedCarts.Remove(cart);
            MarkDirty();
        }

        public static void RegisterOrRefreshValuable(ValuableObject? valuable)
        {
            if (valuable == null || !IsRuntimeEnabled())
            {
                return;
            }

            float currentValue = GetValuableCurrent(valuable);
            if (trackedValuables.TryGetValue(valuable, out float previousValue))
            {
                totalValue += currentValue - previousValue;
                trackedValuables[valuable] = currentValue;
            }
            else
            {
                trackedValuables[valuable] = currentValue;
                totalValue += currentValue;
            }

            MarkDirty();
        }

        public static void UnregisterValuable(ValuableObject? valuable)
        {
            if (valuable == null)
            {
                return;
            }

            if (trackedValuables.TryGetValue(valuable, out float previousValue))
            {
                trackedValuables.Remove(valuable);
                totalValue -= previousValue;
            }

            MarkDirty();
        }

        public static void RegisterOrRefreshValuableBox(ItemValuableBox? valuableBox)
        {
            if (valuableBox == null || !IsRuntimeEnabled())
            {
                return;
            }

            float currentValue = valuableBox.gameObject.activeInHierarchy ? valuableBox.CurrentValue : 0f;
            if (trackedValuableBoxes.TryGetValue(valuableBox, out float previousValue))
            {
                totalValue += currentValue - previousValue;
                trackedValuableBoxes[valuableBox] = currentValue;
            }
            else
            {
                trackedValuableBoxes[valuableBox] = currentValue;
                totalValue += currentValue;
            }

            MarkDirty();
        }

        public static void UnregisterValuableBox(ItemValuableBox? valuableBox)
        {
            if (valuableBox == null)
            {
                return;
            }

            if (trackedValuableBoxes.TryGetValue(valuableBox, out float previousValue))
            {
                trackedValuableBoxes.Remove(valuableBox);
                totalValue -= previousValue;
            }

            MarkDirty();
        }

        internal static ValueBreakdownSnapshot GetSnapshot()
        {
            if (!IsRuntimeEnabled())
            {
                return default;
            }

            bool mapOpen = IsMapOpen();
            float now = Time.unscaledTime;
            bool shouldRefresh = ShouldRefreshSnapshot(mapOpen, now);

            if (shouldRefresh)
            {
                EnsureSynchronizedState();
                currentSnapshot = BuildSnapshot(mapOpen);
                lastSnapshotRefreshTime = now;
                snapshotDirty = false;
            }

            lastMapOpen = mapOpen;
            return currentSnapshot;
        }

        private static void EnsureSynchronizedState()
        {
            if (!IsRunActive())
            {
                return;
            }

            if (fullResyncRequested)
            {
                RebuildTrackedState();
            }
            else
            {
                CleanupTrackedCollections();
                trackedCarts.RemoveWhere(cart => cart == null);
            }
        }

        private static void RebuildTrackedState()
        {
            trackedValuables.Clear();
            trackedValuableBoxes.Clear();
            trackedCarts.Clear();
            totalValue = 0f;

            if (!IsRunActive())
            {
                fullResyncRequested = false;
                MarkDirty();
                return;
            }

            ValuableObject[] valuables = UnityEngine.Object.FindObjectsOfType<ValuableObject>();
            for (int i = 0; i < valuables.Length; i++)
            {
                ValuableObject valuable = valuables[i];
                if (valuable == null)
                {
                    continue;
                }

                float currentValue = GetValuableCurrent(valuable);
                trackedValuables[valuable] = currentValue;
                totalValue += currentValue;
            }

            ItemValuableBox[] valuableBoxes = UnityEngine.Object.FindObjectsOfType<ItemValuableBox>();
            for (int i = 0; i < valuableBoxes.Length; i++)
            {
                ItemValuableBox valuableBox = valuableBoxes[i];
                if (valuableBox == null)
                {
                    continue;
                }

                float currentValue = valuableBox.gameObject.activeInHierarchy ? valuableBox.CurrentValue : 0f;
                trackedValuableBoxes[valuableBox] = currentValue;
                totalValue += currentValue;
            }

            PhysGrabCart[] carts = UnityEngine.Object.FindObjectsOfType<PhysGrabCart>();
            for (int i = 0; i < carts.Length; i++)
            {
                PhysGrabCart cart = carts[i];
                if (cart != null)
                {
                    trackedCarts.Add(cart);
                }
            }

            fullResyncRequested = false;
            MarkDirty();
            Logger.LogDebug($"Rebuilt tracked state for {trackedValuables.Count} valuables, {trackedValuableBoxes.Count} valuable boxes, and {trackedCarts.Count} carts.");
        }

        private static void CleanupTrackedCollections()
        {
            List<ValuableObject> staleValuables = new List<ValuableObject>();
            foreach (KeyValuePair<ValuableObject, float> entry in trackedValuables)
            {
                if (entry.Key == null)
                {
                    staleValuables.Add(entry.Key!);
                }
            }

            for (int i = 0; i < staleValuables.Count; i++)
            {
                ValuableObject staleValuable = staleValuables[i];
                if (trackedValuables.TryGetValue(staleValuable, out float removedValue))
                {
                    trackedValuables.Remove(staleValuable);
                    totalValue -= removedValue;
                }
            }

            List<ItemValuableBox> staleBoxes = new List<ItemValuableBox>();
            foreach (KeyValuePair<ItemValuableBox, float> entry in trackedValuableBoxes)
            {
                if (entry.Key == null)
                {
                    staleBoxes.Add(entry.Key!);
                }
            }

            for (int i = 0; i < staleBoxes.Count; i++)
            {
                ItemValuableBox staleBox = staleBoxes[i];
                if (trackedValuableBoxes.TryGetValue(staleBox, out float removedValue))
                {
                    trackedValuableBoxes.Remove(staleBox);
                    totalValue -= removedValue;
                }
            }

            totalValue = Mathf.Max(0f, totalValue);
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

            ComputeBreakdownValues(out float mapValue, out float cartsValue, out float cartsValueOutsideExtraction, out float haulerValue, out float haulerValueOutsideExtraction, out float extractionValue);

            snapshot.MapValue = Mathf.Max(0f, mapValue);
            snapshot.CartsValue = Mathf.Max(0f, cartsValue);
            snapshot.HaulerValue = Mathf.Max(0f, haulerValue);
            snapshot.ExtractionValue = Mathf.Max(0f, extractionValue);
            snapshot.RemainingValue = Mathf.Max(0f, snapshot.MapValue - cartsValueOutsideExtraction - haulerValueOutsideExtraction - snapshot.ExtractionValue);
            snapshot.HaulGoal = Mathf.Max(0, currentGoal);
            snapshot.CurrentHaul = Mathf.Max(0, GetRoundDirectorInt("currentHaul"));
            snapshot.HideAfterCompletion = hideAfterCompletion;
            snapshot.UseRemainingForPrimary = Configuration.IsClosedModeRemaining();
            snapshot.IsVisible = hasVisibleReason && !hideAfterCompletion;
            snapshot.ShowCompactHud = snapshot.IsVisible && !mapOpen && Configuration.AlwaysOn.Value;
            snapshot.ShowMapPanel = snapshot.IsVisible && mapOpen;

            totalValue = snapshot.MapValue;

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
                || fullResyncRequested
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

        private static void ComputeBreakdownValues(out float mapValue, out float cartsValue, out float cartsValueOutsideExtraction, out float haulerValue, out float haulerValueOutsideExtraction, out float extractionValue)
        {
            mapValue = Mathf.Max(0f, totalValue);
            ComputeHaulerValues(out haulerValue, out haulerValueOutsideExtraction);
            extractionValue = Mathf.Max(0f, GetRoundDirectorInt("currentHaul"));
            ComputeCartValues(out cartsValue, out cartsValueOutsideExtraction);
        }

        private static void ComputeCartValues(out float cartsValue, out float cartsValueOutsideExtraction)
        {
            cartsValue = 0f;
            cartsValueOutsideExtraction = 0f;
            if (trackedCarts.Count == 0)
            {
                return;
            }

            List<PhysGrabCart> staleCarts = new List<PhysGrabCart>();

            foreach (PhysGrabCart cart in trackedCarts)
            {
                if (cart == null)
                {
                    staleCarts.Add(cart!);
                    continue;
                }

                if (TryGetCartHaulCurrent(cart, out int haulCurrent))
                {
                    cartsValue += haulCurrent;

                    RoomVolumeCheck roomVolumeCheck = cart.GetComponent<RoomVolumeCheck>();
                    if (!IsRoomVolumeCheckInExtraction(roomVolumeCheck))
                    {
                        cartsValueOutsideExtraction += haulCurrent;
                    }
                }
            }

            if (staleCarts.Count > 0)
            {
                for (int i = 0; i < staleCarts.Count; i++)
                {
                    trackedCarts.Remove(staleCarts[i]);
                }
            }
        }

        private static bool IsRoomVolumeCheckInExtraction(RoomVolumeCheck? roomVolumeCheck)
        {
            if (roomVolumeCheck == null || roomVolumeCheckInExtractionPointField == null)
            {
                return false;
            }

            object? raw = roomVolumeCheckInExtractionPointField.GetValue(roomVolumeCheck);
            return raw is bool boolValue && boolValue;
        }

        private static void ComputeHaulerValues(out float haulerValue, out float haulerValueOutsideExtraction)
        {
            haulerValue = 0f;
            haulerValueOutsideExtraction = 0f;
            if (trackedValuableBoxes.Count == 0)
            {
                return;
            }

            HashSet<ItemValuableBox>? extractionBoxes = null;
            if (RoundDirector.instance != null && RoundDirector.instance.valuableBoxHaulList != null && RoundDirector.instance.valuableBoxHaulList.Count > 0)
            {
                extractionBoxes = new HashSet<ItemValuableBox>(RoundDirector.instance.valuableBoxHaulList);
            }

            foreach (KeyValuePair<ItemValuableBox, float> entry in trackedValuableBoxes)
            {
                ItemValuableBox valuableBox = entry.Key;
                if (valuableBox == null)
                {
                    continue;
                }

                haulerValue += entry.Value;
                if (extractionBoxes == null || !extractionBoxes.Contains(valuableBox))
                {
                    haulerValueOutsideExtraction += entry.Value;
                }
            }
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

            object? raw = cartHaulCurrentField.GetValue(cart);
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

        private static void LogMissingCartFieldWarning(string fieldName)
        {
            if (cartFieldWarningLogged)
            {
                return;
            }

            cartFieldWarningLogged = true;
            Logger.LogWarning($"Unable to access PhysGrabCart.{fieldName}. Cart values may be inaccurate.");
        }

        public static void LogDebug(string message)
        {
            if (Configuration.DebugLogging.Value)
            {
                Logger.LogDebug(message);
            }
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

        private static float TryGetFloat(object obj, params string[] names)
        {
            if (obj == null)
            {
                return 0f;
            }

            Type type = obj.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                MemberInfo? member = GetCachedMember(type, names[i]);
                if (member == null)
                {
                    continue;
                }

                object? value = member switch
                {
                    FieldInfo field => field.GetValue(obj),
                    PropertyInfo property => property.GetValue(obj, null),
                    _ => null
                };

                if (TryConvertToFloat(value, out float result))
                {
                    return result;
                }
            }

            return 0f;
        }

        private static MemberInfo? GetCachedMember(Type type, string name)
        {
            if (!cachedMembers.TryGetValue(type, out Dictionary<string, MemberInfo?>? membersByName))
            {
                membersByName = new Dictionary<string, MemberInfo?>();
                cachedMembers[type] = membersByName;
            }

            if (membersByName.TryGetValue(name, out MemberInfo? member))
            {
                return member;
            }

            member = (MemberInfo?)AccessTools.Field(type, name) ?? AccessTools.Property(type, name);
            membersByName[name] = member;
            return member;
        }

        private static bool TryConvertToFloat(object? value, out float result)
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
