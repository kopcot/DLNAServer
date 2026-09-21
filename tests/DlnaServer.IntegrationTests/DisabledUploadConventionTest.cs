using System.Reflection;
using DlnaServer.Host.Controllers;
using DlnaServer.Host.Uploads;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the upload endpoint being absent, rather than merely guarded, when uploads are off.
    /// </summary>
    /// <remarks>
    /// The first version of this convention cleared the controller's selectors instead of removing it,
    /// which left an action carrying <c>[ApiController]</c> with no attribute route - and MVC refuses
    /// that at <c>MapControllers</c>, so the server did not start at all. Uploads ship off, so that was
    /// the default configuration. Nothing in the suite or the build said a word about it; it was found by
    /// starting the server with the setting off.
    /// </remarks>
    [TestFixture]
    internal sealed class DisabledUploadConventionTest
    {
        [Test]
        public void Apply_RemovesTheUploadControllerEntirely()
        {
            // Arrange
            var application = ApplicationWith(typeof(UploadController), typeof(ManageController));
            var convention = new DisabledUploadConvention();

            // Act
            convention.Apply(application);

            // Assert
            application.Controllers.Should().NotContain(
                c => c.ControllerType.AsType() == typeof(UploadController),
                "because a controller left in the model with no route is what stopped the server booting - "
                + "off has to mean the endpoint does not exist");
        }

        [Test]
        public void Apply_LeavesEveryOtherControllerAlone()
        {
            // Arrange
            var application = ApplicationWith(typeof(UploadController), typeof(ManageController));
            var convention = new DisabledUploadConvention();

            // Act
            convention.Apply(application);

            // Assert
            application.Controllers.Should().ContainSingle(
                "because exactly one controller is taken out")
                .Which.ControllerType.AsType().Should().Be(typeof(ManageController),
                    "because turning uploads off must not disturb the rest of the surface");
        }

        [Test]
        public void Apply_WithNoUploadController_DoesNothing()
        {
            // Arrange
            var application = ApplicationWith(typeof(ManageController));
            var convention = new DisabledUploadConvention();

            // Act
            var applying = () => convention.Apply(application);

            // Assert
            applying.Should().NotThrow(
                "because the convention runs over whatever model MVC built, and a missing controller is "
                + "not its problem to report");
        }

        private static ApplicationModel ApplicationWith(params Type[] controllerTypes)
        {
            var application = new ApplicationModel();

            foreach (var type in controllerTypes)
            {
                application.Controllers.Add(new ControllerModel(type.GetTypeInfo(), []));
            }

            return application;
        }
    }
}
