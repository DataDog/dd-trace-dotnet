// <copyright file="ICorProfilerCallback11.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerCallback11 : ICorProfilerCallback10
{
    public static new readonly Guid Guid = Guid.Parse("42350846-AAED-47F7-B128-FD0C98881CDE");

    HResult LoadAsNotificationOnly(out int pbNotificationOnly)
    {
        pbNotificationOnly = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback11.LoadAsNotificationOnly");
        return HResult.E_NOTIMPL;
    }
}
#endif
