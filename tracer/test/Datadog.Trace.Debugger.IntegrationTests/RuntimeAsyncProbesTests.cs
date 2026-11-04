// <copyright file="RuntimeAsyncProbesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Samples.Probes.TestRuns.SmokeTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests;

/// <summary>
/// Runs the async probe tests against Samples.Probes.RuntimeAsync, where the async test methods are compiled as
/// .NET 11 runtime-async methods instead of state machines.
/// </summary>
[Collection(nameof(ProbesTests))]
[UsesVerify]
public class RuntimeAsyncProbesTests : ProbesTestsBase
{
    public RuntimeAsyncProbesTests(ITestOutputHelper output)
        : base("Probes.RuntimeAsync", "ProbeTests.RuntimeAsync", output)
    {
    }

    public static IEnumerable<object[]> RuntimeAsyncProbeTests() =>
    [
        [typeof(AsyncCallChain)],
        [typeof(AsyncInstanceMethod)],
        [typeof(AsyncLineProbeWithFieldsArgsAndLocalsTest)],
        [typeof(AsyncRecursiveCall)],
        [typeof(AsyncSpanOnMethodWithArgsTest)],
        [typeof(AsyncSpanOnMethodWithExceptionProbeTest)],
        [typeof(AsyncStaticMethod)],
        [typeof(AsyncTaskReturnTest)],
        [typeof(AsyncTaskReturnWithExceptionTest)],
        [typeof(AsyncThrowException)],
        [typeof(AsyncTryCatchTest)],
        [typeof(AsyncTryFinallyTest)],
    ];

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(RuntimeAsyncProbeTests))]
    public async Task MethodProbeTest(Type testType)
    {
        if (EnvironmentHelper.GetTargetFramework() != "net11.0")
        {
            throw new SkipException("Samples.Probes.RuntimeAsync only targets net11.0.");
        }

        var testDescription = DebuggerTestHelper.SpecificTestDescription(testType);
        if (!testDescription.IsOptimized)
        {
            throw new SkipException("Runtime-async probe tests have approvals for optimized code only.");
        }

        if (testType == typeof(AsyncInstanceMethod) && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Should run only on Windows. Different approvals between Windows/Linux.");
        }

        await RunMethodProbeTests(testDescription, useStatsD: true);
    }
}
