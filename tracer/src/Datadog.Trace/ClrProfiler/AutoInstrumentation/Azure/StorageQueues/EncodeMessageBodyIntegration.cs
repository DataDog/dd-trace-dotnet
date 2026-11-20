// <copyright file="EncodeMessageBodyIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;

/// <summary>
/// Azure.Storage.Queues QueueMessageCodec.EncodeMessageBody calltarget instrumentation.
/// The send-side integration publishes the pathway context to inject; this seam is where the
/// message text is produced, so the injection rewrites the returned (encoded) text here.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Storage.Queues",
    TypeName = "Azure.Storage.Queues.QueueMessageCodec",
    MethodName = "EncodeMessageBody",
    ReturnTypeName = ClrNames.String,
    ParameterTypeNames = new[] { "System.BinaryData", "Azure.Storage.Queues.QueueMessageEncoding" },
    MinimumVersion = "12.5.0",
    MaximumVersion = "12.*.*",
    IntegrationName = AzureStorageQueuesCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EncodeMessageBodyIntegration
{
    private const int Base64Encoding = 1;

    internal static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TArg1 binaryData, TArg2 messageEncoding)
    {
        if (AzureStorageQueuesCommon.CurrentPendingPathwayContext is null)
        {
            return CallTargetState.GetDefault();
        }

        try
        {
            var encoding = Convert.ToInt32((object?)messageEncoding, System.Globalization.CultureInfo.InvariantCulture);
            return new CallTargetState(null, encoding);
        }
        catch
        {
            return CallTargetState.GetDefault();
        }
    }

    internal static CallTargetReturn<string> OnMethodEnd<TTarget>(TTarget instance, string? result, Exception? exception, in CallTargetState state)
    {
        if (exception is not null || result is null || state.State is not int encoding)
        {
            return new CallTargetReturn<string>(result);
        }

        var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
        if (!dataStreamsManager.IsEnabled || !Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureStorageQueues))
        {
            return new CallTargetReturn<string>(result);
        }

        var pathwayContext = AzureStorageQueuesCommon.CurrentPendingPathwayContext;
        if (pathwayContext is null)
        {
            return new CallTargetReturn<string>(result);
        }

        try
        {
            var messageText = encoding == Base64Encoding
                ? Encoding.UTF8.GetString(Convert.FromBase64String(result))
                : result;

            var injected = AzureStorageQueuesCommon.InjectPathwayContextIntoMessageText(dataStreamsManager, messageText, pathwayContext);
            if (injected is null || ReferenceEquals(injected, messageText))
            {
                return new CallTargetReturn<string>(result);
            }

            var encodedResult = encoding == Base64Encoding
                ? Convert.ToBase64String(Encoding.UTF8.GetBytes(injected))
                : injected;

            return new CallTargetReturn<string>(encodedResult);
        }
        catch
        {
            return new CallTargetReturn<string>(result);
        }
    }
}
