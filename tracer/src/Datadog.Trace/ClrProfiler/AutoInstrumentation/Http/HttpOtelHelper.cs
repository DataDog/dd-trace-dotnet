// <copyright file="HttpOtelHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http
{
    internal static class HttpOtelHelper
    {
        internal static void SetRequestMethod(ISpan span, string method)
            => span.SetTag("http.request.method", method);

        internal static void SetResponseStatusCode(ISpan span, int statusCode)
            => span.SetTag("http.response.status_code", statusCode.ToString());

        internal static void SetClientUrl(ISpan span, string rawUrl)
        {
            if (StringUtil.IsNullOrEmpty(rawUrl))
            {
                return;
            }

            span.SetTag("url.full", rawUrl);

            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                span.SetTag("url.scheme", uri.Scheme);
                span.SetTag("url.path", uri.AbsolutePath);
                if (!StringUtil.IsNullOrEmpty(uri.Query))
                {
                    span.SetTag("url.query", uri.Query);
                }

                if (uri.Port > 0)
                {
                    span.SetTag("server.port", uri.Port.ToString());
                }
            }
        }

        internal static void SetServerUrl(ISpan span, string rawUrl)
        {
            if (StringUtil.IsNullOrEmpty(rawUrl))
            {
                return;
            }

            span.SetTag("url.full", rawUrl);

            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                span.SetTag("url.scheme", uri.Scheme);
                span.SetTag("url.path", uri.AbsolutePath);
                if (!StringUtil.IsNullOrEmpty(uri.Query))
                {
                    span.SetTag("url.query", uri.Query);
                }

                if (uri.Port > 0)
                {
                    span.SetTag("server.port", uri.Port.ToString());
                }
            }
        }

        internal static void SetServerAddress(ISpan span, string host)
        {
            if (!StringUtil.IsNullOrEmpty(host))
            {
                span.SetTag("server.address", host);
            }
        }

        internal static void SetUserAgent(ISpan span, string ua)
        {
            if (!StringUtil.IsNullOrEmpty(ua))
            {
                span.SetTag("user_agent.original", ua);
            }
        }

        internal static void SetClientAddress(ISpan span, string ip)
        {
            if (!StringUtil.IsNullOrEmpty(ip))
            {
                span.SetTag("client.address", ip);
            }
        }

        internal static void SetNetworkPeerAddress(ISpan span, string ip)
        {
            if (!StringUtil.IsNullOrEmpty(ip))
            {
                span.SetTag("network.peer.address", ip);
            }
        }
    }
}
