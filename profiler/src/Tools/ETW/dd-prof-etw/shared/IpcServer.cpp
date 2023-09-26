// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IpcClient.h"  // TODO: return codes should be defined in another shared header file
#include "IpcServer.h"
#include "DebugHelpers.h"
#include <iostream>
#include <memory>

IpcServer::IpcServer()
{
}

IpcServer::IpcServer(const std::string& portName,
                     INamedPipeHandler* pHandler,
                     uint32_t inBufferSize,
                     uint32_t outBufferSize,
                     uint32_t maxInstances,
                     uint32_t timeoutMS)
{
    _portName = portName;
    _inBufferSize = inBufferSize;
    _outBufferSize = outBufferSize;
    _maxInstances = maxInstances;
    _timeoutMS = timeoutMS;
    _pHandler = pHandler;

    _serverCount = 0;
}

std::unique_ptr<IpcServer> IpcServer::StartAsync(
    const std::string& portName,
    INamedPipeHandler* pHandler,
    uint32_t inBufferSize,
    uint32_t outBufferSize,
    uint32_t maxInstances,
    uint32_t timeoutMS
    )
{
    if (pHandler == nullptr)
    {
        return nullptr;
    }

    auto server = std::make_unique<IpcServer>(
        portName, pHandler, inBufferSize, outBufferSize, maxInstances, timeoutMS
        );

    // let a threadpool thread process the command; allowing the server to process more incoming commands
    if (!::TrySubmitThreadpoolCallback(StartCallback, (PVOID)server.get(), nullptr))
    {
        ShowLastError("Impossible to add the Start callback into the threadpool...");
        return nullptr;
    }

    return server;
 }

void IpcServer::Stop()
{
    _stopRequested.store(true);
}

void CALLBACK IpcServer::StartCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    IpcServer* pThis = reinterpret_cast<IpcServer*>(context);

    // TODO: there is no timeout on ConnectNamedPipe
    // so we would need to use the overlapped version to support _stopRequested :^(
    while (!pThis->_stopRequested.load())
    {
        HANDLE hNamedPipe =
            ::CreateNamedPipeA(
                pThis->_portName.c_str(),
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                pThis->_maxInstances,
                pThis->_outBufferSize,
                pThis->_inBufferSize,
                0,
                nullptr
                );

        pThis->_serverCount++;

        if (hNamedPipe == INVALID_HANDLE_VALUE)
        {
            ShowLastError("Failed to create named pipe...");
#ifdef _DEBUG
            std::cout << "--> for server #" << pThis->_serverCount << "...\n";
#endif

            pThis->_pHandler->OnStartError();
            return;
        }

#ifdef _DEBUG
        std::cout << "Listening to server #" << pThis->_serverCount << "...\n";
#endif

        if (!::ConnectNamedPipe(hNamedPipe, nullptr) && ::GetLastError() != ERROR_PIPE_CONNECTED)
        {
            ShowLastError("ConnectNamedPipe failed...");

            pThis->_pHandler->OnConnectError();
            return;
        }

        // TODO: how to use smart pointers in that case?
        auto pServerInfo = new ServerInfo();
        pServerInfo->pThis = pThis;
        pServerInfo->hPipe = hNamedPipe;

        // let a threadpool thread process the read/write communication; allowing the server to process more incoming connections
        if (!::TrySubmitThreadpoolCallback(ConnectCallback, pServerInfo, nullptr))
        {
            ShowLastError("Impossible to add the Connect callback into the threadpool...");

            pThis->_pHandler->OnStartError();
            return;
        }
    }
}

void CALLBACK IpcServer::ConnectCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    ServerInfo* pInfo = reinterpret_cast<ServerInfo*>(context);
    IpcServer* pThis = pInfo->pThis;
    HANDLE hPipe = pInfo->hPipe;
    delete pInfo;

    // this is a blocking call until the communication ends on this named pipe
    pThis->_pHandler->OnConnect(hPipe);

    // cleanup
    ::DisconnectNamedPipe(hPipe);
    ::CloseHandle(hPipe);
}