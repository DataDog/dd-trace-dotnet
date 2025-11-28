// <copyright file="HttpWebRequest_BeginGetRequestStream_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// CallTarget integration for HttpWebRequest.BeginGetRequestStream
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.IAsyncResult,
        ParameterTypeNames = new[] { ClrNames.AsyncCallback, ClrNames.Object },
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetCoreAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.IAsyncResult,
        ParameterTypeNames = new[] { ClrNames.AsyncCallback, ClrNames.Object },
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = "8",
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HttpWebRequest_BeginGetRequestStream_Integration
    {
        private const string MethodName = "BeginGetRequestStream";

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, AsyncCallback callback, object state)
            => WebRequestCommon.GetRequestStream_OnMethodBegin(instance);
    }
}
