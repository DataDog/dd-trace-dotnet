// <copyright file="CallTargetAotHandlerKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Identifies a concrete AOT-generated CallTarget binding using runtime type handles for the integration,
/// instrumented target, optional return type, and up to eight argument types.
/// </summary>
internal readonly struct CallTargetAotHandlerKey : IEquatable<CallTargetAotHandlerKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotHandlerKey"/> struct.
    /// </summary>
    /// <param name="kind">The handler family represented by this key.</param>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The instrumented target type handle.</param>
    /// <param name="returnType">The return type handle used by non-void end handlers.</param>
    /// <param name="arg1">The first argument type handle.</param>
    /// <param name="arg2">The second argument type handle.</param>
    /// <param name="arg3">The third argument type handle.</param>
    /// <param name="arg4">The fourth argument type handle.</param>
    /// <param name="arg5">The fifth argument type handle.</param>
    /// <param name="arg6">The sixth argument type handle.</param>
    /// <param name="arg7">The seventh argument type handle.</param>
    /// <param name="arg8">The eighth argument type handle.</param>
    private CallTargetAotHandlerKey(
        CallTargetAotHandlerKind kind,
        RuntimeTypeHandle integrationType,
        RuntimeTypeHandle targetType,
        RuntimeTypeHandle returnType,
        RuntimeTypeHandle arg1,
        RuntimeTypeHandle arg2,
        RuntimeTypeHandle arg3,
        RuntimeTypeHandle arg4,
        RuntimeTypeHandle arg5,
        RuntimeTypeHandle arg6,
        RuntimeTypeHandle arg7,
        RuntimeTypeHandle arg8)
    {
        Kind = kind;
        IntegrationType = integrationType;
        TargetType = targetType;
        ReturnType = returnType;
        Arg1 = arg1;
        Arg2 = arg2;
        Arg3 = arg3;
        Arg4 = arg4;
        Arg5 = arg5;
        Arg6 = arg6;
        Arg7 = arg7;
        Arg8 = arg8;
    }

    /// <summary>
    /// Gets the handler family represented by this key.
    /// </summary>
    public CallTargetAotHandlerKind Kind { get; }

    /// <summary>
    /// Gets the integration type handle.
    /// </summary>
    public RuntimeTypeHandle IntegrationType { get; }

    /// <summary>
    /// Gets the instrumented target type handle.
    /// </summary>
    public RuntimeTypeHandle TargetType { get; }

    /// <summary>
    /// Gets the return type handle used by value-returning end handlers.
    /// </summary>
    public RuntimeTypeHandle ReturnType { get; }

    /// <summary>
    /// Gets the first argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg1 { get; }

    /// <summary>
    /// Gets the second argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg2 { get; }

    /// <summary>
    /// Gets the third argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg3 { get; }

    /// <summary>
    /// Gets the fourth argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg4 { get; }

    /// <summary>
    /// Gets the fifth argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg5 { get; }

    /// <summary>
    /// Gets the sixth argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg6 { get; }

    /// <summary>
    /// Gets the seventh argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg7 { get; }

    /// <summary>
    /// Gets the eighth argument type handle.
    /// </summary>
    public RuntimeTypeHandle Arg8 { get; }

    /// <summary>
    /// Creates a begin-handler key for the supplied binding and argument types.
    /// </summary>
    /// <param name="kind">The begin handler family.</param>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <param name="argumentTypes">The optional argument type handles.</param>
    /// <returns>The constructed begin-handler key.</returns>
    public static CallTargetAotHandlerKey CreateBegin(CallTargetAotHandlerKind kind, RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType, RuntimeTypeHandle[] argumentTypes)
    {
        return new CallTargetAotHandlerKey(
            kind,
            integrationType,
            targetType,
            default,
            GetArgument(argumentTypes, 0),
            GetArgument(argumentTypes, 1),
            GetArgument(argumentTypes, 2),
            GetArgument(argumentTypes, 3),
            GetArgument(argumentTypes, 4),
            GetArgument(argumentTypes, 5),
            GetArgument(argumentTypes, 6),
            GetArgument(argumentTypes, 7));
    }

    /// <summary>
    /// Creates a slow begin-handler key for the supplied binding.
    /// </summary>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <returns>The constructed slow begin-handler key.</returns>
    public static CallTargetAotHandlerKey CreateBeginSlow(RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType)
    {
        return new CallTargetAotHandlerKey(CallTargetAotHandlerKind.BeginSlow, integrationType, targetType, default, default, default, default, default, default, default, default, default);
    }

    /// <summary>
    /// Creates a void-end-handler key for the supplied binding.
    /// </summary>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <returns>The constructed void-end-handler key.</returns>
    public static CallTargetAotHandlerKey CreateEndVoid(RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType)
    {
        return new CallTargetAotHandlerKey(CallTargetAotHandlerKind.EndVoid, integrationType, targetType, default, default, default, default, default, default, default, default, default);
    }

    /// <summary>
    /// Creates a value-returning end-handler key for the supplied binding and return type.
    /// </summary>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <param name="returnType">The return type handle.</param>
    /// <returns>The constructed value-end-handler key.</returns>
    public static CallTargetAotHandlerKey CreateEndReturn(RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType, RuntimeTypeHandle returnType)
    {
        return new CallTargetAotHandlerKey(CallTargetAotHandlerKind.EndReturn, integrationType, targetType, returnType, default, default, default, default, default, default, default, default);
    }

    /// <summary>
    /// Creates an async-end-handler key for Task or ValueTask target methods that do not expose a typed result value.
    /// </summary>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <returns>The constructed async object-result handler key.</returns>
    public static CallTargetAotHandlerKey CreateAsyncEndObject(RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType)
    {
        return new CallTargetAotHandlerKey(CallTargetAotHandlerKind.AsyncEndObject, integrationType, targetType, default, default, default, default, default, default, default, default, default);
    }

    /// <summary>
    /// Creates an async-end-handler key for Task{TResult} or ValueTask{TResult} target methods.
    /// </summary>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <param name="resultType">The async result type handle.</param>
    /// <returns>The constructed async typed-result handler key.</returns>
    public static CallTargetAotHandlerKey CreateAsyncEndResult(RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType, RuntimeTypeHandle resultType)
    {
        return new CallTargetAotHandlerKey(CallTargetAotHandlerKind.AsyncEndResult, integrationType, targetType, resultType, default, default, default, default, default, default, default, default);
    }

    /// <summary>
    /// Creates a typed task-return continuation key for Task{TResult} target methods.
    /// </summary>
    /// <param name="integrationType">The integration type handle.</param>
    /// <param name="targetType">The target type handle.</param>
    /// <param name="returnType">The full task return type handle.</param>
    /// <returns>The constructed typed task-return continuation key.</returns>
    public static CallTargetAotHandlerKey CreateAsyncReturnTaskResult(RuntimeTypeHandle integrationType, RuntimeTypeHandle targetType, RuntimeTypeHandle returnType)
    {
        return new CallTargetAotHandlerKey(CallTargetAotHandlerKind.AsyncReturnTaskResult, integrationType, targetType, returnType, default, default, default, default, default, default, default, default);
    }

    /// <inheritdoc />
    public bool Equals(CallTargetAotHandlerKey other)
    {
        return Kind == other.Kind &&
               IntegrationType.Equals(other.IntegrationType) &&
               TargetType.Equals(other.TargetType) &&
               ReturnType.Equals(other.ReturnType) &&
               Arg1.Equals(other.Arg1) &&
               Arg2.Equals(other.Arg2) &&
               Arg3.Equals(other.Arg3) &&
               Arg4.Equals(other.Arg4) &&
               Arg5.Equals(other.Arg5) &&
               Arg6.Equals(other.Arg6) &&
               Arg7.Equals(other.Arg7) &&
               Arg8.Equals(other.Arg8);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is CallTargetAotHandlerKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)Kind;
            hashCode = (hashCode * 397) ^ IntegrationType.GetHashCode();
            hashCode = (hashCode * 397) ^ TargetType.GetHashCode();
            hashCode = (hashCode * 397) ^ ReturnType.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg1.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg2.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg3.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg4.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg5.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg6.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg7.GetHashCode();
            hashCode = (hashCode * 397) ^ Arg8.GetHashCode();
            return hashCode;
        }
    }

    /// <summary>
    /// Returns the argument handle at the requested index when present; otherwise the default handle value.
    /// </summary>
    /// <param name="argumentTypes">The provided argument handles.</param>
    /// <param name="index">The argument index to read.</param>
    /// <returns>The argument handle at the requested index or the default handle value.</returns>
    private static RuntimeTypeHandle GetArgument(RuntimeTypeHandle[] argumentTypes, int index)
    {
        return argumentTypes.Length > index ? argumentTypes[index] : default;
    }
}
