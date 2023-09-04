#pragma once

#include <unordered_map>
#include <vector>
#include <string>

#include "IIpcEndpoint.h"
#include "NettraceFormat.h"

#include "BlockParser.h"


class EventCacheThread
{
public:
    uint32_t SequenceNumber;
    uint64_t LastCachedEventTimestamp;
};


// TODO: define an interface IIEventPipeSession because it will propably
//       be used by the profilers pipeline. Maybe just mocking IIpcEndPoint could be enough
class EventPipeSession
{
public:
    EventPipeSession(int pid, IIpcEndpoint* pEndpoint, uint64_t sessionId);
    ~EventPipeSession();

    bool Listen();
    bool Stop();

public:
    DWORD Error;
    int _pid;
    uint64_t SessionId;

private:
    EventPipeSession();

    // helper functions that keep track of the current position
    // since the beginning of the "file"
    bool Read(LPVOID buffer, DWORD bufferSize);
    bool ReadByte(uint8_t& byte);
    bool ReadDWord(uint32_t& dword);

    // objects parsing helpers
    bool ReadHeader();
    bool ReadTraceObjectHeader();
    bool ReadObjectFields(ObjectFields& objectFields);
    bool ReadNextObject();
    ObjectType GetObjectType(ObjectHeader& header);

    bool ParseStackBlock(ObjectHeader& header);
    bool ParseMetadataBlock(ObjectHeader& header);
    bool ParseEventBlock(ObjectHeader& header);
    bool ParseSequencePointBlock(ObjectHeader& header);

    bool ExtractBlock(const char* blockName, uint32_t& blockSize, uint64_t& blockOriginInFile);
    bool ReadBlockSize(const char* blockName, uint32_t& blockSize);

    bool SkipBytes(DWORD byteCount);
    bool SkipPadding();
    bool SkipBlock(const char* blockName);

private:
    bool Is64Bit;
    IIpcEndpoint* _pEndpoint;
    bool _stopRequested;

    // parsers
    MetadataParser _metadataParser;
    EventParser _eventParser;
    StackParser _stackParser;
    SequencePointParser _sequencePointParser;

    // Keep track of the position since the beginning of the "file"
    // i.e. starting at 0 from the first character of the NettraceHeader
    //      Nettrace
    uint64_t _position;

    // buffer used to read each block that will be then parsed
    uint8_t* _pBlock;
    uint32_t _blockSize;

    // per block header
    EventBlobHeader _blobHeader;

    // per thread event info
    std::unordered_map<uint64_t, EventCacheThread> _threads;

    // per metadataID event metadata description
    std::unordered_map<uint32_t, EventCacheMetadata> _metadata;

    // per stackID stack
    // only one will be used depending on the bitness of the monitored application
    std::unordered_map<uint32_t, EventCacheStack32> _stacks32;
    std::unordered_map<uint32_t, EventCacheStack64> _stacks64;
};

