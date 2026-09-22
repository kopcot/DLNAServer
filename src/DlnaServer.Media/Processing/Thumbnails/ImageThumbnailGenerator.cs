using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using SkiaSharp;

namespace DlnaServer.Media.Processing.Thumbnails
{
    /// <summary>
    /// Produces thumbnails for image files using SkiaSharp.
    /// </summary>
    /// <remarks>
    /// Every Skia handle here is disposed on the same statement that creates it. Skia allocates its
    /// buffers natively, outside the managed heap, so a leak does not show up as GC pressure - it shows
    /// up as resident memory that never comes back. The live reference server carries roughly 350 MB of
    /// native memory against a 42 MB managed heap, and this is one of the contributors.
    /// </remarks>
    internal sealed partial class ImageThumbnailGenerator
    {
        // 24 M px x 4 bytes = 96 MB of native decode buffer, and above it the file is skipped with
        // LogSourceTooLarge - an already-handled path. The limit is for hostile input: the pixel count
        // comes from the file header, so it is attacker-chosen for anything writable into a source
        // folder. Was 80 M px, i.e. 320 MB for one image - 1.6x the entire working-set target.
        //
        // This is a BUDGET, not a "no real photograph is this big" claim, which is what the comment used
        // to say and was wrong: phone sensors are 48-200 MP and a 6016x4016 DSLR frame is 24,160,256 px,
        // just over. Such a photo is skipped, permanently and on every pass. That is the accepted trade
        // on a 2-4 GB appliance - DecodeScaled spares JPEG and WebP the full-resolution decode, but PNG
        // decodes whole, which is why a cap exists at all rather than scaling covering it.
        private const long MaxDecodePixels = 24_000_000;

        // Generous for a 480x360 JPEG - 25-45 KB in practice, so nothing real comes near this. Lowered
        // from 4 MB because adoption runs once per file, and a first pass over an already-previewed
        // library takes that path for every one of 25,504 of them: a quarter of the worst-case array is
        // a quarter of the large object heap churn. Both figures are above the 85,000-byte LOH threshold,
        // so this bounds the churn rather than eliminating it.
        private const long MaxAdoptedThumbnailBytes = 1L * 1024 * 1024;

        private readonly ILogger<ImageThumbnailGenerator> _logger;

        public ImageThumbnailGenerator(ILogger<ImageThumbnailGenerator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Describes a thumbnail that already exists on disk, without regenerating it.
        /// </summary>
        /// <remarks>
        /// Only the header is decoded, so this costs a few bytes of read rather than a full decode. The
        /// reference does the same thing by checking the target file before doing any work - which is what
        /// lets a library that has been previewed once survive a redeploy without regenerating every
        /// thumbnail it already has.
        /// </remarks>
        public GeneratedThumbnail? Describe(string targetPath, ThumbnailRequest request, DateTime? notOlderThanUtc = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var info = new FileInfo(targetPath);

                if (!info.Exists || info.Length == 0)
                {
                    return null;
                }

                // An adopted thumbnail is read whole into a managed array when StoreContent is on, and the
                // file comes from a folder anyone can write to - so a 1.5 GB image planted as
                // "Film.mkv.jpg" would land on the large object heap. No real thumbnail approaches this.
                if (info.Length > MaxAdoptedThumbnailBytes)
                {
                    LogAdoptedThumbnailTooLarge(targetPath, info.Length, MaxAdoptedThumbnailBytes);
                    return null;
                }

                // Thumbnail paths are deterministic from the media file's name, so a re-encoded video
                // finds its old preview sitting there and adopts it forever. Refusing an image older than
                // the media is what makes a changed file actually get a new thumbnail.
                if (notOlderThanUtc is { } threshold && info.LastWriteTimeUtc < threshold)
                {
                    LogThumbnailStale(targetPath, info.LastWriteTimeUtc, threshold);
                    return null;
                }

                using var input = File.OpenRead(targetPath);
                using var codec = SKCodec.Create(input, out var codecResult);

                if (codec is null)
                {
                    // Unreadable, so treat it as absent and let it be generated over.
                    LogUnsupportedImage(targetPath, codecResult);
                    return null;
                }

                return new GeneratedThumbnail(
                    targetPath,
                    request.Mime,
                    codec.Info.Width,
                    codec.Info.Height,
                    info.Length,
                    request.StoreContent ? File.ReadAllBytes(targetPath) : null,
                    WasAdopted: true);
            }
            catch (Exception exception)
            {
                // Same reasoning as Generate below: an existing thumbnail that cannot be read is simply
                // not adopted, and the caller generates a fresh one.
                LogThumbnailReadFailed(targetPath, exception);
                return null;
            }
        }

