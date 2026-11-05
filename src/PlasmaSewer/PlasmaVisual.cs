using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlasmaSewer
{
    internal static class PlasmaVisual
    {
        private static GameObject effect;
        private static Sprite FindSprite()
        {
            foreach (var mod in ModLoader.Mods)
                if (mod.IsEnabled && mod.Manifest.ID == PlasmaSewerMod.ModId && mod.Sprites.TryGetValue("plasma_leak_fx", out var sprite))
                    return sprite;
            return null;
        }
        internal static void SetVisible(bool visible)
        {
            if (!visible) { Hide(); return; }
            var faucet = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.faucet as GameItemElement : null;
            var sprite = FindSprite();
            if (faucet == null || faucet.background == null || faucet.background.image == null || sprite == null || UISettings.current == null) { Hide(); return; }
            if (effect != null && effect.transform.parent != faucet.background.transform) Hide();
            if (effect == null)
            {
                effect = new GameObject("personal.plasma-sewer.effect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)effect.transform;
                rect.SetParent(faucet.background.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .25f);
                rect.pivot = new Vector2(.5f, .5f);
                float grid = Math.Max(1, UISettings.current.gridSize);
                rect.sizeDelta = new Vector2(grid * 3.0f, grid * 2.25f);
                rect.anchoredPosition = new Vector2(0f, -grid * .3f);
                var image = effect.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;
                effect.AddComponent<PlasmaPulse>();
            }
            effect.SetActive(true);
        }
        internal static void Hide()
        {
            if (effect == null) return;
            UnityEngine.Object.Destroy(effect);
            effect = null;
        }
    }

    public sealed class PlasmaPulse : MonoBehaviour
    {
        private Image graphic;
        private void Awake() { graphic = GetComponent<Image>(); }
        private void Update()
        {
            if (graphic == null) return;
            float pulse = .70f + .18f * Mathf.Sin(Time.unscaledTime * 3.2f);
            graphic.color = new Color(1f, 1f, 1f, pulse);
        }
    }
}
