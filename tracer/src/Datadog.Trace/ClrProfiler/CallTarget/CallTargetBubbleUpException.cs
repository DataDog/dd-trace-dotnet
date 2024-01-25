// <copyright file="CallTargetBubbleUpException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Initializes a new instance of the <see cref="CallTargetBubbleUpException"/> class.
/// Any exception which wants to bypass the catch handler, thrown from an integration, should inherit from it.
/// </summary>
[System.ComponentModel.Browsable(false)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class CallTargetBubbleUpException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetBubbleUpException"/> class.
    /// </summary>
    public CallTargetBubbleUpException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetBubbleUpException"/> class.
    /// </summary>
    /// <param name="message">message</param>
    public CallTargetBubbleUpException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetBubbleUpException"/> class.
    /// </summary>
    /// <param name="message">message</param>
    /// <param name="inner">inner</param>
    public CallTargetBubbleUpException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetBubbleUpException"/> class.
    /// </summary>
    /// <param name="info">info</param>
    /// <param name="context">context</param>
    public CallTargetBubbleUpException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }

    /// <summary>
    /// To know if any inner exception is of type CallTargetBubbleUpException
    /// </summary>
    /// <param name="exception">exception</param>
    /// <returns>whether any child  is of type</returns>
    public static bool IsCallTargetBubbleUpException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is CallTargetBubbleUpException)
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
