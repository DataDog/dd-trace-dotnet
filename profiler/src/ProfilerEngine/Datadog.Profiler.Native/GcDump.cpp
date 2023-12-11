#include "GcDump.h"

GcDump::GcDump(int pid)
{
    _pid = pid;
    _pClient = nullptr;
    _pSession = nullptr;
    _hListenerThread = nullptr;
}

GcDump::~GcDump()
{
    std::cout << "GcDump::~GcDump()" << std::endl;

    Cleanup();
}

DWORD WINAPI GcDump::ListenToGCDumpEvents(void* pParam)
{
    GcDump* pThis = static_cast<GcDump*>(pParam);
    pThis->_pSession->Listen();

    return 0;
}

bool GcDump::TriggerDump()
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
        _pClient = nullptr;
        return false;
    }

    DWORD tid = 0;
    _hListenerThread = ::CreateThread(nullptr, 0, ListenToGCDumpEvents, this, 0, &tid);

    // wait for the end of the first GC corresponding to the .gcdump
    ::WaitForSingleObject(_gcDumpState._hEventStop, INFINITE);

    // TODO: check that stopping in the same thread is safe
    Cleanup();

    return true;
}

void GcDump::Cleanup()
{
    if (_pSession == nullptr)
    {
        return;
    }

    _pSession->Stop();

    delete _pSession;
    _pSession = nullptr;

    ::WaitForSingleObject(_hListenerThread, INFINITE);
    ::CloseHandle(_hListenerThread);
    _hListenerThread = nullptr;

    delete _pClient;
    _pClient = nullptr;
}
