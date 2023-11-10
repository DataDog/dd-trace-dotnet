// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EtwEventsHandler.h"
#include "Protocol.h"
#include "IpcClient.h"

#include <sstream>
#include <iostream>


EtwEventsHandler::EtwEventsHandler()
{
    _showMessages = false;
}

EtwEventsHandler::EtwEventsHandler(bool showMessages)
{
    _showMessages = showMessages;
}

EtwEventsHandler::~EtwEventsHandler()
{
    Stop();
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
                    if (_showMessages)
                    {
                        std::cout << "Read buffer was too small...\n";
                    }
                    return false;
                }

                continue;
            }
            else
            {
                if (lastError == ERROR_BROKEN_PIPE)
                {
                    if (_showMessages)
                    {
                        std::cout << "Disconnected client...\n";
                    }
                }
                else
                {
                    if (_showMessages)
                    {
                        std::cout << "Error reading from pipe (" << lastError << ")...\n ";
                    }
                }
                return false;
            }
        }
        else
        {
            if (!IsMessageValid(pMessage))
            {
                if (_showMessages)
                {
                    std::cout << "Invalid Magic signature...\n";
                }

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



// keywords
const int KEYWORD_CONTENTION = 0x00004000;
const int KEYWORD_GC         = 0x00000001;
const int KEYWORD_STACKWALK  = 0x40000000;

// events id
const int EVENT_CONTENTION_STOP = 91; // version 1 contains the duration in nanoseconds

const int EVENT_ALLOCATION_TICK = 10; // version 4 contains the size + reference
const int EVENT_GC_TRIGGERED = 35;
const int EVENT_GC_START = 1;                 // V2
const int EVENT_GC_END = 2;                   // V1
const int EVENT_GC_HEAP_STAT = 4;             // V1
const int EVENT_GC_GLOBAL_HEAP_HISTORY = 205; // V2
const int EVENT_GC_SUSPEND_EE_BEGIN = 9;      // V1
const int EVENT_GC_RESTART_EE_END = 3;        // V2

const int EVENT_GC_JOIN = 203;
const int EVENT_GC_PER_HEAP_HISTORY = 204;

const int EVENT_SW_STACK = 82;


bool EtwEventsHandler::GetClrEvent(const ClrEventsMessage* pMessage, std::string& name, uint16_t& id, uint64_t& keyword, uint8_t& level)
{
    //const EVENT_HEADER* pHeader = &(pMessage->EtwHeader);
    const EVENT_HEADER* pHeader = (EVENT_HEADER*)((byte*)(&(pMessage->EtwHeader)) + 7);
    level = pHeader->EventDescriptor.Level;
    id = pHeader->EventDescriptor.Id;

    keyword = pHeader->EventDescriptor.Keyword;
    if (pHeader->EventDescriptor.Keyword == KEYWORD_GC)
    {
        switch (id)
        {
            case EVENT_ALLOCATION_TICK:
                name = "AllocationTick";
            break;

            case EVENT_GC_TRIGGERED:
                name = "GCTriggered";
            break;

            case EVENT_GC_START:
                name = "GCStart";
            break;

            case EVENT_GC_END:
                name = "GCEnd";
            break;

            case EVENT_GC_HEAP_STAT:
                name = "GCHeapStat";
            break;

            case EVENT_GC_GLOBAL_HEAP_HISTORY:
                name = "GCGlobalHeapHistory";
            break;

            case EVENT_GC_SUSPEND_EE_BEGIN:
                name = "GCSuspendEEBegin";
            break;

            case EVENT_GC_RESTART_EE_END:
                name = "GCRestartEEEnd";
            break;

            case EVENT_GC_PER_HEAP_HISTORY:
                name = "GCPerHeapHistory";
            break;

            case EVENT_GC_JOIN:
                name = "GCJOIN";
            break;

            default:
            {
                std::stringstream buffer;
                buffer << "GC-" << id;
                name = buffer.str();
            }
            break;
        }
    }
    else if (pHeader->EventDescriptor.Keyword == KEYWORD_CONTENTION)
    {
        if (id == EVENT_CONTENTION_STOP)
        {
            name = "ContentionStop";
        }
        else
        {
            std::stringstream buffer;
            buffer << "Lock-" << id;
            name = buffer.str();
        }
    }
    else if (pHeader->EventDescriptor.Keyword == KEYWORD_STACKWALK)
    {
        if (id == EVENT_SW_STACK)
        {
            name = "StackWalk";
        }
        else
        {
            std::stringstream buffer;
            buffer << "StackWalk-" << id;
            name = buffer.str();
        }
    }
    else
    {
        std::stringstream buffer;
        buffer << "?-" << id;
        name = buffer.str();
    }


    return true;
}

void EtwEventsHandler::OnConnect(HANDLE hPipe)
{
    const DWORD bufferSize = (1 << 16) + sizeof(IpcHeader);
    auto buffer = std::make_unique<uint8_t[]>(bufferSize);
    auto message = reinterpret_cast<ClrEventsMessage*>(buffer.get());

    DWORD readSize;
    while (!_stopRequested.load())
    {
        readSize = 0;
        if (!ReadEvents(hPipe, buffer.get(), bufferSize, readSize))
        {
            if (_showMessages)
            {
                std::cout << "Stop reading events\n";
            }
            break;
        }

        // check the message based on the expected command
        if (message->CommandId == Commands::ClrEvents)
        {
            //if (_showMessages)
            //{
            //    std::cout << "Event received: " << message->Size << " == " << readSize << " bytes\n";
            //}

            if (message->Size > readSize)
            {
                if (_showMessages)
                {
                    std::cout << "Invalid format: read size " << readSize << " bytes is smaller than supposed message size " << message->Size + sizeof(IpcHeader) << "\n";
                }

                // TODO: maybe we should stop the communication???
                continue;
            }

            if (_showMessages)
            {
                std::string name;
                uint16_t id;
                uint64_t keywords;
                uint8_t level;
                if (GetClrEvent(message, name, id, keywords, level))
                {
                    std::cout << "   " << id << " | " << name << "      ( " << keywords << ", " << (uint16_t)level << ")\n";
                }
                else
                {
                    std::cout << "   Impossible to get CLR event details...\n";
                }

            }

            // fire and forget so no need to answer
            //WriteSuccessResponse(hPipe);
        }
        else
        {
            if (_showMessages)
            {
                std::cout << "Invalid command (" << message->CommandId << ")...\n";
            }

            // fire and forget so no need to answer
            //WriteErrorResponse(hPipe);
            break;
        }
    }
}

