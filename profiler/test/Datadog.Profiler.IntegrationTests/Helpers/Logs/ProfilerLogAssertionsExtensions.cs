// <copyright file="ProfilerLogAssertionsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    internal static class ProfilerLogAssertionsExtensions
    {
        public static ProfilerLogGroupAssertions Should(this ProfilerLogGroup instance)
        {
            return new ProfilerLogGroupAssertions(instance);
        }
    }
}
