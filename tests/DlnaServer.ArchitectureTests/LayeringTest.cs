using NetArchTest.Rules;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Enforces the direction of dependencies between projects.
    /// </summary>
    /// <remarks>
    /// The reference implementation was one project, so nothing could enforce a boundary and none
    /// existed: SOAP endpoints held EF entities, controllers reached into repositories and the
    /// protocol layer could not be exercised without a database. These rules stop that recurring.
    /// </remarks>
    [TestFixture]
    internal sealed class LayeringTest
    {
        [Test]
        public void Core_DependsOnNoOtherSolutionProject()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Core)
                .Should()
                .NotHaveDependencyOnAny(
                    SolutionAssemblies.PersistenceNamespace,
                    SolutionAssemblies.MediaNamespace,
                    SolutionAssemblies.UpnpNamespace,
                    SolutionAssemblies.HostNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because Core is the shared contract every other project depends on, so it must depend on none of them; offenders: {0}",
                Describe(result));
        }

        [Test]
        public void Core_DoesNotDependOnEntityFrameworkOrAspNetCore()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Core)
                .Should()
                .NotHaveDependencyOnAny(
                    SolutionAssemblies.EntityFrameworkNamespace,
                    SolutionAssemblies.SqliteNamespace,
                    SolutionAssemblies.AspNetCoreNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because the domain model and DTOs must not be shaped by the storage or the web framework; offenders: {0}",
                Describe(result));
        }

        /// <summary>
        /// This is what lets the protocol output be asserted byte-for-byte without a database or a server.
        /// </summary>
        [Test]
        public void Upnp_DoesNotDependOnPersistenceOrEntityFramework()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Upnp)
                .Should()
                .NotHaveDependencyOnAny(
                    SolutionAssemblies.PersistenceNamespace,
                    SolutionAssemblies.EntityFrameworkNamespace,
                    SolutionAssemblies.SqliteNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because the protocol layer must stay testable without a database; offenders: {0}",
                Describe(result));
        }

        [Test]
        public void Media_DoesNotDependOnPersistenceOrEntityFramework()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Media)
                .Should()
                .NotHaveDependencyOnAny(
                    SolutionAssemblies.PersistenceNamespace,
                    SolutionAssemblies.EntityFrameworkNamespace,
                    SolutionAssemblies.SqliteNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because scanning and thumbnailing operate on files and DTOs, not on the database; offenders: {0}",
                Describe(result));
        }

        [Test]
        public void Persistence_DoesNotDependOnUpnpMediaOrHost()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Persistence)
                .Should()
                .NotHaveDependencyOnAny(
                    SolutionAssemblies.UpnpNamespace,
                    SolutionAssemblies.MediaNamespace,
                    SolutionAssemblies.HostNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because storage sits below the layers that use it; offenders: {0}",
                Describe(result));
        }

        [Test]
        public void Persistence_DoesNotDependOnAspNetCore()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Persistence)
                .Should()
                .NotHaveDependencyOn(SolutionAssemblies.AspNetCoreNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because persistence must be usable outside a web host - migrations and tests run without one; offenders: {0}",
                Describe(result));
        }

        /// <summary>
        /// Entity Framework is an implementation detail of one project. If a second project picks up a
        /// reference, entities have started leaking even if the compiler has not caught it yet.
        /// </summary>
        [Test]
        public void EntityFramework_IsReferencedOnlyByPersistence()
        {
            // Arrange
            // Admin included: it references Persistence directly, so it is the assembly most able to
            // acquire a direct EF dependency, and it was the one this rule did not cover.
            var assemblies = new[]
            {
                SolutionAssemblies.Core,
                SolutionAssemblies.Media,
                SolutionAssemblies.Upnp,
                SolutionAssemblies.Host,
                SolutionAssemblies.Admin,
            };

            // Act
            var offenders = assemblies
                .Where(static assembly => assembly
                    .GetReferencedAssemblies()
                    .Any(static reference =>
                        reference.Name is not null
                        && reference.Name.StartsWith(SolutionAssemblies.EntityFrameworkNamespace, StringComparison.Ordinal)))
                .Select(static assembly => assembly.GetName().Name)
                .ToArray();

            // Assert
            offenders.Should().BeEmpty(
                "because only DlnaServer.Persistence may reference Entity Framework");
        }

        /// <summary>
        /// The admin UI reaches only Core and Persistence.
        /// </summary>
        /// <remarks>
        /// Stated in <c>CLAUDE.md</c> - "references only Core and Persistence" - and enforced by nothing.
        /// It is the seam that forces <c>ILibraryScanSignal</c>, <c>IRestartSignal</c> and
        /// <c>IDatabaseResetSignal</c> to exist at all: a page cannot start a scan directly because it
        /// cannot see the host. A stray reference would make every one of those look redundant.
        /// </remarks>
        [Test]
        public void Admin_DependsOnNeitherHostNorUpnpNorMedia()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Admin)
                .Should()
                .NotHaveDependencyOnAny(
                    SolutionAssemblies.HostNamespace,
                    SolutionAssemblies.UpnpNamespace,
                    SolutionAssemblies.MediaNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because the admin UI reaches the host only through the signal interfaces in Core, which "
                + "is what stops it depending on the executable that hosts it; offenders: {0}",
                Describe(result));
        }

        /// <summary>
        /// The UPnP layer speaks the protocol, not HTTP.
        /// </summary>
        /// <remarks>
        /// <c>CLAUDE.md</c> says "no EF, no HTTP" for this project, and only the EF half was checked. The
        /// distinction is what keeps DIDL-Lite and SSDP message building testable without a request.
        /// </remarks>
        [Test]
        public void Upnp_DoesNotDependOnAspNetCore()
        {
            // Arrange & Act
            var result = Types.InAssembly(SolutionAssemblies.Upnp)
                .Should()
                .NotHaveDependencyOn(SolutionAssemblies.AspNetCoreNamespace)
                .GetResult();

            // Assert
            result.IsSuccessful.Should().BeTrue(
                "because building a DIDL document or an SSDP datagram must not need a request or a "
                + "response to exist; offenders: {0}",
                Describe(result));
        }

        /// <summary>
        /// Nothing references the executable.
        /// </summary>
        /// <remarks>
        /// <c>CLAUDE.md</c> states it of the Host project - "the only executable; nothing references it".
        /// Two of the four assemblies that could break it were covered, by rules aimed at something else.
        /// </remarks>
        [Test]
        public void Host_IsReferencedByNothing()
        {
            // Arrange
            var assemblies = new[]
            {
                SolutionAssemblies.Core,
                SolutionAssemblies.Persistence,
                SolutionAssemblies.Media,
                SolutionAssemblies.Upnp,
                SolutionAssemblies.Admin,
            };

            // Act
            var offenders = assemblies
                .Where(static assembly => assembly
                    .GetReferencedAssemblies()
                    .Any(static reference => reference.Name == SolutionAssemblies.HostNamespace))
                .Select(static assembly => assembly.GetName().Name)
                .ToArray();

            // Assert
            offenders.Should().BeEmpty(
                "because the executable composes the other projects and none of them may compose it");
        }

        private static string Describe(TestResult result)
        {
            return result.FailingTypeNames is null
                ? "none reported"
                : string.Join(", ", result.FailingTypeNames);
        }
    }
}
