using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// RoutePattern for duck typing
    /// </summary>
    [DuckCopy]
    public struct RoutePattern
    {
        /// <summary>
        /// Gets the list of IReadOnlyList&lt;RoutePatternPathSegment&gt;
        /// </summary>
        public IEnumerable PathSegments;

        /// <summary>
        /// Gets the RoutePattern.RawText
        /// </summary>
        public string RawText;
    }
}
