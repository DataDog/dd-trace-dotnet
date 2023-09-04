#include <iostream>
#include <sstream>
#include "BlockParser.h"


EventParserBase::EventParserBase(std::unordered_map<uint32_t, EventCacheMetadata>& metadata)
    :
    _metadata(metadata)
{
    _is64Bit = true;  // will be set later
}

// look at implementation:
//  TraceEventNativeMethods.EVENT_RECORD* ReadEvent() implementation
//  EventPipeBlock.FromStream(Deserializer)
bool EventParserBase::OnParse()
{
    // read event block header
    EventBlockHeader ebHeader = {};
    if (!Read(&ebHeader, sizeof(ebHeader)))
    {
        std::cout << "Error while reading " << GetBlockName() << "Block header\n";
        return false;
    }

    // skip any optional content if any
    if (ebHeader.HeaderSize > sizeof(EventBlockHeader))
    {
        uint8_t optionalSize = ebHeader.HeaderSize - sizeof(EventBlockHeader);
        if (!SkipBytes(optionalSize))
        {
            std::cout << "Error while skipping optional info from " << GetBlockName() << "Block header\n";
            return false;
        }
    }

    // from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
    // the rest of the block is a list of Event blobs
    //
    DWORD blobSize = 0;
    DWORD totalBlobSize = 0;
    DWORD remainingBlockSize = _blockSize - ebHeader.HeaderSize;
    bool isCompressed = ((ebHeader.Flags & 1) == 1);

    // Note: in order to gain space, some fields of the header could be "inherited"
    // from the header of the previous blob --> need to pass it from blob to blob
    EventBlobHeader header = {};
    while (OnParseBlob(header, isCompressed, blobSize))
    {
        totalBlobSize += blobSize;
        blobSize = 0;

        if (totalBlobSize >= remainingBlockSize - 1) // try to detect last blob
        {
            // don't forget to check the end of block tag
            uint8_t tag;
            if (!ReadByte(tag) || (tag != NettraceTag::EndObject))
            {
                std::cout << "Missing end of block tag\n";
                return false;
            }

            return true;
        }
    }

    // TODO: for debug, continue if we fail to parse the block
    return true;
    //return false;

}

// from EventPipeEventHeader.ReadFromFormatV4
// https://github.dev/microsoft/perfview/blob/b5d1f0423ed5fb6521fae0f3c9e92c886752ac8d/src/TraceEvent/EventPipe/EventPipeEventSource.cs#L1439
bool EventParserBase::ReadUncompressedHeader(EventBlobHeader& header, DWORD& size)
{
    EventBlobHeader_V4 headerV4;
    if (!Read(&headerV4, sizeof(headerV4)))
    {
        std::cout << "Impossible to read uncompressed blob header\n";
        return false;
    }
    size += sizeof(headerV4);
    header.EventSize = headerV4.EventSize;
    header.MetadataId = headerV4.MetadataId & 0x7FFFFFFF;
    header.IsSorted = ((uint32_t)headerV4.MetadataId & 0x80000000) == 0;
    header.SequenceNumber = headerV4.SequenceNumber;
    header.ThreadId = headerV4.ThreadId;
    header.CaptureThreadId = headerV4.CaptureThreadId;
    header.ProcessorNumber = headerV4.ProcessorNumber;
    header.StackId = headerV4.StackId;
    header.Timestamp = headerV4.Timestamp;
    header.ActivityId = headerV4.ActivityId;
    header.RelatedActivityId = headerV4.RelatedActivityId;
    header.PayloadSize = headerV4.PayloadSize;
    header.HeaderSize = sizeof(headerV4);
    header.TotalNonHeaderSize = header.EventSize - header.HeaderSize;

    return true;
}

bool EventParserBase::ReadCompressedHeader(EventBlobHeader& header, DWORD& size)
{
    // used to compute the compressed header size
    uint32_t headerStartPos = _pos;

    // read Flags byte
    uint8_t flags;
    if (!ReadByte(flags))
    {
        std::cout << "Error while reading compressed header flags\n";
        return false;
    }
    size += sizeof(flags);
    header.IsSorted = (flags & 64) == 64;

    if ((flags & CompressedHeaderFlags::MetadataId) != 0)
    {
        if (!ReadVarUInt32(header.MetadataId, size))
        {
            std::cout << "Error while reading compressed header metadata ID\n";
            return false;
        }
    }

    if ((flags & CompressedHeaderFlags::CaptureThreadAndSequence) != 0)
    {
        uint32_t val;
        if (!ReadVarUInt32(val, size))
        {
            std::cout << "Error while reading compressed header sequence number\n";
            return false;
        }
        header.SequenceNumber += val + 1;

        if (!ReadVarUInt64(header.CaptureThreadId, size))
        {
            std::cout << "Error while reading compressed header captured thread ID\n";
            return false;
        }

        if (!ReadVarUInt32(header.ProcessorNumber, size))
        {
            std::cout << "Error while reading compressed header processor number\n";
            return false;
        }
    }
    else
    {
        if (header.MetadataId != 0)
        {
            // !! reuse the header from the previous blob
            header.SequenceNumber++;
        }
    }

    if ((flags & CompressedHeaderFlags::ThreadId) != 0)
    {
        if (!ReadVarUInt64(header.ThreadId, size))
        {
            std::cout << "Error while reading compressed header thread ID\n";
            return false;
        }
    }

    if ((flags & CompressedHeaderFlags::StackId) != 0)
    {
        if (!ReadVarUInt32(header.StackId, size))
        {
            std::cout << "Error while reading compressed header stack ID\n";
            return false;
        }
    }

    uint64_t timestampDelta = 0;
    if (!ReadVarUInt64(timestampDelta, size))
    {
        std::cout << "Error while reading compressed header timestamp delta\n";
        return false;
    }
    header.Timestamp += timestampDelta;

    if ((flags & CompressedHeaderFlags::ActivityId) != 0)
    {
        if (!Read(&header.ActivityId, sizeof(header.ActivityId)))
        {
            std::cout << "Error while reading compressed header activity ID\n";
            return false;
        }
        size += sizeof(header.ActivityId);
    }

    if ((flags & (byte)CompressedHeaderFlags::RelatedActivityId) != 0)
    {
        if (!Read(&header.RelatedActivityId, sizeof(header.RelatedActivityId)))
        {
            std::cout << "Error while reading compressed header related activity ID\n";
            return false;
        }
        size += sizeof(header.RelatedActivityId);
    }

    header.IsSorted = (flags & CompressedHeaderFlags::Sorted) != 0;

    if ((flags & CompressedHeaderFlags::DataLength) != 0)
    {
        if (!ReadVarUInt32(header.PayloadSize, size))
        {
            std::cout << "Error while reading compressed header payload size\n";
            return false;
        }
    }

    header.HeaderSize = _pos - headerStartPos;
    header.TotalNonHeaderSize = header.PayloadSize;

    return true;
}



