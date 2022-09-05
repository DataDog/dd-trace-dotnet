// <copyright file="HttpExceptionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Net.Sockets;

namespace Datadog.Trace.Util.Http;

internal static class HttpExceptionExtensions
{
    public static bool IsSocketException(this Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SocketException)
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
