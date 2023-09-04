#include <iostream>

#include "BlockParser.h"


StackParser::StackParser(
    std::unordered_map<uint32_t, EventCacheStack32>& stacks32,
    std::unordered_map<uint32_t, EventCacheStack64>& stacks64
) :
_stacks32(stacks32),
_stacks64(stacks64)
{
}


bool StackParser::OnParse()
{
    StackBlockHeader stackHeader;
    if (!Read(&stackHeader, sizeof(stackHeader)))
    {
        std::cout << "Error while reading stack block header\n";
        return false;
    }

    // TODO: uncomment to dump stack header
    DumpStackHeader(stackHeader);

    // from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
    //
    // The payload contains callstacks as a sequence of:
    //   uint32_t bytesCount
    //   list of addresses (up to bytesCount)
    // the id of each callstack is computed based on the stackHeader.FirstId
    // (i.e. incrementing it after a callstack is read)
    // Note: it is possible to have empty callstack (bytesCount == 0)
    //
    uint32_t stackId = stackHeader.FirstId;
    DWORD stackSize = 0;
    DWORD totalStacksSize = 0;
    DWORD remainingBlockSize = _blockSize - sizeof(stackHeader);
    while (ParseStack(stackId, stackSize))
    {
        stackId++;
        totalStacksSize += stackSize;
        stackSize = 0;

        if (totalStacksSize >= remainingBlockSize - 1) // try to detect last stack
        {
            // don't forget to check the end block tag
            uint8_t tag;
            if (!ReadByte(tag) || (tag != NettraceTag::EndObject))
            {
                std::cout << "Missing end of block tag: " << (uint8_t)tag << "\n";
                return false;
            }

            return true;
        }
    }

    return false;
}

bool StackParser::ParseStack(uint32_t stackId, DWORD& size)
{
    uint32_t stackSize;
    if (!ReadDWord(stackSize))
    {
        std::cout << "Error while reading stack #" << stackId << "\n";
        return false;
    }
    size += sizeof(stackSize);

    uint16_t frameCount = stackSize / PointerSize;

    // check for empty stacks
    if (frameCount == 0)
        return true;

    if (PointerSize == 8)
    {
        _stacks64[stackId].Id = stackId;
        _stacks64[stackId].Frames.reserve(frameCount);
    }
    else
    {
        _stacks32[stackId].Id = stackId;
        _stacks32[stackId].Frames.reserve(frameCount);
    }

    //std::cout << frameCount << " frames\n";
    // add frames
    for (size_t i = 0; i < frameCount; i++)
    {
        if (PointerSize == 8)
        {
            uint64_t frame;
            if (!ReadLong(frame))
            {
                std::cout << "Error while reading stack frame #" << i << "\n";
                return false;
            }
            size += sizeof(frame);

            _stacks64[stackId].Frames.push_back(frame);
            //std::cout << "   " << std::hex << frame << std::dec << "\n";
        }
        else
        {
            uint32_t frame;
            if (!ReadDWord(frame))
            {
                auto error = ::GetLastError();
                std::cout << "Error while reading stack frame #" << i << "\n";
                return false;
            }
            size += sizeof(frame);

            _stacks32[stackId].Frames.push_back(frame);
            //std::cout << "   " << std::hex << frame << std::dec << "\n";
        }
    }

    return true;
}
