// <copyright file="HttpWebRequest_GetRequestStreamV9_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest;

/// <summary>
/// CallTarget integration for HttpWebRequest.GetRequestStream
/// </summary>
[InstrumentMethod(
    AssemblyName = WebRequestCommon.NetCoreAssembly,
    TypeName = WebRequestCommon.HttpWebRequestTypeName,
    MethodName = "GetRequestStream",
    ReturnTypeName = ClrNames.Stream,
    MinimumVersion = "9",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = WebRequestCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class HttpWebRequest_GetRequestStreamV9_Integration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        if (WebRequestCommon.TryInjectHeaders(instance)
         && instance is HttpWebRequest { AllowWriteStreamBuffering: false } request)
        {
            // In .NET 9, if BeginGetRequestStream is called and write stream buffering
            // is not enabled, it _immediately_ initiates a request using the underlying
            // http client. Without additional work, we would end up creating two spans:
            // - 1 for the WebRequest, which covers the "full" request duration
            // - 1 for the HttpClient request, which covers the "initial" request
            // We avoid starting the second span by disabling tracing
            request.Headers[HttpHeaderNames.TracingEnabled] = "false";
            return new CallTargetState(scope: null, state: request.Headers);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<Stream?> OnMethodEnd<TTarget>(TTarget instance, Stream? returnValue, Exception? exception, in CallTargetState state)
    {
        if (state.State is WebHeaderCollection headers)
        {
            headers.Remove(HttpHeaderNames.TracingEnabled);
            // We save the start time here because we have no other way of getting it later
            // We've also already copied the headers to the underlying HttpClient, so adding it shouldn't
            // mean it gets sent either, it's just a storage area
            headers[HttpHeaderNames.InternalStartTime] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        }

        return new CallTargetReturn<Stream?>(returnValue);
    }
}
#endif
