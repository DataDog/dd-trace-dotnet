// <copyright file="IDuckCopy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// DuckCopy interface
/// </summary>
/// <typeparam name="T">Type of the duckcopy proxy</typeparam>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDuckCopy<T>
{
    /// <summary>
    /// Emit an instance of the duckcopy proxy
    /// </summary>
    /// <returns>Duckcopy proxy</returns>
    public T DuckCopy();
}
