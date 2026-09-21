using System.Reflection;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Guards the rule that entities are for the database and DTOs are for everything else.
    /// </summary>
    /// <remarks>
    /// The compiler already stops another project referencing an entity, because entities are internal.
    /// These rules cover what it cannot: an entity accidentally made public, or one surfacing on a
    /// public signature inside the persistence assembly itself.
    /// </remarks>
    [TestFixture]
    internal sealed class EntityBoundaryTest
    {
        [Test]
        public void Entities_AreAllInternal()
        {
            // Arrange & Act
            var publicEntities = SolutionAssemblies.Persistence
                .GetTypes()
                .Where(static type =>
                    type.Namespace == SolutionAssemblies.EntitiesNamespace
                    && type.IsPublic)
                .Select(static type => type.Name)
                .ToArray();

            // Assert
            publicEntities.Should().BeEmpty(
                "because a public entity can be referenced from any project, which is exactly the leak "
                + "the DTO boundary exists to prevent");
        }

        [Test]
        public void DbContext_IsNotPublic()
        {
            // Arrange & Act
            var dbContext = SolutionAssemblies.Persistence
                .GetTypes()
                .Single(static type => type.Name == "DlnaDbContext");

            // Assert
            dbContext.IsPublic.Should().BeFalse(
                "because a public DbContext lets any layer bypass the repositories and query entities directly");
        }

        /// <summary>
        /// A public method returning or accepting an entity would defeat the boundary from the inside.
        /// </summary>
        [Test]
        public void PublicPersistenceApi_NeverExposesAnEntityType()
        {
            // Arrange
            var entityTypes = SolutionAssemblies.Persistence
                .GetTypes()
                .Where(static type => type.Namespace == SolutionAssemblies.EntitiesNamespace)
                .ToHashSet();

            var publicMethods = SolutionAssemblies.Persistence
                .GetExportedTypes()
                .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                .Where(static method => method.DeclaringType?.Assembly == SolutionAssemblies.Persistence);

            // Act
            var offenders = publicMethods
                .Where(method =>
                    MentionsEntity(method.ReturnType, entityTypes)
                    || method.GetParameters().Any(parameter => MentionsEntity(parameter.ParameterType, entityTypes)))
                .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
                .Distinct()
                .ToArray();

            // Assert
            offenders.Should().BeEmpty(
                "because everything crossing the persistence boundary must be a DTO");
        }

        [Test]
        public void RepositoryInterfaces_ReturnOnlyContractTypes()
        {
            // Arrange
            var repositoryInterfaces = SolutionAssemblies.Persistence
                .GetExportedTypes()
                .Where(static type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal))
                .ToArray();

            repositoryInterfaces.Should().NotBeEmpty("because the repositories are the persistence API");

            // Act
            var offenders = repositoryInterfaces
                .SelectMany(static type => type.GetMethods())
                .SelectMany(static method => Unwrap(method.ReturnType))
                .Where(static type =>
                    type.Assembly == SolutionAssemblies.Persistence
                    && type.Namespace != SolutionAssemblies.PersistenceNamespace)
                .Select(static type => type.FullName!)
                .Distinct()
                .ToArray();

            // Assert
            offenders.Should().BeEmpty(
                "because a repository returns contracts from DlnaServer.Core.Contracts, never persistence types");
        }

        private static bool MentionsEntity(Type type, IReadOnlySet<Type> entityTypes)
        {
            return Unwrap(type).Any(entityTypes.Contains);
        }

        /// <summary>
        /// Flattens a type into itself plus any generic arguments, so <c>Task&lt;List&lt;MediaFile&gt;&gt;</c>
        /// is caught rather than hiding an entity behind a wrapper.
        /// </summary>
        private static IEnumerable<Type> Unwrap(Type type)
        {
            yield return type;

            if (!type.IsGenericType)
            {
                yield break;
            }

            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var nested in Unwrap(argument))
                {
                    yield return nested;
                }
            }
        }
    }
}
