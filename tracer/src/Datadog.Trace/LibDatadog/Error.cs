// <copyright file="Error.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a generic error with message returned by the libdatadog library.
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/error.rs#L14
/// </summary>
/// <summary>
/// </summary>
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
