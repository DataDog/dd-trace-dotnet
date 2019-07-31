using System.Runtime.InteropServices;

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
        /// The value of <see cref="RuntimeInformation.FrameworkDescription"/>.
        /// </summary>
        public const string LanguageInterpreter = "Datadog-Meta-Lang-Interpreter";

        /// <summary>
        /// The interpreter version for the given language, e.g. "4.7.2" for .NET Framework or "2.1" for .NET Core.
        /// The value of <see cref="RuntimeInformation.FrameworkDescription"/>.
        /// </summary>
        public const string LanguageVersion = "Datadog-Meta-Lang-Version";

        /// <summary>
        /// The version of the tracer that generated this span.
        /// </summary>
        public const string TracerVersion = "Datadog-Meta-Tracer-Version";
    }
}
