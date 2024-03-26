// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Web;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        internal static string GetUrlForSpan(this IHttpRequestMessage request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(request.RequestUri, queryStringManager);

        internal static string GetUrlForSpan(this HttpRequestBase request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(request.Url, queryStringManager);

        internal static string GetUrlForSpan(this HttpRequest request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(request.Url, queryStringManager);
    }
}
#endif
