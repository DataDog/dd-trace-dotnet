// <copyright file="HttpWebRequestMessageProcessHttpResponseMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

/// <summary>
/// Amazon.Runtime.Internal.Transform.IWebResponseData Amazon.Runtime.HttpWebRequestMessage::ProcessHttpResponseMessage() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.Core",
    TypeName = "Amazon.Runtime.HttpWebRequestMessage",
    MethodName = "ProcessHttpResponseMessage",
    ReturnTypeName = "Amazon.Runtime.Internal.Transform.HttpClientResponseData",
    ParameterTypeNames = [ClrNames.HttpResponseMessage],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HttpWebRequestMessageProcessHttpResponseMessageIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TResponseMessage>(TTarget instance, TResponseMessage? responseMessage)
        where TTarget : IHttpWebRequestMessage, IDuckType
    {
        // V4 of the SDK uses a templating system to generate the request Uri and other data
        // so this is now the source of truth for the request data
        if (instance.Instance is not null
            && instance.RequestUri is not null
            && Tracer.Instance.InternalActiveScope?.Span.Tags is AwsSdkTags tags)
        {
            tags.HttpUrl = instance.RequestUri.GetLeftPart(UriPartial.Path);
        }

        return CallTargetState.GetDefault();
    }
}