        public GeneratedThumbnail? Generate(string sourcePath, string targetPath, ThumbnailRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                using var input = File.OpenRead(sourcePath);
                using var codec = SKCodec.Create(input, out var codecResult);

                if (codec is null)
                {
                    LogUnsupportedImage(sourcePath, codecResult);
                    return null;
                }

                // A truncated file can produce a codec whose header parses but reports a zero dimension.
                // ThumbnailSize.Calculate rejects that with ArgumentOutOfRangeException - correctly, its
                // contract is a programmer one - so the dimensions are validated here instead, where a
                // corrupt file is an expected input rather than a defect.
                if (codec.Info.Width <= 0 || codec.Info.Height <= 0)
                {
                    LogUnusableDimensions(sourcePath, codec.Info.Width, codec.Info.Height);
                    return null;
                }

                // The header is trusted for dimensions but not for what they imply. Skia allocates
                // width * height * 4 bytes of NATIVE memory to decode, so a ~1 MB file declaring
                // 40000x40000 asks for about 6.4 GB on a box that has 2-4 GB. The allocation is outside
                // the managed heap, so nothing in /manage/memory sees it coming, and on Linux overcommit
                // the malloc succeeds and the OOM killer takes the process as the pages are touched -
                // which no catch below can intercept. The file also stays in the library, so the next
                // pass would repeat it.
                if ((long)codec.Info.Width * codec.Info.Height > MaxDecodePixels)
                {
                    LogSourceTooLarge(sourcePath, codec.Info.Width, codec.Info.Height, MaxDecodePixels);
                    return null;
                }

                var (width, height) = ThumbnailSize.Calculate(
                    codec.Info.Width,
                    codec.Info.Height,
                    request.MaxWidth,
                    request.MaxHeight);

                using var source = DecodeScaled(codec, width);

                if (source is null)
                {
                    LogUnsupportedImageDecode(sourcePath);
                    return null;
                }

                // Keep the source's own colour and alpha types. Forcing a specific one makes Resize
                // fail outright on formats that cannot be converted directly.
                using var resized = new SKBitmap(width, height, source.ColorType, source.AlphaType);

                // Linear sampling rather than the reference's SKFilterMode.Nearest, which is the
                // cheapest and worst resample available.
                if (!source.ScalePixels(resized, SKSamplingOptions.Default))
                {
                    LogResizeFailed(sourcePath, width, height);
                    return null;
                }

                using var image = SKImage.FromBitmap(resized);
                using var encoded = image.Encode(ToSkiaFormat(request.Mime), request.Quality);

                if (encoded is null)
                {
                    LogEncodeFailed(sourcePath, request.Mime.ToString());
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                // Materialise once and use it for both destinations. This used to write the file and
                // then read it straight back with File.ReadAllBytes to get the database copy - a second
                // full read of a file this method had just produced, and a second allocation of bytes
                // already in hand, once per thumbnail. On a first scan of a 25,000-file library that is
                // 25,000 redundant reads off a NAS volume.
                var content = encoded.AsSpan().ToArray();

                File.WriteAllBytes(targetPath, content);

                return new GeneratedThumbnail(
                    targetPath,
                    request.Mime,
                    width,
                    height,
                    content.Length,
                    request.StoreContent ? content : null,
                    WasAdopted: false);
            }
            catch (Exception exception)
            {
                // Skia is a native decoder fed arbitrary user files: a corrupt or hostile image can
                // throw types that have nothing to do with I/O, and one of them reaching the hosted
                // service stopped the whole host. Failing to produce a thumbnail is never fatal.
                LogThumbnailFailed(sourcePath, exception.Message);
                return null;
            }
        }

        // Decodes at the smallest size the codec natively supports at or above the target width, so a
        // 6000x4000 photo costs about 1.5 MB at 1/8 rather than 96 MB at full size. Only JPEG and WebP
        // implement scaled decode; every other format returns its full dimensions here and decodes whole,
        // which is why MaxDecodePixels is checked before this and not instead of it.
        private static SKBitmap? DecodeScaled(SKCodec codec, int targetWidth)
        {
            var scale = targetWidth / (float)codec.Info.Width;
            var scaled = codec.GetScaledDimensions(scale);
            var info = codec.Info
                .WithSize(scaled.Width, scaled.Height)
                .WithColorType(codec.Info.ColorType)
                .WithAlphaType(codec.Info.AlphaType);

            return SKBitmap.Decode(codec, info);
        }

        private static SKEncodedImageFormat ToSkiaFormat(DlnaMime mime)
        {
            return mime switch
            {
                DlnaMime.ImagePng => SKEncodedImageFormat.Png,
                DlnaMime.ImageWebp => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg,
            };
        }
    }
}
