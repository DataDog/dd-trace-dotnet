// <copyright file="CoverageGetCoverageResultIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

#pragma warning disable SA1201

/// <summary>
/// Coverlet.Core.CoverageResult Coverlet.Core.Coverage::GetCoverageResult() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "coverlet.core",
    TypeName = "Coverlet.Core.Coverage",
    MethodName = "GetCoverageResult",
    ReturnTypeName = "Coverlet.Core.CoverageResult",
    ParameterTypeNames = [],
    MinimumVersion = "3.0.0",
    MaximumVersion = "6.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CoverageGetCoverageResultIntegration
{
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        if (!DotnetCommon.IsDataCollectorDomain)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }

        object? modules = null;
        if (returnValue.TryDuckCast<ICoverageResultProxy>(out var coverageResultProxy))
        {
            modules = coverageResultProxy.Modules;
        }
        else if (returnValue.TryDuckCast<ICoverageResultProxyV3>(out var coverageResultProxyV3))
        {
            modules = coverageResultProxyV3.Modules;
        }
        else
        {
            Common.Log.Warning("CoverageGetCoverageResultIntegration: Could not cast to ICoverageResultProxy or ICoverageResultProxyV3");
            return new CallTargetReturn<TReturn>(returnValue);
        }

        if (modules is not null &&
            instance?.GetType().Assembly() is { } assembly &&
            assembly.GetType("Coverlet.Core.CoverageSummary") is { } coverageSummaryType)
        {
            var coverageSummary = Activator.CreateInstance(coverageSummaryType).DuckCast<ICoverageSummaryProxy>();
            var coverageDetails = coverageSummary!.CalculateLineCoverage(modules);
            var percentage = coverageDetails.Percent;
            DotnetCommon.Log.Information("CoverageGetCoverageResult.Percentage: {Value}", percentage);

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

        return new CallTargetReturn<TReturn>(returnValue);
    }

    internal interface ICoverageResultProxy : IDuckType
    {
        object? Modules { get; set; }
    }

    internal interface ICoverageResultProxyV3 : IDuckType
    {
        [DuckField]
        object? Modules { get; set; }
    }

    internal interface ICoverageSummaryProxy : IDuckType
    {
        [Duck(ParameterTypeNames = ["Coverlet.Core.Modules, coverlet.core"])]
        CoverageDetails CalculateLineCoverage(object modules);
    }

    [DuckCopy]
    internal struct CoverageDetails
    {
        public double Percent;
    }
}
