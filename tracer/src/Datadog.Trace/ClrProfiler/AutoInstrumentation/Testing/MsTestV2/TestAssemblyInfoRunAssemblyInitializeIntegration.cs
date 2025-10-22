// <copyright file="TestAssemblyInfoRunAssemblyInitializeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo.RunAssemblyInitialize(TestContext testContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo",
    MethodName = "RunAssemblyInitialize",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.TestContext"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo",
    MethodName = "RunAssemblyInitialize",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.TestContext"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestAssemblyInfoRunAssemblyInitializeIntegration
{
    private static readonly MethodInfo EmptyCleanUpMethodInfo = typeof(TestAssemblyInfoRunAssemblyInitializeIntegration).GetMethod(nameof(EmptyCleanUpMethod), BindingFlags.NonPublic | BindingFlags.Static)!;

    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext? testContext)
        where TTarget : ITestAssemblyInfo
    {
        if (!MsTestIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        lock (instance.Instance!)
        {
            instance.AssemblyCleanupMethod ??= EmptyCleanUpMethodInfo;
        }

        string? assemblyName = null;
        if (testContext.TryDuckCast<TestContextStruct>(out var context) && context.TestMethod is { AssemblyName: { } contextAssemblyName })
        {
            assemblyName = contextAssemblyName;
        }

        if (string.IsNullOrEmpty(assemblyName) && instance.Instance?.TryDuckCast<ITestAssemblyInfoWithAssembly>(out var instanceWithAssembly) == true)
        {
            assemblyName = instanceWithAssembly.Assembly.GetName().Name;
        }

        if (string.IsNullOrEmpty(assemblyName))
        {
            Common.Log.Warning("Module name cannot be extracted!");
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, MsTestIntegration.GetOrCreateTestModuleFromTestAssemblyInfo(instance, assemblyName));
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        if (state.State is TestModule module && exception is not null)
        {
            module.SetErrorInfo(exception);
        }

        return CallTargetReturn.GetDefault();
    }

    private static void EmptyCleanUpMethod()
    {
    }

#pragma warning disable SA1201
    internal interface ITestAssemblyInfoWithAssembly
#pragma warning restore SA1201
    {
        Assembly Assembly { get; }
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo.RunAssemblyInitialize(TestContext testContext, out LogMessageListener? logListener) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo",
    MethodName = "RunAssemblyInitialize",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.TestContext", ClrNames.Ignore],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestAssemblyInfoRunAssemblyInitializeIntegrationV3_9
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext, TLogMessageListener>(TTarget instance, TContext? testContext, ref TLogMessageListener? logListener)
        where TTarget : ITestAssemblyInfo
        => TestAssemblyInfoRunAssemblyInitializeIntegration.OnMethodBegin(instance, testContext);

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        => TestAssemblyInfoRunAssemblyInitializeIntegration.OnMethodEnd(instance, exception, state);
}
