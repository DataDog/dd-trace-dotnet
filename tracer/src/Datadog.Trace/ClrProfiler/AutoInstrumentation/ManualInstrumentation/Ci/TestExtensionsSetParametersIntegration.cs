// <copyright file="TestExtensionsSetParametersIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci;

/// <summary>
/// Datadog.Trace.Ci.TestExtensions::SetParameters() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Ci.TestExtensions",
    MethodName = "SetParameters",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Datadog.Trace.Ci.ITest", "Datadog.Trace.Ci.TestParameters"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestExtensionsSetParametersIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTest, TParameters>(TTest test, in TParameters parameters)
        where TParameters : ITestParameters
    {
        // Test is an ITest, so it could be something arbitrary - if so, we just ignore it.
        // This is not ideal, but these methods can be directly duck typed using the same "shape" as Test,
        // so it's the lesser of two evils.

        if (test is IDuckType { Instance: Test automaticTest } && parameters.Instance is not null)
        {
            automaticTest.SetParameters(new TestParameters { Arguments = parameters.Arguments, Metadata = parameters.Metadata });
        }

        return CallTargetState.GetDefault();
    }
}
