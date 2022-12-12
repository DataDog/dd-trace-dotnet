// <copyright file="CallTargetBubbleUpException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget;

internal class CallTargetBubbleUpException : Exception
{
    internal CallTargetBubbleUpException()
    {
    }

    internal CallTargetBubbleUpException(string message)
        : base(message)
    {
    }

    internal CallTargetBubbleUpException(string message, Exception inner)
        : base(message, inner)
    {
    }

    internal CallTargetBubbleUpException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}
