// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;

internal static class ContextPropagation
{
    private const int MaxClientContextLength = 3583;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagation));

    internal static void Inject(Tracer tracer, IInvokeRequest request, SpanContext spanContext)
    {
        try
        {
            var encoded = request.ClientContextBase64;
            JObject context;
            if (StringUtil.IsNullOrEmpty(encoded))
            {
                context = new JObject();
            }
            else
            {
                if (encoded.Length > MaxClientContextLength)
                {
                    return;
                }

                var bytes = Convert.FromBase64String(encoded);
                using var reader = new JsonTextReader(new StringReader(EncodingHelpers.Utf8NoBom.GetString(bytes, 0, bytes.Length)))
                {
                    DateParseHandling = DateParseHandling.None,
                };
                context = JObject.Load(reader);
                while (reader.Read())
                {
                    // Validate that the original context contains no trailing content.
                }
            }

            var customToken = context["custom"];
            if (customToken is not null && customToken.Type != JTokenType.Null && customToken is not JObject)
            {
                return;
            }

            var custom = customToken as JObject ?? new JObject();
            tracer.TracerManager.SpanContextPropagator.Inject(new PropagationContext(spanContext, Baggage.Current), custom, default(CarrierSetter));
            context["custom"] = custom;

            var json = JsonHelper.TokenToString(context, Formatting.None);
            var updated = Convert.ToBase64String(EncodingHelpers.Utf8NoBom.GetBytes(json));
            if (updated.Length <= MaxClientContextLength)
            {
                request.ClientContextBase64 = updated;
            }
        }
        catch (Exception)
        {
            // Context is customer data: leave it unchanged and do not include it in logs.
            Log.Debug("Unable to inject trace context into Lambda ClientContext.");
        }
    }

    private readonly struct CarrierSetter : ICarrierSetter<JObject>
    {
        public void Set(JObject carrier, string key, string value) => carrier[key] = value;
    }
}
