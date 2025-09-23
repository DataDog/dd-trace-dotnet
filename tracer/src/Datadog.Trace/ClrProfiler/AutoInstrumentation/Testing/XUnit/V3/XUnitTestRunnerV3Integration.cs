// <copyright file="XUnitTestRunnerV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestRunner`2.Run calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestRunner`2",
    MethodName = "RunTest",
    ParameterTypeNames = ["_"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[System.TimeSpan]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerV3Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : IXunitTestRunnerContextV3
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var runnerInstance = new TestRunnerStruct
        {
            Aggregator = context.Aggregator,
            TestCase = new CustomTestCase
            {
                DisplayName = context.Test.TestCase.TestCaseDisplayName,
                Traits = context.Test.Traits.ToDictionary(
                    k => k.Key,
                    v => v.Value as List<string> ?? v.Value?.ToList()),
                UniqueID = context.Test.TestCase.UniqueID
            },
            TestClass = context.Test.TestCase.TestClass.Class,
            TestMethod = context.TestMethod,
            TestMethodArguments = context.TestMethodArguments!
        };

        var state = Tuple.Create(
            XUnitIntegration.CreateTest(
                ref runnerInstance,
                testCaseMetadata: ((context.MessageBus as IDuckType)?.Instance as RetryMessageBus)?.GetMetadata(context.Test.TestMethod.UniqueID)),
            (object)context);
        return new CallTargetState(null, state);
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        if (state.State is Tuple<Test?, object> tuple && tuple.Item1 == Test.Current)
        {
            // Restore the AsyncLocal set
            // This is used to mimic the ExecutionContext copy from the StateMachine
            // CallTarget integrations does this automatically when using a normal `Scope`
            // in this case we have to do it manually.
            Test.Current = null;
        }

        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is Tuple<Test?, object> { Item1: { } test, Item2: { } context })
        {
            var testRunnerContext = context.DuckCast<IXunitTestRunnerContextV3>();
            XUnitIntegration.FinishTest(test, testRunnerContext.Aggregator);
        }

        return returnValue;
    }
}
