// <copyright file="GetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq
{
    /// <summary>
    /// IBM MQ Put calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = IbmMqConstants.IbmMqAssemblyName,
        TypeName = IbmMqConstants.MqDestinationTypeName,
        MethodName = "Get",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = [IbmMqConstants.MqMessageTypeName, IbmMqConstants.MqMessageGetOptionsTypeName, ClrNames.Int32],
        MinimumVersion = "9.0.0",
        MaximumVersion = "9.*.*",
        IntegrationName = IbmMqConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class GetIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TOptions>(TTarget instance, TMessage msg, TOptions options, int maxSize)
        {
            return new CallTargetState(null, msg, DateTimeOffset.UtcNow);
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
            where TTarget : IMqQueue, IDuckType
        {
            if (instance.Instance != null)
            {
                if (state.State != null && state.State.TryDuckCast<IMqMessage>(out var msg))
                {
                    var scope = IbmMqHelper.CreateConsumerScope(Tracer.Instance, state.StartTime, instance, msg);
                    var dataStreams = Tracer.Instance.TracerManager.DataStreamsManager;
                    if (dataStreams.IsEnabled && exception == null && scope != null)
                    {
                        var adapter = IbmMqHelper.GetHeadersAdapter(msg);
                        PathwayContext? pathwayContext = null;
                        try
                        {
                            pathwayContext = dataStreams.ExtractPathwayContextAsBase64String(adapter);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error extracting PathwayContext from IbmMq message");
                        }

                        var edgeTags = new[] { "direction:in", $"topic:{instance.Name}", $"type:{IbmMqConstants.QueueType}" };

                        scope.Span.SetDataStreamsCheckpoint(
                            dataStreams,
                            CheckpointKind.Consume,
                            edgeTags,
                            msg.MessageLength,
                            timeInQueueMs: 0,
                            pathwayContext);
                        // we need to inject new context, since message objects can theoretically be reused
                        // hence we need to make sure the parent hash changes properly
                        dataStreams.InjectPathwayContextAsBase64String(scope.Span.Context.PathwayContext, IbmMqHelper.GetHeadersAdapter(msg));
                    }

                    scope.DisposeWithException(exception);
                }
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
