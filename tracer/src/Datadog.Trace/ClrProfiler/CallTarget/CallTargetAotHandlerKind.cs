// <copyright file="CallTargetAotHandlerKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Identifies the CallTarget handler family represented by an AOT registration entry.
/// </summary>
internal enum CallTargetAotHandlerKind
{
    /// <summary>
    /// A begin handler that receives only the instance.
    /// </summary>
    Begin0,

    /// <summary>
    /// A begin handler that receives the instance and one argument.
    /// </summary>
    Begin1,

    /// <summary>
    /// A begin handler that receives the instance and two arguments.
    /// </summary>
    Begin2,

    /// <summary>
    /// A begin handler that receives the instance and three arguments.
    /// </summary>
    Begin3,

    /// <summary>
    /// A begin handler that receives the instance and four arguments.
    /// </summary>
    Begin4,

    /// <summary>
    /// A begin handler that receives the instance and five arguments.
    /// </summary>
    Begin5,

    /// <summary>
    /// A begin handler that receives the instance and six arguments.
    /// </summary>
    Begin6,

    /// <summary>
    /// A begin handler that receives the instance and seven arguments.
    /// </summary>
    Begin7,

    /// <summary>
    /// A begin handler that receives the instance and eight arguments.
    /// </summary>
    Begin8,

    /// <summary>
    /// A slow begin handler that receives the instance and an object-array copy of the target arguments.
    /// </summary>
    BeginSlow,

    /// <summary>
    /// An end handler for void-returning target methods.
    /// </summary>
    EndVoid,

    /// <summary>
    /// An end handler for value-returning target methods.
    /// </summary>
    EndReturn,

    /// <summary>
    /// An async-end continuation handler for Task or ValueTask target methods without a typed result.
    /// </summary>
    AsyncEndObject,

    /// <summary>
    /// An async-end continuation handler for Task{TResult} or ValueTask{TResult} target methods.
    /// </summary>
    AsyncEndResult,

    /// <summary>
    /// A typed task-return continuation wrapper for Task{TResult} target methods.
    /// </summary>
    AsyncReturnTaskResult,
}
