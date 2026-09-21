using System.Reflection;
using Microsoft.Extensions.Logging;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Enforces the conventions the project states about itself but had no test for.
    /// </summary>
    /// <remarks>
    /// Both rules below are written down - unique <c>[LoggerMessage]</c> EventIds per class, and every
    /// entity carrying <c>int Id</c> plus <c>Guid PublicId</c> - and both were checked only by whoever
    /// happened to read the file. Reflection makes them cost nothing to keep.
    /// </remarks>
    [TestFixture]
    internal sealed class ConventionTest
    {
        /// <summary>
        /// Two logging methods on one class must not share an EventId.
        /// </summary>
        /// <remarks>
        /// A duplicate compiles and runs; it only shows up as two different events indistinguishable in a
        /// log, which is precisely when someone is trying to work out what a live server did.
        /// </remarks>
        [Test]
        public void LoggerMessageEventIds_AreUniqueWithinEachClass()
        {
            // Arrange
            var assemblies = new[]
            {
                SolutionAssemblies.Core,
                SolutionAssemblies.Persistence,
                SolutionAssemblies.Media,
                SolutionAssemblies.Upnp,
                SolutionAssemblies.Host,
                SolutionAssemblies.Admin,
            };

            // Act
            var duplicates = new List<string>();

            foreach (var type in assemblies.SelectMany(static assembly => assembly.GetTypes()))
            {
                var ids = type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(static method => method.GetCustomAttribute<LoggerMessageAttribute>())
                    .Where(static attribute => attribute is not null)
                    .Select(static attribute => attribute!.EventId)
                    .ToList();

                duplicates.AddRange(ids
                    .GroupBy(static id => id)
                    .Where(static group => group.Count() > 1)
                    .Select(group => $"{type.FullName} reuses EventId {group.Key} {group.Count()} times"));
            }

            // Assert
            duplicates.Should().BeEmpty(
                "because two events sharing an id are indistinguishable in a log, which defeats the "
                + "reason for giving them ids at all");
        }

        /// <summary>
        /// Every entity carries the internal surrogate key and the external identifier.
        /// </summary>
        /// <remarks>
        /// <c>Id</c> is what foreign keys and indexes are built on and never leaves persistence;
        /// <c>PublicId</c> is what DTOs, DIDL-Lite ObjectIDs and admin URLs carry. An entity missing
        /// either breaks a rule the whole repository boundary is built on.
        /// </remarks>
        [Test]
        public void EveryEntity_HasAnIntIdAndAGuidPublicId()
        {
            // Arrange
            var entities = SolutionAssemblies.Persistence
                .GetTypes()
                .Where(static type => type.Namespace == SolutionAssemblies.EntitiesNamespace)
                .Where(static type => type is { IsClass: true, IsAbstract: false })
                .ToList();

            // Act
            var offenders = entities
                .Where(static type =>
                    type.GetProperty("Id")?.PropertyType != typeof(int)
                    || type.GetProperty("PublicId")?.PropertyType != typeof(Guid))
                .Select(static type => type.FullName ?? type.Name)
                .ToList();

            // Assert
            entities.Should().NotBeEmpty(
                "because a rule that finds no types to check passes while proving nothing");
            offenders.Should().BeEmpty(
                "because every entity needs the internal integer key and the external identifier");
        }
    }
}
