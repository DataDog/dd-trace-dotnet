// <copyright file="ICorProfilerInfo5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo5 : ICorProfilerInfo4
{
    public static new readonly Guid Guid = new("07602928-CE38-4B83-81E7-74ADAF781214");

    /*
     * The code profiler calls GetEventMask2 to obtain the current event
     * categories for which it is to receive event notifications from the CLR
     *
     * *pdwEventsLow is a bitwise combination of values from COR_PRF_MONITOR
     * *pdwEventsHigh is a bitwise combination of values from COR_PRF_HIGH_MONITOR
     */
    HResult GetEventMask2(
        out CorPrfMonitor pdwEventsLow,
        out CorPrfHighMonitor pdwEventsHigh)
    {
        pdwEventsLow = default;
        pdwEventsHigh = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo5.GetEventMask2");
        return HResult.E_NOTIMPL;
    }

    /*
     * The code profiler calls SetEventMask2 to set the event categories for
     * which it is set to receive notification from the CLR.
     *
     * dwEventsLow is a bitwise combination of values from COR_PRF_MONITOR
     * dwEventsHigh is a bitwise combination of values from COR_PRF_HIGH_MONITOR
     */
    HResult SetEventMask2(
        CorPrfMonitor dwEventsLow,
        CorPrfHighMonitor dwEventsHigh)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo5.SetEventMask2");
        return HResult.E_NOTIMPL;
    }
}
#endif
