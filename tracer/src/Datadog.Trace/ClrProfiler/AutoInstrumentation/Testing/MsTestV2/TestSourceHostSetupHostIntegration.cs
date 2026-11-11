// <copyright file="TestSourceHostSetupHostIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Carries the discovery count into MSTest's isolated execution AppDomain.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MSTestAdapter.PlatformServices",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.TestSourceHost",
    MethodName = "SetupHost",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "4.3.3",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestSourceHostSetupHostIntegration
{
    internal interface ITestSourceHost : IDuckType
    {
        AppDomain? AppDomain { get; }
    }

    /// <summary>
    /// Transfers discovery totals after MSTest has created the isolated execution AppDomain.
    /// </summary>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : ITestSourceHost
    {
        if (exception is null && instance.AppDomain is { } executionDomain)
        {
            MsTestIntegration.CopyTotalTestCasesTo(executionDomain);
        }

        return CallTargetReturn.GetDefault();
    }
}
#endif
