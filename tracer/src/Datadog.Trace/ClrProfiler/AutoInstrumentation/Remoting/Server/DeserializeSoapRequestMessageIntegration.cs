// <copyright file="DeserializeSoapRequestMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Server
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.CoreChannel.DeserializeSoapRequestMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.CoreChannel",
        MethodName = "DeserializeSoapRequestMessage",
        ReturnTypeName = "System.Runtime.Remoting.Messaging.IMessage",
        ParameterTypeNames = new[] { "System.IO.Stream", "System.Runtime.Remoting.Messaging.Header[]", ClrNames.Bool, "System.Runtime.Serialization.Formatters.TypeFilterLevel" },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class DeserializeSoapRequestMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DeserializeSoapRequestMessageIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <typeparam name="THeaderArray">Type of the remoting header array</typeparam>
        /// <typeparam name="TEnum">Type of the security level enum</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="inputStream">The stream for the incoming request message</param>
        /// <param name="headerArray">An array of remoting headers</param>
        /// <param name="strictBinding">A flag indicating whether the binding is strict</param>
        /// <param name="securityLevel">An enum for the security level</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, THeaderArray, TEnum>(TTarget instance, Stream inputStream, THeaderArray headerArray, bool strictBinding, TEnum securityLevel)
        {
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">The returned IMessage instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            // Do nothing if the IMessage is still null.
            // An exception will be thrown by the remoting infrastructure anyways
            if (returnValue is IMessage requestMsg
                && StateHelper.ActiveRequestHeaders.Value is ITransportHeaders requestHeaders)
            {
                // Extract span context
                SpanContext? propagatedContext = null;
                try
                {
                    propagatedContext = SpanContextPropagator.Instance.Extract(requestHeaders, (headers, key) =>
                    {
                        var value = headers[key];
                        return value is null ?
                            Array.Empty<string>() :
                            new string[] { value.ToString() };
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated headers.");
                }

                // Start the server span and leave it open for the *SerializeResponseIntegration to close
                _ = RemotingIntegration.CreateServerScope(requestMsg, propagatedContext);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
