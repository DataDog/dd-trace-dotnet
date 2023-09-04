#pragma once

#include <stdint.h>
#include <iostream>
#include <stdio.h>
#include <windows.h>

#include "IIpcEndpoint.h"


// possible errors when dealing with named pipes
// const int ERROR_PIPE_NOT_CONNECTED = 0xE9;

// from https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md
//
// Every Diagnostic IPC Message will start with a headerand every header will :
//  - start with a magic version numberand a size
//  - sizeof(IpcHeader) == 20
//  - encode numbers little - endian
//  - account for the size of the payload in the size value, i.e., IpcHeader.size == sizeof(IpcHeader) + PayloadStruct.GetSize()
// size = 14 + 2 + 1 + 1 + 2 = 20 bytes
// The reserved field is reserved for future use. It is unused in DOTNET_IPC_V1 and must be 0x0000.

// .NET 5: from diagnosticsprotocol.h
enum class IpcMagicVersion : uint8_t
{
    DOTNET_IPC_V1 = 0x01,
    // FUTURE
};

enum class DiagnosticServerCommandSet : uint8_t
{
    // reserved = 0x00,
    Dump        = 0x01,
    EventPipe   = 0x02,
    Profiler    = 0x03,
    Process     = 0x04,

    Server      = 0xFF,
};

enum class DiagnosticServerResponseId : uint8_t
{
    OK = 0x00,
    // future
    Error = 0xFF,
};

struct MagicVersion
{
    uint8_t Magic[14];
};

// The header to be associated with every command and response
// to/from the diagnostics server
#pragma pack(1)
struct IpcHeader
{
    union
    {
        MagicVersion _magic;
        uint8_t  Magic[14];  // Magic Version number; a 0 terminated char array
    };
    uint16_t Size;       // The size of the incoming packet, size = header + payload size
    uint8_t  CommandSet; // The scope of the Command.
    uint8_t  CommandId;  // The command being sent
    uint16_t Reserved;   // reserved for future use and must be 0
};

const MagicVersion DotnetIpcMagic_V1 = { "DOTNET_IPC_V1" };


// https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md
//
// Upon successful connection, the runtime will send a fixed-size, 34 byte buffer containing the following information:

struct AdvertiseVersion
{
    uint8_t Magic[8];
};

const AdvertiseVersion DotnetAdvertiseMagic_V1 = { "ADVR_V1" };

struct IpcAdvertiseMessage
{
    union
    {
        AdvertiseVersion _magic;
        char Magic[8];          // (8 bytes) "ADVR_V1\0" (ASCII chars + null byte)
    };
    uint8_t runtimeCookie[16];  // (16 bytes) CLR Instance Cookie(little - endian)
    uint64_t processId;         // (8 bytes) PID(little - endian)
    uint16_t future;            // (2 bytes) unused for future - proofing
};


const IpcHeader GenericSuccessHeader =
{
    { DotnetIpcMagic_V1 },
    (uint16_t)sizeof(IpcHeader),
    (uint8_t)DiagnosticServerCommandSet::Server,
    (uint8_t)DiagnosticServerResponseId::OK,
    (uint16_t)0x0000
};

const IpcHeader GenericErrorHeader =
{
    { DotnetIpcMagic_V1 },
    (uint16_t)sizeof(IpcHeader),
    (uint8_t)DiagnosticServerCommandSet::Server,
    (uint8_t)DiagnosticServerResponseId::Error,
    (uint16_t)0x0000
};



// PROCESS commands (available in .NET 5+)
//
const IpcHeader ProcessInfoMessage =
{
    { DotnetIpcMagic_V1 },
    (uint16_t)sizeof(IpcHeader),
    (uint8_t)DiagnosticServerCommandSet::Process,
    (uint8_t)DiagnosticServerResponseId::OK,
    (uint16_t)0x0000
};


class ProcessInfoRequest
{
public:
    ProcessInfoRequest();
    ~ProcessInfoRequest();

    bool Send(HANDLE hPipe);
    bool Process(IIpcEndpoint* pEndpoint);

public:
    DWORD Error;
    uint64_t Pid;
    GUID RuntimeCookie;
    wchar_t* CommandLine;
    wchar_t* OperatingSystem;
    wchar_t* Architecture;

private:
    bool ParseResponse(DWORD payloadSize);

private:
    uint8_t* _buffer;
};


// EVENTPIPE commands
//
enum class EventPipeCommandId : uint8_t
{
    // reserved = 0x00,
    StopTracing = 0x01,     // stop a given session
    CollectTracing = 0x02,  // create/start a given session
    CollectTracing2 = 0x03, // create/start a given session with/without rundown
};


const uint8_t DotnetProviderMagicLength = 32;
struct MagicProvider
{
    wchar_t Magic[DotnetProviderMagicLength];
};

// 32 wchar_t (including \0)
const MagicProvider DotnetProviderMagic = { L"Microsoft-Windows-DotNETRuntime" };

