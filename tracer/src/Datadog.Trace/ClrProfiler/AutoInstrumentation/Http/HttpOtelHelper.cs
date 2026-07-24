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
        private static readonly System.Collections.Generic.HashSet<string> KnownMethods = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "TRACE", "PATCH", "CONNECT", "QUERY"
        };

        internal static string GetResourceNameMethod(string method)
        {
            if (StringUtil.IsNullOrEmpty(method))
            {
                return "HTTP";
            }

            var upper = method.ToUpperInvariant();
            return KnownMethods.Contains(upper) ? upper : "HTTP";
        }

        internal static void SetRequestMethod(ISpan span, string method)
        {
            if (StringUtil.IsNullOrEmpty(method))
            {
                return;
            }

            var upper = method.ToUpperInvariant();
            if (KnownMethods.Contains(upper))
            {
                span.SetTag("http.request.method", upper);
            }
            else
            {
                span.SetTag("http.request.method", "_OTHER");
                span.SetTag("http.request.method_original", method);
            }
        }

        internal static void SetClientUrl(ISpan span, string rawUrl)
        {
            if (StringUtil.IsNullOrEmpty(rawUrl))
            {
                return;
            }

            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                span.SetTag("url.scheme", uri.Scheme);

                // Redact credentials (username:password@host) before setting url.full
                if (!StringUtil.IsNullOrEmpty(uri.UserInfo))
                {
                    var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
                    span.SetTag("url.full", builder.Uri.ToString());
                }
                else
                {
                    span.SetTag("url.full", rawUrl);
                }

                if (span is Span internalSpan && uri.Port > 0 && !uri.IsDefaultPort)
                {
                    internalSpan.SetMetric("server.port", uri.Port);
                }
            }
            else
            {
                span.SetTag("url.full", rawUrl);
            }
        }

        internal static void SetServerUrl(ISpan span, string rawUrl)
        {
            if (StringUtil.IsNullOrEmpty(rawUrl))
            {
                return;
            }

            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                span.SetTag("url.scheme", uri.Scheme);
                span.SetTag("url.path", uri.AbsolutePath);
                if (!StringUtil.IsNullOrEmpty(uri.Query) && uri.Query.Length > 1)
                {
                    span.SetTag("url.query", uri.Query.Substring(1));
                }

                if (span is Span internalSpan && uri.Port > 0 && !uri.IsDefaultPort)
                {
                    internalSpan.SetMetric("server.port", uri.Port);
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
