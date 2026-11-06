using System;
using System.Collections.Generic;

namespace PlasmaSewer
{
    internal static class NewsProjection
    {
        internal const string LeakNewsId = "personal.plasma-sewer.news";
        internal static List<StoreEvent> Page(IReadOnlyList<StoreEvent> original, int day, LeakWindow leak, int requestedPage, int capacity, out int page, out int pageCount)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (!leak.Active(day)) throw new ArgumentException("No active leak", nameof(leak));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            var all = new List<StoreEvent>();
            all.Add(new StoreEvent
            {
                identifier = LeakNewsId,
                newsName = "下水道傳出等離子外洩",
                displayName = "等離子外洩",
                newsDescription = "下水道管線出現異常放電，受影響的接水管暫時流出劇毒污水。維修人員提醒居民避免飲用；即使管線修復，先前裝入容器的污水仍須妥善處理。",
                eventType = StoreEvent.EventType.NORMALE,
                eventArea = StoreEvent.EventArea.LOWER,
                duration = leak.EndExclusive - day,
                isPermanent = false,
                isHiddenFromNewsPaper = false
            });
            foreach (var item in original)
                if (item != null && !item.isHiddenFromNewsPaper && item.identifier != LeakNewsId) all.Add(item);
            pageCount = (all.Count + capacity - 1) / capacity;
            page = Math.Max(0, Math.Min(requestedPage, pageCount - 1));
            int first = page * capacity;
            int count = Math.Min(capacity, all.Count - first);
            return all.GetRange(first, count);
        }
    }
}
