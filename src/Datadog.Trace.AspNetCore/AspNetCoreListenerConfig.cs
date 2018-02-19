namespace Datadog.Trace.AspNetCore
{
    internal class AspNetCoreListenerConfig
    {
        public string ServiceName { get; set; }

        public bool EnableDistributedTracing { get; set; }
    }
}
