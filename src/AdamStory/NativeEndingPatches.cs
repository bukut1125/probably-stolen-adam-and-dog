using HarmonyLib;

namespace AlienStory
{
    // Preserve the game's ExecuteGameOver bookkeeping and summary while
    // substituting our two custom endings for its unknown-ending fallback.
    [HarmonyPatch(typeof(GameOverUIManager), nameof(GameOverUIManager.DisplayUI))]
    internal static class NativeEndingTextPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string __0, ref string __1, ref bool __2)
        {
            var id = StoryEndings.NativeContext;
            if (!StoryEndings.IsOurs(id)) return;
            __0 = StoryEndings.Title(id);
            __1 = StoryEndings.Description(id);
            __2 = false;
        }
    }

    [HarmonyPatch(typeof(RunSummaryUI), "EndingTitle")]
    internal static class NativeEndingSummaryPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string __0, ref string __result)
        {
            if (!StoryEndings.IsOurs(__0)) return true;
            __result = StoryEndings.Title(__0);
            return false;
        }
    }

    [HarmonyPatch(typeof(MachineCassettePlayer), nameof(MachineCassettePlayer.PauseForGameOver))]
    internal static class PreviewCassettePatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !StoryEndings.PreviewActive;
    }

    [HarmonyPatch(typeof(GameOverUIManager), nameof(GameOverUIManager.OnMenuButtonClick))]
    internal static class PreviewMenuPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameOverUIManager __instance)
        {
            if (!StoryEndings.PreviewActive) return true;
            __instance.ContinuePlayClick();
            return false;
        }
    }

    [HarmonyPatch(typeof(GameOverUIManager), nameof(GameOverUIManager.ContinuePlayClick))]
    internal static class PreviewContinuePatch
    {
        [HarmonyPostfix]
        private static void Postfix() => StoryEndings.EndPreview();
    }
}
