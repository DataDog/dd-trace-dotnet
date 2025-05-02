// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <memory>
#include <string>

#include <windows.h>

#include "INamedPipeHandler.h"
#include "IIpcLogger.h"


class IpcServer
{
public:
    IpcServer();
    IpcServer(IIpcLogger* pLogger,
              const std::string& portName,
              INamedPipeHandler* pHandler,
              uint32_t inBufferSize,
              uint32_t outBufferSize,
              uint32_t maxInstances,
              uint32_t timeoutMS);
    ~IpcServer();

    // TODO: remove this method and use Start instead in the sample application
    static IpcServer* StartAsync(
        IIpcLogger* pLogger,
        const std::string& portName,
        INamedPipeHandler* pHandler,
        uint32_t inBufferSize,
        uint32_t outBufferSize,
        uint32_t maxInstances = PIPE_UNLIMITED_INSTANCES,
        uint32_t timeoutMS = NMPWAIT_USE_DEFAULT_WAIT);
    void WaitForNamedPipe(DWORD timeoutMS);
    void Stop();

private:
    static void CALLBACK StartCallback(PTP_CALLBACK_INSTANCE instance, PVOID context);
    void ShowLastError(const char* message, uint32_t lastError = ::GetLastError());

private:
    bool _showMessages;
    std::string _portName;
    uint32_t _inBufferSize;
    uint32_t _outBufferSize;
    uint32_t _maxInstances;
    uint32_t _timeoutMS;
    INamedPipeHandler* _pHandler;
    IIpcLogger* _pLogger;
    HANDLE _hNamedPipe;

    // will be set when the server is initialized
    HANDLE _hInitializedEvent;

    std::atomic<bool> _stopRequested = false;
};

struct ServerInfo
{
public:
    IpcServer* pThis;
    HANDLE hPipe;
};