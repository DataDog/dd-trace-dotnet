// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Threading;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http.QueryStringObfuscation;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpRequestExtensions));

        internal static string GetUrl(this IHttpRequestMessage request, QueryStringManager queryStringManager) => HttpRequestUtils.GetUrl(request.RequestUri.Scheme, request.RequestUri.Host, string.Empty, request.RequestUri.AbsolutePath, request.RequestUri.Query, queryStringManager);

        internal static string GetUrl(this HttpRequestBase request, QueryStringManager queryStringManager) => HttpRequestUtils.GetUrl(request.Url.Scheme, request.Url.Host, string.Empty, request.Url.AbsolutePath, request.Url.Query, queryStringManager);

        internal static string GetUrl(this HttpRequest request, QueryStringManager queryStringManager) => HttpRequestUtils.GetUrl(request.Url.Scheme, request.Url.Host, string.Empty, request.Url.AbsolutePath, request.Url.Query, queryStringManager);
    }
}
#endif
