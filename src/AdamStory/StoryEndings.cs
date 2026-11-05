using UnityEngine;
using UnityEngine.UI;

namespace AlienStory
{
    internal static class StoryEndings
    {
        internal const string SpaceId = "personal.adam-story.space";
        internal const string BadId = "personal.adam-story.judgment";
        internal static string NativeContext;
        internal static bool PreviewActive;
        private static Image previewBackground;
        private static Sprite previousSprite;
        private static Color previousColor;
        private static bool previousPreserveAspect;
        private static Image.Type previousType;
        private static Image previewPanel;
        private static Color previousPanelColor;
        private static GameObject previewOriginalArt;
        private static bool previousArtActive;

        internal static string Title(string id) => id == SpaceId ? "太空結局：歸途" : "壞結局：未通過的考驗";
        internal static string Description(string id) => id == SpaceId
            ? "艙門合上，熟悉的店鋪縮成遠方的一點暖光。你與亞當離開空間站，飛向未知的星海。"
            : "星空亮起藍光。這一次，空間站沒有等到寬恕。";
        private static string Artwork(string id) => id == SpaceId ? "ending_space" : "ending_bad";
        internal static bool IsOurs(string id) => id == SpaceId || id == BadId;

        private static Sprite FindArt(string key)
        {
            foreach (var mod in ModLoader.Mods)
                if (mod.IsEnabled && mod.Manifest.ID == PortraitRegistry.ModId && mod.Sprites.TryGetValue(key, out var sprite) && sprite != null)
                    return sprite;
            return null;
        }

        private static void ApplyBackdrop(GameOverUIManager manager, string spriteKey, bool remember)
        {
            var art = FindArt(spriteKey);
            var image = manager != null && manager.darkScreen != null ? manager.darkScreen.GetComponent<Image>() : null;
            if (image == null || art == null) { Debug.LogError("[AlienStory] Ending artwork or native backdrop unavailable: " + spriteKey); return; }
            if (remember && previewBackground == null)
            {
                previewBackground = image;
                previousSprite = image.sprite;
                previousColor = image.color;
                previousPreserveAspect = image.preserveAspect;
                previousType = image.type;
            }
            image.type = Image.Type.Simple;
            image.sprite = art;
            image.color = Color.white;
            image.preserveAspect = true;
            var panel = manager.uiPanel != null ? manager.uiPanel.GetComponent<Image>() : null;
            if (panel != null)
            {
                if (remember && previewPanel == null) { previewPanel = panel; previousPanelColor = panel.color; }
                panel.color = new Color(0f, 0f, 0f, .55f);
            }
            var originalArt = manager.uiPanel != null ? manager.uiPanel.transform.Find("Art") : null;
            if (originalArt != null)
            {
                if (remember && previewOriginalArt == null) { previewOriginalArt = originalArt.gameObject; previousArtActive = previewOriginalArt.activeSelf; }
                originalArt.gameObject.SetActive(false);
            }
        }

        private static void TriggerNative(string id)
        {
            var player = PlayerStore.Instance;
            var manager = GameOverUIManager.Instance;
            if (player == null || manager == null) { Debug.LogError("[AlienStory] Native ending is unavailable."); return; }
            NativeContext = id;
            try
            {
                if (!ModHelper.GetModDataBool(PortraitRegistry.ModId, "native_ending_fired", false))
                {
                    // The native method records run stats and opens the real ending/summary flow.
                    player.ExecuteGameOver(id);
                    ModHelper.SetModDataBool(PortraitRegistry.ModId, "native_ending_fired", true);
                }
                else
                {
                    // Reopen an already recorded ending without recording the run twice.
                    manager.SetEnding(id);
                    manager.DisplayUI(Title(id), Description(id), false);
                }
                ApplyBackdrop(manager, Artwork(id), false);
            }
            finally { NativeContext = null; }
        }

        internal static void ShowSpaceEnding() => TriggerNative(SpaceId);
        internal static void ShowBadEnding() => TriggerNative(BadId);

        internal static void EndPreview()
        {
            if (!PreviewActive) return;
            if (previewBackground != null)
            {
                previewBackground.sprite = previousSprite;
                previewBackground.color = previousColor;
                previewBackground.preserveAspect = previousPreserveAspect;
                previewBackground.type = previousType;
            }
            previewBackground = null;
            if (previewPanel != null) previewPanel.color = previousPanelColor;
            if (previewOriginalArt != null) previewOriginalArt.SetActive(previousArtActive);
            previewPanel = null;
            previewOriginalArt = null;
            PreviewActive = false;
            StoryRuntime.Dismiss();
        }

        internal static void ShowTest(string title, string text, string spriteKey)
        {
            if (spriteKey == "ore_device")
            {
                if (CustomUIManager.Instance == null) { Debug.LogError("[AlienStory] Test item UI unavailable."); return; }
                string id = "personal.adam-story.test-item";
                CustomUIManager.Window(id, title + "（測試展示）", CustomUIManager.LAYER_OVERLAY)
                    .SetSize(650, 620).Center().SetCloseOnEscape(false)
                    .AddImage(PortraitRegistry.ModId + ":ore_device", 420, 384)
                    .AddLabel(text + "\n這是測試展示，不會發放正式道具。")
                    .AddButton("返回遊戲", () => { CustomUIManager.Instance.CloseWindow(id); StoryRuntime.Dismiss(); }).Show();
                return;
            }
            var manager = GameOverUIManager.Instance;
            if (manager == null) { Debug.LogError("[AlienStory] Native test ending UI unavailable."); return; }
            PreviewActive = true;
            try
            {
                // Preview only: do not call ExecuteGameOver, record run stats or mutate story state.
                manager.DisplayUI(title + "（測試展示）", text + "\n測試不會結束存檔。", true);
                ApplyBackdrop(manager, spriteKey, true);
                if (manager.continuePlayButton != null) manager.continuePlayButton.SetActive(true);
            }
            catch { EndPreview(); throw; }
        }
    }
}
