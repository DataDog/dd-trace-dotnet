// <copyright file="WindowsNamedPipeLargePayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(WindowsNamedPipeLargePayloadTests))]
    public class WindowsNamedPipeLargePayloadTests : LargePayloadTestBase
    {
        public WindowsNamedPipeLargePayloadTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [SkippableFact]
        public void SubmitsTraces()
        {
            EnvironmentHelper.EnableWindowsNamedPipes();
            RunTest();
        }
    }
}
