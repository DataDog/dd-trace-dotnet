using System.Diagnostics;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LoadTests
{
    public class LoadTestPart
    {
        public string Application { get; set; }

        public int? Port { get; set; }

        public bool IsAnchor { get; set; }

        public bool TimeToSetSail { get; set; }

        public string[] CommandLineArgs { get; set; }

        public EnvironmentHelper EnvironmentHelper { get; set; }

        public MockTracerAgent Agent { get; set; }

        public Process Process { get; set; }

        public ProcessResult ProcessResult { get; set; }
    }
}
