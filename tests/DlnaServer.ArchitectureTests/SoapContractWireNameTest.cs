using System.Reflection;
using System.Xml.Serialization;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Every SOAP response contract must pin its own wire name, so its CLR namespace is never part of
    /// the contract.
    /// </summary>
    /// <remarks>
    /// This is the rule that made splitting <c>DlnaServer.Upnp.Soap</c> into one namespace per service a
    /// safe, purely mechanical move: with an explicit element name on every type, moving the file changes
    /// no emitted XML. Without it, a renderer would see a different element after a refactor and reject
    /// the response - a failure that presents as a television showing an empty library, not as a build
    /// error. A new contract that forgets the attribute re-opens that trap, so it fails here instead.
    /// </remarks>
    [TestFixture]
    internal sealed class SoapContractWireNameTest
    {
        private const string SoapNamespace = "DlnaServer.Upnp.Soap";
        private const string ResponseSuffix = "Response";

        [Test]
        public void EverySoapResponse_DeclaresAnExplicitWireName()
        {
            // Arrange
            var contracts = ResponseContracts();

            // Act
            var unnamed = contracts
                .Where(static type =>
                    type.GetCustomAttribute<XmlRootAttribute>() is null
                    && type.GetCustomAttribute<XmlTypeAttribute>() is null)
                .Select(static type => type.FullName)
                .ToArray();

            // Assert
            unnamed.Should().BeEmpty(
                "because a contract without an explicit element name takes its wire name from the CLR "
                + "type and namespace, which makes a refactor a protocol change: "
                + string.Join(", ", unnamed));
        }

        private static Type[] ResponseContracts()
        {
            var types = SolutionAssemblies.Upnp
                .GetTypes()
                .Where(static type =>
                    type.Namespace is { } ns
                    && ns.StartsWith(SoapNamespace, StringComparison.Ordinal)
                    && type.IsClass
                    && !type.IsNested
                    && type.Name.EndsWith(ResponseSuffix, StringComparison.Ordinal))
                .ToArray();

            types.Should().NotBeEmpty(
                "because the SOAP namespace holds the response contracts under governance - an empty "
                + "set would mean this rule silently stopped checking anything");

            return types;
        }
    }
}
