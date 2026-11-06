using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PlasmaSewer
{
    internal static class NewsScope
    {
        [ThreadStatic] internal static int Depth;
        [ThreadStatic] internal static NewsUIManager Manager;
        internal sealed class State { internal bool exited; internal NewsUIManager previous; }
        internal static void Exit(State state)
        {
            if (state == null || state.exited) return;
            state.exited = true;
            if (Depth > 0) Depth--;
            Manager = state.previous;
        }
    }

    [HarmonyPatch(typeof(NewsUIManager), nameof(NewsUIManager.PopulateUI))]
    internal static class NewsPopulatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(NewsUIManager __instance, out NewsScope.State __state)
        {
            __state = new NewsScope.State { previous = NewsScope.Manager };
            NewsScope.Manager = __instance;
            NewsScope.Depth++;
            NewsPager.BeginPopulate();
        }
        [HarmonyPostfix]
        private static void Postfix(NewsUIManager __instance, NewsScope.State __state)
        {
            NewsScope.Exit(__state);
            try { NewsPager.Refresh(__instance); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[plasma-sewer] Newspaper page controls unavailable: " + ex.Message); }
        }
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, NewsScope.State __state)
        {
            NewsScope.Exit(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(NewsUIManager), nameof(NewsUIManager.ToggleUI))]
    internal static class NewsOpenPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NewsUIManager __instance)
        {
            if (__instance != null && __instance.uiGameObject != null && !__instance.uiGameObject.activeSelf) NewsPager.ResetPage();
        }
    }

    [HarmonyPatch(typeof(StoreEventManager), nameof(StoreEventManager.GetActiveEvents))]
    internal static class NewsEventsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref List<StoreEvent> __result)
        {
            if (NewsScope.Depth <= 0 || __result == null || !PlasmaSewerMod.Active || PlayerStore.Instance == null) return;
            try
            {
                int day = StoreStation.GetDayCounter();
                string run = PlayerStore.Instance.runID ?? "slot-" + PlayerStore.Instance.saveSlotId;
                var leak = EventPolicy.ForDay(run, day);
                var page = NewsProjection.Page(__result, day, leak, NewsPager.PageIndex, 4, out int selected, out int count);
                if (count > 1 && !NewsPager.TryEnsure(NewsScope.Manager))
                {
                    UnityEngine.Debug.LogWarning("[plasma-sewer] Newspaper page controls unavailable; keeping original news intact.");
                    return;
                }
                NewsPager.SetProjection(selected, count);
                __result = page;
            }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[plasma-sewer] Newspaper entry could not be generated: " + ex.Message); }
        }
    }
}
