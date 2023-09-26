// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EtwEventsHandler.h"
#include "DebugHelpers.h"
#include "..\shared\Protocol.h"
#include "..\shared\IpcClient.h"

#include <iostream>


EtwEventsHandler::EtwEventsHandler()
{
}

void EtwEventsHandler::Stop()
{
    _stopRequested.store(true);
}

void EtwEventsHandler::OnStartError()
{
    Stop();
}

void EtwEventsHandler::OnConnectError()
{
    Stop();
}

bool EtwEventsHandler::ReadEvents(HANDLE hPipe, uint8_t* pBuffer, DWORD bufferSize, DWORD& readSize)
{
    bool success = true;
    auto pMessage = reinterpret_cast<ClrEventsMessage*>(pBuffer);
    DWORD totalReadSize = 0;
    DWORD lastError = ERROR_SUCCESS;

    while (totalReadSize < bufferSize)
    {
        success = ::ReadFile(hPipe, &(pBuffer[totalReadSize]), bufferSize - totalReadSize, &readSize, nullptr);

        if (!success || readSize == 0)
        {
            lastError = ::GetLastError();

            if (lastError == ERROR_MORE_DATA)
            {
                totalReadSize += readSize;
                if (totalReadSize == bufferSize)
                {
#ifdef _DEBUG
                    std::cout << "Read buffer was too small...\n";
#endif
                    return false;
                }

                continue;
            }
            else
            {
                if (lastError == ERROR_BROKEN_PIPE)
                {
#ifdef _DEBUG
                    std::cout << "Disconnected client...\n";
#endif
                }
                else
                {
#ifdef _DEBUG
                    std::cout << "Error reading from pipe (" << lastError << ")...\n ";
#endif
                }
                return false;
            }
        }
        else
        {
            if (!IsMessageValid(pMessage))
            {
                // fire and forget
                //WriteErrorResponse(hPipe);
                return false;
            }

            return true;
        }
    }

    // too big for the buffer
    return false;
}

void EtwEventsHandler::OnConnect(HANDLE hPipe)
{
    DWORD bufferSize = (1 << 16) + sizeof(IpcHeader);
    auto buffer = std::make_unique<uint8_t[]>(bufferSize);
    auto message = reinterpret_cast<ClrEventsMessage*>(buffer.get());

    DWORD readSize;
    while (!_stopRequested.load())
    {
        readSize = 0;
        if (!ReadEvents(hPipe, buffer.get(), bufferSize, readSize))
        {
            std::cout << "Stop reading events\n";
            break;
        }

        // check the message based on the expected command
        if (message->CommandId == Commands::ClrEvents)
        {
            std::cout << "Events received: " << message->Size - sizeof(IpcHeader) << " / " << readSize << " bytes\n";

            // fire and forget
            //WriteSuccessResponse(hPipe);
        }
        else
        {
            std::cout << "Invalid command (" << message->CommandId << ")...\n";

            // fire and forget
            //WriteErrorResponse(hPipe);
            break;
        }
    }
}

