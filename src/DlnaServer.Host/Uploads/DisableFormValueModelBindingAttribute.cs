using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DlnaServer.Host.Uploads
{
    /// <summary>
    /// Stops MVC reading the whole form before the action sees the request.
    /// </summary>
    /// <remarks>
    /// <b>Without this an upload cannot stream, and the failure is not a warning.</b> MVC's form value
    /// providers call <c>ReadFormAsync</c> for <i>any</i> request with a form content type, whether or
    /// not the action binds anything from it - so the body was already consumed and buffered by the time
    /// the action ran, and <c>MultipartReader</c> threw "Unexpected end of Stream, the content may have
    /// already been read by another component". Found by posting a file to the running server; the build
    /// and every test were clean.
    /// <para>
    /// Buffering is not just slower here, it is wrong: <c>ReadFormAsync</c> spools each file to a
    /// temporary file first, so a film would be written to disc twice and would meet the 128 MB
    /// multipart limit on the way.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var factories = context.ValueProviderFactories;

            factories.RemoveType<FormValueProviderFactory>();
            factories.RemoveType<FormFileValueProviderFactory>();
            factories.RemoveType<JQueryFormValueProviderFactory>();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
            // Nothing to undo: the factory list is per request.
        }
    }
}
