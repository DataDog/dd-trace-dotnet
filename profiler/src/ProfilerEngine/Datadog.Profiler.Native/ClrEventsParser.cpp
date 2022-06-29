// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrEventsParser.h"

#include <iostream>
#include <sstream>

#include "Log.h"


ClrEventsParser::ClrEventsParser(ICorProfilerInfo12* pCorProfilerInfo)
    :
    _pCorProfilerInfo{pCorProfilerInfo}
{
}

const char* GetEventName(DWORD eventId)
{
    switch (eventId)
    {
        // GC events
        case 1: return "GcStart";
        case 2: return "GCEnd";
        case 3: return "GCRestartEEEnd";
        case 4: return "GCHeapStats";
        case 7: return "GCRestartEEBegin";
        case 8: return "GCSuspendEEEnd";
        case 9: return "GCSuspendEEBegin";
        case 13: return "GCFinalizersEnd";
        case 14: return "GCFinalizersBegin";
        case 35: return "GCTriggered";
        case 202: return "GCMarkWithType";
        case 204: return "GCPerHeapHistory";
        case 205: return "GCGlobalHeap";
        // verbose
        case 10: return "___GCAllocationTick___";
        case 29: return "___FinalizeObject";
        case 200: return "___IncreaseMemoryPressure";
        case 203: return "___GCJoin";

        // Contention events
        case 81: return "ContentionStart";
        case 91: return "ContentionStop";

        // other verbose
        //
        default:
        {
            return nullptr;
        }
    }
}

void DumpEvent(
    std::string& providerName,
    EventPipeMetadataInstance& metadata,
    DWORD eventId,
    DWORD eventVersion,
    ULONG numStackFrames
    )
{
    std::stringstream buffer;
    buffer << providerName << " | "
           << "k=0x" << std::hex << metadata.keywords << std::dec << " = ";

    if (WStrLen(metadata.name) > 0)
    {
        buffer << "'" << shared::ToString(shared::WSTRING(metadata.name)) << "' : ";
    }
    else
    {
        // most event name are empty strings
        const char* wellKnownEventName = GetEventName(metadata.id);
        if (wellKnownEventName != nullptr)
        {
            buffer << "'" << wellKnownEventName << "' : ";
        }
        else
        {
            buffer << "'' : ";
        }
    }
    buffer << metadata.id << "(" << eventId << ") v"
           << metadata.version << "(" << eventVersion << ")";

    if (numStackFrames > 0)
    {
        buffer << " [" << numStackFrames << " frames]";
    }
    buffer << std::endl;
    std::cout << buffer.str();
}

void ClrEventsParser::ParseEvent(
    EVENTPIPE_PROVIDER provider,
    DWORD eventId,
    DWORD eventVersion,
    ULONG cbMetadataBlob,
    LPCBYTE metadataBlob,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread,
    ULONG numStackFrames,
    UINT_PTR stackFrames[])
{
    // Currently, only "Microsoft-Windows-DotNETRuntime" provider is used so no need to check.
    // However, during the test, a last (k=0 id=1 V1) event is sent from "Microsoft-DotNETCore-EventPipe".
    auto providerName = GetProviderName(provider);

    // This implementation tries to cache the metadata in a thread-safe container.
    // Maybe directly reading the keyword from the metadata blob without lock might be more effective.
    auto metadata = GetMetadata(metadataBlob, cbMetadataBlob);

    // Dump to see all received events
    //DumpEvent(providerName, metadata, eventId, eventVersion, numStackFrames);

    if (KEYWORD_GC == (metadata.keywords & KEYWORD_GC))
    {
        ParseGcEvent(metadata.id, metadata.version, cbEventData, eventData);
    }
    else
    if (KEYWORD_CONTENTION == (metadata.keywords & KEYWORD_CONTENTION))
    {
        ParseContentionEvent(metadata.id, metadata.version, cbEventData, eventData);
    }
}


// dump the buffer every 16 bytes + corresponding ASCII characters
// ex:  44 4F 54 4E 45 54 5F 49  50 43 5F 56 31 00 77 00    DOTNET_IPC_V1.w.
const DWORD LineWidth = 16;

char GetCharFromBinary(uint8_t byte)
{
    if ((byte >= 0x21) && (byte <= 0x7E))
        return static_cast<char>(byte);

    return '.';
}

void DumpBuffer(const uint8_t* pBuffer, DWORD byteCount)
{
    //// TODO: uncomment to skip displaying memory buffer on console
    // return;

    DWORD pos = 0;
    char stringBuffer[LineWidth + 1];
    ::ZeroMemory(stringBuffer, LineWidth + 1);

    std::cout << std::hex;
    for (DWORD i = 0; i < byteCount; i++)
    {
        std::cout << std::uppercase << std::setfill('0') << std::setw(2) << (int)pBuffer[i] << " ";
        stringBuffer[pos] = GetCharFromBinary(pBuffer[i]);
        pos++;

        if (pos % LineWidth == 0)
        {
            std::cout << "    ";
            std::cout << stringBuffer;
            std::cout << "\n";

            ::ZeroMemory(stringBuffer, LineWidth + 1);
            pos = 0;
        }
        else if (pos % (LineWidth / 2) == 0)
        {
            std::cout << " ";
        }
    }

    // show the remaining characters if any
    if (pos > 0)
    {
        for (size_t i = 0; i < LineWidth - pos; i++)
        {
            std::cout << "   ";
        }

        if (pos > LineWidth / 2)
            std::cout << "    ";
        else
            std::cout << "     ";
        std::cout << stringBuffer;
        std::cout << "\n";
    }

    // reset to default
    std::cout << std::setfill(' ') << std::setw(1) << std::dec;
}

