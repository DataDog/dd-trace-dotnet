#pragma once

#include "EventPipe/DiagnosticsClient.h"

class GcDumpSession
{
public:
    GcDumpSession(int pid);

    bool TriggerDump();
    void StopDump(); // TODO: should be automatic but how to notify the caller that the "gcdump" is over?

private:
    int _pid;
    DiagnosticsClient* _pClient;
    EventPipeSession* _pSession;
    HANDLE _hListenerThread;
};
