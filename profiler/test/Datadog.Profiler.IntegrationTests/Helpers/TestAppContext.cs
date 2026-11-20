// <copyright file="TestAppContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Application and framework selected for one profiler integration test case.
    /// </summary>
    public sealed class TestAppContext : IXunitSerializable
    {
        public TestAppContext()
        {
        }

        public TestAppContext(TestApplicationDescriptor application, TestFramework framework)
        {
            Application = application;
            Framework = framework;
        }

        public TestApplicationDescriptor Application { get; private set; }

        public TestFramework Framework { get; private set; }

        public bool IsNetFramework => Framework == TestFramework.Net48;

        public bool IsAtLeast(TestFramework minimum)
        {
            if (Framework == TestFramework.None || minimum == TestFramework.None)
            {
                return false;
            }

            // net48 does not participate in the version order used by ranges.
            if (Framework == TestFramework.Net48 || minimum == TestFramework.Net48)
            {
                return Framework == TestFramework.Net48 && minimum == TestFramework.Net48;
            }

            return Framework >= minimum;
        }

        public override string ToString()
        {
            if (Application == null)
            {
                return string.Empty;
            }

            return string.Concat(Application.FolderName, " ", TestFrameworkInfo.ToMoniker(Framework));
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            var application = (TestApplication)Enum.Parse(typeof(TestApplication), info.GetValue<string>("Application"));
            Application = TestApplicationCatalog.Get(application);
            Framework = (TestFramework)Enum.Parse(typeof(TestFramework), info.GetValue<string>("Framework"));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("Application", Application.Application.ToString());
            info.AddValue("Framework", Framework.ToString());
        }

        internal TestApplicationRunner CreateRunner(
            ITestOutputHelper output,
            string commandLine = null,
            bool enableTracer = false,
            bool enableProfiler = true)
        {
            return new TestApplicationRunner(this, output, commandLine, enableTracer, enableProfiler);
        }
    }
}
