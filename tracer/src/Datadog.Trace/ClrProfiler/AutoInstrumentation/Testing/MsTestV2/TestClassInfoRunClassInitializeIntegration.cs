// <copyright file="TestClassInfoRunClassInitializeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo.RunClassInitialize(TestContext testContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "RunClassInitialize",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.Ignore],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestClassInfoRunClassInitializeIntegration
{
    private static readonly MethodInfo EmptyCleanUpMethodInfo = typeof(TestAssemblyInfoRunAssemblyInitializeIntegration).GetMethod(nameof(EmptyCleanUpMethod), BindingFlags.NonPublic | BindingFlags.Static)!;

    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext? testContext)
        where TTarget : ITestClassInfo
    {
        if (!MsTestIntegration.IsEnabled || instance.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        lock (instance.Instance)
        {
            instance.ClassCleanupMethod ??= EmptyCleanUpMethodInfo;
        }

        return new CallTargetState(null, MsTestIntegration.GetOrCreateTestSuiteFromTestClassInfo(instance));
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        if (state.State is TestSuite suite && exception is not null)
        {
            suite.SetErrorInfo(exception);
        }

        return CallTargetReturn.GetDefault();
    }

    private static void EmptyCleanUpMethod()
    {
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo.RunClassInitialize(TestContext testContext, out LogMessageListener? logListener) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "RunClassInitialize",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.Ignore, ClrNames.Ignore],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestClassInfoRunClassInitializeIntegrationV3_9
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext, TLogMessageListener>(TTarget instance, TContext? testContext, ref TLogMessageListener? logListener)
        where TTarget : ITestClassInfo
        => TestClassInfoRunClassInitializeIntegration.OnMethodBegin(instance, testContext);

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        => TestClassInfoRunClassInitializeIntegration.OnMethodEnd(instance, exception, state);
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo.RunClassInitialize(TestContext testContext, out LogMessageListener? logListener) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "RunClassInitializeAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.Ignore],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "RunClassInitializeAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.Ignore],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestClassInfoRunClassInitializeAsyncIntegration
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext? testContext)
        where TTarget : ITestClassInfo
        => TestClassInfoRunClassInitializeIntegration.OnMethodBegin(instance, testContext);

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        TestClassInfoRunClassInitializeIntegration.OnMethodEnd(instance, exception, state);
        return returnValue;
    }
}
