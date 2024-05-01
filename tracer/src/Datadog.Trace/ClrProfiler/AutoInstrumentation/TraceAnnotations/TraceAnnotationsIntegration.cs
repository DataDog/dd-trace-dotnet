// <copyright file="TraceAnnotationsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations
{
    /// <summary>
    /// Calltarget instrumentation to generate a span for any arbitrary method
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TraceAnnotationsIntegration
    {
        internal static readonly IntegrationId IntegrationId = IntegrationId.TraceAnnotations;
        private static readonly ConcurrentDictionary<RuntimeHandleTuple, TraceAnnotationInfo> InstrumentedMethodCache = new ConcurrentDictionary<RuntimeHandleTuple, TraceAnnotationInfo>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="methodHandle">The RuntimeMethodHandle representing the instrumented method</param>
        /// <param name="typeHandle">The RuntimeTypeHandle representing the instrumented method's owning type</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle)
        {
            var info = InstrumentedMethodCache.GetOrAdd(
                new RuntimeHandleTuple(methodHandle, typeHandle),
                key => TraceAnnotationInfoFactory.Create(MethodBase.GetMethodFromHandle(key.MethodHandle, key.TypeHandle)));

            var tags = new TraceAnnotationTags();
            var scope = Tracer.Instance.StartActiveInternal(info.OperationName, tags: tags);
            scope.Span.ResourceName = info.ResourceName;
            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            // If the return type is Task/Task<T>/ValueTask/ValueTask<TResult>, defer the scope disposal. Otherwise, dispose it now since the async callback will not be invoked
#if NETCOREAPP3_1_OR_GREATER
            bool closeAsync = returnValue is Task
                              || returnValue is ValueTask
                              || (returnValue?.GetType() is Type returnType && returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
#else
            bool closeAsync = returnValue is Task;
#endif

            if (!closeAsync)
            {
                state.Scope.DisposeWithException(exception);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return valuevalue</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
