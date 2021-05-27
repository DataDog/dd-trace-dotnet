// <copyright file="AspNetCore5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5 : AspNetCoreBase, IDisposable
    {
        public AspNetCore5(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper)
        {
        }

        [Theory]
        [InlineData(true, HttpStatusCode.Forbidden)]
        [InlineData(false, HttpStatusCode.OK)]
        public async Task TestBlockedRequestAsync(bool enableSecurity, HttpStatusCode expectedStatusCode)
        {
            Environment.SetEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", "1");
            Environment.SetEnvironmentVariable("DD_ENABLE_SECURITY", enableSecurity.ToString());
            Environment.SetEnvironmentVariable("DD_VERSION", "1.0.0");
            Environment.SetEnvironmentVariable("DD_TRACE_HEADER_TAGS", "sample.correlation.identifier, Server");
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");
            Environment.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", "$(ProjectDir)$(OutputPath)profiler-lib");
            Environment.SetEnvironmentVariable("DD_INTEGRATIONS", "$(ProjectDir)$(OutputPath)profiler-lib\\integrations.json");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$(ProjectDir)$(OutputPath)profiler-lib\\Datadog.Trace.ClrProfiler.Native.dll");
            await RunOnSelfHosted();
            var (statusCode, _) = await SubmitRequest("/Home?arg=database()");
            Assert.Equal(expectedStatusCode, statusCode);
        }
    }
}
#endif
