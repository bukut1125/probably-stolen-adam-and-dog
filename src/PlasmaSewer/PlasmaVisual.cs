using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlasmaSewer
{
    internal static class PlasmaVisual
    {
        private static GameObject effect;
        private static Sprite croppedEffect;
        private static Sprite waterMaskSprite;
        private static Texture2D waterMaskTexture;
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
        private static bool PrepareWaterMask(Sprite source)
        {
            if (source == null || source.texture == null) return false;
            try
            {
                var sourceRect = source.textureRect;
                if ((int)sourceRect.width != 160 || (int)sourceRect.height != 80 || !source.texture.isReadable) return false;
                Color[] pixels = source.texture.GetPixels((int)sourceRect.x, (int)sourceRect.y, 160, 80);
                var mask = new Color32[160 * 80];
                int selected = 0;
                for (int y = 0; y < 80; y++)
                for (int x = 0; x < 160; x++)
                {
                    int index = y * 160 + x;
                    Color32 p = pixels[index];
                    bool water = PipeMaskPolicy.IsWater(x, 79 - y, p.r, p.g, p.b, p.a);
                    mask[index] = new Color32(255, 255, 255, water ? (byte)255 : (byte)0);
                    if (water) selected++;
                }
                if (selected < 500) return false;
                waterMaskTexture = new Texture2D(160, 80, TextureFormat.RGBA32, false);
                waterMaskTexture.filterMode = FilterMode.Point;
                waterMaskTexture.wrapMode = TextureWrapMode.Clamp;
                waterMaskTexture.SetPixels32(mask);
                waterMaskTexture.Apply(false, false);
                waterMaskSprite = Sprite.Create(waterMaskTexture, new Rect(0, 0, 160, 80), new Vector2(.5f, .5f), 100f);
                if (waterMaskSprite != null) return true;
                UnityEngine.Object.Destroy(waterMaskTexture);
                waterMaskTexture = null;
                return false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[plasma-sewer] Could not construct safe water-only effect mask: " + ex.Message);
                if (waterMaskSprite != null) UnityEngine.Object.Destroy(waterMaskSprite);
                if (waterMaskTexture != null) UnityEngine.Object.Destroy(waterMaskTexture);
                waterMaskSprite = null;
                waterMaskTexture = null;
                return false;
            }
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
                if (!PrepareWaterMask(faucet.background.image.sprite)) return;
                effect = new GameObject("personal.plasma-sewer.water-mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
                var maskRect = (RectTransform)effect.transform;
                maskRect.SetParent(faucet.background.transform, false);
                maskRect.anchorMin = Vector2.zero;
                maskRect.anchorMax = Vector2.one;
                maskRect.offsetMin = Vector2.zero;
                maskRect.offsetMax = Vector2.zero;
                var maskImage = effect.GetComponent<Image>();
                maskImage.sprite = waterMaskSprite;
                maskImage.type = Image.Type.Simple;
                maskImage.raycastTarget = false;
                effect.GetComponent<Mask>().showMaskGraphic = false;
                var flow = new GameObject("personal.plasma-sewer.flow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)flow.transform;
                rect.SetParent(effect.transform, false);
                rect.anchorMin = new Vector2(PipeFxLayout.MinX, PipeFxLayout.MinY);
                rect.anchorMax = new Vector2(PipeFxLayout.MaxX, PipeFxLayout.MaxY);
                rect.pivot = new Vector2(.5f, .5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = flow.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
                flow.AddComponent<PlasmaPulse>();
            }
            effect.SetActive(true);
        }
        internal static void Hide()
        {
            if (effect != null) UnityEngine.Object.Destroy(effect);
            if (waterMaskSprite != null) UnityEngine.Object.Destroy(waterMaskSprite);
            if (waterMaskTexture != null) UnityEngine.Object.Destroy(waterMaskTexture);
            effect = null;
            waterMaskSprite = null;
            waterMaskTexture = null;
        }
    }

    public sealed class PlasmaPulse : MonoBehaviour
    {
        private Image graphic;
        private void Awake() { graphic = GetComponent<Image>(); }
        private void Update()
        {
            if (graphic == null) return;
            float pulse = .50f + .12f * Mathf.Sin(Time.unscaledTime * 3.2f);
            graphic.color = new Color(1f, 1f, 1f, pulse);
        }
    }
}
