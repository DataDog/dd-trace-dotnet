// <copyright file="Error.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.LibDatadog;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct Error
{
    internal readonly FFIVec Message;

    public Exception ToException()
    {
        var messageBytes = Message.ToByteArray();
        var message = Encoding.UTF8.GetString(messageBytes);
        return new Exception(message);
    }
}
