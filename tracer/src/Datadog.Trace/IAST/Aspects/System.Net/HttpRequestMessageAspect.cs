// <copyright file="HttpRequestMessageAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
/*
#if !NETFRAMEWORK
using System.Net.Http;
#else
using System.Reflection;
#endif
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

#nullable enable

namespace Datadog.Trace.IAST.Aspects.System.Net;

#pragma warning disable CS0618 // Type or member is obsolete
/// <summary> HttpRequestMessage class aspects </summary>
[AspectClass("System.Net.Http", AspectFilter.StringOptimization)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpRequestMessageAspect
{
#if !NETFRAMEWORK
    /// <summary>
    /// System.HttpRequestMessage::.ctor(System.Net.Http.HttpMethod,System.String) aspect.
    /// </summary>
    /// <param name="method">The method that will be used</param>
    /// <param name="uriText">A string that identifies the resource to be represented by the System.Uri instance.</param>
    /// <returns>The initialized System.Uri instance created using the specified URI string.</returns>
    [AspectCtorReplace("System.Net.Http.HttpRequestMessage::.ctor(System.Net.Http.HttpMethod,System.String)")]
    public static HttpRequestMessage Init(HttpMethod method, string uriText)
    {
        var result = new HttpRequestMessage(method, uriText);

        if (result.RequestUri is not null)
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.RequestUri, uriText);
        }

        return result;
    }
#else
    [AspectCtorReplace("System.Net.Http.HttpRequestMessage::.ctor(System.Net.Http.HttpMethod,System.String)")]
    public static object Init(object method, string uriText)
    {
        Type messagetype = Type.GetType("System.Net.Http.HttpRequestMessage");
        ConstructorInfo constructor = messagetype.GetConstructor(new[] { method.GetType(), typeof(string) });
        var result = constructor.Invoke(new object[] { method, uriText });
        var message = result as IHttpRequestMessage;

        if (message?.RequestUri is not null)
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(message.RequestUri, uriText);
        }

        return result;
    }
#endif
}
*/
