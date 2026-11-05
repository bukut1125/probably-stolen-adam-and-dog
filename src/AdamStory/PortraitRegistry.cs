using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AlienStory
{
    internal static class PortraitRegistry
    {
        internal const string Prefix = "personal.adam-story.portrait.";
        internal const string ModId = "personal.adam-story";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Allowed = new HashSet<string> {
            "adam_normal", "adam_hunger1", "adam_hunger2", "adam_hunger3", "adam_scavenger", "alien_god"
        };

        internal static string Id(string asset) => Prefix + asset;

        internal static bool Bind(SpriteDict target, string fullId)
        {
            if (target == null || target.spriteDictionary == null || fullId == null || !fullId.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            var asset = fullId.Substring(Prefix.Length);
            if (!Allowed.Contains(asset)) return false;
            if (!Cache.TryGetValue(fullId, out var portrait) || portrait == null || portrait.texture == null)
            {
                portrait = null;
                foreach (var mod in ModLoader.Mods)
                {
                    if (mod.IsEnabled && mod.Manifest.ID == ModId && mod.Sprites.TryGetValue(asset, out var source) && source != null && source.texture != null)
                    {
                        var tex = source.texture;
                        // Match the visible height and lower baseline of vanilla customer sprites.
                        portrait = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .625f), tex.height / 1.675f);
                        Cache[fullId] = portrait;
                        break;
                    }
                }
            }
            if (portrait == null) return false;
            if (target.spriteDictionary.TryGetValue(fullId, out var existing)) return ReferenceEquals(existing, portrait);
            target.spriteDictionary.Add(fullId, portrait);
            return true;
        }

        internal static void Clear()
        {
            var dict = SpriteDict.Instance;
            if (dict != null && dict.spriteDictionary != null)
                foreach (var item in Cache)
                    if (dict.spriteDictionary.TryGetValue(item.Key, out var current) && ReferenceEquals(current, item.Value))
                        dict.spriteDictionary.Remove(item.Key);
            Cache.Clear();
        }
    }

    [HarmonyPatch(typeof(SpriteDict), nameof(SpriteDict.GetSprite))]
    internal static class StoryPortraitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(SpriteDict __instance, string __0, ref Sprite __result)
        {
            if (__0 == null || !__0.StartsWith(PortraitRegistry.Prefix, StringComparison.Ordinal)) return true;
            try { if (PortraitRegistry.Bind(__instance, __0)) return true; }
            catch (Exception e) { Debug.LogError("[AlienStory] Portrait lookup: " + e); }
            Debug.LogError("[AlienStory] Missing character portrait: " + __0 + "; random replacement suppressed.");
            __result = null;
            return false;
        }
    }
}
