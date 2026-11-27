// <copyright file="TestAppFact.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Xunit;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    [XunitTestCaseDiscoverer("Datadog.Profiler.IntegrationTests.Xunit.TestAppFrameworkDiscover", "Datadog.Profiler.IntegrationTests")]
    internal class TestAppFact : FactAttribute
    {
        public TestAppFact(TestApplication application, params TestFramework[] frameworks)
        {
            Application = application;
            SelectedFrameworks = frameworks ?? [];
        }

        public TestApplication Application { get; }

        public TestFramework[] SelectedFrameworks { get; }

        public TestFramework MinimumFramework { get; set; }

        public TestFramework MaximumFramework { get; set; }

        public TestFramework[] ExcludedFrameworks { get; set; }
    }
}
