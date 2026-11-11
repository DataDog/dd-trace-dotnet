// <copyright file="LiveObjectsProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LiveObjects
{
    public class LiveObjectsProfilerTest
    {
        private const string ScenarioGenerics = "--scenario 13";

        private readonly ITestOutputHelper _output;

        public LiveObjectsProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net7)]
        public void ShouldGetGarbageCollectionSamples(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: ScenarioGenerics);
            runner.Environment.EnableOnly(ProfilerFeature.LiveHeap);

            using var run = runner.Run();

            Assert.True(run.UploadCount > 0);
            Assert.True(run.Profiles.EveryProfileHasOnlyMatchingSamples(sample => sample.Labels.Has("object lifetime")));
        }
    }
}
