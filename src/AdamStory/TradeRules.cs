using System.Collections.Generic;

namespace AlienStory
{
    // Snapshot of explicitly offered, player-owned items. The game adapter must
    // validate ownership and map each object and stack without mutating it.
    public sealed class OfferedItem
    {
        public string Id;
        public int Count;
        public bool IsFood;
        public bool IsWater;
        public int KcalPerUnit;
    }

    public static class TradeRules
    {
        public static bool Aid(IReadOnlyList<OfferedItem> items)
        {
            if (items == null || items.Count != 1) return false;
            var item = items[0];
            return item != null && item.Count == 1 && (item.IsFood || item.IsWater);
        }

        public static bool Electronics(IReadOnlyList<OfferedItem> items)
            => Exactly(items, "common_electronic", 3);
        public static bool Battery(IReadOnlyList<OfferedItem> items)
            => Exactly(items, "energy_credit_breeder", 1);

        private static bool Exactly(IReadOnlyList<OfferedItem> items, string id, int needed)
        {
            if (items == null || items.Count == 0) return false;
            long units = 0;
            foreach (var item in items)
            {
                if (item == null || item.Id != id || item.Count <= 0) return false;
                units += item.Count;
            }
            return units == needed;
        }

        public static bool Restitution(IReadOnlyList<OfferedItem> items)
        {
            if (items == null || items.Count == 0) return false;
            long kcal = 0;
            foreach (var item in items)
            {
                if (item == null || !item.IsFood || item.Count <= 0 || item.KcalPerUnit <= 0) return false;
                // Saturate: only the threshold matters and untrusted quantities must not overflow.
                if (kcal < 100) kcal += (long)item.Count * item.KcalPerUnit;
            }
            return kcal >= 100;
        }
    }
}
