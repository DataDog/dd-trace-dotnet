using System;

namespace Datadog.Trace.Util
{
    internal interface IClock
    {
        DateTime UtcNow { get; }
    }
}
