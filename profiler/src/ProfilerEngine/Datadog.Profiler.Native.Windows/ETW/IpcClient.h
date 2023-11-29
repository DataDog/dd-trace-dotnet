// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <string>
#include <windows.h>

#include "IIpcLogger.h"


// TODO: move it to a file shared between clients and servers
enum NamedPipesCode : uint32_t
{
    Success         = ERROR_SUCCESS,            //    0
    Broken          = ERROR_BROKEN_PIPE,        //  109
    NotConnected    = ERROR_PIPE_NOT_CONNECTED, //  233
    MoreData        = ERROR_MORE_DATA,          //  234
    MissingData     = 1024,                     // 1024
};


class IpcClient
{
public:
    static std::unique_ptr<IpcClient> Connect(IIpcLogger* pLogger, const std::string& portName, uint32_t timeoutMS = NMPWAIT_USE_DEFAULT_WAIT);
    IpcClient(IIpcLogger* pLogger, HANDLE hPipe);

    uint32_t Send(PVOID pBuffer, uint32_t bufferSize);
    uint32_t Read(PVOID pBuffer, uint32_t bufferSize);
    bool Disconnect();
    ~IpcClient();

private:
    IpcClient();
    uint32_t ShowLastError(const char* message, uint32_t lastError = ::GetLastError());
    static HANDLE GetEndPoint(IIpcLogger* pLogger, const std::string& portName, uint16_t timeoutMS);

private:
    HANDLE _hPipe;
    IIpcLogger* _pLogger;
};

