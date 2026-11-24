// <copyright file="TestFrameworkPlatform.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Which <see cref="TestFramework"/> values <c>profiler/src/Demos/Directory.Build.props</c> builds
    /// for an operating system and architecture. Project files may build a subset, or add <c>net5.0</c>.
    /// </summary>
    internal static class TestFrameworkPlatform
    {
        public static bool IsBuilt(TestFramework framework, bool windows, bool arm64)
        {
            return framework switch
            {
                TestFramework.None => false,
                TestFramework.Net48 => windows,
                // netcoreapp3.1 is excluded only on non-Windows Arm64.
                TestFramework.NetCoreApp31 => windows || !arm64,
                TestFramework.Net5 => true,
                TestFramework.Net6 => true,
                TestFramework.Net7 => true,
                TestFramework.Net8 => true,
                TestFramework.Net9 => true,
                TestFramework.Net10 => true,
                TestFramework.Net11 => true,
                _ => throw new ArgumentOutOfRangeException(nameof(framework), framework, "Unknown test framework."),
            };
        }
    }
}
