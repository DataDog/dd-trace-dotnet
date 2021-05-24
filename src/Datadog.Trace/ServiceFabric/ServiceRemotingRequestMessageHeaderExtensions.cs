// <copyright file="ServiceRemotingRequestMessageHeaderExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;

namespace Datadog.Trace.ServiceFabric
{
    internal static class ServiceRemotingRequestMessageHeaderExtensions
    {
        public static bool TryAddHeader(this IServiceRemotingRequestMessageHeader headers, string headerName, string headerValue)
        {
            if (!headers.TryGetHeaderValue(headerName, out _))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(headerValue);
                headers.AddHeader(headerName, bytes);
                return true;
            }

            return false;
        }

        public static bool TryGetHeaderValueString(this IServiceRemotingRequestMessageHeader headers, string headerName, out string? headerValue)
        {
            if (headers.TryGetHeaderValue(headerName, out var bytes) && bytes is not null)
            {
                headerValue = Encoding.UTF8.GetString(bytes);
                return true;
            }

            headerValue = default;
            return false;
        }
    }
}