// from CLR gcinterface.h
enum GCEventKeyword
{
    GCEventKeyword_None = 0x0,
    GCEventKeyword_GC = 0x1,
    // Duplicate on purpose, GCPrivate is the same keyword as GC,
    // with a different provider
    GCEventKeyword_GCPrivate = 0x1,
    GCEventKeyword_GCHandle = 0x2,
    GCEventKeyword_GCHandlePrivate = 0x4000,
    GCEventKeyword_GCHeapDump = 0x100000,
    GCEventKeyword_GCSampledObjectAllocationHigh = 0x200000,
    GCEventKeyword_GCHeapSurvivalAndMovement = 0x400000,
    GCEventKeyword_GCHeapCollect = 0x800000,
    GCEventKeyword_GCHeapAndTypeNames = 0x1000000,
    GCEventKeyword_GCSampledObjectAllocationLow = 0x2000000,
    GCEventKeyword_All = GCEventKeyword_GC
    | GCEventKeyword_GCPrivate
    | GCEventKeyword_GCHandle
    | GCEventKeyword_GCHandlePrivate
    | GCEventKeyword_GCHeapDump
    | GCEventKeyword_GCSampledObjectAllocationHigh
    | GCEventKeyword_GCHeapSurvivalAndMovement
    | GCEventKeyword_GCHeapCollect
    | GCEventKeyword_GCHeapAndTypeNames
    | GCEventKeyword_GCSampledObjectAllocationLow
};
// https://docs.microsoft.com/en-us/dotnet/framework/performance/garbage-collection-etw-events

// from https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace
enum EventKeyword : uint64_t
{
    gc                              = 0x1,
    gchandle                        = 0x2,
    fusion                          = 0x4,
    loader                          = 0x8,
    jit                             = 0x10,
    ngen                            = 0x20,
    startenumeration                = 0x40,
    endenumeration                  = 0x80,
    security                        = 0x400,
    appdomainresourcemanagement     = 0x800,
    jittracing                      = 0x1000,
    interop                         = 0x2000,
    contention                      = 0x4000,
    exception                       = 0x8000,
    threading                       = 0x10000,
    jittedmethodiltonativemap       = 0x20000,
    overrideandsuppressngenevents   = 0x40000,
    type                            = 0x80000,
    gcheapdump                      = 0x100000,
    gcsampledobjectallocationhigh   = 0x200000,
    gcheapsurvivalandmovement       = 0x400000,
    gcheapcollect                   = 0x800000,
    gcheapandtypenames              = 0x1000000,
    gcsampledobjectallocationlow    = 0x2000000,
    perftrack                       = 0x20000000,
    stack                           = 0x40000000,
    threadtransfer                  = 0x80000000,
    debugger                        = 0x100000000,
    monitoring                      = 0x200000000,
    codesymbols                     = 0x400000000,
    eventsource                     = 0x800000000,
    compilation                     = 0x1000000000,
    compilationdiagnostic           = 0x2000000000,
    methoddiagnostic                = 0x4000000000,
    typediagnostic                  = 0x8000000000,
};

enum class EventVerbosityLevel : uint32_t
{
    LogAlways       = 0,
    Critical        = 1,
    Error           = 2,
    Warning         = 3,
    Informational   = 4,
    Verbose         = 5
};

const uint32_t CircularBufferMBSize = 16;
const uint32_t NetTraceFormat = 1;

#pragma pack(1)
struct StartSessionMessage : public IpcHeader
{
    uint32_t CircularBufferMB;  // 16 MB
    uint32_t Format;            // 1 for NetTrace format
    uint8_t RequestRundown;     // 0 because don't want rundown

    // array of provider configuration
    uint32_t ProviderCount;     // 1 only: Microsoft-Windows-DotNETRuntime
    uint64_t Keywords;          // from EventKeyword
    uint32_t Verbosity;         // from EventPipeEventLevel
    uint32_t ProviderStringLen; // number of UTF16 characters = 32 (including last \0)
    union                       // dotnet provider name
    {
        MagicProvider _magic;
        uint8_t Provider[2 * DotnetProviderMagicLength];
    };
    uint32_t Arguments;         // 0 for empty string (no argument)
};

class EventPipeStartRequest
{
public:
    EventPipeStartRequest();

    bool Process(IIpcEndpoint* pEndpoint, uint64_t keywords, EventVerbosityLevel verbosity);

public:
    DWORD Error;
    uint64_t SessionId;
};


#pragma pack(1)
struct StopSessionMessage : public IpcHeader
{
    uint64_t SessionId;
};

class EventPipeStopRequest
{
public:
    EventPipeStopRequest();

    bool Process(IIpcEndpoint* pEndpoint, uint64_t sessionId);

public:
    DWORD Error;
};

// helper functions
//

void DumpBuffer(const uint8_t* pBuffer, DWORD byteCount);
inline void DumpGuid(GUID guid)
{
    LPOLESTR szGuid;
    ::StringFromCLSID(guid, &szGuid);
    std::wcout << szGuid;
    ::CoTaskMemFree(szGuid);
}
