// <copyright file="NUnitCommandBuilderMakeTestCommandIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// NUnit.Framework.Internal.Commands.TestCommand NUnit.Framework.Internal.Execution.CommandBuilder::MakeTestCommand(NUnit.Framework.Internal.TestMethod) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.CommandBuilder",
    MethodName = "MakeTestCommand",
    ReturnTypeName = "NUnit.Framework.Internal.Commands.TestCommand",
    ParameterTypeNames = ["NUnit.Framework.Internal.TestMethod"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.6.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.CommandBuilder",
    MethodName = "MakeSkipCommand",
    ReturnTypeName = "NUnit.Framework.Internal.Commands.SkipCommand",
    ParameterTypeNames = ["NUnit.Framework.Internal.Test"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.6.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class NUnitCommandBuilderMakeTestCommandIntegration
{
    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (NUnit.Framework.Internal.Commands.TestCommand)</typeparam>
    /// <param name="returnValue">Instance of NUnit.Framework.Internal.Commands.TestCommand</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A return value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state)
    {
        return new CallTargetReturn<TReturn>(NUnitIntegration.WrapWithRetryCommand(returnValue));
    }
}
