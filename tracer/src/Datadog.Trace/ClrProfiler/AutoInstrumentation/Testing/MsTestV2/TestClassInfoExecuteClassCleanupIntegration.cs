// <copyright file="TestClassInfoExecuteClassCleanupIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo.ExecuteClassCleanup() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "ExecuteClassCleanup",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestClassInfoExecuteClassCleanupIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        where TTarget : ITestClassInfo
    {
        if (MsTestIntegration.IsEnabled && MsTestIntegration.GetOrCreateTestSuiteFromTestClassInfo(instance) is { } suite)
        {
            return new CallTargetState(null, suite);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        if (state.State is TestSuite suite)
        {
            if (exception is not null)
            {
                suite.SetErrorInfo(exception);
            }

            suite.Close();
        }

        return CallTargetReturn.GetDefault();
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo.ExecuteClassCleanup() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "ExecuteClassCleanup",
    ReturnTypeName = ClrNames.Ignore,
    ParameterTypeNames = [ClrNames.Ignore, ClrNames.Ignore],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestClassInfoExecuteClassCleanupIntegrationV3_9
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TArg, TArg2>(TTarget instance, TArg? arg, ref TArg2? arg2)
        where TTarget : ITestClassInfo
        => TestClassInfoExecuteClassCleanupIntegration.OnMethodBegin(instance);

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        TestClassInfoExecuteClassCleanupIntegration.OnMethodEnd(instance, exception, state);
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo.ExecuteClassCleanup() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "ExecuteClassCleanupAsync",
    ReturnTypeName = ClrNames.Ignore,
    ParameterTypeNames = [ClrNames.Ignore],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
    MethodName = "ExecuteClassCleanupAsync",
    ReturnTypeName = ClrNames.Ignore,
    ParameterTypeNames = [ClrNames.Ignore],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestClassInfoExecuteClassCleanupAsyncIntegration
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TArg>(TTarget instance, TArg? arg)
        where TTarget : ITestClassInfo
        => TestClassInfoExecuteClassCleanupIntegration.OnMethodBegin(instance);

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        TestClassInfoExecuteClassCleanupIntegration.OnMethodEnd(instance, exception, state);
        return returnValue;
    }
}
