using DlnaServer.Host.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace DlnaServer.Host.Uploads
{
    /// <summary>
    /// Takes the upload endpoint out of the application entirely when uploading is switched off.
    /// </summary>
    /// <remarks>
    /// Applied once, at startup, from the value <c>config.json</c> held then - which is what makes
    /// <c>Dlna.Upload.Enabled</c> a setting that needs a restart rather than one the endpoint re-reads. A
    /// server with uploads off has no route that accepts a file, so there is nothing to get wrong later:
    /// no flag to forget to check, and nothing for a future change to leave half-guarded.
    /// <para>
    /// <b>The controller is removed, not silenced.</b> Clearing its selectors instead left an action with
    /// <c>[ApiController]</c> and no attribute route, and MVC refuses that outright:
    /// <c>MapControllers</c> threw "has ApiExplorer enabled, but is using conventional routing" and the
    /// server did not start - in the DEFAULT configuration, since uploads ship off. The build was clean
    /// and every test passed; only running it with the setting off found this.
    /// </para>
    /// </remarks>
    internal sealed class DisabledUploadConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            ArgumentNullException.ThrowIfNull(application);

            var upload = application.Controllers
                .FirstOrDefault(static c => c.ControllerType.AsType() == typeof(UploadController));

            if (upload is not null)
            {
                _ = application.Controllers.Remove(upload);
            }
        }
    }
}
