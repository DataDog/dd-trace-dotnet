// <copyright file="ICorProfilerCallback10.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerCallback10 : ICorProfilerCallback9
{
    public static new readonly Guid Guid = Guid.Parse("CEC5B60E-C69C-495F-87F6-84D28EE16FFB");

    // This event is triggered whenever an EventPipe event is configured to be delivered.
    //
    // Documentation Note: All pointers are only valid during the callback

    HResult EventPipeEventDelivered(
        IntPtr provider,
        int eventId,
        int eventVersion,
        uint cbMetadataBlob,
        byte* metadataBlob,
        uint cbEventData,
        byte* eventData,
        in Guid pActivityId,
        in Guid pRelatedActivityId,
        ThreadId eventThread,
        uint numStackFrames,
        nint* stackFrames)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback10.EventPipeEventDelivered");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeProviderCreated(IntPtr provider)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback10.EventPipeProviderCreated");
        return HResult.E_NOTIMPL;
    }
}
#endif
