using System;
using HarmonyLib;
using UnityEngine;

namespace PlasmaSewer
{
    public sealed class PlasmaSewerMod : IMod
    {
        internal const string ModId = "personal.plasma-sewer";
        internal const string PlasmaTag = "personal.plasma-sewer.contaminated";
        internal static string ToxinPart => Liquid.GetCurrentPartFromId(Liquid.LIQUID_ZYANIDE);
        internal static bool IsPlasma(GameItem item) => item != null && item.IsTag(PlasmaTag) && item.GetTagReadonly(ToxinPart).GetInt() > 0;
        private static ConfigEntry<bool> preview;
        private static bool enabled;
        internal static bool Enabled => enabled;

        public void Init(ModManifest manifest) { }
        public void OnEnable()
        {
            enabled = true;
            var config = ModHelper.GetConfig(ModId);
            preview = config.Define("preview_effect", false, "等離子外洩", () => "特效預覽", () => "只顯示外洩特效，不改變水質。", false);
            ModHook.OnGameLoadedLate += OnLoaded;
            ModHook.OnWakingUpLate += OnWake;
            ModHook.OnModConfigApplied += OnConfigApplied;
            ModHook.OnNetworkUpgradeUnlocked += OnUpgrade;
            ModHook.OnCreateTooltipLate += AddTooltip;
        }
        public void OnDisable()
        {
            enabled = false;
            ModHook.OnGameLoadedLate -= OnLoaded;
            ModHook.OnWakingUpLate -= OnWake;
            ModHook.OnModConfigApplied -= OnConfigApplied;
            ModHook.OnNetworkUpgradeUnlocked -= OnUpgrade;
            ModHook.OnCreateTooltipLate -= AddTooltip;
            PlasmaVisual.Hide();
            NewsPager.Destroy();
        }
        internal static bool HasSewerTap => PlayerStore.Instance != null && PlayerStore.Instance.networkUpgrade != null && (NetworkUpgrade.IsUnlocked("GUTTERFLOW_TAP") || NetworkUpgrade.IsUnlocked("RUSTWATER_TAP"));
        internal static bool Active
        {
            get
            {
                if (!enabled || PlayerStore.Instance == null || StoreStation.Instance == null) return false;
                return EventPolicy.IsActive(PlayerStore.Instance.runID ?? "slot-" + PlayerStore.Instance.saveSlotId, StoreStation.GetDayCounter());
            }
        }
        internal static void RefreshVisual()
        {
            PlasmaVisual.SetVisible(enabled && HasSewerTap && (Active || (preview != null && preview.Value)));
        }
        private static void OnConfigApplied(string id) { if (id == ModId) RefreshVisual(); }
        private static void OnUpgrade(string id) { if (id == "GUTTERFLOW_TAP" || id == "RUSTWATER_TAP") RefreshVisual(); }
        private static void OnLoaded() => RefreshVisual();
        private static void OnWake()
        {
            RefreshVisual();
            if (!HasSewerTap || PlayerStore.Instance == null) return;
            int day = StoreStation.GetDayCounter();
            string run = PlayerStore.Instance.runID ?? "slot-" + PlayerStore.Instance.saveSlotId;
            bool now = EventPolicy.IsActive(run, day), previous = EventPolicy.IsActive(run, day - 1);
            if (now && !previous && StoreUIManager.Instance != null)
                StoreUIManager.Instance.Notify("等離子外洩", "下水道水源遭污染；這幾天接出的水有劇毒。請小心處理。 ");
            else if (!now && previous && StoreUIManager.Instance != null)
                StoreUIManager.Instance.Notify("外洩已解除", "下水道水源已恢復；之前接出的污水仍有危險。 ");
        }
        private static void AddTooltip(RichTextBuilder text, GameItem item)
        {
            if (item != null && item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) && IsPlasma(item))
                text.AddLine("含等離子劇毒污水，請勿飲用。", color: RenderHandler.ColorPalette.Red);
        }
    }

    // Observe the real faucet fill. Vanilla executes all normal callbacks, audio,
    // capacity and water mixing first; only the newly added water is converted.
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.Target))]
    internal static class FaucetFillPatch
    {
        internal struct State { internal bool armed; internal int volume, water; }
        [HarmonyPrefix]
        private static void Prefix(GameItem __instance, GameItem __0, out State __state)
        {
            __state = default;
            if (__instance == null || __0 == null || !PlasmaSewerMod.Active || !PlasmaSewerMod.HasSewerTap ||
                !__instance.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) || !WaterHelper.IsFaucet(__0)) return;
            __state.armed = true;
            __state.volume = WaterHelper.GetTotalVolume(__instance);
            __state.water = __instance.GetTagReadonly(Liquid.GetCurrentPartFromId(Liquid.LIQUID_WATER)).GetInt();
        }
        [HarmonyPostfix]
        private static void Postfix(GameItem __instance, State __state)
        {
            if (!__state.armed) return;
            string waterKey = Liquid.GetCurrentPartFromId(Liquid.LIQUID_WATER);
            int waterAfter = __instance.GetTagReadonly(waterKey).GetInt();
            int dose = EventPolicy.PlannedContamination(__state.volume, WaterHelper.GetTotalVolume(__instance), __state.water, waterAfter);
            if (dose <= 0) return;
            var filter = WaterHelper.GetFilterItem(__instance);
            int retained = filter == null ? dose : Math.Max(0, Math.Min(dose, WaterHelper.HandleFilter(Liquid.LIQUID_ZYANIDE, dose, null, filter)));
            __instance.ModifyTag(waterKey, tag => tag.SetInt(waterAfter - dose), false);
            if (retained > 0)
            {
                WaterHelper.AddLiquid(__instance, Liquid.LIQUID_ZYANIDE, retained);
                __instance.EnableTag(PlasmaSewerMod.PlasmaTag, false);
            }
            else WaterFeatureHelper.UpdateWaterFeature(__instance, false);
            SpriteHelper.UpdateWaterContainerSprite(__instance);
        }
    }

    // The toxin itself uses the vanilla liquid. This marker carries only the
    // plasma-specific appearance and warning when contaminated water is poured.
    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.TransferLiquid))]
    internal static class PlasmaTransferPatch
    {
        internal struct State { internal bool sourceWasPlasma; internal int targetToxin; }
        [HarmonyPrefix]
        private static void Prefix(GameItem sourceContainer, GameItem targetContainer, out State __state)
        {
            __state = default;
            if (!PlasmaSewerMod.Enabled || sourceContainer == null || targetContainer == null) return;
            __state.sourceWasPlasma = PlasmaSewerMod.IsPlasma(sourceContainer);
            __state.targetToxin = targetContainer.GetTagReadonly(PlasmaSewerMod.ToxinPart).GetInt();
            if (__state.targetToxin <= 0 && targetContainer.IsTag(PlasmaSewerMod.PlasmaTag))
                targetContainer.DisableTag(PlasmaSewerMod.PlasmaTag, false);
        }
        [HarmonyPostfix]
        private static void Postfix(GameItem sourceContainer, GameItem targetContainer, State __state)
        {
            if (!PlasmaSewerMod.Enabled || sourceContainer == null || targetContainer == null) return;
            if (__state.sourceWasPlasma && targetContainer.GetTagReadonly(PlasmaSewerMod.ToxinPart).GetInt() > __state.targetToxin)
            {
                targetContainer.EnableTag(PlasmaSewerMod.PlasmaTag, false);
                SpriteHelper.UpdateWaterContainerSprite(targetContainer);
            }
            if (sourceContainer.IsTag(PlasmaSewerMod.PlasmaTag) && sourceContainer.GetTagReadonly(PlasmaSewerMod.ToxinPart).GetInt() <= 0)
                sourceContainer.DisableTag(PlasmaSewerMod.PlasmaTag, false);
        }
    }

    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.AddLiquid))]
    internal static class PlasmaStaleMarkerPatch
    {
        [HarmonyPrefix]
        private static void Prefix(GameItem __0, string __1)
        {
            if (PlasmaSewerMod.Enabled && __0 != null && __1 == Liquid.LIQUID_ZYANIDE && __0.IsTag(PlasmaSewerMod.PlasmaTag) &&
                __0.GetTagReadonly(PlasmaSewerMod.ToxinPart).GetInt() <= 0)
                __0.DisableTag(PlasmaSewerMod.PlasmaTag, false);
        }
    }

    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.GetLiquidContainerColor))]
    internal static class PlasmaColorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __0, ref string __result)
        {
            if (PlasmaSewerMod.Enabled && PlasmaSewerMod.IsPlasma(__0)) __result = "38DDE4";
        }
    }

    [HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.ApplyFaucetState))]
    internal static class FaucetVisualPatch
    {
        [HarmonyPostfix]
        private static void Postfix() => PlasmaSewerMod.RefreshVisual();
    }
}
