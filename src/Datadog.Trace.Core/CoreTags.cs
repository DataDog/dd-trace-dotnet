using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Standard span tags used by integrations.
    /// </summary>
    public static class CoreTags
    {
        /// <summary>
        /// The environment of the profiled service.
        /// </summary>
        public const string Env = "env";

        /// <summary>
        /// The name of the integration that generated the span.
        /// Use OpenTracing tag "component"
        /// </summary>
        public const string InstrumentationName = "component";

        /// <summary>
        /// The name of the method that was instrumented to generate the span.
        /// </summary>
        public const string InstrumentedMethod = "instrumented.method";

        /// <summary>
        /// The kind of span (e.g. client, server). Not to be confused with <see cref="Span.Type"/>.
        /// </summary>
        public const string SpanKind = "span.kind";

        /// <summary>
        /// The error message of an exception
        /// </summary>
        public const string ErrorMsg = "error.msg";

        /// <summary>
        /// The type of an exception
        /// </summary>
        public const string ErrorType = "error.type";

        /// <summary>
        /// The stack trace of an exception
        /// </summary>
        public const string ErrorStack = "error.stack";

        /// <summary>
        /// Language tag, applied to root spans that are .NET runtime (e.g., ASP.NET)
        /// </summary>
        public const string Language = "language";

        /// <summary>
        /// Obsolete. Use <see cref="ManualKeep"/>.
        /// </summary>
        [Obsolete("This field will be removed in futures versions of this library. Use ManualKeep instead.")]
        public const string ForceKeep = "force.keep";

        /// <summary>
        /// Obsolete. Use <see cref="ManualDrop"/>.
        /// </summary>
        [Obsolete("This field will be removed in futures versions of this library. Use ManualDrop instead.")]
        public const string ForceDrop = "force.drop";

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserKeep"/>.
        /// </summary>
        public const string ManualKeep = "manual.keep";

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserReject"/>.
        /// </summary>
        public const string ManualDrop = "manual.drop";

        /// <summary>
        /// Configures Trace Analytics.
        /// </summary>
        public const string Analytics = "_dd1.sr.eausr";

        /// <summary>
        /// The sampling priority for the entire trace.
        /// </summary>
        public const string SamplingPriority = "sampling.priority";
    }
}
