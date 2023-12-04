#include "GcDumpSession.h"

GcDumpSession::GcDumpSession(int pid)
{
    _pid = pid;
    _pClient = nullptr;
    _hListenerThread = nullptr;
}

DWORD WINAPI ListenToGCDumpEvents(void* pParam)
{
    EventPipeSession* pSession = static_cast<EventPipeSession*>(pParam);

    pSession->Listen();

    return 0;
}

bool GcDumpSession::TriggerDump()
{
    if (_pClient != nullptr)
    {
        return false;
    }

    _pClient = DiagnosticsClient::Create(_pid, nullptr);
    if (_pClient == nullptr)
    {
        return false;
    }

    _gcDumpState.Clear();
    _pSession = _pClient->OpenEventPipeSession(
        &_gcDumpState,
        EventKeyword::gc | EventKeyword::gcheapcollect | EventKeyword::gcheapdump | EventKeyword::type | EventKeyword::gcheapandtypenames,
        EventVerbosityLevel::Verbose);
    if (_pSession == nullptr)
    {
        delete _pClient;
        return false;
    }

    DWORD tid = 0;
    _hListenerThread = ::CreateThread(nullptr, 0, ListenToGCDumpEvents, _pSession, 0, &tid);

    // wait for the first GC corresponding to the .gcdump
    // TODO: add timeout
    ::WaitForSingleObject(_hListenerThread, INFINITE);

    Cleanup();

    return true;
}

void GcDumpSession::Cleanup()
{
    if (_pSession == nullptr)
    {
        return;
    }

    _pSession->Stop();

    delete _pSession;
    _pSession = nullptr;

    ::CloseHandle(_hListenerThread);
    _hListenerThread = nullptr;

    delete _pClient;
    _pClient = nullptr;
}
