// <copyright file="RedisExecuteSyncDefaultIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// StackExchange.Redis.RedisBase.ExecuteSync[T] calltarget instrumentation
    /// Identical to RedisExecuteSyncIntegration except this one includes a default parameter at the end
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = ["StackExchange.Redis", "StackExchange.Redis.StrongName"],
        MethodName = "ExecuteSync",
        ReturnTypeName = "!!0",
        ParameterTypeNames = ["StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint",  "!!0"],
        MinimumVersion = "1.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = nameof(IntegrationId.StackExchangeRedis),
        TypeName = "StackExchange.Redis.RedisBase")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class RedisExecuteSyncDefaultIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TProcessor, TServerEndPoint, TDefault>(TTarget instance, TMessage message, TProcessor resultProcessor, TServerEndPoint serverEndPoint, TDefault defaultValue)
            where TTarget : IRedisBase
            where TMessage : IMessageData, IDuckType
        => RedisExecuteSyncIntegration.OnMethodBegin(instance, message, resultProcessor, serverEndPoint);

        internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
