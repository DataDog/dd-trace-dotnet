// <copyright file="StackFrameInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;
using Datadog.Trace.Vendors.MessagePack.Decoders;

namespace Datadog.Trace.Iast;

internal readonly struct StackFrameInfo
{
    public StackFrameInfo(StackFrame? stackFrame, bool isValid)
    {
        StackFrame = stackFrame;
        IsValid = isValid;
    }

    public StackFrame? StackFrame { get; }

    public bool IsValid { get; }
}
