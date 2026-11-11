// <copyright file="UnitTestDiscovererSendTestCasesAsyncIntegrationV4_4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Counts discovered tests before MSTest applies its execution filter.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MSTestAdapter.PlatformServices",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.UnitTestDiscoverer",
    MethodName = "SendTestCasesAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement]", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IUnitTestElementSink", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestElementFilterProvider", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IAdapterMessageLogger"],
    MinimumVersion = "4.4.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UnitTestDiscovererSendTestCasesAsyncIntegrationV4_4
{
    /// <summary>
    /// Captures the unfiltered count used by EFD; collections avoid a second enumeration.
    /// </summary>
    internal static CallTargetState OnMethodBegin<TTarget, TSink, TFilter, TLogger>(TTarget instance, IEnumerable? testElements, TSink sink, TFilter filter, TLogger logger)
    {
        var count = 0;
        if (testElements is ICollection collection)
        {
            count = collection.Count;
        }
        else if (testElements is not null)
        {
            foreach (var element in testElements)
            {
                count++;
            }
        }

        MsTestIntegration.AddTotalTestCases(count);
        return CallTargetState.GetDefault();
    }
}
