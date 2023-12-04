#pragma once

#include "EventPipe/DiagnosticsClient.h"

class GcDumpSession
{
public:
    GcDumpSession(int pid);

    bool TriggerDump();
    inline GcDumpState* GetGcDumpState() { return &_gcDumpState; }

private:
    void Cleanup();

private:
    int _pid;
    DiagnosticsClient* _pClient;
    EventPipeSession* _pSession;
    HANDLE _hListenerThread;
    GcDumpState _gcDumpState;
};
