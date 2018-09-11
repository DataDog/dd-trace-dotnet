using System;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ProcessResult : IDisposable
    {
        public ProcessResult(
            Process process,
            string standardOutput,
            string standardError,
            int exitCode)
        {
            Process = process;
            StandardOutput = standardOutput;
            StandardError = standardError;
            ExitCode = exitCode;
        }

        public Process Process { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public int ExitCode { get; }

        public void Dispose()
        {
            Process?.Dispose();
        }
    }
}
