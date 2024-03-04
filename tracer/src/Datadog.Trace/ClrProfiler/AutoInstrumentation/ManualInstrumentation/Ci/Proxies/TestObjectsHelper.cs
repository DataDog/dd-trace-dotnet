// <copyright file="TestObjectsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

internal static class TestObjectsHelper<TMarkerType>
{
    private static readonly Type ITestSessionType;
    private static readonly Type ITestModuleType;
    private static readonly Type ITestSuiteType;
    private static readonly Type ITestType;

    static TestObjectsHelper()
    {
        var assembly = typeof(TMarkerType).Assembly;
        ITestSessionType = assembly.GetType("Datadog.Trace.Ci.ITestSession")!;
        ITestModuleType = assembly.GetType("Datadog.Trace.Ci.ITestModule")!;
        ITestSuiteType = assembly.GetType("Datadog.Trace.Ci.ITestSuite")!;
        ITestType = assembly.GetType("Datadog.Trace.Ci.ITest")!;
    }

    public static object CreateTestModule(TestModule testModule)
        => new ManualTestModule(testModule, ITestModuleType, ITestSuiteType, ITestType).Proxy;

    public static object CreateTestSession(TestSession testSession)
        => new ManualTestSession(testSession, ITestSessionType, ITestModuleType, ITestSuiteType, ITestType).Proxy;
}
