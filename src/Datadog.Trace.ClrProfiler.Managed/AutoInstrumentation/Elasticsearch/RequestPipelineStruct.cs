#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch
{
    /// <summary>
    /// Duck-copy struct for RequestPipeline
    /// </summary>
    [DuckCopy]
    public struct RequestPipelineStruct
    {
        public object RequestParameters;
    }
}
