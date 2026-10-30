// <copyright file="VoidResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog.ServiceDiscovery;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Libdatadog interop mapping of ddog_VoidResult: an operation which may fail.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VoidResult
{
    public ResultTag Tag;

    public Error Error;

    public bool IsOk => Tag == ResultTag.Ok;

    /// <summary>
    /// Releases the error, if any, and throws it as <see cref="LibDatadogException"/>.
    /// </summary>
    public void ThrowIfError()
    {
        if (IsOk)
        {
            return;
        }

        var exception = Error.ToException();
        NativeInterop.Common.DropError(ref Error);
        throw exception;
    }
}
