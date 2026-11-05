using System;
using System.Collections.Generic;

namespace AlienStory
{
    internal static class GameTradeAdapter
    {
        // Only inspect the items deliberately placed in the barter area.
        // Reject a mixed area so the base game's swap cannot silently take unrelated items.
        internal static bool TrySnapshot(out List<OfferedItem> items)
        {
            items = new List<OfferedItem>();
            var entry = EmporiumEntry.Instance;
            if (entry == null || entry.backInvinvElement == null) return false;
            var all = entry.backInvinvElement.childItems;
            var owned = entry.GetOwnedItemsBack();
            if (all == null || owned == null || all.Count == 0 || all.Count != owned.Count) return false;
            var unique = new HashSet<GameItem>();
            foreach (var item in all)
            {
                if (item == null || !unique.Add(item) || !owned.Contains(item) || item.unitCount <= 0) return false;
                int kcal = 0;
                if (item.IsTag(FoodItemHelper.CALORIE_VALUE_TAG))
                {
                    var calorie = item.GetTagReadonly(FoodItemHelper.CALORIE_VALUE_TAG);
                    if (calorie != null) kcal = Math.Max(0, calorie.GetInt());
                }
                bool isWater = item.IsGameItemType("WATER") ||
                    (item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) && WaterHelper.GetWaterPurity(item) > 0);
                items.Add(new OfferedItem {
                    Id = item.identifier,
                    Count = item.unitCount,
                    IsFood = item.IsGameItemType("FOOD") && kcal > 0,
                    IsWater = isWater,
                    KcalPerUnit = kcal
                });
            }
            return true;
        }

        internal static bool Matches(Stage stage)
        {
            if (!TrySnapshot(out var offered)) return false;
            switch (stage)
            {
                case Stage.Aid: return TradeRules.Aid(offered);
                case Stage.Scavenger: return TradeRules.Electronics(offered);
                case Stage.Battery: return TradeRules.Battery(offered);
                case Stage.Restitution: return TradeRules.Restitution(offered);
                default: return false;
            }
        }
    }
}
