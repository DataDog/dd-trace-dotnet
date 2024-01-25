// <copyright file="WindowsNamedPipeLargePayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("LargePayloadTests")]
    public class WindowsNamedPipeLargePayloadTests : LargePayloadTestBase
    {
        public WindowsNamedPipeLargePayloadTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// To be enabled when Windows Named Pipes is available in the MockTracerAgent
        /// </summary>
        [SkippableFact(Skip = "Windows named pipes are not yet supported in the MockTracerAgent")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        public async Task SubmitsTraces()
        {
            EnvironmentHelper.EnableWindowsNamedPipes();
            await RunTest();
        }
    }
}
