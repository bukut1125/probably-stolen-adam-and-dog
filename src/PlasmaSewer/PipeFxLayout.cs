namespace PlasmaSewer
{
    // Pixel measurements from the installed 160x80 faucet1 sprite and the
    // generated 1774x887 effect. Coordinates use a bottom-left UI origin.
    internal static class PipeFxLayout
    {
        internal const int TextureWidth = 1774, TextureHeight = 887;
        internal const int CropX = 390, CropY = 369, CropWidth = 995, CropHeight = 208;
        internal const float MinX = 27f / 160f, MinY = 44f / 80f;
        internal const float MaxX = 120f / 160f, MaxY = 75f / 80f;
        internal static bool SupportsTexture(int width, int height) => width == TextureWidth && height == TextureHeight;
    }
}
