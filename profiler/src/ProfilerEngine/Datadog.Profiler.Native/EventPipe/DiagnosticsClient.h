#pragma once

#include "IIpcEndpoint.h"
#include "EventPipeSession.h"
#include "DiagnosticsProtocol.h"

// This class is used to send and process ONE request: create one instance per command.
// For example, listening to CLR events requires one instance to start the session
// and one instance to stop it.
// Don't reuse an instance after a command has been set
class DiagnosticsClient
{
public:
    static DiagnosticsClient* Create(int pid, const wchar_t* recordingFilename);
    static DiagnosticsClient* Create(const wchar_t* recordFilename, const wchar_t* recordingFilename);
    ~DiagnosticsClient();

    // Expose the available commands from the protocol
    //

    // PROCESS
    bool GetProcessInfo(ProcessInfoRequest& request);

    // EVENTPIPE
    // The Stop command to cancel the receiving of CLR events (hence returning from EventPipeSession::Listen())
    //
    EventPipeSession* OpenEventPipeSession(uint64_t keywords, EventVerbosityLevel verbosity);
    bool StopEventPipeSession(uint64_t sessionId);

    // DUMP
    // PROFILE
    // COUNTER
    //
private:
    DiagnosticsClient(int pid, IIpcEndpoint* pEndpoint);

private:
    int _pid;
    IIpcEndpoint* _pEndpoint;
};

