// <copyright file="SyncProcessMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Client
{
    /// <summary>
    /// System.Runtime.Remoting.Messaging.IMessageSink.SyncProcessMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.BinaryClientFormatterSink",
        MethodName = "SyncProcessMessage",
        ReturnTypeName = "System.Runtime.Remoting.Messaging.IMessage",
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Messaging.IMessage", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.SoapClientFormatterSink",
        MethodName = "SyncProcessMessage",
        ReturnTypeName = "System.Runtime.Remoting.Messaging.IMessage",
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Messaging.IMessage", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class SyncProcessMessageIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="msg">Message instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IMessage msg)
        {
            var scope = RemotingIntegration.CreateClientScope(msg);
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
