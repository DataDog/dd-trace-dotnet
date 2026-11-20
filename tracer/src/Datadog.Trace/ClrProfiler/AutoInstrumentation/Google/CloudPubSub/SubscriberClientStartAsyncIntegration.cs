// <copyright file="SubscriberClientStartAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    /// <summary>
    /// Google.Cloud.PubSub.V1.SubscriberClientImpl.StartAsync calltarget instrumentation.
    /// Records the subscription id against the handler instance so the per-message
    /// instrumentation can identify the subscription.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = GooglePubSubConstants.AssemblyName,
        TypeName = GooglePubSubConstants.SubscriberClientImplTypeName,
        MethodName = "StartAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { GooglePubSubConstants.SubscriptionHandlerTypeName },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = GooglePubSubConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SubscriberClientStartAsyncIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, THandler>(TTarget instance, THandler handler)
            where TTarget : ISubscriberClient, IDuckType
        {
            if (handler is not null)
            {
                GooglePubSubCommon.RememberSubscription(instance.Instance!, handler);
            }

            return CallTargetState.GetDefault();
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
        {
            return response;
        }
    }
}
