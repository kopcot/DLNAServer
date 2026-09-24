using DlnaServer.Admin.Components;
using DlnaServer.Admin.Formatting;
using DlnaServer.Admin.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DlnaServer.Admin
{
    /// <summary>
    /// Registers the admin UI's own services.
    /// </summary>
    /// <remarks>
    /// The registration lives here rather than in the host's <c>Program</c> so the services themselves can
    /// stay <c>internal</c> to this project: the host cannot name an internal type, but it can call this.
    /// </remarks>
    public static class AdminServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the formatting and link-building services every admin component depends on.
        /// </summary>
        public static IServiceCollection AddDlnaAdminServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Both are stateless and hold no configuration, so one instance serves every circuit.
            _ = services.AddSingleton<IMediaFormatter, MediaFormatter>();
            _ = services.AddSingleton<IAdminUrls, AdminUrls>();

            // One per process on purpose: every open Dashboard, pre-rendered or live, shares the last count.
            _ = services.AddSingleton<LibraryCountsCache>();

            return services;
        }
    }
}
