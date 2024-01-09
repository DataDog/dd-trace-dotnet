using Datadog.Trace.OpenTracing;
using System;
using ITracer = OpenTracing.ITracer;

if (args.Length == 0)
{
    throw new InvalidOperationException("Must provide an argument to define the scenario");
}

var scenario = args[0];

var scenarioToRun = scenario switch
{
    _ => throw new InvalidOperationException("Unknown scenario: " + scenario),
};

await scenarioToRun;

return 0;

ITracer GetWrappedTracer()
{
#pragma warning disable CS0618 // Type or member is obsolete
    return OpenTracingTracerFactory.WrapTracer(Datadog.Trace.Tracer.Instance);
#pragma warning restore CS0618
}

ITracer CreateTracer(string defaultServiceName)
{
#pragma warning disable CS0618 // Type or member is obsolete
    return OpenTracingTracerFactory.CreateTracer(defaultServiceName: defaultServiceName);
#pragma warning restore CS0618
}

