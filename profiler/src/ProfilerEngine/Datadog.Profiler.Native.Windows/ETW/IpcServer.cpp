// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IpcClient.h"  // TODO: return codes should be defined in another shared header file
#include "IpcServer.h"
#include "..\SecurityDescriptorHelpers.h"

#include <iostream>
#include <sstream>
#include <memory>


IpcServer::IpcServer()
{
    _showMessages = false;
    _pHandler = nullptr;
    _stopRequested.store(false);
    _pLogger = nullptr;
    _hNamedPipe = nullptr;

    _hInitializedEvent = ::CreateEvent(nullptr, true, false, nullptr);
}

IpcServer::~IpcServer()
{
    Stop();

    ::CloseHandle(_hInitializedEvent);
    _hInitializedEvent = nullptr;
}

void IpcServer::Stop()
{
    // Stop could be called when error and in the destructor
    auto alreadyStopped = _stopRequested.exchange(true);
    if (alreadyStopped)
    {
        return;
    }

    // we also need to close the handle to the named pipe the server is listing to in order to unblock the ConnectNamedPipe() call
    // and allow the server to really stop
    if (_hNamedPipe != nullptr)
    {
        // connecting to the server pipe will unblock the ConnectNamedPipe() call
        HANDLE hPipe = ::CreateFileA(
            _portName.c_str(),
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH,
            nullptr);
        if (hPipe != INVALID_HANDLE_VALUE)
        {
            ::CloseHandle(hPipe);
        }

        // cleanup the server pipe if needed (could be already closed in StartCallback())
        if (_hNamedPipe != nullptr)
        {
            ::DisconnectNamedPipe(_hNamedPipe);
            ::CloseHandle(_hNamedPipe);
            _hNamedPipe = nullptr;
        }
    }
}

void IpcServer::WaitForNamedPipe(DWORD timeoutMS)
{
    if (_hInitializedEvent == nullptr)
    {
        return;
    }

    // returns 0 if the event is signaled and WAIT_TIMEOUT if the timeout is reached (and the event is not signaled)
    ::WaitForSingleObject(_hInitializedEvent, timeoutMS);
}

IpcServer::IpcServer(IIpcLogger* pLogger,
                     const std::string& portName,
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
    _pLogger = pLogger;
    _hNamedPipe = nullptr;
    _showMessages = false;

    // will be set when the named pipe is initialized
    _hInitializedEvent = ::CreateEvent(nullptr, true, false, nullptr);
}

IpcServer* IpcServer::StartAsync(
    IIpcLogger* pLogger,
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

    // the lifetime of this instance is the lifetime of the application (i.e. it won't be deleted to avoid random crashes)
    auto server = new IpcServer(
        pLogger, portName, pHandler, inBufferSize, outBufferSize, maxInstances, timeoutMS
        );

    // let a threadpool thread process the command because there is a blocking call to ConnectNamedPipe()
    if (!::TrySubmitThreadpoolCallback(StartCallback, server, nullptr))
    {
        server->ShowLastError("Impossible to add the Start callback into the threadpool...");
        return nullptr;
    }

    // wait until the server has created the named pipe so the Agent will be able to connect
    server->WaitForNamedPipe(200);

    // wait a bit more for the blocking ConnectNamedPipe call is made to ensure that the Agent will be able to connect
    ::Sleep(100);

    return server;
 }


// We've seen crashes that might indicate that the profiler was shut down and this callback was still running.
 // So we need to check if the IpcServer has not been stopped before trying to use it.
 void CALLBACK IpcServer::StartCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    IpcServer* pThis = reinterpret_cast<IpcServer*>(context);

    // There is no timeout on ConnectNamedPipe()
    // so we would need to use the overlapped version to support _stopRequested :^(
    // Instead, the server will stop when the named pipe is closed
    std::string errorMessage;
    auto emptySA = MakeNoSecurityAttributes(errorMessage);
    if (emptySA == nullptr)
    {
        pThis->ShowLastError("Failed to create the empty Dacl...");
        pThis->_pHandler->OnStartError();
        return;
    }


    // It is possible that an error occurred when trying to connect to the Agent
    // in that case, no need to start to listen to the pipe
    // The pipe will be cleaned up in Stop()
    if (pThis->_stopRequested.load())
    {
        return;
    }

    pThis->_hNamedPipe =
        ::CreateNamedPipeA(
            pThis->_portName.c_str(),
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            pThis->_maxInstances,
            pThis->_outBufferSize,
            pThis->_inBufferSize,
            pThis->_timeoutMS,
            emptySA.get()
            );

    if (pThis->_hNamedPipe == INVALID_HANDLE_VALUE)
    {
        pThis->ShowLastError("Failed to create named pipe...");
        if (pThis->_pLogger != nullptr)
        {
            std::stringstream builder;
            builder << "--> for server...";
            pThis->_pLogger->Error(builder.str());
        }

        pThis->_pHandler->OnStartError();
        ::SetEvent(pThis->_hInitializedEvent);
        return;
    }

    std::stringstream builder;
    builder << "Listening to named pipe '" << pThis->_portName << "'...";
    pThis->_pLogger->Info(builder.str());

    // the Agent can connect to the named pipe
    ::SetEvent(pThis->_hInitializedEvent);


    // It is possible that an error occurred when trying to connect to the Agent
    // in that case, no need to start to listen to the pipe
    // The pipe will be cleaned up in Stop()
    if (pThis->_stopRequested.load())
    {
        return;
    }

    // this is a blocking call waiting for the Agent to connect
    // if the agent is not running, it is going to block until the pipe is closed
    // --> the ETW manager should detect the agent is not running and close the pipe
    if (!::ConnectNamedPipe(pThis->_hNamedPipe, nullptr) && ::GetLastError() != ERROR_PIPE_CONNECTED)
    {
        pThis->ShowLastError("ConnectNamedPipe failed...");
        pThis->_pHandler->OnConnectError();

        ::CloseHandle(pThis->_hNamedPipe);
        pThis->_hNamedPipe = nullptr;

        return;
    }

    // It is possible that an error occurred when trying to connect to the Agent
    // in that case, no need to start to listen to the pipe
    // The pipe will be cleaned up in Stop()
    if (pThis->_stopRequested.load())
    {
        return;
    }

    // this is a blocking call until the communication ends on this named pipe
    pThis->_pHandler->OnConnect(pThis->_hNamedPipe);

    // cleanup
    ::DisconnectNamedPipe(pThis->_hNamedPipe);
    ::CloseHandle(pThis->_hNamedPipe);
    pThis->_hNamedPipe = nullptr;
}

void IpcServer::ShowLastError(const char* message, uint32_t lastError)
{
    if (_pLogger != nullptr)
    {
        std::stringstream builder;
        builder << message << " (" << lastError << ")";
        _pLogger->Error(builder.str());
    }
}
