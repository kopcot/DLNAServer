using System.Reflection;
using DlnaServer.Core.Configuration;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Pins two lists that are maintained by hand and whose omissions are silent.
    /// </summary>
    /// <remarks>
    /// Both have already failed in production once. Reflection makes them cost nothing to keep, which is
    /// the same argument <c>ConventionTest</c> makes for the rules it enforces.
    /// </remarks>
    [TestFixture]
    internal sealed class DlnaOptionsValidatorCoverageTest
    {
        /// <summary>
        /// Every options section is reached by validation.
        /// </summary>
        /// <remarks>
        /// <c>Validate</c> names its sections one at a time, and <c>Database</c> and
        /// <c>Compatibility</c> were both missing - which made every <c>[Range]</c> on them dead. A
        /// <c>MaxBrowseRequestedCount</c> of -1 then reached <c>Paginate</c> and threw out of
        /// <c>GetRange</c> on every Browse from every renderer, permanently, with the library simply
        /// gone. Nothing failed the build and nothing failed a test.
        /// <para>
        /// Asserted by behaviour rather than by reading the method: each section is given a value its
        /// own annotations forbid, and validation has to object. A section nobody validates produces no
        /// failure, which is exactly the hole.
        /// </para>
        /// </remarks>
        [Test]
        public void Validate_ObjectsToAnOutOfRangeValueInEverySection()
        {
            // Arrange
            var validator = new DlnaOptionsValidator();

            var sections = typeof(DlnaOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static property => property.PropertyType.Namespace == typeof(ServerOptions).Namespace)
                .ToArray();

            sections.Should().NotBeEmpty("because the reflection above has to find the sections at all");

            var unvalidated = new List<string>();

            foreach (var section in sections)
            {
                // Act - one violation at a time, so a failure names the section that went unnoticed.
                var options = new DlnaOptions();
                var target = section.GetValue(options)!;

                if (!TrySetOutOfRange(target))
                {
                    continue;
                }

                var result = validator.Validate(name: null, options);

                if (!result.Failed)
                {
                    unvalidated.Add(section.Name);
                }
            }

            // Assert
            unvalidated.Should().BeEmpty(
                "because a section that no ValidateAnnotations call names has every one of its own "
                + "[Range] and [Required] attributes silently ignored");
        }

        /// <summary>
        /// The middleware's media-only list and the constants the endpoints are mapped from agree.
        /// </summary>
        /// <remarks>
        /// The four SOAP paths are registered through SoapCore's <c>UseSoapEndpoint</c>, which endpoint
        /// filters structurally cannot reach - so this middleware is the only thing keeping the whole
        /// ContentDirectory API off the admin port. It held those paths as its own string literals, so
        /// renaming a constant would have moved the endpoint and left the guard watching the old path,
        /// silently, in the one direction nothing else was guarding.
        /// </remarks>
        [Test]
        public void AdminSurfaceMiddleware_GuardsEverySoapControlPath()
        {
            // Arrange
            var mapped = typeof(UpnpServices.ControlPath)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field is { IsLiteral: true, IsInitOnly: false })
                .Select(static field => (string)field.GetRawConstantValue()!)
                .ToArray();

            // Act
            var guarded = AdminSurfaceMiddleware.MediaOnlyPaths;

            // Assert
            mapped.Should().NotBeEmpty("because the constants have to be found for this to prove anything");
            guarded.Should().BeEquivalentTo(mapped,
                "because a control path the endpoints are mapped from but the middleware does not know "
                + "about is reachable on the admin port, and one the middleware knows about but nothing "
                + "maps is a guard watching a path that no longer exists");
        }

        /// <summary>
        /// Sets one property of a section outside what its own annotations allow.
        /// </summary>
        private static bool TrySetOutOfRange(object section)
        {
            foreach (var property in section.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>()
                    is not { } range
                    || property.PropertyType != typeof(int)
                    || !property.CanWrite)
                {
                    continue;
                }

                property.SetValue(section, (int)range.Minimum - 1);

                return true;
            }

            return false;
        }
    }
}
