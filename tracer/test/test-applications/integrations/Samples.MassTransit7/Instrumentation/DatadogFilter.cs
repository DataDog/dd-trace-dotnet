using System.Threading.Tasks;
using GreenPipes;

namespace Samples.MassTransit7.Instrumentation;

/// <summary>
/// A custom MassTransit filter that intercepts the message pipeline.
/// This demonstrates how a filter can be injected to instrument message processing.
/// </summary>
/// <typeparam name="T">The context type (e.g., ConsumeContext, SendContext)</typeparam>
public class DatadogFilter<T> : IFilter<T>
    where T : class, PipeContext
{
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("datadog");
    }

    public async Task Send(T context, IPipe<T> next)
    {
        Console.WriteLine($"[DatadogFilter] BEFORE - Context type: {context.GetType().Name}");

        // Here you would:
        // 1. Create a span
        // 2. Extract/inject trace context from/to headers
        // 3. Set span tags

        try
        {
            await next.Send(context);
            Console.WriteLine($"[DatadogFilter] AFTER - Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatadogFilter] AFTER - Exception: {ex.Message}");
            throw;
        }
    }
}
