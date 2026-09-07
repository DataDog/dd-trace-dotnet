// <copyright file="TestMethodInfoGetRetryAttributeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Wraps the resolved retry policy so Datadog retries finish before class and assembly cleanup.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MSTestAdapter.PlatformServices",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo",
    MethodName = "GetRetryAttribute",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.RetryBaseAttribute",
    ParameterTypeNames = [],
    MinimumVersion = "4.4.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodInfoGetRetryAttributeIntegration
{
    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is null && returnValue is not null && MsTestIntegration.IsEnabled)
        {
            return new CallTargetReturn<TReturn?>((TReturn)MsTestRetryPolicy.Wrap(returnValue, typeof(TReturn)));
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
