using DlnaServer.Core.Contracts;
using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.Persistence.Repositories
{
    /// <inheritdoc cref="IUploadDeviceRepository"/>
    internal sealed class UploadDeviceRepository : IUploadDeviceRepository
    {
        private readonly DlnaDbContext _dbContext;

        public UploadDeviceRepository(DlnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<string?> GetLastDestinationAsync(
            string fingerprint,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

            return _dbContext.UploadDevices
                .AsNoTracking()
                .Where(d => d.Fingerprint == fingerprint)
                .Select(static d => d.LastDestination)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <remarks>
        /// Tracked rather than projected, because this one reads its own row to add to a running total.
        /// </remarks>
        public async Task RecordAsync(
            UploadDeviceUpdateDto device,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(device);

            var existing = await _dbContext.UploadDevices
                .FirstOrDefaultAsync(d => d.Fingerprint == device.Fingerprint, cancellationToken);

            if (existing is null)
            {
                _ = _dbContext.UploadDevices.Add(new UploadDeviceEntity
                {
                    Fingerprint = device.Fingerprint,
                    RemoteAddress = device.RemoteAddress,
                    UserAgent = device.UserAgent,
                    AcceptLanguage = device.AcceptLanguage,
                    LastDestination = device.Destination,
                    LastUploadUtc = DateTime.UtcNow,
                    UploadCount = device.FileCount,
                });
            }
            else
            {
                // The address is re-read every time: a device on DHCP keeps its fingerprint and changes
                // its address, and the address that matters is the one it uploaded from last.
                existing.RemoteAddress = device.RemoteAddress;
                existing.UserAgent = device.UserAgent;
                existing.AcceptLanguage = device.AcceptLanguage;
                existing.LastDestination = device.Destination;
                existing.LastUploadUtc = DateTime.UtcNow;
                existing.UploadCount += device.FileCount;
            }

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
