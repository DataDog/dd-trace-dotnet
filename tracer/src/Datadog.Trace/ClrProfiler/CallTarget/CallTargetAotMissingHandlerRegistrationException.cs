// <copyright file="CallTargetAotMissingHandlerRegistrationException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Signals that AOT mode is enabled but no generated handler registration exists for the requested integration binding.
/// </summary>
internal sealed class CallTargetAotMissingHandlerRegistrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotMissingHandlerRegistrationException"/> class.
    /// </summary>
    /// <param name="message">The descriptive error message.</param>
    public CallTargetAotMissingHandlerRegistrationException(string message)
        : base(message)
    {
    }
}
