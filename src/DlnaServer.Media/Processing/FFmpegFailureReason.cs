namespace DlnaServer.Media.Processing
{
    /// <summary>
    /// Turns what ffmpeg or ffprobe printed on failure into a reason short enough for one log line.
    /// </summary>
    /// <remarks>
    /// Xabe puts the tool's whole standard error into the exception message. On a damaged stream ffmpeg
    /// reports every bad packet, so one warning on the NAS carried 139,365 lines and three of them filled a
    /// 32 MB log file. The lines worth keeping are the error lines in the middle; the banner at the top and
    /// the progress lines at the bottom say nothing about the failure.
    /// </remarks>
    internal static class FFmpegFailureReason
    {
        // A reason at or under both limits is already readable and is returned untouched.
        private const int MaxLines = 8;

        private const int MaxLength = 2_000;

        private const int MaxLineLength = 200;

        private static readonly string[] _errorMarkers =
            ["error", "invalid", "fail", "unable", "cannot", "could not", "no such", "not found", "does not"];

        internal static string Summarise(string message)
        {
            ArgumentNullException.ThrowIfNull(message);

            var span = message.AsSpan();

            if (span.Length <= MaxLength && span.Count('\n') < MaxLines)
            {
                return message;
            }

            var kept = new List<string>(MaxLines);
            var totalLines = 0;
            var errorLines = 0;
            var lastLine = ReadOnlySpan<char>.Empty;

            foreach (var rawLine in span.EnumerateLines())
            {
                var line = rawLine.Trim();

                if (line.IsEmpty)
                {
                    continue;
                }

                totalLines++;
                lastLine = line;

                if (!IsErrorLine(line))
                {
                    continue;
                }

                errorLines++;

                if (kept.Count < MaxLines)
                {
                    var text = line.Length > MaxLineLength
                        ? string.Concat(line[..MaxLineLength], "...")
                        : line.ToString();

                    if (!kept.Contains(text, StringComparer.Ordinal))
                    {
                        kept.Add(text);
                    }
                }
            }

            // Nothing looked like an error: the last line is where ffmpeg usually says why it stopped.
            if (kept.Count == 0 && !lastLine.IsEmpty)
            {
                kept.Add(lastLine.Length > MaxLineLength
                    ? string.Concat(lastLine[..MaxLineLength], "...")
                    : lastLine.ToString());
            }

            return $"{string.Join(" | ", kept)} ({errorLines} error line(s) in {totalLines} lines of ffmpeg output)";
        }

        private static bool IsErrorLine(ReadOnlySpan<char> line)
        {
            for (var index = 0; index < _errorMarkers.Length; index++)
            {
                if (line.Contains(_errorMarkers[index], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
