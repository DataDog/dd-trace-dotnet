using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Endpoint for duck typing
    /// </summary>
    [DuckCopy]
    public struct RouteEndpoint
    {
        /// <summary>
        /// Delegates to Endpoint.RoutePattern;
        /// </summary>
        public RoutePattern RoutePattern;

        /// <summary>
        /// Delegates to Endpoint.DisplayName;
        /// </summary>
        public string DisplayName;
    }
}
