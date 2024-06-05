// <copyright file="ManagedVanguardStopIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Void Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard::Stop() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TraceDataCollector",
    TypeName = "Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard",
    MethodName = "Stop",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ManagedVanguardStopIntegration
{
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IManagedVanguardProxy
    {
        if (instance.GetOutputCoverageFiles() is { } lstFiles)
        {
            foreach (var file in lstFiles.Distinct())
            {
                if (file is null)
                {
                    continue;
                }

                if (Path.GetExtension(file).Equals(".xml", StringComparison.OrdinalIgnoreCase) &&
                    DotnetCommon.TryGetCoveragePercentageFromXml(file, out var percentage))
                {
                    DotnetCommon.Log.Information("MicrosoftCodeCoverage.Percentage: {Value}", percentage);

                    // Extract session variables (from out of process sessions)
                    if (SpanContextPropagator.Instance.Extract(
                            EnvironmentHelpers.GetEnvironmentVariables(),
                            new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor)) is { } sessionContext)
                    {
                        try
                        {
                            var name = $"session_{sessionContext.SpanId}";
                            Common.Log.Debug("DataCollector.Enabling IPC client: {Name}", name);
                            using var ipcClient = new IpcClient(name);
                            Common.Log.Debug("DataCollector.Sending session code coverage: {Value}", percentage);
                            ipcClient.TrySendMessage(new SessionCodeCoverageMessage(percentage));
                        }
                        catch (Exception ex)
                        {
                            Common.Log.Error(ex, "Error enabling IPC client and sending coverage data");
                        }
                    }
                }
            }
        }

        return CallTargetReturn.GetDefault();
    }

    /// <summary>
    /// DuckTyping interface for Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard
    /// </summary>
#pragma warning disable SA1201
    internal interface IManagedVanguardProxy : IDuckType
#pragma warning restore SA1201
    {
        /// <summary>
        /// Calls method: System.Collections.Generic.IList`1[System.String] Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard::GetOutputCoverageFiles()
        /// </summary>
        IList<string>? GetOutputCoverageFiles();
    }
}
