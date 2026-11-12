// <copyright file="ICorProfilerInfo12.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo12 : ICorProfilerInfo11
{
    public static new readonly Guid Guid = new("27b24ccd-1cb1-47c5-96ee-98190dc30959");

    HResult EventPipeStartSession(
        uint cProviderConfigs,
        COR_PRF_EVENTPIPE_PROVIDER_CONFIG* pProviderConfigs,
        int requestRundown,
        out EVENTPIPE_SESSION pSession)
    {
        pSession = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeStartSession");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeAddProviderToSession(
        EVENTPIPE_SESSION session,
        COR_PRF_EVENTPIPE_PROVIDER_CONFIG providerConfig)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeAddProviderToSession");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeStopSession(
        EVENTPIPE_SESSION session)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeStopSession");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeCreateProvider(
                char* providerName,
                out EVENTPIPE_PROVIDER pProvider)
    {
        pProvider = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeCreateProvider");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeGetProviderInfo(
                EVENTPIPE_PROVIDER provider,
                uint cchName,
                out uint pcchName,
                char* providerName)
    {
        pcchName = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeGetProviderInfo");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeDefineEvent(
                EVENTPIPE_PROVIDER provider,
                char* eventName,
                uint eventID,
                ulong keywords,
                uint eventVersion,
                uint level,
                byte opcode,
                int needStack,
                uint cParamDescs,
                COR_PRF_EVENTPIPE_PARAM_DESC* pParamDescs,
                out EVENTPIPE_EVENT pEvent)
    {
        pEvent = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeDefineEvent");
        return HResult.E_NOTIMPL;
    }

    HResult EventPipeWriteEvent(
                EVENTPIPE_EVENT @event,
                uint cData,
                COR_PRF_EVENT_DATA* data,
                in Guid pActivityId,
                in Guid pRelatedActivityId)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo12.EventPipeWriteEvent");
        return HResult.E_NOTIMPL;
    }
}
#endif
