﻿// <copyright file="AssemblyResolverCtorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;

/// <summary>
/// System.Void Microsoft.VisualStudio.TestPlatform.Common.Utilities.AssemblyResolver::.ctor(System.Collections.Generic.IEnumerable`1[System.String]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.Common",
    TypeName = "Microsoft.VisualStudio.TestPlatform.Common.Utilities.AssemblyResolver",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[System.String]"],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class AssemblyResolverCtorIntegration
{
    private const string IntegrationName = "TestPlatformAssemblyResolver";

    private static readonly List<Task> LstTasks = new();

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TArg1">Type of the argument 1 (System.Collections.Generic.IEnumerable`1[System.String])</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="directories">Instance of System.Collections.Generic.IEnumerable`1[System.String]</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 directories)
    {
        Common.Log.Debug("Microsoft.VisualStudio.TestPlatform.Common.Utilities.AssemblyResolver.ctor started.");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (LstTasks)
        {
            LstTasks.Add(tcs.Task);
        }

        return new CallTargetState(null, tcs);
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A return value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        if (state.State is TaskCompletionSource<bool> tcs)
        {
            tcs.TrySetResult(exception is null);
            Common.Log.Debug("Microsoft.VisualStudio.TestPlatform.Common.Utilities.AssemblyResolver.ctor finished.");
        }

        return CallTargetReturn.GetDefault();
    }

    internal static Task WaitForCallToBeCompletedAsync()
    {
        lock (LstTasks)
        {
            var lstTasks = LstTasks;
            return lstTasks.Count switch
            {
                0 => Task.CompletedTask,
                1 => lstTasks[0],
                _ => Task.WhenAll(LstTasks)
            };
        }
    }
}
