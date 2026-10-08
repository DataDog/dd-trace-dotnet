// <copyright file="CallTargetInvocationKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The <c>CallTargetInvoker</c> entry points the native rewriter calls, each one served by a different handler.
/// </summary>
internal enum CallTargetInvocationKind
{
    /// <summary><c>BeginMethod&lt;TIntegration, TTarget, TArg1..TArgN&gt;</c> (by value or by reference): <c>BeginMethodHandler</c>.</summary>
    Begin,

    /// <summary><c>BeginMethod&lt;TIntegration, TTarget&gt;(TTarget, object[])</c>: <c>BeginMethodSlowHandler</c>.</summary>
    BeginSlow,

    /// <summary><c>EndMethod&lt;TIntegration, TTarget&gt;</c>: <c>EndMethodHandler&lt;TIntegration, TTarget&gt;</c>.</summary>
    EndVoid,

    /// <summary><c>EndMethod&lt;TIntegration, TTarget, TReturn&gt;</c>: <c>EndMethodHandler&lt;TIntegration, TTarget, TReturn&gt;</c> and its continuation generator.</summary>
    EndReturn,

    /// <summary><c>EndMethodRuntimeAsync&lt;TIntegration, TTarget, TDeclaredReturn&gt;</c>.</summary>
    EndRuntimeAsyncVoid,

    /// <summary><c>EndMethodRuntimeAsync&lt;TIntegration, TTarget, TReturn, TDeclaredReturn&gt;</c>.</summary>
    EndRuntimeAsyncReturn,
}
#endif
