using DlnaServer.Core.Contracts;
using DlnaServer.Persistence.Repositories;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Keeps every update it is given in <see cref="Recorded"/>, and throws <see cref="FailWith"/> from
    /// <see cref="RecordAsync"/> when it is set.
    /// </summary>
    internal sealed class FakeUploadDeviceRepository : IUploadDeviceRepository
    {
        public Exception? FailWith { get; init; }

        public List<UploadDeviceUpdateDto> Recorded { get; } = [];

        public Task<string?> GetLastDestinationAsync(string fingerprint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task RecordAsync(UploadDeviceUpdateDto device, CancellationToken cancellationToken = default)
        {
            Recorded.Add(device);

            return FailWith is null
                ? Task.CompletedTask
                : Task.FromException(FailWith);
        }
    }
}
