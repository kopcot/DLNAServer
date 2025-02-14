namespace DLNAServer.Helpers.Files
{
    public static class ThumbnailHelper
    {
        public static (int newHeight, int newWidth, double scaleFactor) CalculateResize(int actualHeight, int actualWidth, int maxHeight = 320, int maxWidth = 480)
        {
            double scaleHeight = Math.Min(maxHeight, actualHeight) / (double)actualHeight;
            double scaleWidth = Math.Min(maxWidth, actualWidth) / (double)actualWidth;
            double scaleFactor = Math.Min(scaleHeight, scaleWidth);

            int newHeight = (int)(actualHeight * scaleFactor);
            int newWidth = (int)(actualWidth * scaleFactor);

            return (newHeight, newWidth, scaleFactor);
        }
    }
}
