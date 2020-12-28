using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tests
{
    internal class SimpleClock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }
}
