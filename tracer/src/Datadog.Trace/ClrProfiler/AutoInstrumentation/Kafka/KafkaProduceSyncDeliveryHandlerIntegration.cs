// <copyright file="KafkaProduceSyncDeliveryHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Confluent.Kafka Producer.TypedDeliveryHandlerShim_Action.HandleDeliveryReport calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Confluent.Kafka",
        TypeName = "Confluent.Kafka.Producer`2+TypedDeliveryHandlerShim_Action",
        MethodName = ".ctor",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String, "!0", "!1", KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = "1.4.0",
        MaximumVersion = "2.*.*",
        IntegrationName = KafkaConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class KafkaProduceSyncDeliveryHandlerIntegration
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<KafkaProduceSyncDeliveryHandlerIntegration>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TKey">Type of the message key</typeparam>
        /// <typeparam name="TValue">Type of the message value</typeparam>
        /// <typeparam name="TActionOfDeliveryReport">Type of the delivery report</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="topic">The topic to which the message was sent</param>
        /// <param name="key">The message key value</param>
        /// <param name="value">The message value</param>
        /// <param name="handler">The delivery handler instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TKey, TValue, TActionOfDeliveryReport>(TTarget instance, string topic, TKey key, TValue value, TActionOfDeliveryReport handler)
        {
            if (handler is null)
            {
                // Handled in KafkaProduceSyncIntegration
                return CallTargetState.GetDefault();
            }

            try
            {
                // The current span should be started in KafkaProduceSyncIntegration.OnMethodBegin
                // The OnMethodBegin and OnMethodEnd of this integration happens between KafkaProduceSyncIntegration.OnMethodBegin
                // and KafkaProduceSyncIntegration.OnMethodEnd, so the consumer span is active for the duration of this integration
                var activeScope = Tracer.Instance?.InternalActiveScope;
                var span = activeScope?.Span;
                if (span is null)
                {
                    Logger.Warning("Unexpected null span for Kafka Producer with delivery handler");
                    return CallTargetState.GetDefault();
                }

                var newAction = CachedWrapperDelegate<TActionOfDeliveryReport>.CreateWrapper(handler, span);

                Action<ITypedDeliveryHandlerShimAction> updateHandlerAction = inst => inst.Handler = newAction;

                // store the call to update the handler variable as state
                // so we update it at the _end_ of the constructor
                return new CallTargetState(scope: activeScope, state: updateHandlerAction);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating wrapped delegate for delivery report");
                return CallTargetState.GetDefault();
            }
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            if (state.State is Action<ITypedDeliveryHandlerShimAction> updateHandlerAction
             && instance.TryDuckCast<ITypedDeliveryHandlerShimAction>(out var shim))
            {
                try
                {
                    updateHandlerAction.Invoke(shim);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "There was an error updating the delivery report handler to ");
                    // Not ideal to close the span here immediately, but as we can't trace the result,
                    // we don't really have a choice
                    state.Scope?.Span.Finish();
                }
            }

            return CallTargetReturn.GetDefault();
        }

        /// <summary>
        /// Helper method used by <see cref="CachedWrapperDelegate{TActionDelegate}"/> to create a delegate
        /// </summary>
        /// <param name="originalHandler">The original delivery report handler </param>
        /// <param name="span">A <see cref="Span"/> that can be manipulated when the action is invoked</param>
        /// <typeparam name="TDeliveryReport">Type of the delivery report</typeparam>
        /// <returns>The wrapped action</returns>
        internal static Action<TDeliveryReport> WrapAction<TDeliveryReport>(Action<TDeliveryReport> originalHandler, Span span)
        {
            return new Action<TDeliveryReport>(
                value =>
                {
                    if (span.Tags is KafkaTags tags && value.TryDuckCast<IDeliveryReport>(out var report))
                    {
                        var isError = report?.Error is not null && report.Error.IsError;
                        if (isError)
                        {
                            // Set the error tags manually, as we don't have an exception + stack trace here
                            // Should we create a stack trace manually?
                            var ex = new Exception(report.Error.ToString());
                            KafkaHelper.DisableHeadersIfUnsupportedBroker(ex); // "Unknown broker error" will be the message that disables it
                            span.SetException(ex);
                        }

                        if (report?.Partition is not null)
                        {
                            tags.Partition = report.Partition.ToString();
                        }

                        // Won't have offset if is error
                        if (!isError && report?.Offset is not null)
                        {
                            tags.Offset = report.Offset.ToString();
                        }

                        if (!isError)
                        {
                            var dataStreams = Tracer.Instance.TracerManager.DataStreamsManager;
                            dataStreams.TrackBacklog(
                                $"partition:{report.Partition.Value},topic:{report.Topic},type:kafka_produce",
                                report.Offset.Value);
                        }
                    }

                    // call previous delegate
                    try
                    {
                        originalHandler(value);
                    }
                    finally
                    {
                        span.Finish();
                    }
                });
        }

        /// <summary>
        /// Helper class for creating a <typeparamref name="TActionDelegate"/> that wraps an <see cref="Action{T}"/>,
        /// </summary>
        /// <typeparam name="TActionDelegate">Makes the assumption that TActionDelegate is an <see cref="Action{T}"/></typeparam>
        internal static class CachedWrapperDelegate<TActionDelegate>
        {
            private static readonly CreateWrapperDelegate _createWrapper;

            static CachedWrapperDelegate()
            {
                // This type makes the following assumption: TActionDelegate = Action<TParam> !

                // Get the Action<T> WrapHelper.WrapAction<T>(Action<T> value) methodinfo
                var wrapActionMethod = typeof(KafkaProduceSyncDeliveryHandlerIntegration)
                   .GetMethod(nameof(WrapAction), BindingFlags.NonPublic | BindingFlags.Static);

                // Create the generic method using the inner generic types of TActionDelegate => TParam
                wrapActionMethod = wrapActionMethod.MakeGenericMethod(typeof(TActionDelegate).GetGenericArguments());

                // With Action<TParam> WrapHelper.WrapAction<TParam>(Action<TParam> value) method info we create a delegate
                _createWrapper = (CreateWrapperDelegate)wrapActionMethod.CreateDelegate(typeof(CreateWrapperDelegate));
            }

            private delegate TActionDelegate CreateWrapperDelegate(TActionDelegate value, Span span);

            public static TActionDelegate CreateWrapper(TActionDelegate value, Span span)
            {
                // we call the delegate passing the instance of the previous delegate
                return _createWrapper(value, span);
            }
        }
    }
}