void ClrEventsParser::ParseGcEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
// look for AllocationTick_V4
    if ((id == EVENT_ALLOCATION_TICK) && (version == 4))
    {
        //template tid = "GCAllocationTick_V4" >
        //    <data name = "AllocationAmount" inType = "win:UInt32" />
        //    <data name = "AllocationKind" inType = "win:UInt32" />
        //    <data name = "ClrInstanceID" inType = "win:UInt16" />
        //    <data name = "AllocationAmount64" inType = "win:UInt64"/>
        //    <data name = "TypeID" inType = "win:Pointer" />
        //    <data name = "TypeName" inType = "win:UnicodeString" />
        //    <data name = "HeapIndex" inType = "win:UInt32" />
        //    <data name = "Address" inType = "win:Pointer" />
        //    <data name = "ObjectSize" inType = "win:UInt64" />
        //DumpBuffer(pEventData, cbEventData);

        AllocationTickV4Payload payload{0};
        ULONG offset = 0;
        if (!Read(payload.AllocationAmount, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.AllocationKind, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.ClrInstanceId, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.AllocationAmount64, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.TypeId, pEventData, cbEventData, offset))
        {
            return;
        }
        payload.TypeName = ReadWideString(pEventData, cbEventData, &offset);
        if (payload.TypeName == nullptr)
        {
            return;
        }
        if (!Read(payload.HeapIndex, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.Address, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.ObjectSize, pEventData, cbEventData, offset))
        {
            return;
        }

        std::wstringstream buffer;
        buffer << WStr("AllocationTick - ") << payload.TypeName;
        buffer << WStr(" (") << payload.ObjectSize << WStr(" bytes)");
        buffer << std::endl;
        std::wcout << buffer.str();
    }
}

void ClrEventsParser::ParseContentionEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
// look for ContentionStop_V1
    if ((id == EVENT_CONTENTION_STOP) && (version == 1))
    {
        //<template tid="ContentionStop_V1">
        //    <data name="ContentionFlags" inType="win:UInt8" />
        //    <data name="ClrInstanceID" inType="win:UInt16" />
        //    <data name="DurationNs" inType="win:Double" />
        //DumpBuffer(pEventData, cbEventData);

        ContentionStopV1Payload payload{0};
        ULONG offset = 0;
        if (!Read<ContentionStopV1Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        std::stringstream buffer;
        buffer << "ContentionStop";
        buffer << ((payload.ContentionFlags == 0) ? " - managed " : " - native ");
        buffer << "(" << payload.DurationNs << " ns)";
        buffer << std::endl;
        std::cout << buffer.str();
    }
}


const std::string UnknownProvider("UnknownProvider");

std::string ClrEventsParser::GetProviderName(EVENTPIPE_PROVIDER provider)
{
    auto it = _providerNameCache.find(provider);
    if (it == _providerNameCache.end())
    {
        WCHAR nameBuffer[LONG_LENGTH];
        ULONG nameCount;
        HRESULT hr = _pCorProfilerInfo->EventPipeGetProviderInfo(provider, LONG_LENGTH, &nameCount, nameBuffer);
        if (FAILED(hr))
        {
            //Log::Error("EventPipeGetProviderInfo failed with hr=0x%x", hr);
            return UnknownProvider;
        }

        _providerNameCache.insertNew(provider, shared::ToString(shared::WSTRING(nameBuffer)));

        it = _providerNameCache.find(provider);
        assert(it != _providerNameCache.end());
    }

    return it->second;
}

EventPipeMetadataInstance ClrEventsParser::GetMetadata(LPCBYTE pMetadata, ULONG cbMetadata)
{
    EventPipeMetadataInstance metadata {0};
    if (pMetadata == NULL || cbMetadata == 0)
    {
        return metadata;
    }

    // TODO: find a way to limit the number of find() calls (ask Gregory who had to do this)
    auto it = _metadataCache.find(pMetadata);
    if (it == _metadataCache.end())
    {
        ULONG offset = 0;
        //metadata.id = ReadFromBuffer<UINT32>(pMetadata, cbMetadata, &offset);
        //metadata.name = ReadWideString(pMetadata, cbMetadata, &offset);
        //metadata.keywords = ReadFromBuffer<INT64>(pMetadata, cbMetadata, &offset);
        //metadata.version = ReadFromBuffer<UINT32>(pMetadata, cbMetadata, &offset);
        //metadata.level = ReadFromBuffer<UINT32>(pMetadata, cbMetadata, &offset);
        Read(metadata.id, pMetadata, cbMetadata, offset);
        metadata.name = ReadWideString(pMetadata, cbMetadata, &offset);
        Read(metadata.keywords, pMetadata, cbMetadata, offset);
        Read(metadata.version, pMetadata, cbMetadata, offset);
        Read(metadata.level, pMetadata, cbMetadata, offset);

        _metadataCache.insertNew(pMetadata, metadata);
        it = _metadataCache.find(pMetadata);
    }

    return it->second;
}