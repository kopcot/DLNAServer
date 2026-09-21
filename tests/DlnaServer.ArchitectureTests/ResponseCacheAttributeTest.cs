using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace DlnaServer.ArchitectureTests
{
    /// <summary>
    /// Guards the one <see cref="ResponseCacheAttribute"/> setting that turns into a runtime failure
    /// rather than a no-op.
    /// </summary>
    /// <remarks>
    /// Written after it happened. The response-caching middleware was removed on 2026-09-06 because it
    /// could never store anything - every attribute in this server uses
    /// <see cref="ResponseCacheLocation.Client"/>, which emits <c>Cache-Control: private</c>, and the
    /// middleware only caches <c>public</c>. What that reasoning missed is that one action also set
    /// <c>VaryByQueryKeys</c>, and MVC throws <c>InvalidOperationException: 'VaryByQueryKeys' requires
    /// the response cache middleware</c> when that filter executes without it.
    /// <para>
    /// The cost of missing it was the whole media port answering 500 while the admin port looked healthy,
    /// on a build that had passed 560 tests and a local run - because nothing in either had fetched
    /// <c>/media/description.xml</c>. A compile-time-shaped check is the only thing that catches this
    /// without an end-to-end HTTP test, which this solution deliberately does not have.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class ResponseCacheAttributeTest
    {
        [Test]
        public void ResponseCache_DoesNotUseVaryByQueryKeys_WhichNeedsMiddlewareThisServerDoesNotRun()
        {
            // Arrange
            var actions = SolutionAssemblies.Host
                .GetTypes()
                .Where(static type => typeof(ControllerBase).IsAssignableFrom(type))
                .SelectMany(static type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            // Act
            var offenders = actions
                .Select(static action => new
                {
                    Action = action,
                    Cache = action.GetCustomAttribute<ResponseCacheAttribute>(),
                })
                .Where(static entry => entry.Cache?.VaryByQueryKeys is { Length: > 0 })
                .Select(static entry =>
                    $"{entry.Action.DeclaringType?.Name}.{entry.Action.Name} sets VaryByQueryKeys")
                .ToList();

            // Assert
            offenders.Should().BeEmpty(
                "because VaryByQueryKeys only affects the server-side response cache, which this server "
                + "does not register - and MVC throws at request time rather than ignoring it, so an "
                + "endpoint that carries it returns 500 for every caller");
        }
    }
}
