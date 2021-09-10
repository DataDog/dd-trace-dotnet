// <copyright file="NoMultiLoaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.IO;
using System.Linq;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NoMultiLoaderTests : TestHelper
    {
        public NoMultiLoaderTests(ITestOutputHelper output)
            : base("NoMultiLoader", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SingleLoaderTest()
        {
            string tmpFile = Path.GetTempFileName();
            SetEnvironmentVariable("DD_TRACE_LOG_PATH", tmpFile);
            using ProcessResult processResult = RunSampleAndWaitForExit(9696);
            string[] logFileContent = File.ReadAllLines(tmpFile);
            int numOfLoadersLoad = logFileContent.Count(line => line.Contains("Datadog.Trace.ClrProfiler.Managed.Loader loaded"));
            Assert.Equal(1, numOfLoadersLoad);
        }
    }
}
