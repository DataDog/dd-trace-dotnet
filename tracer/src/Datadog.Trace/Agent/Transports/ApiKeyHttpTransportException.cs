// <copyright file="ApiKeyHttpTransportException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Agent.Transports;

internal sealed class ApiKeyHttpTransportException : InvalidOperationException
{
    public ApiKeyHttpTransportException(string message)
        : base(message)
    {
    }

    public ApiKeyHttpTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
