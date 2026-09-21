namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Works out the pixel size of a generated thumbnail.
    /// </summary>
    public static class ThumbnailSize
    {
        /// <summary>
        /// Scales the source down to fit the bounding box, preserving aspect ratio.
        /// </summary>
        /// <remarks>
        /// Only ever shrinks. A source already smaller than the box is returned unchanged - upscaling
        /// would cost bytes and quality for no gain. Carried over from the reference's
        /// <c>ThumbnailHelper.CalculateResize</c>, which behaved the same way.
        /// </remarks>
        public static (int Width, int Height) Calculate(
            int sourceWidth,
            int sourceHeight,
            int maxWidth,
            int maxHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);

            var scaleWidth = Math.Min(maxWidth, sourceWidth) / (double)sourceWidth;
            var scaleHeight = Math.Min(maxHeight, sourceHeight) / (double)sourceHeight;
            var scale = Math.Min(scaleWidth, scaleHeight);

            // At least one pixel each way: a very wide, very short source can otherwise round to zero,
            // and an encoder handed a zero dimension throws.
            return (
                Math.Max(1, (int)(sourceWidth * scale)),
                Math.Max(1, (int)(sourceHeight * scale)));
        }
    }
}
