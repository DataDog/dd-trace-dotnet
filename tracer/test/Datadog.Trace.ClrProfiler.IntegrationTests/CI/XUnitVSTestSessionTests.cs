// <copyright file="XUnitVSTestSessionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public class XUnitVSTestSessionTests : VSTestSessionTests
{
    public XUnitVSTestSessionTests(ITestOutputHelper output)
        : base("XUnitTests", "DD_TRACE_XUNIT_ENABLED", output)
    {
    }
}
