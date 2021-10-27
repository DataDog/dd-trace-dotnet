// <copyright file="Log4NetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AdoNet
{
    /// <summary>
    /// Instrumentation wrappers for log4net.Util.AppenderAttachedImpl.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class Log4NetIntegration
    {
        private const string Major1 = "1";
        private const string Major2 = "2";

        private const string Log4NetAssemblyName = "log4net";
        private const string AppenderAttachedImplTypeName = "log4net.Util.AppenderAttachedImpl";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Log4NetIntegration));

        /// <summary>
        /// Instrumentation wrapper for log4net.Util.AppenderAttachedImpl.AppendLoopOnAppenders.
        /// </summary>
        /// <param name="impl">The object referenced by this in the instrumented method.</param>
        /// <param name="loggingEvent">The logging event passed to the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = Log4NetAssemblyName,
            TargetMethod = "AppendLoopOnAppenders",
            TargetType = AppenderAttachedImplTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32, "log4net.Core.LoggingEvent" },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static int AppendLoopOnAppenders(
            object impl,
            object loggingEvent,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = nameof(AppendLoopOnAppenders);
            Func<object, object, int> instrumentedMethod;

            try
            {
                var targetType = impl.GetInstrumentedType(AppenderAttachedImplTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<object, object, int>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(loggingEvent)
                       .WithNamespaceAndNameFilters(ClrNames.Int32, "log4net.Core.LoggingEvent")
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: AppenderAttachedImplTypeName,
                    methodName: methodName,
                    instanceType: impl.GetType().AssemblyQualifiedName);
                throw;
            }

            var tracer = Tracer.Instance;

            if (tracer.Settings.LogsInjectionEnabled &&
                loggingEvent is not null &&
                loggingEvent.TryDuckCast<LoggingEventStruct>(out var loggingEventStruct) &&
                !loggingEventStruct.Properties.Contains(CorrelationIdentifier.ServiceKey))
            {
                loggingEventStruct.Properties[CorrelationIdentifier.ServiceKey] = tracer.DefaultServiceName ?? string.Empty;
                loggingEventStruct.Properties[CorrelationIdentifier.VersionKey] = tracer.Settings.ServiceVersion ?? string.Empty;
                loggingEventStruct.Properties[CorrelationIdentifier.EnvKey] = tracer.Settings.Environment ?? string.Empty;

                var span = tracer.ActiveScope?.Span;
                if (span is not null)
                {
                    loggingEventStruct.Properties[CorrelationIdentifier.TraceIdKey] = span.TraceId;
                    loggingEventStruct.Properties[CorrelationIdentifier.SpanIdKey] = span.SpanId;
                }
            }

            return instrumentedMethod(impl, loggingEvent);
        }

        /*
         * Ducktyping types
         */

        /// <summary>
        /// LoggingEvent struct for duck typing
        /// </summary>
        [DuckCopy]
        public struct LoggingEventStruct
        {
            /// <summary>
            /// Gets the properties of the logging event
            /// </summary>
            public IDictionary Properties;
        }
    }
}
