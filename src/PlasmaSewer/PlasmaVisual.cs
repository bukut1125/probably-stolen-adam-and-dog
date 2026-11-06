using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlasmaSewer
{
    internal static class PlasmaVisual
    {
        private static GameObject effect;
        private static Sprite croppedEffect;
        private static Sprite FindSprite()
        {
            foreach (var mod in ModLoader.Mods)
                if (mod.IsEnabled && mod.Manifest.ID == PlasmaSewerMod.ModId && mod.Sprites.TryGetValue("plasma_pipe_fx", out var sprite) && sprite != null && sprite.texture != null)
                {
                    var texture = sprite.texture;
                    if (!PipeFxLayout.SupportsTexture(texture.width, texture.height)) return null;
                    if (croppedEffect == null || croppedEffect.texture != texture)
                    {
                        if (croppedEffect != null) UnityEngine.Object.Destroy(croppedEffect);
                        texture.filterMode = FilterMode.Point;
                        croppedEffect = Sprite.Create(texture,
                            new Rect(PipeFxLayout.CropX, PipeFxLayout.CropY, PipeFxLayout.CropWidth, PipeFxLayout.CropHeight),
                            new Vector2(.5f, .5f), 100f);
                    }
                    return croppedEffect;
                }
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
                rect.anchorMin = new Vector2(PipeFxLayout.MinX, PipeFxLayout.MinY);
                rect.anchorMax = new Vector2(PipeFxLayout.MaxX, PipeFxLayout.MaxY);
                rect.pivot = new Vector2(.5f, .5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = effect.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
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
