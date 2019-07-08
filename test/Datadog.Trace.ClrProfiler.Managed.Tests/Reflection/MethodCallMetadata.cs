namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class MethodCallMetadata
    {
        public int MetadataToken { get; set; }

        public object[] Parameters { get; set; }
    }
}