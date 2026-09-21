using System.Reflection;
using DlnaServer.Core.Configuration;
using DlnaServer.Persistence;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// The assemblies under governance, resolved from a type in each rather than by name so a rename
    /// or a removed project breaks the build instead of silently skipping its rules.
    /// </summary>
    internal static class SolutionAssemblies
    {
        public const string CoreNamespace = "DlnaServer.Core";
        public const string PersistenceNamespace = "DlnaServer.Persistence";
        public const string MediaNamespace = "DlnaServer.Media";
        public const string UpnpNamespace = "DlnaServer.Upnp";
        public const string HostNamespace = "DlnaServer.Host";
        public const string AdminNamespace = "DlnaServer.Admin";

        public const string ContractsNamespace = "DlnaServer.Core.Contracts";
        public const string EntitiesNamespace = "DlnaServer.Persistence.Entities";

        public const string EntityFrameworkNamespace = "Microsoft.EntityFrameworkCore";
        public const string SqliteNamespace = "Microsoft.Data.Sqlite";
        public const string AspNetCoreNamespace = "Microsoft.AspNetCore";

        public static Assembly Core => typeof(DlnaOptions).Assembly;

        public static Assembly Persistence => typeof(IDatabaseInitializer).Assembly;

        // Resolved from a type in each, as the summary promises. These three used to be Assembly.Load by
        // name, which fails at RUN time rather than build time - so a rename did exactly what the comment
        // said it could not, and the governance simply stopped applying.
        public static Assembly Media => typeof(Media.MediaServiceCollectionExtensions).Assembly;

        public static Assembly Upnp => typeof(Upnp.Didl.DidlDocument).Assembly;

        public static Assembly Host => typeof(Host.Program).Assembly;

        // Admin was absent entirely, so every layering and EF-isolation rule silently skipped it while it
        // referenced Persistence directly.
        public static Assembly Admin => typeof(Admin.App).Assembly;
    }
}
