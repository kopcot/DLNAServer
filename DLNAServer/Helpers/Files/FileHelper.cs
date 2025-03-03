using DLNAServer.Helpers.Logger;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DLNAServer.Helpers.Files
{
    public static partial class FileHelper
    {
        private static readonly ArrayPool<byte> ArrayPool_Buffer = ArrayPool<byte>.Shared;
        private static readonly MemoryPool<byte> MemoryPool_CachedData = MemoryPool<byte>.Shared;
        public static async Task<ReadOnlyMemory<byte>?> ReadFileAsync<T>(string filePath, ILogger<T> _logger, long maxSizeOfFile = long.MaxValue)
        {
            const int bufferSize = 8 * 1_024; // less as 85,000 bytes in size for not need to use Large Object Heap (LOH) 
            const int maxMemoryPoolSize = 1024 * 1_024; // 1MB
            byte[]? buffer = ArrayPool_Buffer.Rent(bufferSize);

            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                await using (FileStream fileStream = new(
                    path: filePath,
                    mode: FileMode.Open,
                    access: FileAccess.Read,
                    share: FileShare.ReadWrite,
                    bufferSize: bufferSize,
                    options: FileOptions.SequentialScan | FileOptions.Asynchronous))
                {
                    LogCheckFileSize(_logger);
                    if (fileStream.Length > (long)int.MaxValue ||
                        fileStream.Length > maxSizeOfFile ||
                        fileStream.Length == 0)
                    {
                        LogFileSizeIncorrect(
                            _logger,
                            fileStream.Length,
                            (long)int.MaxValue,
                            maxSizeOfFile,
                            filePath
                        );
                        return null;
                    }

                    var fileSize = (int)fileStream.Length;

                    int bytesRead;
                    int offset = 0;

                    LogFileStartReading(_logger, filePath);
                    if (fileSize > maxMemoryPoolSize)
                    {
                        byte[]? cachedData = GC.AllocateUninitializedArray<byte>(fileSize, pinned: false);
                        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, bufferSize).ConfigureAwait(false)) > 0)
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

                        return cachedData.AsMemory();
                    }
                    else
                    {
                        using (var cachedDataMemoryOwner = MemoryPool_CachedData.Rent(GetNextSize(fileSize)))
                        {
                            var cachedData = cachedDataMemoryOwner.Memory;
                            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, bufferSize).ConfigureAwait(false)) > 0)
                            {
                                if (fileSize < offset + bytesRead)
                                {
                                    return null;
                                }

                                CopyToMemory(ref cachedData, ref buffer, offset, bytesRead);

                                offset += bytesRead; // Move the offset forward
                            }

                            LogFileDoneReading(_logger, filePath);

                            return cachedData[0..fileSize];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogGeneralErrorMessage(ex);
                return null;
            }
            finally
            {
                ArrayPool_Buffer.Return(buffer, true);
            }
        }
        private static int GetNextSize(int size)
        {
            const int minSize = 8 * 1024;

            return (int)BitOperations.RoundUpToPowerOf2((nuint)Math.Max(size, minSize));
        }
        private static void CopyToMemory(ref Memory<byte> cachedMemory, ref byte[] buffer, int offset, int bytesRead)
        {
            Span<byte> cachedSpan = cachedMemory.Span;

            if (offset + bytesRead > cachedSpan.Length
                || bytesRead > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesRead), "Invalid buffer or offset size.");
            }

            ref byte destRef = ref MemoryMarshal.GetReference(cachedSpan[offset..]);
            ref byte srcRef = ref buffer[0];

            Unsafe.CopyBlockUnaligned(ref destRef, ref srcRef, (uint)bytesRead);
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
