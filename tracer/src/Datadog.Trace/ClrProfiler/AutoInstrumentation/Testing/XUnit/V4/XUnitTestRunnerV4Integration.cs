// <copyright file="XUnitTestRunnerV4Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V4;

/// <summary>
/// Instruments xUnit 4 individual test execution.
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestRunner`2",
    MethodName = "RunTest",
    ParameterTypeNames = ["_"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[System.TimeSpan]",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerV4Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : IXunitTestRunnerContextV4
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var runnerInstance = CreateTestRunnerData(context);

        var testCaseUniqueID = context.Test.TestCase.UniqueID;
        var testCaseMetadata = ((context.MessageBus as IDuckType)?.Instance as RetryMessageBus)?.GetMetadata(testCaseUniqueID);
        var test = XUnitIntegration.CreateTest(ref runnerInstance, testCaseMetadata);
        var state = Tuple.Create(test, (object)context);
        return new CallTargetState(null, state);
    }

    internal static TestRunnerStruct CreateTestRunnerData(IXunitTestRunnerContextV4 context)
    {
        return new TestRunnerStruct
        {
            Aggregator = context.Aggregator,
            TestCase = new CustomTestCase
            {
                DisplayName = context.Test.TestCase.TestCaseDisplayName,
                Traits = context.Test.Traits.ToDictionary(
                    keyValuePair => keyValuePair.Key,
                    keyValuePair => keyValuePair.Value as List<string> ?? keyValuePair.Value?.ToList()),
                UniqueID = context.Test.TestCase.UniqueID,
            },
            TestClass = context.Test.TestCase.TestClass.Class,
            TestMethod = context.Method,
            TestMethodArguments = context.MethodArguments!,
            SkipReason = context.Test.SkipReason,
        };
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        if (state.State is Tuple<Test?, object> tuple && tuple.Item1 == Test.Current)
        {
            // Restore the AsyncLocal set. This mimics the ExecutionContext copy performed by the
            // state machine, which CallTarget normally handles automatically for a regular Scope.
            Test.Current = null;
        }

        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is Tuple<Test?, object> { Item1: { } test, Item2: { } context })
        {
            var testRunnerContext = context.DuckCast<IXunitTestRunnerContextV4>();
            XUnitIntegration.FinishTest(test, testRunnerContext.Aggregator);
        }

        return returnValue;
    }
}
