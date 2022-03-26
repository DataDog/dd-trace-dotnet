// <copyright file="TestAppFact.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Profiler.SmokeTests
{
    [XunitTestCaseDiscoverer("Datadog.Profiler.SmokeTests.TestAppFrameworkDiscover", "Datadog.Profiler.IntegrationTests")]
    internal class TestAppFact : FactAttribute
    {
        private bool _useNativeLoader;

        public TestAppFact(string appAssembly)
        {
            AppAssembly = appAssembly;
            AppName = GetAppName(appAssembly);
        }

        public string AppAssembly { get; }

        public string AppName { get; set; }

        public bool UseNativeLoader
        {
            get
            {
                return _useNativeLoader;
            }

            set
            {
                _useNativeLoader = value;

                // skip if:
                // - running locally: because we do not have yet the monitoring home (profiler, native loader, profiler) built at the same place
                // - running on linux: there is no monitoring packaging yet
                if (_useNativeLoader && (!EnvironmentHelper.IsRunningOnWindows() || !EnvironmentHelper.IsInCI))
                {
                    Skip = "Skipped because the native loader is not set";
                }
            }
        }

        private string GetAppName(string assemblyName)
        {
            var pos = assemblyName.LastIndexOf('.');
            if (pos == -1)
            {
                throw new ArgumentException($"Missing '.' in assembly name '{assemblyName}'", nameof(assemblyName));
            }

            if (pos == assemblyName.Length - 1)
            {
                throw new ArgumentException($"'.' is forbidden as last character in assembly name '{assemblyName}'", nameof(assemblyName));
            }

            return assemblyName.Substring(pos + 1);
        }
    }
}
