// <copyright file="TestRunRequestHandleTestRunCompleteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// Records runner statistics, independently of framework instrumentation and emitted test events.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.Client",
    TypeName = "Microsoft.VisualStudio.TestPlatform.Client.Execution.TestRunRequest",
    MethodName = "HandleTestRunComplete",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.TestRunCompleteEventArgs", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.TestRunChangedEventArgs", "System.Collections.Generic.ICollection`1[Microsoft.VisualStudio.TestPlatform.ObjectModel.AttachmentSet]", "System.Collections.Generic.ICollection`1[System.String]"],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestRunRequestHandleTestRunCompleteIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TArgs>(TTarget instance, TArgs args, object? lastChunkArgs, object? runContextAttachments, object? executorUris)
        where TArgs : ITestRunCompleteEventArgs, IDuckType
    {
        if (DotnetCommon.DotnetTestIntegrationEnabled && instance is not null)
        {
            var empty = args.Instance is not null
                     && !args.IsAborted
                     && !args.IsCanceled
                     && args.Error is null
                     && args.TestRunStatistics?.ExecutedTests == 0;
            VSTestRunTracker.Complete(instance, empty);
        }

        return CallTargetState.GetDefault();
    }
}
