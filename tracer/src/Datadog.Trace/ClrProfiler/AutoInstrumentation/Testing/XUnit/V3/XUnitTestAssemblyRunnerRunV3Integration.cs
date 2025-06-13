// <copyright file="XUnitTestAssemblyRunnerRunV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestAssemblyRunner`4.Run calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestAssemblyRunner`4",
    MethodName = "Run",
    ParameterTypeNames = ["!0"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class XUnitTestAssemblyRunnerRunV3Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : ITestAssemblyRunnerContextV3, IDuckType
    {
        if (!XUnitIntegration.IsEnabled || context.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        if (context.TestAssembly.AssemblyName is { } assemblyName)
        {
            var testBundleString = new AssemblyName(assemblyName).Name ?? string.Empty;

            // Extract the version of the framework from the TestClassRunner base class
            var frameworkType = instance!.GetType();
            while (frameworkType.IsAbstract == false)
            {
                if (frameworkType.BaseType is { } baseType)
                {
                    frameworkType = baseType;
                }
                else
                {
                    break;
                }
            }

            TestOptimization.Instance.SkippableFeature?.WaitForSkippableTaskToFinish();
            var module = TestModule.InternalCreate(testBundleString, CommonTags.TestingFrameworkNameXUnitV3, frameworkType.Assembly.GetName().Version?.ToString() ?? string.Empty);
            module.EnableIpcClient();
            return new CallTargetState(null, module);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        if (state.State == TestModule.Current)
        {
            // Restore the AsyncLocal set
            // This is used to mimic the ExecutionContext copy from the StateMachine
            // CallTarget integrations does this automatically when using a normal `Scope`
            // in this case we have to do it manually.
            TestModule.Current = null;
        }

        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is TestModule testModule)
        {
            await testModule.CloseAsync().ConfigureAwait(false);

            // Because we are auto-instrumenting a VSTest testhost process we need to manually call the shutdown process
            TestOptimization.Instance.Close();
        }

        return returnValue;
    }
}
