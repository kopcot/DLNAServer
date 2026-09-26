using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Enforces the convention CLAUDE.md states but had no test for: every protocol DTO property documents
    /// its wire name, in bold, next to the attribute that carries it.
    /// </summary>
    /// <remarks>
    /// The convention exists so a reader never has to cross-reference the UPnP spec to learn what a
    /// property becomes on the wire - see the <c>BrowseItem.cs</c>-derived example in <c>CLAUDE.md</c>.
    /// Checked against the compiled XML documentation file rather than the source text, so a doc comment
    /// that fails to compile - a stray <c>&lt;</c>, an unresolved <c>&lt;see cref=&quot;...&quot;/&gt;</c> -
    /// is caught here as a missing summary rather than passing on a technicality.
    /// </remarks>
    [TestFixture]
    internal sealed class WireContractDocumentationTest
    {
        [Test]
        public void EveryWireProperty_DocumentsItsWireNameInBold()
        {
            // Arrange
            var assembly = SolutionAssemblies.Upnp;
            var documentationPath = Path.ChangeExtension(assembly.Location, ".xml");

            File.Exists(documentationPath).Should().BeTrue(
                $"because {nameof(SolutionAssemblies.Upnp)} must build with GenerateDocumentationFile "
                + $"on for this rule to check anything - expected {documentationPath}");

            var summariesByMemberId = LoadSummariesByMemberId(documentationPath);

            var wireProperties = assembly
                .GetTypes()
                .Where(static type => type.IsClass && !type.IsNested)
                .SelectMany(static type => type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(static property =>
                    property.GetCustomAttribute<XmlElementAttribute>() is not null
                    || property.GetCustomAttribute<XmlAttributeAttribute>() is not null
                    || property.GetCustomAttribute<XmlTextAttribute>() is not null)
                .ToList();

            // Act
            var offenders = wireProperties
                .Where(property =>
                {
                    var memberId = $"P:{property.DeclaringType!.FullName!.Replace('+', '.')}.{property.Name}";
                    return !summariesByMemberId.TryGetValue(memberId, out var summary)
                        || !summary.Contains("<b>", StringComparison.Ordinal);
                })
                .Select(property => $"{property.DeclaringType!.FullName}.{property.Name}")
                .ToArray();

            // Assert
            wireProperties.Should().NotBeEmpty(
                "because a rule that finds no properties to check passes while proving nothing");
            offenders.Should().BeEmpty(
                "because every property serialised onto the wire documents its element or attribute "
                + "name in bold, so a reader never has to cross-reference the UPnP spec to learn what it "
                + "becomes on the wire: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// Reads the compiled XML documentation file into a member-id -> raw summary inner-XML map.
        /// </summary>
        private static Dictionary<string, string> LoadSummariesByMemberId(string documentationPath)
        {
            var document = XDocument.Load(documentationPath);

            return document
                .Descendants("member")
                .Where(static member => member.Attribute("name") is not null)
                .ToDictionary(
                    static member => member.Attribute("name")!.Value,
                    static member => member.Element("summary")?.ToString() ?? string.Empty);
        }
    }
}
