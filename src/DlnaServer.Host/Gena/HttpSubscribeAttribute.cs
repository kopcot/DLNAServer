using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DlnaServer.Host.Gena
{
    /// <summary>
    /// Routes the HTTP <c>SUBSCRIBE</c> method to an action.
    /// </summary>
    /// <remarks>
    /// GENA uses two methods ASP.NET Core knows nothing about, so they need declaring. Carried over from
    /// the reference, which needed the same thing for the same reason.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpSubscribeAttribute : HttpMethodAttribute
    {
        private static readonly string[] _supportedMethods = ["SUBSCRIBE"];

        public HttpSubscribeAttribute()
            : base(_supportedMethods)
        {
        }

        public HttpSubscribeAttribute([StringSyntax("Route")] string template)
            : base(_supportedMethods, template)
        {
            ArgumentNullException.ThrowIfNull(template);
        }
    }
}
