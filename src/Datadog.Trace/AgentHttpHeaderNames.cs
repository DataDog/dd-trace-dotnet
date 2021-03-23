using System.Collections.Generic;

namespace Datadog.Trace
{
    /// <summary>
    /// Names of HTTP headers that can be used when sending traces to the Trace Agent.
    /// </summary>
    internal static class AgentHttpHeaderNames
    {
        /// <summary>
        /// The language-specific tracer that generated this span.
        /// Always ".NET" for the .NET Tracer.
        /// </summary>
        public const string Language = "Datadog-Meta-Lang";

        /// <summary>
        /// The interpreter for the given language, e.g. ".NET Framework" or ".NET Core".
        /// </summary>
        public const string LanguageInterpreter = "Datadog-Meta-Lang-Interpreter";

        /// <summary>
        /// The interpreter version for the given language, e.g. "4.7.2" for .NET Framework or "2.1" for .NET Core.
        /// </summary>
        public const string LanguageVersion = "Datadog-Meta-Lang-Version";

        /// <summary>
        /// The version of the tracer that generated this span.
        /// </summary>
        public const string TracerVersion = "Datadog-Meta-Tracer-Version";

        /// <summary>
        /// The number of unique traces per request.
        /// </summary>
        public const string TraceCount = "X-Datadog-Trace-Count";

        /// <summary>
        /// The id of the container where the traced application is running.
        /// </summary>
        public const string ContainerId = "Datadog-Container-ID";

        /// <summary>
        /// Gets the default constant header that should be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] DefaultHeaders { get; } =
        {
            new(Language, ".NET"),
            new(TracerVersion, TracerConstants.AssemblyVersion),
            new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
            new(LanguageInterpreter, FrameworkDescription.Instance.Name),
            new(LanguageVersion, FrameworkDescription.Instance.ProductVersion)
        };
    }
}
