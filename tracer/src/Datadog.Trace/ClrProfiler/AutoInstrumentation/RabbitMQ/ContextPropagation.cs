// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    internal static class ContextPropagation
    {
        public static void HeadersSetter(IDictionary<string, object> carrier, string key, string value)
        {
            carrier[key] = Encoding.UTF8.GetBytes(value);
        }

        public static IEnumerable<string> HeadersGetter(IDictionary<string, object> carrier, string key)
        {
            if (carrier.TryGetValue(key, out object value) && value is byte[] bytes)
            {
                return new[] { Encoding.UTF8.GetString(bytes) };
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}
