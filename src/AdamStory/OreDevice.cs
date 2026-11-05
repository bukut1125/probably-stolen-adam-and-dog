using System;
using HarmonyLib;
using UnityEngine;

namespace AlienStory
{
    internal static class OreDevice
    {
        private const string ModId = PortraitRegistry.ModId;
        internal const string Id = "personal.adam-story.ore-device";
        private const string Description = "每三天蓄能一次，啟動後產生十個金屬礦石。\n來自亞當的答謝";
        private const string NextTag = "personal.adam-story.next-day";
        private const string RemainingTag = "personal.adam-story.ore-remaining";
        private const string PendingKey = "device_pending";
        private static Sprite scaledIcon;

        internal static void Register(ModItemDirectory directory)
        {
            if (DirectoryMaster.Has<GameItem>(Id)) throw new InvalidOperationException("Item ID already exists: " + Id);
            if (!directory.Add(Id, Create)) throw new InvalidOperationException("Could not register " + Id);
        }

        private static int GetInt(GameItem item, string key) => item.IsTag(key) ? item.GetTagReadonly(key).GetInt() : 0;
        private static void SetInt(GameItem item, string key, int value) => item.ModifyTag(key, tag => tag.Enable().SetInt(value), false);

        private static GameItem Create()
        {
            PrepareIcon();
            var item = ItemDirectory.CreateEmptyItem(Id);
            item.name = "亞當的礦石產生器";
            item.shortDescription = Description;
            item.SetSpriteAndShapeFromMod(ModId, "ore_device");
            item.SetShape(new GridShapeBuilder().SetDataFill(3, 4, 1).Build());
            item.activateDefault = true;
            item.mayActivateSlotItemFunc = (self, inventory, slot) => true;
            item.canActivateSlotItemFunc = (self, inventory, slot) => true;
            item.onActivateSlotItemFunc = Activate;
            item.onLoaded = RestoreSavedItem;
            return item;
        }

