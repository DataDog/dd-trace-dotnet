// <copyright file="CachedMessageHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal static class CachedMessageHeadersHelper<TMarkerType>
    {
        private static readonly ActivatorHelper HeadersActivator;

        static CachedMessageHeadersHelper()
        {
            HeadersActivator = new ActivatorHelper(typeof(TMarkerType).Assembly.GetType("Confluent.Kafka.Headers"));
        }

        /// <summary>
        /// Creates a Confluent.Kafka.Headers object and assigns it to an `IMessage` proxy
        /// </summary>
        /// <returns>A proxy for the new Headers object</returns>
        public static IHeaders CreateHeaders()
        {
            var headers = HeadersActivator.CreateInstance();
            return headers.DuckCast<IHeaders>();
        }
    }
}
