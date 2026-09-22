using SkiaSharp;

namespace DlnaServer.Media.Processing.Thumbnails
{
    internal sealed partial class ImageThumbnailGenerator
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "'{SourcePath}' is not an image format SkiaSharp can create from. Codec result: {codecResult}")]
        private partial void LogUnsupportedImage(string sourcePath, SKCodecResult codecResult);
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "'{SourcePath}' is not an image format SkiaSharp can decode")]
        private partial void LogUnsupportedImageDecode(string sourcePath);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Resizing '{SourcePath}' to {Width}x{Height} failed")]
        private partial void LogResizeFailed(string sourcePath, int width, int height);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Encoding a thumbnail for '{SourcePath}' as {Format} failed")]
        private partial void LogEncodeFailed(string sourcePath, string format);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Thumbnail for '{SourcePath}' failed ({Reason})")]
        private partial void LogThumbnailFailed(string sourcePath, string reason);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Could not read the existing thumbnail '{TargetPath}'; it will be generated again")]
        private partial void LogThumbnailReadFailed(string targetPath, Exception exception);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Warning,
            Message = "'{SourcePath}' reports an unusable size of {Width}x{Height}; no thumbnail is made")]
        private partial void LogUnusableDimensions(string sourcePath, int width, int height);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Warning,
            Message = "The existing thumbnail '{TargetPath}' is {SizeInBytes} bytes, over the "
                + "{MaxBytes}-byte adoption limit, so it is ignored rather than read into memory")]
        private partial void LogAdoptedThumbnailTooLarge(string targetPath, long sizeInBytes, long maxBytes);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Debug,
            Message = "The existing thumbnail '{TargetPath}' is older ({ThumbnailUtc:u}) than its media "
                + "file ({MediaUtc:u}), so it is regenerated rather than adopted")]
        private partial void LogThumbnailStale(string targetPath, DateTime thumbnailUtc, DateTime mediaUtc);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Warning,
            Message = "'{SourcePath}' declares {Width}x{Height}, over the {MaxPixels}-pixel decode limit; "
                + "no thumbnail is made because decoding it would exhaust memory")]
        private partial void LogSourceTooLarge(string sourcePath, int width, int height, long maxPixels);
    }
}
