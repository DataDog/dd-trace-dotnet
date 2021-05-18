#if NET5_0
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5 : AspNetCoreBase
    {
        public AspNetCore5(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper)
        {
        }

        [Fact]
        public async Task TestBlockedRequestAsync()
        {
            Environment.SetEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", "1");
            Environment.SetEnvironmentVariable("DD_VERSION", "1.0.0");
            Environment.SetEnvironmentVariable("DD_TRACE_HEADER_TAGS", "sample.correlation.identifier, Server");
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");
            Environment.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", "$(ProjectDir)$(OutputPath)profiler-lib");
            Environment.SetEnvironmentVariable("DD_INTEGRATIONS", "$(ProjectDir)$(OutputPath)profiler-lib\\integrations.json");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$(ProjectDir)$(OutputPath)profiler-lib\\Datadog.Trace.ClrProfiler.Native.dll");
            using var process = await RunTraceTestOnSelfHosted("/Home");
            var (statusCode, _) = await SubmitRequest("/Home?arg=database()");
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, statusCode);
            process.Kill();
        }
    }
}
#endif
