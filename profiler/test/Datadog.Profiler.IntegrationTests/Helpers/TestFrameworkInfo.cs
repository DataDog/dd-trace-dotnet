// <copyright file="TestFrameworkInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class TestFrameworkInfo
    {
        /// <summary>
        /// Frameworks that participate in version ranges, oldest first.
        /// <see cref="TestFramework.Net48"/> is intentionally absent.
        /// </summary>
        public static readonly TestFramework[] VersionOrder =
        {
            TestFramework.NetCoreApp31,
            TestFramework.Net5,
            TestFramework.Net6,
            TestFramework.Net7,
            TestFramework.Net8,
            TestFramework.Net9,
            TestFramework.Net10,
            TestFramework.Net11,
        };

        public static string ToMoniker(TestFramework framework)
        {
            return framework switch
            {
                TestFramework.None => "none",
                TestFramework.Net48 => "net48",
                TestFramework.NetCoreApp31 => "netcoreapp3.1",
                TestFramework.Net5 => "net5.0",
                TestFramework.Net6 => "net6.0",
                TestFramework.Net7 => "net7.0",
                TestFramework.Net8 => "net8.0",
                TestFramework.Net9 => "net9.0",
                TestFramework.Net10 => "net10.0",
                TestFramework.Net11 => "net11.0",
                _ => throw new ArgumentOutOfRangeException(nameof(framework), framework, "Unknown test framework."),
            };
        }

        public static bool TryParse(string moniker, out TestFramework framework)
        {
            framework = moniker switch
            {
                "net48" => TestFramework.Net48,
                "netcoreapp3.1" => TestFramework.NetCoreApp31,
                "net5.0" => TestFramework.Net5,
                "net6.0" => TestFramework.Net6,
                "net7.0" => TestFramework.Net7,
                "net8.0" => TestFramework.Net8,
                "net9.0" => TestFramework.Net9,
                "net10.0" => TestFramework.Net10,
                "net11.0" => TestFramework.Net11,
                _ => TestFramework.None,
            };

            return framework != TestFramework.None;
        }
    }
}
