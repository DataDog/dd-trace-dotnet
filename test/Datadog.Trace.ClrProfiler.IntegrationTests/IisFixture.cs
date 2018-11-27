using System;
using System.Diagnostics;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public sealed class IisFixture : IDisposable
    {
        // start handing out ports at 9500 and keep going up
        private static int _nextPort = 9500;

        private Process _iisExpress;

        public int AgentPort { get; private set; }

        public int HttpPort { get; private set; }

        public void TryStartIis(TestHelper helper)
        {
            lock (this)
            {
                if (_iisExpress == null)
                {
                    AgentPort = Interlocked.Increment(ref _nextPort);
                    HttpPort = Interlocked.Increment(ref _nextPort);

                    // start IIS Express and give it a few seconds to boot up
                    _iisExpress = helper.StartIISExpress(AgentPort, HttpPort);
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                }
            }
        }

        public void Dispose()
        {
            if (_iisExpress != null)
            {
                if (!_iisExpress.HasExited)
                {
                    // sending "Q" to standard input does not work because
                    // iisexpress is scanning console key press, so just kill it.
                    // maybe try this in the future:
                    // https://github.com/roryprimrose/Headless/blob/master/Headless.IntegrationTests/IisExpress.cs
                    _iisExpress.Kill();
                    _iisExpress.WaitForExit();
                }

                _iisExpress?.Dispose();
            }
        }
    }
}
