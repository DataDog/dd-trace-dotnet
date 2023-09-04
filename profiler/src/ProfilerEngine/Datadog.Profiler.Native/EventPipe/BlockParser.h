#pragma once

#include <stdint.h>
#include <iostream>
#include <unordered_map>
#include <windows.h>

#include "NettraceFormat.h"


class EventCacheMetadata
{
public:
    uint32_t     MetadataId;
    std::wstring ProviderName;
    uint32_t     EventId;
    std::wstring EventName; // empty most of the time
    uint64_t     Keywords;
    uint32_t     Version;
    uint32_t     Level;
};

void DumpMetadataDefinition(EventCacheMetadata metadataDef);
void DumpBlobHeader(EventBlobHeader& header);


// TODO: move it to .cpp when no more used in EventPipeSession.cpp
enum EventIDs : uint32_t
{
    AllocationTick = 10,
    ExceptionThrown = 80,
    ContentionStart = 81,
    ContentionStop = 91,
};


class BlockParser
{
public:
    BlockParser();
    bool Parse(uint8_t* pBlock, uint32_t bytesCount, uint64_t blockOriginInFile);
    void SetPointerSize(uint8_t pointerSize);

public:
    uint8_t PointerSize;

protected:
    virtual bool OnParse() = 0;

    // Access helpers
    bool Read(LPVOID buffer, DWORD bufferSize);
    bool ReadByte(uint8_t& byte);
    bool ReadWord(uint16_t& word);
    bool ReadDWord(uint32_t& dword);
    bool ReadLong(uint64_t& ulong);
    bool ReadDouble(double& d);
    bool ReadVarUInt32(uint32_t& val, DWORD& size);
    bool ReadVarUInt64(uint64_t& val, DWORD& size);
    bool ReadWString(std::wstring& wstring, DWORD& bytesRead);
    bool SkipBytes(uint32_t byteCount);

private:
    bool CheckBoundaries(uint32_t byteCount);

// shared fields
protected:
    bool _is64Bit;
    uint32_t _blockSize;
    uint32_t _pos;

private:
    uint8_t* _pBlock;
    uint64_t _blockOriginInFile;
};


class EventParserBase : public BlockParser
{
public:
    EventParserBase(std::unordered_map<uint32_t, EventCacheMetadata>& metadata);

protected:
    std::unordered_map<uint32_t, EventCacheMetadata>& _metadata;

protected:
    virtual bool OnParse();
    virtual bool OnParseBlob(EventBlobHeader& header, bool isCompressed, DWORD& blobSize) = 0;
    virtual const char* GetBlockName() = 0;

// helpers
protected:
    bool ReadCompressedHeader(EventBlobHeader& header, DWORD& size);
    bool ReadUncompressedHeader(EventBlobHeader& header, DWORD& size);
};


// Available block parsers
//

class MetadataParser : public EventParserBase
{
public:
    MetadataParser(std::unordered_map<uint32_t, EventCacheMetadata>& metadata);

protected:
    virtual bool OnParseBlob(EventBlobHeader& header, bool isCompressed, DWORD& blobSize);
    virtual const char* GetBlockName()
    {
        return "Metadata";
    }
};


class EventParser : public EventParserBase
{
// TODO: probably pass a IEventListener interface that contains OnException, OnAllocationTick,...
public:
    EventParser(std::unordered_map<uint32_t, EventCacheMetadata>& metadata);

protected:
    virtual bool OnParseBlob(EventBlobHeader& header, bool isCompressed, DWORD& blobSize);
    virtual const char* GetBlockName()
    {
        return "Event";
    }

// event handlers
private:
    bool OnExceptionThrown(DWORD payloadSize, EventCacheMetadata& metadataDef);
    bool OnAllocationTick(DWORD payloadSize, EventCacheMetadata& metadataDef);
    bool OnContentionStop(uint64_t threadId, DWORD payloadSize, EventCacheMetadata& metadataDef);
};


// from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
#pragma pack(1)
struct StackBlockHeader
{
    uint32_t FirstId;
    uint32_t Count;
};

inline void DumpStackHeader(StackBlockHeader header)
{
    std::cout << "\nStack block header:\n";
    std::cout << "   FirstID: " << header.FirstId << "\n";
    std::cout << "   Count  : " << header.Count << "\n";
}

class EventCacheStack32
{
public:
    uint32_t Id;
    std::vector<uint32_t> Frames;
};

class EventCacheStack64
{
public:
    uint32_t Id;
    std::vector<uint64_t> Frames;
};


class StackParser : public BlockParser
{
public:
    StackParser(
        std::unordered_map<uint32_t, EventCacheStack32>& stacks32,
        std::unordered_map<uint32_t, EventCacheStack64>& stacks64
        );

protected:
    virtual bool OnParse();

private:
    bool ParseStack(uint32_t stackId, DWORD& size);

private:
    std::unordered_map<uint32_t, EventCacheStack32>& _stacks32;
    std::unordered_map<uint32_t, EventCacheStack64>& _stacks64;
};


class SequencePointParser : public BlockParser
{
public:
    SequencePointParser(
        std::unordered_map<uint32_t, EventCacheStack32>& stacks32,
        std::unordered_map<uint32_t, EventCacheStack64>& stacks64
    );

protected:
    virtual bool OnParse();

private:
    std::unordered_map<uint32_t, EventCacheStack32>& _stacks32;
    std::unordered_map<uint32_t, EventCacheStack64>& _stacks64;
};