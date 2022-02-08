// <copyright file="MockSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Demos.CodeHotspotsPoC
{
    internal class MockSpan
    {
        public MockSpan(ulong traceId, ulong spanId)
        {
            this.SpanId = spanId;
            this.TraceId = traceId;
        }

        public ulong TraceId { get; }
        public ulong SpanId { get; }

        public override string ToString()
        {
            if (TraceId == 0 && SpanId == 0)
            {
                return $"{{ZeroSpan}}";
            }
            else
            {
                return $"{{TraceId={TraceId}; SpanId={SpanId}}}";
            }
        }
    }
}