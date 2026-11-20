// <copyright file="TestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Target frameworks the profiler demos can be built for, ordered by version.
    /// <see cref="Net48"/> is a separate family and is not part of a version range.
    /// </summary>
    public enum TestFramework
    {
        None = 0,
        Net48,
        NetCoreApp31,
        Net5,
        Net6,
        Net7,
        Net8,
        Net9,
        Net10,
        Net11,
    }
}
