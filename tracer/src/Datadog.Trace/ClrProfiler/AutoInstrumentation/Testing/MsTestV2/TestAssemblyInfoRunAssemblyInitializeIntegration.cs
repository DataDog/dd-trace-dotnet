// <copyright file="TestAssemblyInfoRunAssemblyInitializeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo.RunAssemblyInitialize(TestContext testContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo",
    MethodName = "RunAssemblyInitialize",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "_" },
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestAssemblyInfoRunAssemblyInitializeIntegration
{
    private static readonly MethodInfo EmptyCleanUpMethodInfo = typeof(TestAssemblyInfoRunAssemblyInitializeIntegration).GetMethod("EmptyCleanUpMethod", BindingFlags.NonPublic | BindingFlags.Static);
    internal static readonly ConditionalWeakTable<object, object> TestAssemblyInfos = new();

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TContext">Type of the ITestMethod</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="testContext">Test context instance</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext testContext)
        where TTarget : ITestAssemblyInfo
    {
        if (!MsTestIntegration.IsEnabled || !testContext.TryDuckCast<TestContextStruct>(out var context))
        {
            return CallTargetState.GetDefault();
        }

        if (!TestAssemblyInfos.TryGetValue(instance.Instance, out var moduleObject))
        {
            var assemblyName = AssemblyName.GetAssemblyName(context.TestMethod.AssemblyName).Name ?? string.Empty;
            var frameworkVersion = instance.Type.Assembly.GetName().Version?.ToString() ?? string.Empty;
            instance.AssemblyCleanupMethod ??= EmptyCleanUpMethodInfo;

            CIVisibility.WaitForSkippableTaskToFinish();
            var module = TestModule.Create(assemblyName, "MSTestV2", frameworkVersion);
            TestAssemblyInfos.Add(instance.Instance, module);
            return new CallTargetState(null, module);
        }

        return new CallTargetState(null, moduleObject);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
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
}
