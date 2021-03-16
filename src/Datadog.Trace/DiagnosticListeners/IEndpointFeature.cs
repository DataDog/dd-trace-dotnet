using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// IEndpointFeature for ducktyping HttpContext
    /// </summary>
    public interface IEndpointFeature
    {
        /// <summary>
        /// Delegates to IEndpointFeature.Endpoint;
        /// </summary>
        [Duck(Name = "Microsoft.AspNetCore.Http.Features.IEndpointFeature.get_Endpoint")]
        RouteEndpoint GetEndpoint();
    }
}
