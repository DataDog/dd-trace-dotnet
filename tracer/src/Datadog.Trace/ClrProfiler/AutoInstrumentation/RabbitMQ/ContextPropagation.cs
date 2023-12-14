// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    internal readonly struct ContextPropagation : ICarrierGetter<IDictionary<string, object>>, ICarrierSetter<IDictionary<string, object>>
    {
        public IEnumerable<string> Get(IDictionary<string, object> carrier, string key)
        {
            if (carrier.TryGetValue(key, out var value) && value is byte[] bytes)
            {
                return new[] { Encoding.UTF8.GetString(bytes) };
            }

            return Enumerable.Empty<string>();
        }

        public void Set(IDictionary<string, object> carrier, string key, string value)
        {
            carrier[key] = Encoding.UTF8.GetBytes(value);
        }
    }
}
