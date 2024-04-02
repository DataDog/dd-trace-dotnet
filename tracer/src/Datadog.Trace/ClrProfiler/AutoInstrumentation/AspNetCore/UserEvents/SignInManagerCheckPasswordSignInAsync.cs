// <copyright file="SignInManagerCheckPasswordSignInAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// CheckPasswordSignInAsync(TUser,System.String,System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Identity",
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "CheckPasswordSignInAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.SignInResult]",
    ParameterTypeNames = ["!0", ClrNames.String, ClrNames.Bool],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SignInManagerCheckPasswordSignInAsync
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TArg1">Type of the argument 1 (TUser)</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="user">Instance of TUser</param>
    /// <param name="password">Instance of System.String</param>
    /// <param name="lockoutOnFailure">Instance of System.Boolean</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 user, ref string password, ref bool lockoutOnFailure)
    {
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (Microsoft.AspNetCore.Identity.SignInResult)</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Instance of Microsoft.AspNetCore.Identity.SignInResult</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TReturn : ISignInResult
    {
        returnValue.Succeeded = false;
        return returnValue;
    }
}
