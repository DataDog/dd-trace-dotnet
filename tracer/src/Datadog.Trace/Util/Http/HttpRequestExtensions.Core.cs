// <copyright file="HttpRequestExtensions.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util.Http.QueryStringObfuscation;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpRequestExtensions));

        internal static string GetUrl(this HttpRequest request, QueryStringManager queryStringManager = null)
        {
            var queryString = request.QueryString.Value;
            return HttpRequestUtils.GetUrl(
                request.Scheme,
                request.Host.Value,
                request.PathBase.ToUriComponent(),
                request.Path.ToUriComponent(),
                queryString,
                queryStringManager);
        }
    }
}
#endif
