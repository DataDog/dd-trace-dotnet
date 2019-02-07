namespace Datadog.Trace
{
    internal static class HttpHeaderNames
    {
        public const string Language = "Datadog-Meta-Lang";
        public const string LanguageInterpreter = "Datadog-Meta-Lang-Interpreter";
        public const string TracerVersion = "Datadog-Meta-Tracer-Version";

        public const string TraceId = "x-datadog-trace-id";
        public const string ParentId = "x-datadog-parent-id";
        public const string SamplingPriority = "x-datadog-sampling-priority";
        public const string TracingDisabled = "x-datadog-tracing-disabled";
    }
}
