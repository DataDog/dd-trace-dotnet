// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EtwEventsHandler.h"
#include "Protocol.h"
#include "IpcClient.h"
#include "../../Datadog.Profiler.Native/ClrEventsParser.h"
#include <iomanip>
#include <iostream>
#include <sstream>


EtwEventsHandler::EtwEventsHandler()
    :
    _showMessages {false},
    _pReceiver {nullptr},
    _pEventsFile {nullptr},
    _logger {nullptr}
{
}

EtwEventsHandler::EtwEventsHandler(IIpcLogger* logger, IEtwEventsReceiver* pClrEventsReceiver, FILE* pEventsFile)
    :
    _logger {logger},
    _showMessages {false},
    _pReceiver {pClrEventsReceiver},
    _pEventsFile {pEventsFile}
{
}

EtwEventsHandler::~EtwEventsHandler()
{
    Cleanup();
}

void EtwEventsHandler::Cleanup()
{
    if (_pEventsFile != nullptr)
    {
        fclose(_pEventsFile);
        _pEventsFile = nullptr;
    }

    _stopRequested.store(true);
}

void EtwEventsHandler::OnStartError()
{
    Cleanup();
}

void EtwEventsHandler::OnConnectError()
{
    Cleanup();
}

void EtwEventsHandler::WriteSuccessResponse(HANDLE hPipe)
{
    DWORD written;
    if (!::WriteFile(hPipe, &SuccessResponse, sizeof(SuccessResponse), &written, nullptr))
    {
        _logger->Warn("Failed to send success response\n");
    }
}

void EtwEventsHandler::OnConnect(HANDLE hPipe)
{
    const DWORD bufferSize = (1 << 16) + sizeof(IpcHeader);
    auto buffer = std::make_unique<uint8_t[]>(bufferSize);
    auto message = reinterpret_cast<ClrEventsMessage*>(buffer.get());

    DWORD readSize;
    DWORD eventsCount = 0;
    while (!_stopRequested.load())
    {
        readSize = 0;
        if (!ReadEvents(hPipe, buffer.get(), bufferSize, readSize))
        {
            _logger->Info("Stop reading events");
            break;
        }

        // serialize the event to file if needed
        if (_pEventsFile != nullptr)
        {
            fwrite(buffer.get(), sizeof(uint8_t), readSize, _pEventsFile);
            std::stringstream builder;
            builder << "Read size = " << readSize << " bytes -- Message size = " << message->Size << " | Event payload size = " << message->Payload.EtwUserDataLength;
            _logger->Info(builder.str());
        }

        // check the message based on the expected command
        if (message->CommandId == Commands::IsAlive)
        {
            WriteSuccessResponse(hPipe);
        }
        else
        if (message->CommandId == Commands::ClrEvents)
        {
            if (message->Size > readSize)
            {
                std::stringstream builder;
                builder << "Invalid format: read size " << readSize << " bytes is smaller than supposed message size " << message->Size + sizeof(IpcHeader);
                _logger->Error(builder.str());

                // TODO: maybe we should stop the communication???
                continue;
            }

            eventsCount++;

            const EVENT_HEADER* pHeader = &(message->EtwHeader);
            uint32_t tid = pHeader->ThreadId;
            uint8_t version = pHeader->EventDescriptor.Version;
            uint64_t keyword = pHeader->EventDescriptor.Keyword;
            uint8_t level = pHeader->EventDescriptor.Level;
            uint16_t id = pHeader->EventDescriptor.Id;
            uint64_t timestamp = pHeader->TimeStamp.QuadPart;

            ClrEventPayload* pPayload = (ClrEventPayload*)(&(message->Payload));
            uint16_t userDataLength = pPayload->EtwUserDataLength;
            uint8_t* pUserData = (uint8_t*)((byte*)&(pPayload->EtwPayload));

            if ((keyword == KEYWORD_GC) && (id == EVENT_ALLOCATION_TICK))
            {
                // TODO: set a breakpoint here to check the content of the payload where the type name should be visible
            }

            if (_pReceiver != nullptr)
            {
                _pReceiver->OnEvent(timestamp, tid, version, keyword, level, id, userDataLength, pUserData);

                std::stringstream builder;
                builder << "ETW event #" << eventsCount << " | " << keyword << " - " << id;
                _logger->Info(builder.str());
            }

            // fire and forget so no need to answer
            //WriteSuccessResponse(hPipe);
        }
        else
        {
            std::stringstream builder;
            builder << "Invalid command (" << message->CommandId << ")...";
            _logger->Error(builder.str());

            // fire and forget so no need to answer
            //WriteErrorResponse(hPipe);
            break;
        }
    }

    // close the event file if needed
    Cleanup();
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
                    _logger->Error("Named pipe read buffer was too small...");
                    return false;
                }

                continue;
            }
            else
            {
                if (lastError == ERROR_BROKEN_PIPE)
                {
                    _logger->Info("Disconnected named pipe client...");
                }
                else
                {
                    std::stringstream builder;
                    builder << "Error reading from named pipe (" << lastError << ")...";
                    _logger->Error(builder.str());
                }
                return false;
            }
        }
        else
        {
            if (!IsMessageValid(pMessage))
            {
                _logger->Error("Invalid Magic signature in message from Agent...");

                // fire and forget
                // WriteErrorResponse(hPipe);
                return false;
            }

            return true;
        }
    }

    // too big for the buffer
    _logger->Error("Named pipe read buffer was too small...");
    return false;
}