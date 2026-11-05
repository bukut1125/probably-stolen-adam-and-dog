namespace AlienStory
{
    public static class PreviewPlanner
    {
        public const int Dog = 1;
        public const int Adam = 2;
        public const int Hunger1 = 4;
        public const int Hunger2 = 8;
        public const int Hunger3 = 16;
        public const int Scavenger = 32;
        public const int God = 64;
        public const int All = Dog | Adam | Hunger1 | Hunger2 | Hunger3 | Scavenger | God;
        public static bool IsComplete(int seen) => (seen & All) == All;
        public static int Missing(int seen) => All & ~seen;
        public static int Arrived(int seen, int role) => seen | (role & All);
    }
}
