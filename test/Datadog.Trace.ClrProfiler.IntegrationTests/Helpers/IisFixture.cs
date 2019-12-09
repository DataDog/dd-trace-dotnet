using System;
using System.Diagnostics;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public sealed class IISFixture
    {
        private readonly object agentLock = new object();

        public MockTracerAgent Agent { get; private set; }

        public void TryConnectIIS()
        {
            lock (agentLock)
            {
                Agent = new MockTracerAgent();
            }
        }
    }
}
