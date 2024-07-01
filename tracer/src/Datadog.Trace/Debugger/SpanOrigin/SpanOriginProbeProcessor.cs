#nullable enable
using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.SpanOrigin;

internal class SpanOriginProbeProcessor : ProbeProcessor
{
    internal SpanOriginProbeProcessor(ProbeDefinition probe)
        : base(probe)
    {
    }
}
