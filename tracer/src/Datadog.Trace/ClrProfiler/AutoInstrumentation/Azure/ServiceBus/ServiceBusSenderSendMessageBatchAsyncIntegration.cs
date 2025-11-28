// <copyright file="ServiceBusSenderSendMessageBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// System.Threading.Tasks.Task Azure.Messaging.ServiceBus.ServiceBusSender::SendMessagesAsync(Azure.Messaging.ServiceBus.ServiceBusMessageBatch,System.Threading.CancellationToken) calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
        MethodName = "SendMessagesAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = ["Azure.Messaging.ServiceBus.ServiceBusMessageBatch", ClrNames.CancellationToken],
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ServiceBusSenderSendMessageBatchAsyncIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TBatch>(TTarget instance, TBatch messageBatch, CancellationToken cancellationToken)
            where TTarget : IServiceBusSender, IDuckType
            where TBatch : IServiceBusMessageBatch, IDuckType
        {
            var spanLinks = BatchSpanContextStorage.ExtractSpanContexts(messageBatch?.Instance);
            return AzureServiceBusCommon.CreateSenderSpan(instance, "send", messageCount: messageBatch?.Count, spanLinks: spanLinks);
        }

        internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return returnValue;
        }
    }
}
