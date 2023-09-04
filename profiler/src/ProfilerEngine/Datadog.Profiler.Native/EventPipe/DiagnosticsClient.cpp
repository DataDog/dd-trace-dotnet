#include "DiagnosticsClient.h"
#include "PidEndpoint.h"
#include "RecordedEndpoint.h"
#include "FileRecorder.h"

DiagnosticsClient* DiagnosticsClient::Create(int pid, const wchar_t* recordingFilename)
{
    FileRecorder* pRecorder = nullptr;
    if (recordingFilename != nullptr)
        pRecorder = new FileRecorder(recordingFilename);

    IIpcEndpoint* pEndpoint = PidEndpoint::Create(pid, pRecorder);
    if (pEndpoint == nullptr)
        return nullptr;

    return new DiagnosticsClient(pid, pEndpoint);
}

DiagnosticsClient* DiagnosticsClient::Create(const wchar_t* recordFilename, const wchar_t* recordingFilename)
{
    IIpcEndpoint* pEndpoint = RecordedEndpoint::Create(recordFilename);
    if (pEndpoint == nullptr)
        return nullptr;

    FileRecorder* pRecorder = nullptr;
    if (recordingFilename != nullptr)
        pRecorder = new FileRecorder(recordingFilename);

    return new DiagnosticsClient(-1, pEndpoint);
}


DiagnosticsClient::DiagnosticsClient(int pid, IIpcEndpoint* pEndpoint)
{
    _pid = pid;
    _pEndpoint = pEndpoint;
}

DiagnosticsClient::~DiagnosticsClient()
{
    if (_pEndpoint != nullptr)
    {
        _pEndpoint->Close();
        _pEndpoint = nullptr;
    }
}


bool DiagnosticsClient::GetProcessInfo(ProcessInfoRequest& request)
{
    return (request.Process(_pEndpoint));
}


EventPipeSession* DiagnosticsClient::OpenEventPipeSession(uint64_t keywords, EventVerbosityLevel verbosity)
{
    EventPipeStartRequest request;
    if (!request.Process(_pEndpoint, keywords, verbosity))
        return nullptr;

    auto session = new EventPipeSession(_pid, _pEndpoint, request.SessionId);
    return session;
}

bool DiagnosticsClient::StopEventPipeSession(uint64_t sessionId)
{
    EventPipeStopRequest request;
    return request.Process(_pEndpoint, sessionId);
}
