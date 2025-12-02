// <copyright file="HttpWebRequest_GetRequestStream_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// CallTarget integration for HttpWebRequest.GetRequestStream
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.Stream,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetCoreAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.Stream,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = "8",
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HttpWebRequest_GetRequestStream_Integration
    {
        private const string MethodName = "GetRequestStream";

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            => WebRequestCommon.GetRequestStream_OnMethodBegin(instance);
    }
}
