// <copyright file="NUnitWorkItemPerformWorkIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// NUnit.Framework.Internal.Execution.WorkItem.PerformWork() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "nunit.framework",
        TypeName = "NUnit.Framework.Internal.Execution.WorkItem",
        MethodName = "PerformWork",
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = NUnitIntegration.IntegrationName,
        CallTargetIntegrationType = IntegrationType.Derived)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NUnitWorkItemPerformWorkIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IWorkItem
        {
            if (!NUnitIntegration.IsEnabled)
            {
                return CallTargetState.GetDefault();
            }

            var item = instance.Test;

            if (item.TestType == "Assembly" && item.Instance.TryDuckCast<ITestAssembly>(out var itemAssembly))
            {
                var assemblyName = itemAssembly.Assembly?.GetName().Name ?? string.Empty;
                var frameworkVersion = item.Type.Assembly.GetName().Version?.ToString() ?? string.Empty;
                NUnitIntegration.SetTestModuleTo(item, TestModule.Create(assemblyName, "NUnit", frameworkVersion));
            }
            else if (item.TestType == "TestFixture" && NUnitIntegration.GetTestModuleFrom(item) is { } module)
            {
                NUnitIntegration.SetTestSuiteTo(item, TestSuite.Create(module, item.Name));
            }

            // TestType: Assembly
            // TestType: TestFixture
            // TestType: TestMethod

            // CIVisibility.Log.Information("+++ +++ +++ NUnit.Framework.Internal.Execution.WorkItem.PerformWork() BEGIN");
            // ShowITest(item);
            // if (item.Parent?.Instance is not null)
            // {
            //     ShowITest(item.Parent);
            // }

            // Check if the test should be skipped by the ITR
            if (instance.Test is { IsSuite: false, Method.MethodInfo: { } } currentTest && NUnitIntegration.ShouldSkip(currentTest))
            {
                var testMethod = currentTest.Method.MethodInfo;
                Common.Log.Debug("ITR: Test skipped: {class}.{name}", testMethod.DeclaringType?.FullName, testMethod.Name);
                currentTest.RunState = RunState.Ignored;
                currentTest.Properties.Set(NUnitIntegration.SkipReasonKey, "Skipped by the Intelligent Test Runner");
            }

            return CallTargetState.GetDefault();

            // static void ShowITest(ITest item)
            // {
            //     CIVisibility.Log.Information($"       **************************************************");
            //     CIVisibility.Log.Information($"         Id: {item.Id}");
            //     CIVisibility.Log.Information($"         Name: {item.Name}");
            //     CIVisibility.Log.Information($"         TestType: {item.TestType}");
            //     CIVisibility.Log.Information($"         FullName: {item.FullName}");
            //     CIVisibility.Log.Information($"         ClassName: {item.ClassName}");
            //     CIVisibility.Log.Information($"         MethodName: {item.MethodName}");
            //     CIVisibility.Log.Information($"         TypeInfo.FullName: {item.TypeInfo?.FullName}");
            //     CIVisibility.Log.Information($"         TypeInfo.Namespace: {item.TypeInfo?.Namespace}");
            //     CIVisibility.Log.Information($"         TypeInfo.Name: {item.TypeInfo?.Name}");
            //     CIVisibility.Log.Information($"         TypeInfo.Type: {item.TypeInfo?.Type}");
            //     CIVisibility.Log.Information($"         TypeInfo.Assembly: {item.TypeInfo?.Assembly?.GetName().FullName}");
            //     CIVisibility.Log.Information($"         RunState: {item.RunState}");
            //     CIVisibility.Log.Information($"         TestCaseCount: {item.TestCaseCount}");
            //     CIVisibility.Log.Information($"         IsSuite: {item.IsSuite}");
            //     CIVisibility.Log.Information($"         HasChildren: {item.HasChildren}");
            //     CIVisibility.Log.Information($"         Tests.Count: {item.Tests?.Count}");
            //     CIVisibility.Log.Information($"         Fixture: {item.Fixture}");
            //     CIVisibility.Log.Information($"       **************************************************");
            // }
        }
    }
}
