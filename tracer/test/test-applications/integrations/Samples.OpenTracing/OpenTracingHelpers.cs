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

    public static Datadog.Trace.ISpan GetDdTraceSpan(this OpenTracing.ISpan span)
        => (Datadog.Trace.ISpan)GetSpanMethod.Invoke(span, Array.Empty<object>());

    public static Datadog.Trace.ISpanContext GetDdTraceSpanContext(this OpenTracing.ISpan span)
        => (Datadog.Trace.ISpanContext)GetSpanContextMethod.Invoke(span.Context, Array.Empty<object>());
}
