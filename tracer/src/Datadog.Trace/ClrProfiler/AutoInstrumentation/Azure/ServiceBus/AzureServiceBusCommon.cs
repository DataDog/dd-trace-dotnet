// <copyright file="AzureServiceBusCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Datadog.Trace.DataStreamsMonitoring.Utils;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    internal class AzureServiceBusCommon
    {
        private static readonly ConditionalWeakTable<object, object?> ApplicationPropertiesToMessageMap = new();

        internal static readonly AsyncLocal<IDictionary<string, object>?> ActiveMessageProperties = new();

        public static void SetMessage(object applicationProperties, object? message)
        {
#if NETCOREAPP3_1_OR_GREATER
            ApplicationPropertiesToMessageMap.AddOrUpdate(applicationProperties, message);
#else
            ApplicationPropertiesToMessageMap.GetValue(applicationProperties, x => message);
#endif
        }

        public static bool TryGetMessage(object applicationProperties, out object? message)
            => ApplicationPropertiesToMessageMap.TryGetValue(applicationProperties, out message);

        internal static long GetMessageSize<T>(T message)
            where T : IServiceBusMessage
        {
            if (message.Instance is null)
            {
                return 0;
            }

            long size = message.Body.ToMemory().Length;

            if (message.ApplicationProperties is null)
            {
                return size;
            }

            foreach (var pair in message.ApplicationProperties)
            {
                size += Encoding.UTF8.GetByteCount(pair.Key);
                size += MessageSizeHelper.TryGetSize(pair.Value);
            }

            return size;
        }
    }
}