BlockParser::BlockParser()
{
    _is64Bit = true;  // will be set later
    _pBlock = nullptr;
    _blockSize = 0;
    _pos = -1;
    _blockOriginInFile = 0;
    PointerSize = 0; // will be set later on (when the trace object payload is read)
}

void BlockParser::SetPointerSize(uint8_t pointerSize)
{
    PointerSize = pointerSize;
    if (pointerSize == 8)
    {
        _is64Bit = true;
    }
    else
        if (pointerSize == 4)
        {
            _is64Bit = false;
        }
        else
        {
            std::stringstream builder;
            builder << "Invalid pointer size: " << pointerSize;
            throw std::exception(builder.str().c_str());
        }
}

bool BlockParser::Parse(uint8_t* pBlock, uint32_t bytesCount, uint64_t blockOriginInFile)
{
    _pBlock = pBlock;
    _blockSize = bytesCount;
    _pos = 0;
    _blockOriginInFile = blockOriginInFile;

    return OnParse();
}

bool BlockParser::ReadByte(uint8_t& byte)
{
    return Read(&byte, sizeof(uint8_t));
}

bool BlockParser::ReadWord(uint16_t& word)
{
    return Read(&word, sizeof(uint16_t));
}

bool BlockParser::ReadDWord(uint32_t& dword)
{
    return Read(&dword, sizeof(uint32_t));
}

bool BlockParser::ReadLong(uint64_t& ulong)
{
    return Read(&ulong, sizeof(uint64_t));
}

bool BlockParser::ReadDouble(double& d)
{
    return Read(&d, sizeof(double));
}

bool BlockParser::Read(LPVOID buffer, DWORD bufferSize)
{
    if (!CheckBoundaries(bufferSize))
        return false;

    memcpy(buffer, &_pBlock[_pos], bufferSize);
    _pos += bufferSize;

    return true;
}

bool BlockParser::SkipBytes(uint32_t byteCount)
{
    if (byteCount == 0)
        return true;

    if (!CheckBoundaries(byteCount))
    {
        // TODO: DumpBuffer()
        return false;
    }

    // TODO: DumpBuffer()
    _pos += byteCount;

    return true;
}

bool BlockParser::ReadVarUInt32(uint32_t& val, DWORD& size)
{
    val = 0;
    int shift = 0;
    byte b;
    do
    {
        if (shift == 5 * 7)
        {
            return false;
        }

        if (!ReadByte(b))
        {
            return false;
        }
        size++;
        val |= (uint32_t)(b & 0x7f) << shift;
        shift += 7;
    } while ((b & 0x80) != 0);

    return true;
}

bool BlockParser::ReadVarUInt64(uint64_t& val, DWORD& size)
{
    val = 0;
    int shift = 0;
    byte b;
    do
    {
        if (shift == 10 * 7)
        {
            return false;
        }
        if (!ReadByte(b))
        {
            return false;
        }
        size++;
        val |= (uint64_t)(b & 0x7f) << shift;
        shift += 7;
    } while ((b & 0x80) != 0);

    return true;
}

// read UTF16 character one after another until the \0 is found to rebuild a string
bool BlockParser::ReadWString(std::wstring& wstring, DWORD& bytesRead)
{
    uint16_t character;
    bytesRead = 0;  // in case of empty string
    while (true)
    {
        if (!ReadWord(character))
        {
            return false;
        }

        // TODO: protect against invalid UNICODE character (due to missing fields in ExceptionThrown event)
        if (character > 256)
        {
            // rewind the character
            _pos = _pos - sizeof(character);

            // this is only covering a missing string
            return (bytesRead == 0);
        }

        bytesRead += sizeof(character);

        // Note that an empty string contains only that \0 character
        if (character == 0) // \0 final character of the string
            return true;

        wstring.push_back(character);
    }
}

// Check for block boundaries
// ------------------------------------
// ex: (1 byte was already read)
//   _pos       = 1
//   _blockSize = 5
//   bufferSize = 4
//
//                0   1   2   3   4
//                |   |   |   |   |
//                V   V   V   V   V
//              |   |   |   |   |   |
//  _pos              1
//  readable          o   o   o   o
//   |--> _blockSize - _pos = 4
//  so would return true
//
bool BlockParser::CheckBoundaries(uint32_t byteCount)
{
    if (byteCount > _blockSize - _pos)
    {
        std::cout << "too many bytes to read + " << byteCount - (_blockSize - _pos) << " bytes\n";
        return false;
    }

    return true;
}

