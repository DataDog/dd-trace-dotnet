// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <Windows.h>


class INamedPipeHandler
{
public:
    virtual ~INamedPipeHandler() = default;

public:
    // Called if an error occurs while creating the named pipe endpoint
    virtual void OnStartError() = 0;

    // Called if an error occurs while connecting the named pipe endpoint
    virtual void OnConnectError() = 0;

    // Called when a client connects to a named pipe endpoint (from a thread pool thread)
    // The pipe handle will be closed when this method returns
    virtual void OnConnect(HANDLE hPipe) = 0;
};
