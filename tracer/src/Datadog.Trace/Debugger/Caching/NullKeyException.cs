// <copyright file="NullKeyException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Debugger.Caching;

internal sealed class NullKeyException : Exception
{
    internal static readonly NullKeyException Instance = new();

    private NullKeyException()
        : base(message: "Null key is not allowed")
    {
    }
}
