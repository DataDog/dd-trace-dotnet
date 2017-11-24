namespace Datadog.Trace
{
    internal static class Constants
    {
        public const string UnkownService = "UnkownService";
        public const string UnkownApp = "UnkownApp";
        public const string WebAppType = "web";
        // TODO:bertrand expose these publicly?
        public const string HttpHeaderTraceId = "x-datadog-trace-id";
        public const string HttpHeaderParentId = "x-datadog-parent-id";
    }
}
