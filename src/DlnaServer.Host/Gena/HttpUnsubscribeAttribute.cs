using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DlnaServer.Host.Gena
{
    /// <summary>
    /// Routes the HTTP <c>UNSUBSCRIBE</c> method to an action.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpUnsubscribeAttribute : HttpMethodAttribute
    {
        private static readonly string[] _supportedMethods = ["UNSUBSCRIBE"];

        public HttpUnsubscribeAttribute()
            : base(_supportedMethods)
        {
        }

        public HttpUnsubscribeAttribute([StringSyntax("Route")] string template)
            : base(_supportedMethods, template)
        {
            ArgumentNullException.ThrowIfNull(template);
        }
    }
}
