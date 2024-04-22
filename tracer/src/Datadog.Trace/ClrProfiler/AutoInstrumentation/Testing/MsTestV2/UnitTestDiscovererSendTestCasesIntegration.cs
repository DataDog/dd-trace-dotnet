// <copyright file="UnitTestDiscovererSendTestCasesIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// System.Void Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.UnitTestDiscoverer::SendTestCases(System.String,System.Collections.Generic.IEnumerable`1[Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement],Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.ITestCaseDiscoverySink,Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IDiscoveryContext,Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.UnitTestDiscoverer",
    MethodName = "SendTestCases",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, "System.Collections.Generic.IEnumerable`1[Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement]", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.ITestCaseDiscoverySink", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IDiscoveryContext", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class UnitTestDiscovererSendTestCasesIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TDiscoverySink, TDiscoveryContext, TLogger>(TTarget instance, ref string? source, ref IEnumerable? testElements, ref TDiscoverySink? discoverySink, ref TDiscoveryContext? discoveryContext, ref TLogger? logger)
    {
        if (testElements?.Cast<object>().Count() is { } count)
        {
            MsTestIntegration.AddTotalTestCases(count);
        }

        return CallTargetState.GetDefault();
    }
}
