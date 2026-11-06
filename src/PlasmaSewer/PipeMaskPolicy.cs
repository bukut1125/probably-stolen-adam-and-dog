namespace PlasmaSewer
{
    // The original faucet1 sprite has a 160x80 local-pixel coordinate system.
    // Do not tint its foreground clamps, lower brace, caps, or white highlight.
    internal static class PipeMaskPolicy
    {
        internal static bool IsWater(int x, int topY, byte red, byte green, byte blue, byte alpha)
        {
            if (x < 27 || x >= 120 || topY < 14 || topY >= 34 || alpha <= 200) return false;
            if (topY >= 24 && ((x >= 47 && x <= 56) || (x >= 84 && x <= 93))) return false;
            return green > 45 && green * 100 > red * 115 && blue * 100 > red * 105;
        }
    }
}
