// <copyright file="LibDatadogException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents an exception thrown by the libdatadog library.
/// </summary>
internal class LibDatadogException : Exception
{
    public LibDatadogException(MaybeError maybeError)
        : base(maybeError.Message.ToString())
    {
    }
}
