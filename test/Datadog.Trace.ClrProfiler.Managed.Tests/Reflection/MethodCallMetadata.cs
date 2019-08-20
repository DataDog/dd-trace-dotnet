namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class MethodCallMetadata
    {
        public string MethodString { get; set; }

        public int MetadataToken { get; set; }

        public object[] Parameters { get; set; }
    }
}
