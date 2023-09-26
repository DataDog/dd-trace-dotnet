// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <iostream>
#include <memory>
#include <string>
#include <Windows.h>


enum Commands : uint8_t
{
    // 0 is not a valid command id

    // commands sent by the profiler
    Register    = 1,
    Unregister  = 2,

    // commands sent by the Agent
    ClrEvents  = 16,
};

enum class ResponseId : uint8_t
{
    OK = 0x00,
    // future
    Error = 0xFF,
};

struct MagicVersion
{
    uint8_t Magic[14];
};
const MagicVersion DD_Ipc_Magic_V1 = {"DD_ETW_IPC_V1"}; // 14 bytes long (including final 0)

// The header to be associated with every command and response
#pragma pack(1)
struct IpcHeader
{
    union
    {
        MagicVersion _magic;
        uint8_t Magic[14]; // Magic Version number; a 0 terminated char array
    };

    // TODO: do we need blocks larger than 64KB?
    uint16_t Size; // The size of the incoming packet, size = header size + payload size
    union
    {
        uint8_t CommandId;    // The command being sent (TODO: do we need to add a group/set of commands?)
        uint8_t ResponseCode; // 0x00 in case of success and 0xFF in case of error
    };
};  // size of header = 17 bytes


// Messages for commands
//
//
#pragma pack(1)
struct RegistrationProcessMessage : public IpcHeader
{
    uint64_t Pid;
};

inline void SetupRegistrationCommand(RegistrationProcessMessage& message, uint64_t pid)
{
    memcpy(message.Magic, &DD_Ipc_Magic_V1, sizeof(DD_Ipc_Magic_V1));
    message.Size = sizeof(RegistrationProcessMessage);
    message.Pid = pid;
}

inline void SetupRegisterCommand(RegistrationProcessMessage& message, uint64_t pid)
{
    SetupRegistrationCommand(message, pid);
    message.CommandId = Commands::Register;
}

inline void SetupUnregisterCommand(RegistrationProcessMessage& message, uint64_t pid)
{
    SetupRegistrationCommand(message, pid);
    message.CommandId = Commands::Unregister;
}

#pragma pack(1)
struct ClrEventsMessage : public IpcHeader
{
    // the size of this payload is given by (message.size - sizeof(IpcHeader))
    uint8_t payload[1];
};

// the given message will probably be dynamically allocated
// Also, the given size is the size of the payload only
inline void SetupSendEventsCommand(ClrEventsMessage* pMessage, uint16_t size)
{
    memcpy(pMessage->Magic, &DD_Ipc_Magic_V1, sizeof(DD_Ipc_Magic_V1));
    pMessage->Size = size + sizeof(IpcHeader);
    pMessage->CommandId = Commands::ClrEvents;
}


// predefined headers for responses
//
//
const IpcHeader SuccessResponse =
{
    { DD_Ipc_Magic_V1 },
    (uint16_t)sizeof(IpcHeader),
    (uint8_t)ResponseId::OK
};

const IpcHeader ErrorResponse =
{
    { DD_Ipc_Magic_V1 },
    (uint16_t)sizeof(IpcHeader),
    (uint8_t)ResponseId::Error
};


// helpers
//
//
inline bool IsMessageValid(IpcHeader* pMessage)
{
    if (memcmp(&DD_Ipc_Magic_V1, pMessage->Magic, sizeof(DD_Ipc_Magic_V1)) != 0)
    {
#ifdef _DEBUG
        std::cout << "Invalid Magic signature...\n";
#endif
        return false;
    }

    return true;
}

inline HANDLE CheckEndpoint(const std::string& pipeName, DWORD timeoutMS)
{
    bool success = ::WaitNamedPipeA(pipeName.c_str(), timeoutMS);
#ifdef _DEBUG
    if (!success)
    {
        std::cout << "Timeout when trying to connect to" << pipeName << "...\n";
    }
#endif

    HANDLE hPipe = ::CreateFileA(
        pipeName.c_str(),
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);

    return hPipe;
}
