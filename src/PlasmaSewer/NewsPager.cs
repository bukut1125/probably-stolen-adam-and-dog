using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlasmaSewer
{
    internal static class NewsPager
    {
        internal static int PageIndex { get; private set; }
        private static int pageCount = 1;
        private static bool projection;
        private static GameObject root;
        private static Button previous, next;
        private static TextMeshProUGUI pageLabel;

        internal static void ResetPage() { PageIndex = 0; }
        internal static void BeginPopulate() { projection = false; pageCount = 1; }
        internal static void SetProjection(int selected, int count) { PageIndex = selected; pageCount = count; projection = true; }

        internal static bool TryEnsure(NewsUIManager manager)
        {
            if (manager == null || manager.layout1 == null || manager.layout1Elements == null || manager.layout1Elements.Count == 0 || manager.layout1Elements[0] == null || manager.layout1Elements[0].title == null) return false;
            var parent = manager.layout1.transform.parent as RectTransform;
            var font = manager.layout1Elements[0].title.font;
            if (parent == null || font == null) return false;
            if (root != null && root.transform.parent != parent) Destroy();
            if (root != null) return true;
            try
            {
                root = new GameObject("personal.plasma-sewer.news-pages", typeof(RectTransform));
                var rect = (RectTransform)root.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(300f, 26f);
                rect.anchoredPosition = new Vector2(0f, 18f);
                previous = MakeButton(root.transform, font, "上一頁", -100f, () => Move(manager, -1));
                next = MakeButton(root.transform, font, "下一頁", 100f, () => Move(manager, 1));
                var labelObject = new GameObject("page", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(root.transform, false);
                labelRect.sizeDelta = new Vector2(75f, 24f);
                pageLabel = labelObject.GetComponent<TextMeshProUGUI>();
                pageLabel.font = font;
                pageLabel.fontSize = 15f;
                pageLabel.alignment = TextAlignmentOptions.Center;
                pageLabel.color = new Color(.20f, .19f, .23f, 1f);
                pageLabel.raycastTarget = false;
                root.SetActive(false);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[plasma-sewer] Newspaper page setup failed: " + ex.Message);
                Destroy();
                return false;
            }
        }

        private static Button MakeButton(Transform parent, TMP_FontAsset font, string caption, float x, Action action)
        {
            var go = new GameObject(caption, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(90f, 24f);
            rect.anchoredPosition = new Vector2(x, 0f);
            var background = go.GetComponent<Image>();
            background.color = new Color(.44f, .42f, .40f, .9f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => action());
            var textObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(go.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 15f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = caption;
            text.raycastTarget = false;
            return button;
        }
        private static void Move(NewsUIManager manager, int delta)
        {
            if (manager == null) return;
            PageIndex = Math.Max(0, Math.Min(PageIndex + delta, pageCount - 1));
            AudioHelper.UIAudioClicked();
            manager.PopulateUI();
        }
        internal static void Refresh(NewsUIManager manager)
        {
            if (!projection || pageCount <= 1)
            {
                if (root != null) root.SetActive(false);
                return;
            }
            if (!TryEnsure(manager)) return;
            previous.interactable = PageIndex > 0;
            next.interactable = PageIndex + 1 < pageCount;
            pageLabel.text = (PageIndex + 1) + " / " + pageCount;
            root.SetActive(true);
        }
        internal static void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; previous = null; next = null; pageLabel = null;
        }
    }
}
