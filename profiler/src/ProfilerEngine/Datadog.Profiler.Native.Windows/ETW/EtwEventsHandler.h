// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <memory>
#include <string>
#include <unordered_map>
#include <vector>
#include <windows.h>

#include "Protocol.h"
#include "INamedPipeHandler.h"
#include "IEtwEventsReceiver.h"
#include "IIpcLogger.h"

class EtwEventsHandler : public INamedPipeHandler
{
public:
    EtwEventsHandler();
    EtwEventsHandler(IIpcLogger* logger, IEtwEventsReceiver* pClrEventsReceiver);
    ~EtwEventsHandler();
    void Stop();

public:
// Inherited via INamedPipeHandler
    void OnStartError() override;
    void OnConnectError() override;
    void OnConnect(HANDLE hPipe) override;

private:
    bool ReadEvents(HANDLE hPipe, uint8_t* pBuffer, DWORD bufferSize, DWORD& readSize);
    void WriteSuccessResponse(HANDLE hPipe);

private:
    std::atomic<bool> _stopRequested = false;
    bool _showMessages;
    IEtwEventsReceiver* _pReceiver;
    IIpcLogger* _logger;
};