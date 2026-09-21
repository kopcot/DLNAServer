using DlnaServer.Core.Diagnostics;
using DlnaServer.Media.Processing;
using DlnaServer.Media.Processing.Provisioning;
using DlnaServer.Media.Processing.Thumbnails;
using DlnaServer.Media.Scanning;
using DlnaServer.Media.Watching;
using Microsoft.Extensions.DependencyInjection;

namespace DlnaServer.Media
{
    /// <summary>
    /// Registers library scanning and media processing.
    /// </summary>
    public static class MediaServiceCollectionExtensions
    {
        public static IServiceCollection AddDlnaMedia(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddSingleton<ILibraryScanner, LibraryScanner>();
            _ = services.AddSingleton<IFileSystemChangeWatcher, FileSystemChangeWatcher>();
            _ = services.AddSingleton<FFmpegProvisioner>();
            _ = services.AddSingleton<IFFmpegProvisioner>(static p => p.GetRequiredService<FFmpegProvisioner>());
            // The same instance behind both interfaces: availability is resolved once and cached on it, so
            // a second registration would report null forever to whoever read the wrong one.
            _ = services.AddSingleton<IMediaCapabilities>(static p => p.GetRequiredService<FFmpegProvisioner>());
            _ = services.AddSingleton<ImageThumbnailGenerator>();
            _ = services.AddSingleton<IMediaProcessor, MediaProcessor>();

            return services;
        }
    }
}
