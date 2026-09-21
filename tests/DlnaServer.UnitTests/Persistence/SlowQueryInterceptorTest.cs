using DlnaServer.Persistence;
using DlnaServer.Persistence.Interceptors;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// Guards the logger category the host filters on to route slow queries into their own log file.
    /// </summary>
    [TestFixture]
    internal sealed class SlowQueryInterceptorTest
    {
        /// <summary>
        /// The category is a literal because the interceptor is internal, so nothing but this test stops
        /// a namespace or type rename from silently emptying <c>logs/slowQuery.log</c> - the filter would
        /// match nothing and the entries would quietly fall back into the application log.
        /// </summary>
        [Test]
        public void SlowQueryLogCategory_MatchesTheInterceptorType()
        {
            // Arrange
            // Act
            var actual = typeof(SlowQueryInterceptor).FullName;

            // Assert
            actual.Should().Be(PersistenceServiceCollectionExtensions.SlowQueryLogCategory,
                "because the host filters the slow-query sink on this exact source context, and a "
                + "mismatch disables that log file without any error");
        }
    }
}
