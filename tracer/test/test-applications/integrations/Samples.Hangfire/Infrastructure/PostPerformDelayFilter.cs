using System;
using System.Threading;
using Hangfire.Server;

namespace Samples.Hangfire.Infrastructure;

/// <summary>A server filter whose OnPerformed takes measurable time, so tests can check it runs inside the job span.</summary>
public sealed class PostPerformDelayFilter : IServerFilter
{
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);

    public void OnPerforming(PerformingContext context)
    {
    }

    public void OnPerformed(PerformedContext context) => Thread.Sleep(Delay);
}
