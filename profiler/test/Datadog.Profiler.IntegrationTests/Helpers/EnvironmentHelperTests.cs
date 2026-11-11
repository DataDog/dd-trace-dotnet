// <copyright file="EnvironmentHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class EnvironmentHelperTests
    {
        [Fact]
        public void GenerateLoaderConfigFile_ConcurrentCalls_WriteDistinctFiles()
        {
            const int count = 16;
            var previousName = TestContext.Current.TestName;
            TestContext.Current.TestName = nameof(EnvironmentHelperTests) + "." + nameof(GenerateLoaderConfigFile_ConcurrentCalls_WriteDistinctFiles);

            try
            {
                var helper = new EnvironmentHelper(framework: "net8.0", enableTracer: false, enableProfiler: false);
                var outputDirectory = helper.GetTestOutputPath();
                var paths = new string[count];

                Parallel.For(0, count, index =>
                {
                    paths[index] = helper.GenerateLoaderConfigFile();
                });

                var expectedNames = new string[count];
                expectedNames[0] = "loader.conf";
                for (var index = 1; index < count; index++)
                {
                    expectedNames[index] = $"loader-{index + 1}.conf";
                }

                paths.Select(Path.GetFileName).Should().BeEquivalentTo(expectedNames);

                foreach (var path in paths)
                {
                    Path.GetDirectoryName(path).Should().Be(outputDirectory);
                    var lines = File.ReadAllLines(path);
                    lines.Should().HaveCount(2);
                    lines[0].Should().StartWith("PROFILER;");
                    lines[1].Should().StartWith("TRACER;");
                }
            }
            finally
            {
                TestContext.Current.TestName = previousName;
            }
        }
    }
}
