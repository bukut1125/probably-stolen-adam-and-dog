using System;
using UnityEngine;

namespace AlienStory
{
    internal static class OreDevice
    {
        private const string ModId = PortraitRegistry.ModId;
        private const string Id = "personal.adam-story.ore-device";
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
        private static void SetInt(GameItem item, string key, int value) => item.ModifyTag(key, tag => tag.Enable().SetInt(value), true);

        private static GameItem Create()
        {
            PrepareIcon();
            var item = ItemDirectory.CreateEmptyItem(Id);
            item.name = "亞當的礦石產生器";
            item.shortDescription = "每三天蓄能一次，啟動後產生十個金屬礦石。";
            item.SetSpriteAndShapeFromMod(ModId, "ore_device");
            item.SetShape(new GridShapeBuilder().SetDataFill(2, 2, 1).Build());
            item.activateDefault = true;
            item.mayActivateSlotItemFunc = (self, inventory, slot) => true;
            item.canActivateSlotItemFunc = (self, inventory, slot) => true;
            item.onActivateSlotItemFunc = Activate;
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
                    float ppu = 100f * texture.width / (2f * grid);
                    scaledIcon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), ppu);
                }
                mod.Sprites["ore_device"] = scaledIcon;
                return;
            }
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
}
