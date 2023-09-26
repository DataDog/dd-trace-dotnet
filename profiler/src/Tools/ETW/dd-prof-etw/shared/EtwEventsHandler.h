// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <memory>
#include <string>
#include <windows.h>

#include "INamedPipeHandler.h"


class EtwEventsHandler : public INamedPipeHandler
{
public:
    EtwEventsHandler();
    void Stop();

public:
// Inherited via INamedPipeHandler
    void OnStartError() override;
    void OnConnectError() override;
    void OnConnect(HANDLE hPipe) override;

private:
    bool ReadEvents(HANDLE hPipe, uint8_t* pBuffer, DWORD bufferSize, DWORD& readSize);

private:
    std::atomic<bool> _stopRequested = false;
};