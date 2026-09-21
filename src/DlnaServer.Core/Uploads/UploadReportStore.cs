using System.Collections.Concurrent;

namespace DlnaServer.Core.Uploads
{
    /// <inheritdoc cref="IUploadReportStore"/>
    public sealed class UploadReportStore : IUploadReportStore
    {
        /// <summary>
        /// Long enough for a redirect and a reload or two, short enough that a report naming files and
        /// folders is not left lying in memory.
        /// </summary>
        private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

        private readonly ConcurrentDictionary<Guid, Held> _reports = new();
        private readonly TimeProvider _timeProvider;

        public UploadReportStore(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public void Add(UploadReport report)
        {
            ArgumentNullException.ThrowIfNull(report);

            var now = _timeProvider.GetUtcNow();

            _reports[report.Id] = new Held(report, now.Add(_lifetime));

            // Swept here rather than on a timer, for the reason the other expiring state in this server
            // uses: an upload is the only thing that grows this, so it is also the only thing that needs
            // to tidy it.
            RemoveLapsed(now);
        }

        public UploadReport? Find(Guid id)
        {
            if (!_reports.TryGetValue(id, out var held))
            {
                return null;
            }

            if (_timeProvider.GetUtcNow() < held.UntilUtc)
            {
                return held.Report;
            }

            _ = _reports.TryRemove(id, out _);

            return null;
        }

        private void RemoveLapsed(DateTimeOffset now)
        {
            foreach (var pair in _reports)
            {
                if (now >= pair.Value.UntilUtc)
                {
                    _ = _reports.TryRemove(pair.Key, out _);
                }
            }
        }

        private sealed record Held(UploadReport Report, DateTimeOffset UntilUtc);
    }
}
