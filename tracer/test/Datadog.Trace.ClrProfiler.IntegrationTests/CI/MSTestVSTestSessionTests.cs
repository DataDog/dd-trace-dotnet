// <copyright file="MSTestVSTestSessionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public class MSTestVSTestSessionTests : VSTestSessionTests
{
    public MSTestVSTestSessionTests(ITestOutputHelper output)
        : base("MSTestTests", "DD_TRACE_MSTESTV2_ENABLED", output)
    {
    }
}
