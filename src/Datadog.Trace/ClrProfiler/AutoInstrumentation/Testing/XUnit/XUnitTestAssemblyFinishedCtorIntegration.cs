// <copyright file="XUnitTestAssemblyFinishedCtorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestAssemblyFinished..ctor calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        TypeName = "Xunit.Sdk.TestAssemblyFinished",
        MethodName = ".ctor",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "_", "_", "_", "_", "_", "_" },
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = XUnitIntegration.IntegrationName)]
    public static class XUnitTestAssemblyFinishedCtorIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TArg1">Type of the argument 1</typeparam>
        /// <typeparam name="TArg2">Type of the argument 2</typeparam>
        /// <typeparam name="TArg3">Type of the argument 3</typeparam>
        /// <typeparam name="TArg4">Type of the argument 4</typeparam>
        /// <typeparam name="TArg5">Type of the argument 5</typeparam>
        /// <typeparam name="TArg6">Type of the argument 6</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testCases">Test cases</param>
        /// <param name="testAssembly">Test assembly</param>
        /// <param name="executionTime">Execution time</param>
        /// <param name="testsRun">Test runs</param>
        /// <param name="testsFailed">Tests failed</param>
        /// <param name="testsSkipped">Tests skipped</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget instance, TArg1 testCases, TArg2 testAssembly, TArg3 executionTime, TArg4 testsRun, TArg5 testsFailed, TArg6 testsSkipped)
        {
            Common.FlushSpans(XUnitIntegration.IntegrationId);
            return CallTargetState.GetDefault();
        }
    }
}
