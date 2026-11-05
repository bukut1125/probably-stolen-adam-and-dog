using System;

namespace PlasmaSewer
{
    internal readonly struct LeakWindow
    {
        internal readonly int Start, EndExclusive;
        internal bool Active(int day) => Start > 0 && day >= Start && day < EndExclusive;
        internal LeakWindow(int start, int endExclusive) { Start = start; EndExclusive = endExclusive; }
    }

    internal static class EventPolicy
    {
        // Deterministic per-save pseudo-random choices; never uses the game's RNG.
        private static uint Roll(string runId, int day)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in runId ?? "") { h ^= c; h *= 16777619; }
                h ^= (uint)day; h *= 16777619;
                h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
                return h;
            }
        }

        internal static LeakWindow ForDay(string runId, int day)
        {
            int start = 0, end = 0;
            for (int d = 1; d <= day; d++)
            {
                if (d < end) continue;
                uint roll = Roll(runId, d);
                if (roll % 100 >= 5) continue;
                start = d;
                end = d + 2 + (int)((roll / 100) % 3);
            }
            return new LeakWindow(start, end);
        }

        internal static bool IsActive(string runId, int day) => day > 0 && ForDay(runId, day).Active(day);
        internal static int PlannedContamination(int beforeVolume, int afterVolume, int beforeWater, int afterWater)
        {
            long added = (long)afterVolume - beforeVolume;
            long waterAdded = (long)afterWater - beforeWater;
            if (added <= 0 || waterAdded <= 0) return 0;
            long dose = Math.Max(1L, added / 10L);
            return (int)Math.Min(Math.Min(dose, waterAdded), int.MaxValue);
        }
    }
}
