// <copyright file="XUnitTestRunnerBaseRunV4Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V4;

/// <summary>
/// Instruments xUnit 4 tests which are skipped before <c>RunTest</c> is invoked.
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestRunnerBase`2",
    MethodName = "Run",
    ParameterTypeNames = ["_"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerBaseRunV4Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : IXunitTestRunnerContextV4
    {
        if (!XUnitIntegration.IsEnabled || instance is null || context.Test.SkipReason is null)
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, context);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is { } rawContext &&
            returnValue.TryDuckCast<IRunSummaryV4>(out var runSummary) &&
            runSummary.Skipped > 0)
        {
            var context = rawContext.DuckCast<IXunitTestRunnerContextV4>();
            var runnerInstance = XUnitTestRunnerV4Integration.CreateTestRunnerData(context);
            TestCaseMetadata? testCaseMetadata = null;
            if (context.MessageBus is IDuckType { Instance: RetryMessageBus retryMessageBus })
            {
                retryMessageBus.TryGetMetadata(context.Test.TestCase.UniqueID, out testCaseMetadata);
            }

            XUnitIntegration.CreateTest(ref runnerInstance, testCaseMetadata);
        }

        return returnValue;
    }
}
