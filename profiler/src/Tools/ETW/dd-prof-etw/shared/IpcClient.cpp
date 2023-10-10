// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IpcClient.h"
#include "DebugHelpers.h"
#include <iostream>


IpcClient::IpcClient()
{
    _showMessages = false;
    _hPipe = nullptr;
}

IpcClient::IpcClient(bool showMessages, HANDLE hPipe)
{
    _showMessages = showMessages;
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


std::unique_ptr<IpcClient> IpcClient::Connect(bool showMessages, const std::string& portName, uint32_t timeoutMS)
{
    HANDLE hPipe = GetEndPoint(showMessages, portName, timeoutMS);
    if (hPipe == INVALID_HANDLE_VALUE)
    {
        if (showMessages)
        {
            DWORD lastError = ::GetLastError();
            std::cout << "Impossible to connect to " << portName << " (" << lastError << ")\n";
        }
        return nullptr;
    }

    return std::make_unique<IpcClient>(showMessages, hPipe);
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
    else
    {
        ShowLastError("Failed to read result", lastError);

        // TODO: would it be possible that bufferSize != readSize (and readSize != 0)?
        return lastError;
    }
}

HANDLE IpcClient::GetEndPoint(bool showMessages, const std::string& portName, uint16_t timeoutMS)
{
    bool success = ::WaitNamedPipeA(portName.c_str(), timeoutMS);
    if (!success)
    {
        if (showMessages)
        {
            std::cout << "Timeout when trying to connect to" << portName << "...\n";
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
    if (_showMessages)
    {
        std::cout << message << " (" << lastError << ")\n";
    }

    return lastError;
}