        private static void PrepareIcon()
        {
            foreach (var mod in ModLoader.Mods)
            {
                if (!mod.IsEnabled || mod.Manifest.ID != ModId || !mod.Sprites.TryGetValue("ore_device", out var source) || source == null || source.texture == null) continue;
                if (scaledIcon == null || scaledIcon.texture != source.texture)
                {
                    var texture = source.texture;
                    int grid = UISettings.current != null ? Math.Max(1, UISettings.current.gridSize) : 16;
                    // Unity UI uses sprite pixels-per-unit relative to a 100 PPU canvas.
                    float ppu = 100f * texture.width / (3f * grid);
                    scaledIcon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), ppu);
                }
                mod.Sprites["ore_device"] = scaledIcon;
                return;
            }
        }

        // Saved items carry their own description and shape; update the description
        // and only expand an existing item when the occupied cells are available.
        private static void RestoreSavedItem(GameItem item)
        {
            item.shortDescription = Description;
            if (item.parentInventory is GameGridInventory grid && item.shape != null &&
                (item.shape.width != 3 || item.shape.height != 4) && item.modifiedShape != null &&
                item.modifiedXOrigin == 0 && item.modifiedYOrigin == 0 &&
                item.modifiedShape.orientation == 0 && !item.modifiedShape.flipped)
            {
                var enlarged = new GridShapeBuilder().SetDataFill(3, 4, 1)
                    .SetTransform(item.modifiedShape).Build();
                if (grid.CheckInventorySlotStillValid(item, enlarged, item.modifiedState))
                    item.SetShape(new GridShapeBuilder().SetDataFill(3, 4, 1).Build());
            }
        }

        internal static void AddTooltip(RichTextBuilder text, GameItem item)
        {
            if (item == null || item.identifier != Id) return;
            int remaining = GetInt(item, RemainingTag);
            int next = GetInt(item, NextTag);
            if (remaining > 0)
                text.AddLine("待領取：" + remaining + " 個金屬礦石（請騰出空間後再次啟動）");
            else if (next <= 0)
                text.AddLine("蓄能狀態：尚未開始");
            else if (StoreStation.GetDayCounter() >= next)
                text.AddLine("蓄能狀態：已完成，可啟動");
            else
                text.AddLine("蓄能狀態：還需 " + (next - StoreStation.GetDayCounter()) + " 天");
            if (item.shape != null && (item.shape.width != 3 || item.shape.height != 4))
                text.AddLine("要擴充為 3×4，請將裝置轉回原方向並騰出周圍空間，再重新載入。");
        }

        internal static void ScheduleReward()
        {
            ModHelper.SetModDataBool(ModId, PendingKey, true);
            TryDeliverReward();
        }

        internal static void TryDeliverReward()
        {
            if (!ModHelper.GetModDataBool(ModId, PendingKey, false) || EmporiumEntry.Instance == null) return;
            var inventory = EmporiumEntry.Instance.invElement;
            if (inventory == null) return;
            var device = DirectoryMaster.Item(Id, true);
            if (device == null) return;
            ModHelper.SetItemOwnerShip(device, true);
            SetInt(device, NextTag, StoreStation.GetDayCounter() + 3);
            var marker = inventory.TryFindOneValidInventorySlot(device);
            if (marker == null || marker.numTransfer < 1) return;
            if (marker.TryAcceptOnce(1) == 1) ModHelper.SetModDataBool(ModId, PendingKey, false);
        }

        private static void Activate(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (item == null || inventory == null) return;
            int today = StoreStation.GetDayCounter();
            int next = GetInt(item, NextTag);
            if (next <= 0) { SetInt(item, NextTag, today + 3); return; }
            int remaining = GetInt(item, RemainingTag);
            if (remaining <= 0)
            {
                if (today < next) return;
                remaining = 10;
            }
            int delivered = 0;
            while (remaining > 0)
            {
                // Moving one item at a time avoids the game's split path, which
                // constructs a fresh item without copying the ownership tag.
                var ore = DirectoryMaster.Item("common_ore", true);
                if (ore == null) break;
                ModHelper.SetItemOwnerShip(ore, true);
                var marker = inventory.TryFindOneValidInventorySlot(ore);
                if (marker == null || marker.numTransfer <= 0) break;
                if (marker.targetItem != null && !GeneralHelper.IsItemEffectivelyOwned(marker.targetItem)) break;
                if (marker.TryAcceptOnce(1) != 1) break;
                delivered++;
                remaining--;
                SetInt(item, RemainingTag, remaining);
            }
            if (delivered == 0) return;
            if (AudioManager.Instance != null) AudioManager.Instance.Play2(AudioHelper.TURBO);
            if (remaining == 0) SetInt(item, NextTag, today + 3);
        }
    }

    // The generated sprite has large transparent margins. Use the visible
    // image rectangle for hit testing and fit it inside the occupied grid.
    [HarmonyPatch(typeof(GameItemElement), "ValidateSelectLocked")]
    internal static class OreDeviceHitTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItemElement __instance)
        {
            if (__instance == null || __instance.identifier != OreDevice.Id || __instance.modifiedShape == null) return;
            var image = __instance.background?.image;
            if (image == null || image.sprite == null || image.sprite.texture == null || UISettings.current == null) return;
            image.alphaHitTestMinimumThreshold = 0f;
            float grid = Math.Max(1, UISettings.current.gridSize);
            float w = __instance.modifiedShape.globalWidth * grid;
            float h = __instance.modifiedShape.globalHeight * grid;
            float aspect = (float)image.sprite.texture.width / image.sprite.texture.height;
            if (w / h > aspect) w = h * aspect; else h = w / aspect;
            image.rectTransform.sizeDelta = new Vector2(w, h);
            var fake = __instance.fakeBackground?.image;
            if (fake != null) fake.rectTransform.sizeDelta = new Vector2(w, h);
        }
    }
}
