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
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations
{
    /// <summary>
    /// Calltarget instrumentation to generate a span for any arbitrary method
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "",
        TypeName = "",
        MethodName = "",
        ReturnTypeName = "",
        ParameterTypeNames = new string[0],
        MinimumVersion = "*",
        MaximumVersion = "*",
        IntegrationName = "TraceAnnotations")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TraceAnnotationsIntegration
    {
        private static readonly ConcurrentDictionary<RuntimeHandleTuple, Lazy<TraceAnnotationInfo>> InstrumentedMethodCache = new ConcurrentDictionary<RuntimeHandleTuple, Lazy<TraceAnnotationInfo>>();

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
                    key => new Lazy<TraceAnnotationInfo>(() => TraceAnnotationInfoFactory.Create(MethodBase.GetMethodFromHandle(key.MethodHandle, key.TypeHandle))))
                    .Value;

            var tags = new TraceAnnotationTags();
            var scope = Tracer.Instance.StartActiveInternal(info.OperationName, tags: tags);
            scope.Span.ResourceName = info.ResourceName;

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
            // If the return type is not Task or Task`1 then we can dispose the scope now, otherwise the scope will be disposed when OnAsyncMethodEnd is invoked
            if (returnValue is not Task)
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
