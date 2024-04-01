// <copyright file="UserStringInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.Iast.Analyzers;

[StructLayout(LayoutKind.Sequential)]
internal struct UserStringInterop
{
    public IntPtr Location;
    public IntPtr Value;
}
