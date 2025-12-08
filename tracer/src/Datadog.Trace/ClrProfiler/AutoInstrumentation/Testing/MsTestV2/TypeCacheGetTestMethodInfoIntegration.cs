// <copyright file="TypeCacheGetTestMethodInfoIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TypeCache::GetTestMethodInfo(Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod,Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext,System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TypeCache",
    MethodName = "GetTestMethodInfo",
    ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext", ClrNames.Bool],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TypeCacheGetTestMethodInfoIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod, TTestContext>(TTarget instance, ref TTestMethod? testMethod, ref TTestContext? testContext, ref bool captureDebugTraces)
        => TypeCacheGetTestMethodInfoIntegrationV3_9.OnMethodBegin(instance, ref testMethod, ref testContext);

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        => TypeCacheGetTestMethodInfoIntegrationV3_9.OnMethodEnd(instance, returnValue, exception, in state);
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TypeCache::GetTestMethodInfo(Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod,Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TypeCache",
    MethodName = "GetTestMethodInfo",
    ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TypeCache",
    MethodName = "GetTestMethodInfo",
    ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public class TypeCacheGetTestMethodInfoIntegrationV3_9
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod, TTestContext>(TTarget instance, ref TTestMethod? testMethod, ref TTestContext? testContext)
    {
        if (!MsTestIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        var cacheItem = new MethodInfoCacheItem
        {
            TestMethod = testMethod,
            TestContext = testContext
        };

        return new CallTargetState(null, cacheItem);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (state.State is MethodInfoCacheItem cacheItem)
        {
            cacheItem.TestMethodInfo = returnValue;
            MsTestIntegration.IsTestMethodRunnableThreadLocal.Value = cacheItem;
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
