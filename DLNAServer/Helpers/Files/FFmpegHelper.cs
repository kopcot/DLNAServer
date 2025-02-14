using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace DLNAServer.Helpers.Files
{
    public static class FFmpegHelper
    {
        private readonly static SemaphoreSlim downloadFFmpegFile = new(1, 1);
        public static async Task EnsureFFmpegDownloaded<T>(ILogger<T> _logger)
        {
            try
            {
                _logger.LogDebug($"{DateTime.Now} - Started downloading ffmpeg files");
                _ = await downloadFFmpegFile.WaitAsync(timeout: TimeSpan.FromMinutes(5));

                var executablesPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "executables");

                DirectoryInfo directoryInfo = new(executablesPath);

                FileHelper.CreateDirectoryIfNoExists(directoryInfo);

                var files = directoryInfo.EnumerateFiles();

                string ffmpegFileName;
                string ffprobeFileName;
                if (OperatingSystem.IsWindows())
                {
                    ffmpegFileName = "ffmpeg.exe";
                    ffprobeFileName = "ffprobe.exe";
                }
                else if (OperatingSystem.IsLinux())
                {
                    ffmpegFileName = "ffmpeg";
                    ffprobeFileName = "ffprobe";
                }
                else
                {
                    _logger.LogError(new ArgumentOutOfRangeException(), "Undefined OperatingSystem");
                    throw new ApplicationException("Undefined OperatingSystem");
                }

                var ffmpeg = files.FirstOrDefault(f => f.Name.Equals(ffmpegFileName, StringComparison.InvariantCultureIgnoreCase));
                var ffprobe = files.FirstOrDefault(f => f.Name.Equals(ffprobeFileName, StringComparison.InvariantCultureIgnoreCase));
                var isDownloaded = ffmpeg != null && ffprobe != null;

                if (!isDownloaded)
                {
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, executablesPath).WaitAsync(TimeSpan.FromMinutes(15));
                    files = directoryInfo.EnumerateFiles();
                    ffmpeg = files.First(f => f.Name.Equals(ffmpegFileName, StringComparison.InvariantCultureIgnoreCase));
                    ffprobe = files.First(f => f.Name.Equals(ffprobeFileName, StringComparison.InvariantCultureIgnoreCase));
                }

                FFmpeg.SetExecutablesPath(executablesPath, ffmpeg!.Name, ffprobe!.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            finally
            {
                _ = downloadFFmpegFile.Release();
            }

            _logger.LogDebug($"{DateTime.Now} - Downloading ffmpeg files done");
        }
    }
}
