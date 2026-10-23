// <copyright file="TestRunRequestOnRunCompletionIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Void Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.RunTestsArgumentExecutor+TestRunRequestEventsRegistrar::TestRunRequest_OnRunCompletion(System.Object,Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.TestRunCompleteEventArgs) calltarget instrumentation
/// </summary>
/// <remarks>
/// Reports the number of tests executed by a vstest.console run to the test session, so a successful session that ran
/// no tests can be reported as skipped. VSTest counts the tests itself, so tests from frameworks that are not
/// instrumented are included, and a session without test events is not mistaken for an empty one.
/// </remarks>
[InstrumentMethod(
    AssemblyNames = ["vstest.console", "vstest.console.arm64"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.RunTestsArgumentExecutor+TestRunRequestEventsRegistrar",
    MethodName = "TestRunRequest_OnRunCompletion",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.Object, "Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.TestRunCompleteEventArgs"],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestRunRequestOnRunCompletionIntegration
{
    internal interface ITestRunCompleteEventArgs : IDuckType
    {
        bool IsAborted { get; }

        bool IsCanceled { get; }

        Exception? Error { get; }

        TestRunStatistics? TestRunStatistics { get; }
    }

    internal static CallTargetState OnMethodBegin<TTarget, TArgs>(TTarget instance, object? sender, TArgs args)
        where TArgs : ITestRunCompleteEventArgs
    {
        if (DotnetCommon.DotnetTestIntegrationEnabled && args.Instance is not null)
        {
            // An aborted, canceled or failed run can't confirm that no tests ran.
            var executedTests = args.IsAborted || args.IsCanceled || args.Error is not null ? null : args.TestRunStatistics?.ExecutedTests;
            SendExecutedTests(executedTests);
        }

        return CallTargetState.GetDefault();
    }

    private static void SendExecutedTests(long? executedTests)
    {
        // The session that owns this run propagates its context through environment variables, whether it was created
        // by this vstest.console process or by a parent process such as the dotnet CLI. Only sessions created by the
        // dotnet test integrations host the IPC server, so manual sessions never receive the count.
        if (!DotnetCommon.TryGetParentSessionSpanId(out var sessionSpanId))
        {
            return;
        }

        try
        {
            using var ipcClient = new IpcClient($"session_{sessionSpanId}");
            var message = new SessionExecutedTestsMessage(executedTests);

            // A lost count could let another run's zero count report the session as empty. The session drains the
            // channel every 100 ms, so a full channel is retried for a short while.
            for (var attempt = 1; !ipcClient.TrySendMessage(message); attempt++)
            {
                if (attempt == 10)
                {
                    DotnetCommon.Log.Warning("TestRunRequestOnRunCompletionIntegration: Could not send the executed tests to the test session.");
                    return;
                }

                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            DotnetCommon.Log.Error(ex, "TestRunRequestOnRunCompletionIntegration: Error sending the executed tests to the test session.");
        }
    }

    [DuckCopy]
    internal struct TestRunStatistics
    {
        public long ExecutedTests;
    }
}
