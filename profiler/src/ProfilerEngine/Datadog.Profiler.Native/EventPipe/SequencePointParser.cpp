#include <iostream>
#include <iomanip>

#include "BlockParser.h"

SequencePointParser::SequencePointParser(
    std::unordered_map<uint32_t, EventCacheStack32>& stacks32,
    std::unordered_map<uint32_t, EventCacheStack64>& stacks64
) :
    _stacks32(stacks32),
    _stacks64(stacks64)
{
}

inline void DumpSequencePointHeader(uint64_t timestamp, uint32_t threadCount)
{
    std::cout << "\nSequence Point block header:\n";
    std::cout << "   Timestamp    : " << timestamp << "\n";
    std::cout << "   Thread Count : " << threadCount << "\n";
}

// a SequencePoint block payload contains the following fields
//    TimeStamp     long
//    ThreadCount   int
//    A sequence of ThreadCount threads, each of which is encoded :
//       ThreadId       long
//       SequenceNumber int
//
bool SequencePointParser::OnParse()
{
    // reset stack caches
    // read https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md#sequencepointblock-object
    // for more details
    _stacks32.clear();
    _stacks64.clear();

    std::cout << "========================================================\n";

    // read header
    uint64_t timestamp;
    uint32_t threadCount;

    if (!ReadLong(timestamp))
    {
        std::cout << "Error while reading timestamp\n";
        return false;
    }
    if (!ReadDWord(threadCount))
    {
        std::cout << "Error while reading thread count\n";
        return false;
    }
    DumpSequencePointHeader(timestamp, threadCount);

    // read per thread sequence number
    uint64_t threadId;
    uint32_t sequenceNumber;
    for (size_t currentThread = 0; currentThread < threadCount; currentThread++)
    {
        if (!ReadLong(threadId))
        {
            std::cout << "Error while reading thread id #" << currentThread << "\n";
            return false;
        }

        if (!ReadDWord(sequenceNumber))
        {
            std::cout << "Error while reading sequence number #" << currentThread << "\n";
            return false;
        }

        std::cout << "   " << std::setw(8) << threadId << " | " << sequenceNumber << "\n";
    }

    return true;
}