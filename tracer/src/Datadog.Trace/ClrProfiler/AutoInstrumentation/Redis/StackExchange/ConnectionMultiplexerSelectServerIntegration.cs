// <copyright file="ConnectionMultiplexerSelectServerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// StackExchange.Redis.ConnectionMultiplexer.ExecuteAsyncImpl calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "StackExchange.Redis",
        TypeName = "StackExchange.Redis.ConnectionMultiplexer",
        MethodName = "SelectServer",
        ReturnTypeName = "StackExchange.Redis.ServerEndPoint",
        ParameterTypeNames = new[] { "StackExchange.Redis.Message" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = StackExchangeRedisHelper.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ConnectionMultiplexerSelectServerIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessage">Type of the message</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">Message instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message)
            where TTarget : IConnectionMultiplexer
            where TMessage : IMessageData
        {
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">Type of the result</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="result">Result instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A result value</returns>
        internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult result, Exception exception, in CallTargetState state)
        {
            var scope = GetActiveRedisScope(Tracer.Instance);

            if (scope != null)
            {
                if (result != null && scope.Span.Tags is RedisTags tags)
                {
                    var hostAndPort = result.ToString()!.Split(':');

                    tags.Host = hostAndPort[0];

                    if (tags.Host.Length > 1)
                    {
                        tags.Port = hostAndPort[1];
                    }
                }
            }

            return new CallTargetReturn<TResult>(result);
        }

        private static Scope GetActiveRedisScope(Tracer tracer)
        {
            var scope = tracer.InternalActiveScope;
            var parent = scope?.Span;

            if (parent is { Type: SpanTypes.Redis }
              && parent.GetTag(Tags.InstrumentationName) == StackExchangeRedisHelper.IntegrationName)
            {
                return scope;
            }

            return null;
        }
    }
}
