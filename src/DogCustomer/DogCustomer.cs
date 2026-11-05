using System;
using HarmonyLib;
using UnityEngine;

namespace DogCustomer
{
    public sealed class DogCustomerMod : IMod
    {
        private const string ModId = "personal.dog-customer";
        internal const string ClientId = "personal.dog-customer.visitor";
        internal const string SpriteId = "personal.dog-customer.portrait";
        private const string SpriteAsset = "dog_customer";
        private const int VisitChancePercent = 30;
        private Sprite portrait;
        internal static DogCustomerMod Active { get; private set; }

        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            StoreClientListDict.storeClientDict.Add(ClientId, CreateCustomer);
            Active = this;
            ModHook.OnGenerateCustomerLate += GenerateCustomer;
        }

        public void OnDisable()
        {
            ModHook.OnGenerateCustomerLate -= GenerateCustomer;
            StoreClientListDict.storeClientDict.Remove(ClientId);
            if (SpriteDict.Instance != null && portrait != null && SpriteDict.Instance.spriteDictionary.TryGetValue(SpriteId, out var current) && current == portrait)
                SpriteDict.Instance.spriteDictionary.Remove(SpriteId);
            if (Active == this) Active = null;
        }

        internal bool EnsurePortrait(SpriteDict dictionary = null)
        {
            var target = dictionary != null ? dictionary : SpriteDict.Instance;
            if (target == null || target.spriteDictionary == null) return false;
            if (portrait == null || portrait.texture == null)
            {
                portrait = null;
                foreach (var mod in ModLoader.Mods)
                {
                    if (mod.IsEnabled && mod.Manifest.ID == ModId && mod.Sprites.TryGetValue(SpriteAsset, out var source) && source != null && source.texture != null)
                    {
                        var texture = source.texture;
                        // The original 288x211 customers occupy the lower part of their canvas.
                        portrait = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.625f), texture.width / 2f);
                        break;
                    }
                }
            }
            if (portrait == null)
            {
                Debug.LogError("[DogCustomer] Portrait is unavailable; refusing a random replacement.");
                return false;
            }
            if (!PortraitBinding.TryBind(target.spriteDictionary, SpriteId, portrait))
            {
                Debug.LogError("[DogCustomer] Portrait identifier is already in use; refusing a random replacement.");
                return false;
            }
            return true;
        }

        private void GenerateCustomer(StoreClientManager manager)
        {
            if (!RNG.Roll(VisitChancePercent) || manager.clientStack.Exists(c => c.identifier == ClientId) || !EnsurePortrait()) return;
            manager.AddClient(CreateCustomer());
        }

        private StoreClient CreateCustomer()
        {
            EnsurePortrait();
            var client = new StoreClient
            {
                identifier = ClientId,
                sourceModId = ModId,
                displayName = "狗狗",
                spriteName = SpriteId,
                clientIntent = StoreClient.ClientIntent.BUY,
                useClientBudget = true,
                isBargainAvailable = false,
            };
            client.OverrideBudget(UnityEngine.Random.Range(100, 301));
            client.clientBuyingTagList.Add("FOOD");
            client.mainDialogue.SetText(client.displayName, "旺！").NextDialogue().SetText(client.displayName, "旺旺～");
            client.acceptDealDialogue = new Dialogue().SetText(client.displayName, "旺旺！");
            client.placedWrongItemWhenSellingToDialogue = new Dialogue().SetText(client.displayName, "旺……？");
            client.placeRightItemWhenSellingToDialogue = new Dialogue().SetText(client.displayName, "旺！旺旺！");
            client.repeatDialogue = new Dialogue().SetText(client.displayName, "旺～");
            client.allDoneDialogue = new Dialogue().SetText(client.displayName, "旺旺～！")
                .SetEndAction(() => PlayerStore.Instance.DismissCurrentClient(false, true, true));
            client.CompleteClientCreation(false);
            return client;
        }
    }

    // The game's missing-key fallback selects a random vanilla customer.
    // Rebind after dictionary recreation, and never use that fallback for the dog.
    [HarmonyPatch(typeof(SpriteDict), nameof(SpriteDict.GetSprite))]
    internal static class DogPortraitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(SpriteDict __instance, string __0, ref Sprite __result)
        {
            if (__0 != DogCustomerMod.SpriteId) return true;
            try
            {
                if (DogCustomerMod.Active != null && DogCustomerMod.Active.EnsurePortrait(__instance)) return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[DogCustomer] Portrait lookup failed: " + e);
            }
            __result = null;
            return false;
        }
    }

    // Game-generated trade and budget reactions also pass through this display method.
    // Restrict the change to the dog and preserve each dialogue's actions and timing.
    [HarmonyPatch(typeof(DialogUIManager), nameof(DialogUIManager.DisplayClientText))]
    internal static class DogSpeechPatch
    {
        private static readonly string[] Barks = { "旺！", "旺旺～", "旺？", "旺……旺。", "旺旺！" };

        [HarmonyPrefix]
        private static void Prefix(Dialogue dialogue)
        {
            var store = PlayerStore.Instance;
            if (dialogue == null || store == null || store.currentClientInstance == null) return;
            var client = store.currentClientInstance.GetClientBlueprint();
            if (client == null || client.identifier != DogCustomerMod.ClientId) return;
            dialogue.SetText(dialogue.title, Barks[UnityEngine.Random.Range(0, Barks.Length)]);
        }
    }
}
