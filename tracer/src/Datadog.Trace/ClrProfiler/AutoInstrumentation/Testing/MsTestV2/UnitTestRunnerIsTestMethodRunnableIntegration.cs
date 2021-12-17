// <copyright file="UnitTestRunnerIsTestMethodRunnableIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.IsTestMethodRunnable calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
        TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
        MethodName = "IsTestMethodRunnable",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new string[] { "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo", "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestResult[]&" },
        MinimumVersion = "14.0.0",
        MaximumVersion = "14.*.*",
        IntegrationName = MsTestIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class UnitTestRunnerIsTestMethodRunnableIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TArg1">Type of the arg1</typeparam>
        /// <typeparam name="TArg2">Type of the arg2</typeparam>
        /// <typeparam name="TArg3">Type of the arg3</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testMethod">Test method argument</param>
        /// <param name="testMethodInfo">Test method info argument</param>
        /// <param name="notRunnableResult">Not runnable result argument</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3>(TTarget instance, TArg1 testMethod, TArg2 testMethodInfo, ref TArg3 notRunnableResult)
        {
            if (MsTestIntegration.IsEnabled)
            {
                MsTestIntegration.IsTestMethodRunnableThreadLocal.Value = testMethodInfo;
            }

            return CallTargetState.GetDefault();
        }
    }
}
