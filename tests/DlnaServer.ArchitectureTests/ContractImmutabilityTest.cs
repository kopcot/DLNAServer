namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Keeps the DTOs in <c>DlnaServer.Core.Contracts</c> immutable and sealed.
    /// </summary>
    /// <remarks>
    /// A DTO travels between layers. If any of them can mutate it, the value a caller handed over is no
    /// longer the value it holds, and a bug becomes very hard to place.
    /// </remarks>
    [TestFixture]
    internal sealed class ContractImmutabilityTest
    {
        [Test]
        public void Contracts_AreAllSealed()
        {
            // Arrange & Act
            var unsealed = ContractTypes()
                .Where(static type => !type.IsSealed)
                .Select(static type => type.Name)
                .ToArray();

            // Assert
            unsealed.Should().BeEmpty("because a DTO is a value, not a base class");
        }

        [Test]
        public void Contracts_HaveNoSettableProperties()
        {
            // Arrange & Act
            var offenders = ContractTypes()
                .SelectMany(static type => type.GetProperties()
                    .Where(static property => IsMutable(property))
                    .Select(property => $"{type.Name}.{property.Name}"))
                .ToArray();

            // Assert
            offenders.Should().BeEmpty(
                "because contracts use init-only properties so a layer cannot alter a DTO it was handed");
        }

        /// <remarks>
        /// <c>byte[]</c>, <c>Dictionary</c> and <c>HashSet</c> are checked as well as the three generic
        /// interfaces this started with. An array is the gap that mattered: init-only stops the reference
        /// being replaced and does nothing about the contents, so the guarantee the summary above claims
        /// was never total - <c>GeneratedThumbnail.Content</c> is a live instance of exactly that.
        /// </remarks>
        [Test]
        public void Contracts_DoNotExposeMutableCollections()
        {
            // Arrange & Act
            var offenders = ContractTypes()
                .SelectMany(static type => type.GetProperties()
                    .Where(static property => IsMutableCollection(property.PropertyType))
                    .Select(property => $"{type.Name}.{property.Name}"))
                .Where(static name => !_allowedMutablePayloads.Contains(name))
                .ToArray();

            // Assert
            offenders.Should().BeEmpty(
                "because a mutable collection on a DTO lets a consumer change it after the fact - "
                + "use IReadOnlyList<T>, or ReadOnlyMemory<byte> for a payload");
        }

        /// <summary>
        /// Array-typed contract properties this rule deliberately allows, by name.
        /// </summary>
        /// <remarks>
        /// A list rather than dropping the array check, so a NEW array on a DTO still fails. This one is
        /// allowed because the alternative costs more than it buys: the payload is produced once by
        /// <c>ImageThumbnailGenerator</c> and consumed once when the row is saved, where EF wants a
        /// <c>byte[]</c> for the column - so moving the contract to <c>ReadOnlyMemory&lt;byte&gt;</c> would
        /// add a full copy of every generated thumbnail at the persistence boundary, in a project whose
        /// hard constraint is memory. Nothing hands this instance to a caller who could mutate it.
        /// </remarks>
        private static readonly HashSet<string> _allowedMutablePayloads =
            new(StringComparer.Ordinal) { "GeneratedThumbnail.Content" };

        private static bool IsMutableCollection(Type type)
        {
            // An array's contents stay writable however the property is declared, which is what makes it
            // worth naming separately from the generic shapes below.
            if (type.IsArray)
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            var definition = type.GetGenericTypeDefinition();

            return definition == typeof(List<>)
                || definition == typeof(ICollection<>)
                || definition == typeof(IList<>)
                || definition == typeof(Dictionary<,>)
                || definition == typeof(IDictionary<,>)
                || definition == typeof(HashSet<>)
                || definition == typeof(ISet<>);
        }

        private static bool IsMutable(System.Reflection.PropertyInfo property)
        {
            var setter = property.SetMethod;

            if (setter is null)
            {
                return false;
            }

            // An init-only setter is emitted as a normal setter carrying this required modifier,
            // which is the only way to tell the two apart through reflection.
            var isInitOnly = setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(static modifier => modifier == typeof(System.Runtime.CompilerServices.IsExternalInit));

            return !isInitOnly;
        }

        private static Type[] ContractTypes()
        {
            var types = SolutionAssemblies.Core
                .GetTypes()
                // StartsWith, not equality: the contracts namespace has sub-namespaces per role
                // (Scanning, Watching, Processing) and every one of them is under the same governance.
                // An exact match would let a DTO escape the rule simply by moving into a folder.
                .Where(static type =>
                    type.Namespace is { } ns
                    && ns.StartsWith(SolutionAssemblies.ContractsNamespace, StringComparison.Ordinal)
                    && type.IsClass
                    && !type.IsNested)
                .ToArray();

            types.Should().NotBeEmpty("because the contracts namespace holds the DTOs under governance");

            return types;
        }
    }
}
