using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Helper struct for retrieving IEndpointFeature from HttpContext
    /// </summary>
    public interface IEndpointFeatureStruct
    {
        /// <summary>
        /// Delegates to IEndpointFeature.Endpoint;
        /// </summary>
        [Duck(Name = "Microsoft.AspNetCore.Http.Features.IEndpointFeature.get_Endpoint")]
        object GetEndpoint();
    }
}
