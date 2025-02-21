using System.Runtime.CompilerServices;

namespace DLNAServer.Helpers.Files
{
    public static class FileHelper
    {
        public static async Task<ReadOnlyMemory<byte>?> ReadFileAsync<T>(string filePath, ILogger<T> _logger, long maxSizeOfFile = long.MaxValue)
        {
            const int bufferSize = 1_024 * 1_024;

            try
            {
                FileInfo fileInfo = new(filePath);
                if (!fileInfo.Exists)
                {
                    return null;
                }

                await using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: bufferSize, FileOptions.SequentialScan))
                {
                    _logger.LogDebug($"{DateTime.Now} - Check file size.");
                    if (fileStream.Length > (long)int.MaxValue ||
                        fileStream.Length > maxSizeOfFile ||
                        fileStream.Length == 0)
                    {
                        _logger.LogDebug($"{DateTime.Now} - File size '{fileStream.Length}' incorrect for caching, max. possible value {(long)int.MaxValue}, max. config size {maxSizeOfFile}, file path = {filePath}");
                        return null;
                    }

                    var fileSize = (int)fileStream.Length;
                    byte[]? cachedData = GC.AllocateUninitializedArray<byte>(fileSize, pinned: false);
                    byte[]? buffer = GC.AllocateUninitializedArray<byte>(bufferSize, pinned: false);

                    int bytesRead;
                    int offset = 0;

                    _logger.LogDebug($"{DateTime.Now} - Start file reading from disk.");
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        if (fileSize < offset + bytesRead)
                        {
                            return null;
                        }

                        Unsafe.CopyBlockUnaligned(ref cachedData[offset], ref buffer[0], (uint)bytesRead);
                        //buffer.AsSpan(0, bytesRead).CopyTo(cachedData.AsSpan(offset));
                        //Array.Copy(buffer, 0, cachedData, offset, bytesRead);
                        offset += bytesRead; // Move the offset forward
                    }
                    _logger.LogDebug($"{DateTime.Now} - File read for cache - {filePath} ");

                    return cachedData;
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message, [filePath]);
                return null;
            }
        }
        public static void CreateDirectoryIfNoExists(DirectoryInfo? directory)
        {
            ArgumentNullException.ThrowIfNull(directory, nameof(directory));

            if (!directory.Exists)
            {
                directory.Create();
                if (OperatingSystem.IsLinux())
                {
                    directory.UnixFileMode =
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                }
            }
        }
    }
}
