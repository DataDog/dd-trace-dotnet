using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    // a single fixture instance is shared among all tests in a class,
    // and they never run in parallel
    public sealed class IisExpressFixture : IDisposable
    {
        // start handing out ports at 9500 and keep going up
        private static int _nextPort = 9500;

        private IisExpress _iisExpress;

        public int AgentPort { get; private set; }

        public int HttpPort { get; private set; }

        public ITestOutputHelper Output { get; set; }

        public bool IsRunning => _iisExpress?.IsRunning ?? false;

        public void StartIis(string sampleAppName)
        {
            if (sampleAppName == null) { throw new ArgumentNullException(nameof(sampleAppName)); }

            if (_iisExpress != null)
            {
                _iisExpress.Stop();
                _iisExpress.Dispose();
            }

            AgentPort = Interlocked.Increment(ref _nextPort);
            HttpPort = Interlocked.Increment(ref _nextPort);

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            IDictionary<string, string> environmentVariables = ProfilerHelper.GetProfilerEnvironmentVariables(
                Instrumentation.ProfilerClsid,
                TestHelper.GetProfilerDllPath(),
                integrationPaths,
                AgentPort);

            _iisExpress = new IisExpress();

            _iisExpress.Message += (sender, e) =>
            {
                if (Output != null && !string.IsNullOrEmpty(e.Data))
                {
                    Output.WriteLine($"[webserver] {e.Data}");
                }
            };

            _iisExpress.OutputDataReceived += (sender, e) =>
            {
                if (Output != null && !string.IsNullOrEmpty(e.Data))
                {
                    Output.WriteLine($"[webserver][stdout] {e.Data}");
                }
            };

            _iisExpress.ErrorDataReceived += (sender, e) =>
            {
                if (Output != null && !string.IsNullOrEmpty(e.Data))
                {
                    Output.WriteLine($"[webserver][stderr] {e.Data}");
                }
            };

            var sampleAppDirectory = Path.Combine(TestHelper.GetSolutionDirectory(), "samples", $"Samples.{sampleAppName}");
            _iisExpress.Start(sampleAppDirectory, Environment.Is64BitProcess, HttpPort, environmentVariables);
        }

        // called after all test methods in a class are finished
        public void Dispose()
        {
            // disconnect the output after all tests are done
            // since it can't be used outside of the context of a test
            Output = null;

            if (_iisExpress != null)
            {
                _iisExpress.Stop();
                _iisExpress.Dispose();
            }
        }
    }
}
