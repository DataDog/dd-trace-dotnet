// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#ifdef _WINDOWS

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <tdh.h>
#include <thread>

#include "IService.h"
#include "shared/src/native-src/string.h"

class IClrEventsListener;


class EtwClrEventsReceiver : public IService
{
public:
    EtwClrEventsReceiver(uint32_t pid, uint64_t keywords, uint8_t verbosity, IClrEventsListener* pEventsListener);
    ~EtwClrEventsReceiver();

    // Inherited via IService
    virtual const char* GetName() override;
    virtual bool Start() override;
    virtual bool Stop() override;

private:
    void MainLoop(void);
    EVENT_TRACE_PROPERTIES* GetSessionProperties();
    static void CALLBACK OnEventReceived(PEVENT_RECORD rec);

private:
    static const GUID ClrProviderGuid;
    const char* _serviceName = "EtwClrEventsReceiver";
    const WCHAR* WorkerThreadName = WStr("DD.Profiler.EtwClrEventsReceiver.WorkerThread");

    static EtwClrEventsReceiver* _pThis;

    uint32_t _pid;
    uint64_t _keywords;
    uint8_t _verbosity;
    IClrEventsListener* _pEventsListener;
    std::thread* _pReceiverThread;
    TRACEHANDLE _hTrace;
    TRACEHANDLE _hParse;

    // these parameters need to be kept until StopTrace is called
    std::wstring _sessionName;
    std::unique_ptr<byte[]> _pProperties;
};

#endif