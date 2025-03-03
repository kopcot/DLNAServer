using DLNAServer.Common;
using DLNAServer.Helpers.Logger;
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
                _logger.LogGeneralDebugMessage("Started downloading ffmpeg files");
                _ = await downloadFFmpegFile.WaitAsync(timeout: TimeSpanValues.Time5min);

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
                    _logger.LogGeneralErrorMessage(new ArgumentOutOfRangeException("OperatingSystem"));
                    throw new ApplicationException("Undefined OperatingSystem");
                }

                var ffmpeg = files.FirstOrDefault(f => f.Name.Equals(ffmpegFileName, StringComparison.InvariantCultureIgnoreCase));
                var ffprobe = files.FirstOrDefault(f => f.Name.Equals(ffprobeFileName, StringComparison.InvariantCultureIgnoreCase));
                var isDownloaded = ffmpeg != null && ffprobe != null;

                if (!isDownloaded)
                {
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, executablesPath).WaitAsync(TimeSpanValues.Time15min);
                    files = directoryInfo.EnumerateFiles();
                    ffmpeg = files.First(f => f.Name.Equals(ffmpegFileName, StringComparison.InvariantCultureIgnoreCase));
                    ffprobe = files.First(f => f.Name.Equals(ffprobeFileName, StringComparison.InvariantCultureIgnoreCase));
                }

                FFmpeg.SetExecutablesPath(executablesPath, ffmpeg!.Name, ffprobe!.Name);
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
            }
            finally
            {
                _ = downloadFFmpegFile.Release();
            }

            _logger.LogGeneralDebugMessage("Downloading ffmpeg files done");
        }
    }
}
