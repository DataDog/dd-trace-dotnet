#pragma once

#include "EventPipe/DiagnosticsClient.h"
#include "Windows.h"

class GcDump
{
public:
    GcDump(int pid);
    ~GcDump();

    bool TriggerDump();
    inline const GcDumpState& GetGcDumpState() { return _gcDumpState; }

private:
    void Cleanup();
    static DWORD WINAPI ListenToGCDumpEvents(void* pParam);

private:
    int _pid;
    DiagnosticsClient* _pClient;
    EventPipeSession* _pSession;
    HANDLE _hListenerThread;
    GcDumpState _gcDumpState;
};
