using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AzureFunction.HttpContext
{
    /// <summary>
    /// HttpContext for duck typing
    /// </summary>
    public interface IHttpContext
    {
        /// <summary>
        /// Gets or sets a key/value collection that can be used to share data within the scope of this request.
        /// </summary>
        IDictionary<object, object> Items { get; set; }

        /// <summary>
        /// Gets the <see cref="T:Microsoft.AspNetCore.Http.HttpRequest" /> object for this request.
        /// </summary>
        IHttpRequest Request { get; }

        /// <summary>
        /// Gets a cancellation token that notifies when the connection underlying
        /// this request is aborted and thus request operations should be cancelled.
        /// </summary>
        CancellationToken RequestAborted { get; }

        /// <summary>
        /// Gets the <see cref="T:System.IServiceProvider" /> that provides access to the request's service container.
        /// </summary>
        IServiceProvider RequestServices { get; }

        /// <summary>
        /// Gets the <see cref="T:Microsoft.AspNetCore.Http.HttpResponse" /> object for this request.
        /// </summary>
        IHttpResponse Response { get; }

        /// <summary>
        /// Gets or sets the object used to manage user session data for this request.
        /// </summary>
        /// ISession Session { get; set; }

        /// <summary>
        /// Gets or sets a unique identifier to represent this request in trace logs.
        /// </summary>
        string TraceIdentifier { get; set; }
    }
}
