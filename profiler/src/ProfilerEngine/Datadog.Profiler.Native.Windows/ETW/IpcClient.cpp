// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IpcClient.h"
#include <iostream>
#include <sstream>

IpcClient::IpcClient()
{
    _pLogger = nullptr;
    _hPipe = nullptr;
}

IpcClient::IpcClient(IIpcLogger* pLogger, HANDLE hPipe)
{
    _pLogger = pLogger;
    _hPipe = hPipe;
}

IpcClient::~IpcClient()
{
    Disconnect();
}

bool IpcClient::Disconnect()
{
    HANDLE hPipe = _hPipe;
    _hPipe = nullptr;
    if (hPipe == nullptr)
    {
        return false;
    }

    return ::CloseHandle(hPipe);
}


std::unique_ptr<IpcClient> IpcClient::Connect(IIpcLogger* pLogger, const std::string& portName, uint32_t timeoutMS)
{
    HANDLE hPipe = GetEndPoint(pLogger, portName, timeoutMS);
    if (hPipe == INVALID_HANDLE_VALUE)
    {
        if (pLogger != nullptr)
        {
            DWORD lastError = ::GetLastError();
            std::ostringstream builder;
            builder << "Impossible to connect to " << portName << " (" << lastError << ")";
            pLogger->Error(builder.str());
        }
        return nullptr;
    }

    return std::make_unique<IpcClient>(pLogger, hPipe);
 }

uint32_t IpcClient::Send(PVOID pBuffer, uint32_t bufferSize)
 {
    DWORD writtenSize;
    if (!::WriteFile(_hPipe, pBuffer, bufferSize, &writtenSize, nullptr))
    {
        auto lastError = ShowLastError("Failed to write to pipe");
        return lastError;
    }
    ::FlushFileBuffers(_hPipe);

    return (bufferSize == writtenSize) ? NamedPipesCode::Success : NamedPipesCode::MissingData;
 }

uint32_t IpcClient::Read(PVOID pBuffer, uint32_t bufferSize)
{
    DWORD readSize;
    if (::ReadFile(_hPipe, pBuffer, bufferSize, &readSize, nullptr))
    {
        if ((readSize != 0) && (readSize != bufferSize))
        {
            return NamedPipesCode::MissingData;
        }

        return NamedPipesCode::Success;
    }

    DWORD lastError = ::GetLastError();
    if (lastError == ERROR_PIPE_NOT_CONNECTED)
    {
        return NamedPipesCode::NotConnected;
    }
    else if (lastError == ERROR_BROKEN_PIPE)
    {
        return NamedPipesCode::Broken;
    }
    else
    {
        ShowLastError("Failed to read result", lastError);

        // TODO: would it be possible that bufferSize != readSize (and readSize != 0)?
        return lastError;
    }
}

HANDLE IpcClient::GetEndPoint(IIpcLogger* pLogger, const std::string& portName, uint16_t timeoutMS)
{
    bool success = ::WaitNamedPipeA(portName.c_str(), timeoutMS);
    if (!success)
    {
        if (pLogger != nullptr)
        {
            std::ostringstream builder;
            builder << "Timeout when trying to connect to" << portName << "...";
        }
    }

    HANDLE hPipe = ::CreateFileA(
        portName.c_str(),
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);

    return hPipe;
}

uint32_t IpcClient::ShowLastError(const char* message, uint32_t lastError)
{
    if (_pLogger != nullptr)
    {
        std::ostringstream builder;
        builder << message << " (" << lastError << ")";
        _pLogger->Error(builder.str());
    }

    return lastError;
}
