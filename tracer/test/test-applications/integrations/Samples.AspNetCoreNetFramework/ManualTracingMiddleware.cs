// <copyright file="ManualTracingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Http;

namespace Samples.AspNetCoreNetFramework
{
    public class ManualTracingMiddleware
    {
        private const string ManualPath = "/manual/mongo";
        private static readonly SpanContextExtractor ContextExtractor = new SpanContextExtractor();

        private readonly RequestDelegate _next;

        public ManualTracingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Path.Equals(ManualPath))
            {
                await _next(context);
                return;
            }

            var parent = ContextExtractor.Extract(context.Request.Headers, GetHeaderValues);
            var settings = new SpanCreationSettings { Parent = parent };

            using (var scope = Tracer.Instance.StartActive("aspnet_core.request", settings))
            {
                scope.Span.ResourceName = context.Request.Method + " " + context.Request.Path;
                scope.Span.SetTag("span.kind", "server");
                scope.Span.SetTag("component", "aspnet_core");
                scope.Span.SetTag("http.method", context.Request.Method);

                try
                {
                    await _next(context);
                    scope.Span.SetTag("http.status_code", context.Response.StatusCode.ToString(CultureInfo.InvariantCulture));
                }
                catch (Exception exception)
                {
                    scope.Span.SetException(exception);
                    throw;
                }
            }
        }

        private static IEnumerable<string> GetHeaderValues(IHeaderDictionary headers, string name)
        {
            return headers.TryGetValue(name, out var values)
                       ? values
                       : Enumerable.Empty<string>();
        }
    }
}
