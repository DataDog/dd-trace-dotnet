// <copyright file="TestApplication.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Demo applications under <c>profiler/src/Demos</c> that profiler tests can start.
    /// </summary>
    public enum TestApplication
    {
        Computer01,
        BuggyBits,
        ExceptionGenerator,
        ParallelCountSites,
        WebsiteAspNetCore01,
        WaitHandles,
        HttpRequest,
        FileAccess,
        WebsiteAspNet,
    }
}
