using Microsoft.AspNetCore.Mvc.Routing;
using System.Diagnostics.CodeAnalysis;

namespace DLNAServer.Helpers.Attributes
{

    /// <summary>
    /// Identifies an action that supports the HTTP SUBSCRIBE method.
    /// </summary>
    public class HttpSubscribeAttribute : HttpMethodAttribute
    {
        private static readonly IEnumerable<string> _supportedMethods = ["SUBSCRIBE"];

        /// <summary>
        /// Creates a new <see cref="HttpSubscribeAttribute"/>.
        /// </summary>
        public HttpSubscribeAttribute()
            : base(_supportedMethods)
        {
        }

        /// <summary>
        /// Creates a new <see cref="HttpSubscribeAttribute"/> with the given route template.
        /// </summary>
        /// <param name="template">The route template. May not be null.</param>
        public HttpSubscribeAttribute([StringSyntax("Route")] string template)
            : base(_supportedMethods, template)
        {
            ArgumentNullException.ThrowIfNull(template);
        }
    }
    /// <summary>
    /// Identifies an action that supports the HTTP UNSUBSCRIBE method.
    /// </summary>
    public class HttpUnsubscribeAttribute : HttpMethodAttribute
    {
        private static readonly IEnumerable<string> _supportedMethods = ["UNSUBSCRIBE"];

        /// <summary>
        /// Creates a new <see cref="HttpUnsubscribeAttribute"/>.
        /// </summary>
        public HttpUnsubscribeAttribute()
            : base(_supportedMethods)
        {
        }

        /// <summary>
        /// Creates a new <see cref="HttpUnsubscribeAttribute"/> with the given route template.
        /// </summary>
        /// <param name="template">The route template. May not be null.</param>
        public HttpUnsubscribeAttribute([StringSyntax("Route")] string template)
            : base(_supportedMethods, template)
        {
            ArgumentNullException.ThrowIfNull(template);
        }
    }
}
