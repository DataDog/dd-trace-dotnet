using System;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public sealed class IisFixture : IDisposable
    {
        // start handing out ports at 9500 and keep going up
        private static int _nextPort = 9500;

        private TestHelper.IISExpress _iisExpress;

        public int AgentPort { get; private set; }

        public int HttpPort { get; private set; }

        public void StartIis(TestHelper helper)
        {
            AgentPort = Interlocked.Increment(ref _nextPort);
            HttpPort = Interlocked.Increment(ref _nextPort);

            // start IIS Express and give it a few seconds to boot up
            _iisExpress = helper.StartIISExpress(AgentPort, HttpPort);
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        public void Dispose()
        {
            _iisExpress?.Dispose();
        }
    }
}
