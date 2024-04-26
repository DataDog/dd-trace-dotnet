// <copyright file="TestFrameworkDiscovererReportDiscoveredTestCaseIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// System.Boolean Xunit.Sdk.TestFrameworkDiscoverer::ReportDiscoveredTestCase(Xunit.Abstractions.ITestCase,System.Boolean,Xunit.Sdk.IMessageBus) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.execution.dotnet",
    TypeName = "Xunit.Sdk.TestFrameworkDiscoverer",
    MethodName = "ReportDiscoveredTestCase",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = ["Xunit.Abstractions.ITestCase", ClrNames.Bool, "Xunit.Sdk.IMessageBus"],
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestFrameworkDiscovererReportDiscoveredTestCaseIntegration
{
    private const string IntegrationName = nameof(IntegrationId.XUnit);

    internal static CallTargetState OnMethodBegin<TTarget, TTestCase, TMessageBus>(TTarget instance, ref TTestCase? testCase, ref bool includeSourceInformation, ref TMessageBus? messageBus)
    {
        XUnitIntegration.IncrementTotalTestCases();
        return CallTargetState.GetDefault();
    }
}
