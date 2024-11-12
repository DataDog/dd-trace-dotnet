using System;
using System.Reflection;
using Datadog.Trace.OpenTracing;

/// <summary>
/// Helper methods for testing the OpenTracing library
/// </summary>
public static class OpenTracingHelpers
{
    private static Type OpenTracingSpanType = typeof(OpenTracingTracerFactory).Assembly.GetType("Datadog.Trace.OpenTracing.OpenTracingSpan");
    private static MethodInfo GetSpanMethod = OpenTracingSpanType.GetProperty("Span", BindingFlags.Instance | BindingFlags.NonPublic).GetMethod;

    private static Type OpenTracingSpanContextType = typeof(OpenTracingTracerFactory).Assembly.GetType("Datadog.Trace.OpenTracing.OpenTracingSpanContext");
    private static MethodInfo GetSpanContextMethod = OpenTracingSpanContextType.GetProperty("Context", BindingFlags.Instance | BindingFlags.NonPublic).GetMethod;
    private static ConstructorInfo OpenTracingSpanContextConstructor = OpenTracingSpanContextType.GetConstructors(BindingFlags.Instance | BindingFlags.Public)[0];

    private static Type OpenTracingSpanBuilderType = typeof(OpenTracingTracerFactory).Assembly.GetType("Datadog.Trace.OpenTracing.OpenTracingSpanBuilder");
    private static ConstructorInfo OpenTracingSpanBuilderConstructor = OpenTracingSpanBuilderType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];

    public static Datadog.Trace.ISpan GetDdTraceCustomSpan(this OpenTracing.ISpan span)
        => (Datadog.Trace.ISpan)GetSpanMethod.Invoke(span, Array.Empty<object>());

    public static Datadog.Trace.ISpanContext GetDdTraceCustomSpanContext(this OpenTracing.ISpan span)
        => (Datadog.Trace.ISpanContext)GetSpanContextMethod.Invoke(span.Context, Array.Empty<object>());

    public static Datadog.Trace.ISpanContext GetDdTraceCustomSpanContext(this OpenTracing.ISpanContext context)
        => (Datadog.Trace.ISpanContext)GetSpanContextMethod.Invoke(context, Array.Empty<object>());

    public static OpenTracing.ISpanBuilder CreateOpenTracingSpanBuilder(OpenTracing.ITracer tracer, string operationName)
        => (OpenTracing.ISpanBuilder)OpenTracingSpanBuilderConstructor.Invoke([tracer, operationName]);

    public static OpenTracing.ISpanContext CreateOpenTracingSpanContext(Datadog.Trace.ISpanContext spanContext)
        => (OpenTracing.ISpanContext)OpenTracingSpanContextConstructor.Invoke([spanContext]);

    public static ulong? GetParentId(this Datadog.Trace.ISpanContext spanContext)
    {
        // spanContext will be a duck-typed Datadog.Trace SpanContext
        var autoSpanContext = GetDuckTypedInstance(spanContext);
        var parentIdMethod = autoSpanContext
                            .GetType()
                            .GetProperty("ParentId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .GetMethod;

        return (ulong?)parentIdMethod.Invoke(autoSpanContext, []);
    }

    public static object GetTraceContext(this Datadog.Trace.ISpanContext spanContext)
    {
        // spanContext will be a duck-typed Datadog.Trace SpanContext
        var autoSpanContext = GetDuckTypedInstance(spanContext);
        var traceContext = autoSpanContext
                            .GetType()
                            .GetProperty("TraceContext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .GetMethod;

        return traceContext.Invoke(autoSpanContext, []);
    }

    public static (TimeSpan Duration, DateTimeOffset StartTime) GetInternalProperties(this Datadog.Trace.ISpan span)
    {
        // span will be a duck-typed Datadog.Trace Span
        var autoSpan = GetDuckTypedInstance(span);
        var durationMethod = autoSpan
                            .GetType()
                            .GetProperty("Duration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .GetMethod;

        var startTimeMethod = autoSpan
                            .GetType()
                            .GetProperty("StartTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .GetMethod;

        var duration = (TimeSpan)durationMethod.Invoke(autoSpan, []);
        var startTime = (DateTimeOffset)startTimeMethod.Invoke(autoSpan, []);
        return (Duration: duration, StartTime: startTime);
    }

    private static object GetDuckTypedInstance(object duckType)
    {
        // span will be a duck-typed SpanContext
        var autoContextMethod = duckType
                               .GetType()
                               .GetProperty("Instance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               .GetMethod;
        var originalInstance = autoContextMethod.Invoke(duckType, []);
        return originalInstance;
    }
}
