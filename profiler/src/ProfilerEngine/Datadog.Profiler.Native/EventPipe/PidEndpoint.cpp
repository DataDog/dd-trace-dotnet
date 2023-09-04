#include <iostream>
#include <stdio.h>

#include "IIpcRecorder.h"
#include "PidEndpoint.h"


PidEndpoint::PidEndpoint(IIpcRecorder* pRecorder)
    : IpcEndpoint(pRecorder)
{
}

PidEndpoint* PidEndpoint::Create(int pid, IIpcRecorder* pRecorder)
{
    if (pid <= 0)
        return nullptr;

// TODO: implement the Linux version based on Domain Socket instead of named pipe

    return CreateForWindows(pid, pRecorder);

}

PidEndpoint* PidEndpoint::CreateForWindows(int pid, IIpcRecorder* pRecorder)
{
    PidEndpoint* pEndpoint = new PidEndpoint(pRecorder);

    // build the pipe name as described in the protocol
    wchar_t pszPipeName[256];
    // https://docs.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-createnamedpipew
    int nCharactersWritten = -1;
    nCharactersWritten = wsprintf(
        pszPipeName,
        L"\\\\.\\pipe\\dotnet-diagnostic-%d",
        pid
    );

    // check that CLR has created the diagnostics named pipe
    if (!::WaitNamedPipe(pszPipeName, 200))
    {
        auto error = ::GetLastError();
        std::cout << "Diagnostics named pipe is not available for process #" << pid << " (" << error << ")" << "\n";
        return nullptr;
    }

    // connect to the named pipe
    HANDLE hPipe;
    hPipe = ::CreateFile(
        pszPipeName,    // pipe name
        GENERIC_READ |  // read and write access
        GENERIC_WRITE,
        0,              // no sharing
        NULL,           // default security attributes
        OPEN_EXISTING,  // opens existing pipe
        0,              // default attributes
        NULL);          // no template file

    if (hPipe == INVALID_HANDLE_VALUE)
    {
        std::cout << "Impossible to connect to " << pszPipeName << "\n";
        return nullptr;
    }

    pEndpoint->_handle = hPipe;
    return pEndpoint;
}

bool PidEndpoint::Close()
{
    if (_handle != 0)
    {
        // TODO: check for Linux
        CloseForWindows();

        _handle = 0;
        return true;
    }

    return false;
}

void PidEndpoint::CloseForWindows()
{
    ::CloseHandle(_handle);
}
