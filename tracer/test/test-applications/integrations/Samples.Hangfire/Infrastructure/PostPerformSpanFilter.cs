using System.Diagnostics;
using Hangfire.Server;

namespace Samples.Hangfire.Infrastructure;

/// <summary>Opens a span in OnPerformed, so tests can check that callback runs inside the job span.</summary>
public sealed class PostPerformSpanFilter : IServerFilter
{
    private static readonly ActivitySource Source = new("Samples.Hangfire.Filters");

    public void OnPerforming(PerformingContext context)
    {
    }

    public void OnPerformed(PerformedContext context)
    {
        using var activity = Source.StartActivity("PostPerform");
    }
}
