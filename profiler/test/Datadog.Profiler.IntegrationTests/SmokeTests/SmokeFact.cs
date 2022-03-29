// <copyright file="SmokeFact.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Profiler.SmokeTests
{
    [XunitTestCaseDiscoverer("Datadog.Profiler.SmokeTests.SmokeTestFrameworkDiscover", "Datadog.Profiler.IntegrationTests")]
    internal class SmokeFact : FactAttribute
    {
        public SmokeFact(string appAssembly)
        {
            AppAssembly = appAssembly;
            AppName = GetAppName(appAssembly);
        }

        public string AppAssembly { get; }

        public string AppName { get; set; }

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
