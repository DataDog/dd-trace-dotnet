// <copyright file="NUnitTestAssemblyRunnerRunIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// NUnit.Framework.Api.NUnitTestAssemblyRunner.Run() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "nunit.framework",
        TypeName = "NUnit.Framework.Api.NUnitTestAssemblyRunner",
        MethodName = "Run",
        ReturnTypeName = "NUnit.Framework.Interfaces.ITestResult",
        ParameterTypeNames = new[] { "NUnit.Framework.Interfaces.ITestListener", "NUnit.Framework.Interfaces.ITestFilter" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = NUnitIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class NUnitTestAssemblyRunnerRunIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TListener">Type of test listener</typeparam>
        /// <typeparam name="TFilter">Type of test filter</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testListener">Test listener</param>
        /// <param name="testFilter">Test filter</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TListener, TFilter>(TTarget instance, TListener testListener, TFilter testFilter)
            where TTarget : INUnitTestAssemblyRunner
        {
            if (!NUnitIntegration.IsEnabled)
            {
                return CallTargetState.GetDefault();
            }

            // TestType: Assembly
            // TestType: TestFixture
            // TestType: TestMethod
            //
            // CIVisibility.Log.Information("*** NUnit.Framework.Api.NUnitTestAssemblyRunner() BEGIN");
            // var item = instance.LoadedTest.Instance.DuckCast<ITestAssembly>();
            // CIVisibility.Log.Information($"       Id: {item.Id}");
            // CIVisibility.Log.Information($"       Name: {item.Name}");
            // CIVisibility.Log.Information($"       TestType: {item.TestType}");
            // CIVisibility.Log.Information($"       FullName: {item.FullName}");
            // CIVisibility.Log.Information($"       ClassName: {item.ClassName}");
            // CIVisibility.Log.Information($"       MethodName: {item.MethodName}");
            // CIVisibility.Log.Information($"       TypeInfo.FullName: {item.TypeInfo?.FullName}");
            // CIVisibility.Log.Information($"       TypeInfo.Namespace: {item.TypeInfo?.Namespace}");
            // CIVisibility.Log.Information($"       TypeInfo.Name: {item.TypeInfo?.Name}");
            // CIVisibility.Log.Information($"       TypeInfo.Type: {item.TypeInfo?.Type}");
            // CIVisibility.Log.Information($"       TypeInfo.Assembly: {item.TypeInfo?.Assembly?.GetName().FullName}");
            // CIVisibility.Log.Information($"       RunState: {item.RunState}");
            // CIVisibility.Log.Information($"       TestCaseCount: {item.TestCaseCount}");
            // CIVisibility.Log.Information($"       IsSuite: {item.IsSuite}");
            // CIVisibility.Log.Information($"       HasChildren: {item.HasChildren}");
            // CIVisibility.Log.Information($"       Tests.Count: {item.Tests?.Count}");
            // CIVisibility.Log.Information($"       Fixture: {item.Fixture}");
            // CIVisibility.Log.Information($"       Assembly.FullName: {item.Assembly?.GetName().FullName}");
            // CIVisibility.Log.Information($"       TestListener: {testListener}");
            // CIVisibility.Log.Information($"       TestFilter: {testFilter}");
            //
            // ShowITest(item);

            return CallTargetState.GetDefault();

            static void ShowITest(ITest item)
            {
                foreach (var childItem in item.Tests)
                {
                    if (childItem.TryDuckCast<ITest>(out var child))
                    {
                        CIVisibility.Log.Information($"       **************************************************");
                        CIVisibility.Log.Information($"         Id: {child.Id}");
                        CIVisibility.Log.Information($"         Name: {child.Name}");
                        CIVisibility.Log.Information($"         TestType: {child.TestType}");
                        CIVisibility.Log.Information($"         FullName: {child.FullName}");
                        CIVisibility.Log.Information($"         ClassName: {child.ClassName}");
                        CIVisibility.Log.Information($"         MethodName: {child.MethodName}");
                        CIVisibility.Log.Information($"         TypeInfo.FullName: {child.TypeInfo?.FullName}");
                        CIVisibility.Log.Information($"         TypeInfo.Namespace: {child.TypeInfo?.Namespace}");
                        CIVisibility.Log.Information($"         TypeInfo.Name: {child.TypeInfo?.Name}");
                        CIVisibility.Log.Information($"         TypeInfo.Type: {child.TypeInfo?.Type}");
                        CIVisibility.Log.Information($"         TypeInfo.Assembly: {child.TypeInfo?.Assembly?.GetName().FullName}");
                        CIVisibility.Log.Information($"         RunState: {child.RunState}");
                        CIVisibility.Log.Information($"         TestCaseCount: {child.TestCaseCount}");
                        CIVisibility.Log.Information($"         IsSuite: {child.IsSuite}");
                        CIVisibility.Log.Information($"         HasChildren: {child.HasChildren}");
                        CIVisibility.Log.Information($"         Tests.Count: {child.Tests?.Count}");
                        CIVisibility.Log.Information($"         Fixture: {child.Fixture}");
                        CIVisibility.Log.Information($"       **************************************************");
                        if (child.Tests?.Count > 0)
                        {
                            ShowITest(child);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">TestResult type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Original method return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>Return value of the method</returns>
        internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
        {
            // CIVisibility.Log.Information("*** NUnit.Framework.Api.NUnitTestAssemblyRunner() END");
            // CIVisibility.Log.Information($"       {instance}, {returnValue}");
            return new CallTargetReturn<TResult>(returnValue);
        }
    }
}
