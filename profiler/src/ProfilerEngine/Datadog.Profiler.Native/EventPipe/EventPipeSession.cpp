#include <iostream>

#include "EventPipeSession.h"
#include "DiagnosticsProtocol.h"
#include "DiagnosticsClient.h"

// from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
//
// The header is formed by:
//  "Nettrace" in ASCII (no final \0)
//  20 as uint32_t (length of following string)
//  "!FastSerialization.1" in ASCII

#pragma pack(1)
struct NettraceHeader
{
    uint8_t Magic[8];               // "Nettrace" with not '\0'
    uint32_t FastSerializationLen;  // 20
    uint8_t FastSerialization[20];  // "!FastSerialization.1" with not '\0'
};

const char* NettraceHeaderMagic = "Nettrace";
const char* FastSerializationMagic = "!FastSerialization.1";

bool IsSameAsString(uint8_t* bytes, uint16_t length, const char* characters)
{
    return memcmp(bytes, characters, length) == 0;
}

bool CheckNettraceHeader(NettraceHeader& header)
{
    if (!IsSameAsString(header.Magic, sizeof(header.Magic), NettraceHeaderMagic))
        return false;

    if (header.FastSerializationLen != strlen(FastSerializationMagic))
        return false;

    if (!IsSameAsString(header.FastSerialization, sizeof(header.FastSerialization), FastSerializationMagic))
        return false;

    return true;
};


// from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
//
// The TraceObject header is formed by an ObjectHeader followed by:
//  "Trace" in ASCII (no final \0)

#pragma pack(1)
struct TraceObjectHeader : ObjectHeader
{
  //NettraceTag TagTraceObject;         // 5
  //NettraceTag TagTypeObjectForTrace;  // 5
  //NettraceTag TagType;                // 1
  //uint32_t Version;                   // 4
  //uint32_t MinReaderVersion;          // 4
  //uint32_t NameLength;                // 5
    uint8_t Name[5];                    // 'Trace'
    NettraceTag TagEndTraceObject;      // 6
};

bool CheckTraceObjectHeader(TraceObjectHeader& header)
{
    if (header.TagTraceObject != NettraceTag::BeginPrivateObject) return false;
    if (header.TagTypeObjectForTrace != NettraceTag::BeginPrivateObject) return false;
    if (header.TagType != NettraceTag::NullReference) return false;

    if (header.MinReaderVersion != 4) return false;

    if (header.NameLength != 5) return false;
    if (!IsSameAsString(header.Name, sizeof(header.Name), "Trace")) return false;

    if (header.TagEndTraceObject != NettraceTag::EndObject) return false;

    return true;
}

const uint32_t BLOCK_SIZE = 4*1024;
const uint32_t MAX_BLOCK_SIZE = 100*1024;  // max buffer size sent by CLR

EventPipeSession::EventPipeSession(int pid, IIpcEndpoint* pEndpoint, uint64_t sessionId)
    :
    _pid(pid),
    _metadataParser(_metadata),
    _eventParser(_metadata),
    _stackParser(_stacks32, _stacks64),
    _sequencePointParser(_stacks32, _stacks64),
    _pEndpoint(pEndpoint),
    SessionId(sessionId)
{
    Is64Bit = true;  // will be computed when the nettrace stream will be read in Listen()
    Error = 0;
    _position = 0;
    _stopRequested = false;
    _blobHeader = {};
    _blockSize = BLOCK_SIZE;
    _pBlock = new uint8_t[_blockSize];
    ::ZeroMemory(_pBlock, _blockSize);
}

EventPipeSession::~EventPipeSession()
{
    delete [] _pBlock;
}

bool EventPipeSession::Listen()
{
    if (!ReadHeader())
        return false;

    if (!ReadTraceObjectHeader())
        return false;

    ObjectFields ofTrace;
    if (!ReadObjectFields(ofTrace))
        return false;

    // use the "trace object" fields to figure out the bitness of the application
    Is64Bit = ofTrace.PointerSize == 8;
    _stackParser.SetPointerSize(ofTrace.PointerSize);
    _metadataParser.SetPointerSize(ofTrace.PointerSize);
    _eventParser.SetPointerSize(ofTrace.PointerSize);

    // don't forget to check the end object tag
    uint8_t tag;
    if (!ReadByte(tag) || (tag != NettraceTag::EndObject))
        return false;

    // read one "object" after the other
    // until the EventPipe gets deconnected
    // after the Stop command has been processed
    while (ReadNextObject())
    {
        std::cout << "------------------------------------------------\n";
        std::cout << "\n________________________________________________\n";
    }

    return _stopRequested;
}


bool EventPipeSession::Stop()
{
    _stopRequested = true;

    if (_pid == -1)
        return true;

    // it is neeeded to use a different ipc connection to stop the Session
    DiagnosticsClient* pStopClient = DiagnosticsClient::Create(_pid, nullptr);
    pStopClient->StopEventPipeSession(SessionId);
    delete pStopClient;

    return true;
}


void DumpObjectHeader(ObjectHeader& header)
{
    std::cout << "\nObjectHeader: \n";
    std::cout << "   TagTraceObject         = " << (uint8_t)header.TagTraceObject << "\n";
    std::cout << "   TagTypeObjectForTrace  = " << (uint8_t)header.TagTypeObjectForTrace << "\n";
    std::cout << "   TagType                = " << (uint8_t)header.TagType << "\n";
    std::cout << "   Version                = " << header.Version << "\n";
    std::cout << "   MinReaderVersion       = " << header.MinReaderVersion << "\n";
    std::cout << "   NameLength             = " << header.NameLength << "\n";
}

// look at FastSerialization implementation with a decompiler:
//  .ReadObject()
//  .ReadObjectDefinition()
bool EventPipeSession::ReadNextObject()
{
    // get the type of object from the header
    ObjectHeader header;
    if (!Read(&header, sizeof(ObjectHeader)))
    {
        Error = ::GetLastError();
        if (Error == ERROR_PIPE_NOT_CONNECTED)
        {
            std::cout << "EventPipe has been deconnected...\n";
        }
        else
        {
            std::cout << "Error while reading Object header: 0x" << std::hex << Error << std::dec << "\n";
        }

        return false;
    }

    ObjectType ot = GetObjectType(header);
    if (ot == ObjectType::Unknown)
    {
        std::cout << "Invalid object header type:\n";
        DumpObjectHeader(header);
        return false;
    }

    // don't forget to check the end object tag
    uint8_t tag;
    if (!ReadByte(tag) || (tag != NettraceTag::EndObject))
    {
        std::cout << "Missing end of object tag: " << (uint8_t)tag << "\n";
        return false;
    }

    switch (ot)
    {
        case ObjectType::EventBlock:
            return ParseEventBlock(header);
        case ObjectType::MetadataBlock:
            return ParseMetadataBlock(header);
        case ObjectType::StackBlock:
            return ParseStackBlock(header);
        case ObjectType::SequencePointBlock:
            return ParseSequencePointBlock(header);

        default:
            return false;
    }
}

const char* EventBlockName = "EventBlock";
const char* MetadataBlockName = "MetadataBlock";
const char* StackBlockName = "StackBlock";
const char* SequencePointBlockName = "SPBlock";

ObjectType EventPipeSession::GetObjectType(ObjectHeader& header)
{
    // check validity
    if (header.TagTraceObject != NettraceTag::BeginPrivateObject) return ObjectType::Unknown;
    if (header.TagTypeObjectForTrace != NettraceTag::BeginPrivateObject) return ObjectType::Unknown;
    if (header.TagType != NettraceTag::NullReference) return ObjectType::Unknown;

    // figure out which type it is based on the name:
    //   EventBlock -> "EventBlock"  (size = 10)
    //   MetadataBlock -> "MetadataBlock" (size = 13)
    //   StackBlock -> "StackBlock" (size = 10)
    //   SequencePointBlock -> "SPBlock" (size = 7)
    if (header.NameLength == 13)
    {
        uint8_t buffer[13];
        if (!Read(buffer, 13))
            return ObjectType::Unknown;

        if (IsSameAsString(buffer, 13, MetadataBlockName))
            return ObjectType::MetadataBlock;

        return ObjectType::Unknown;
    }
    else
    if (header.NameLength == 10)
    {
        uint8_t buffer[10];
        if (!Read(buffer, 10))
            return ObjectType::Unknown;

        if (IsSameAsString(buffer, 10, EventBlockName))
            return ObjectType::EventBlock;
        else
        if (IsSameAsString(buffer, 10, StackBlockName))
            return ObjectType::StackBlock;

        return ObjectType::Unknown;
    }
    else
    if (header.NameLength == 7)
    {
        uint8_t buffer[7];
        if (!Read(buffer, 7))
            return ObjectType::Unknown;

        if (IsSameAsString(buffer, 7, SequencePointBlockName))
            return ObjectType::SequencePointBlock;

        return ObjectType::Unknown;
    }

    return ObjectType::Unknown;
}

bool EventPipeSession::ReadBlockSize(const char* blockName, uint32_t& blockSize)
{
    if (!ReadDWord(blockSize))
    {
        Error = ::GetLastError();
        std::cout << "Error while reading " << blockName << " block size: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }
    // Note: blockSizeInBytes does not include padding bytes to ensure alignment.

    // the rest of the block must be 4 bytes aligned with the beginning of the file
    if (!SkipPadding())
        return false;

    return true;
}


// look at:
//  EventpipeEventBlock.ReadBlockContent()
bool EventPipeSession::ParseEventBlock(ObjectHeader& header)
{
    if (header.MinReaderVersion != 2) return false;

    //// TODO: uncomment to dump the event block instead of parsing it
    //return SkipBlock("Event");

    uint32_t blockSize = 0;

    // read the block and send it to the corresponding parser
    uint64_t blockOriginInFile = 0;
    if (!ExtractBlock("Event", blockSize, blockOriginInFile))
        return false;

    return _eventParser.Parse(_pBlock, blockSize, blockOriginInFile);
}


// look at implementation:
//  TraceEventNativeMethods.EVENT_RECORD* ReadEvent() implementation
//  EventPipeBlock.FromStream(Deserializer)
bool EventPipeSession::ParseMetadataBlock(ObjectHeader& header)
{
    if (header.MinReaderVersion != 2) return false;

    // TODO: uncomment to dump metadata block
    //return SkipBlock("Metadata");

    uint32_t blockSize = 0;

    // read the block and send it to the corresponding parser
    uint64_t blockOriginInFile = 0;
    if (!ExtractBlock("Metadata", blockSize, blockOriginInFile))
        return false;

    return _metadataParser.Parse(_pBlock, blockSize, blockOriginInFile);
}

const std::wstring DotnetRuntimeProvider = L"Microsoft-Windows-DotNETRuntime";
const std::wstring EventPipeProvider = L"Microsoft-DotNETCore-EventPipe";


bool EventPipeSession::ParseStackBlock(ObjectHeader& header)
{
    if (header.MinReaderVersion != 2) return false;

    uint32_t blockSize = 0;

    // read the block and send it to the corresponding parser
    uint64_t blockOriginInFile = 0;
    if (!ExtractBlock("Stack", blockSize, blockOriginInFile))
        return false;

    return _stackParser.Parse(_pBlock, blockSize, blockOriginInFile);
}

bool EventPipeSession::ParseSequencePointBlock(ObjectHeader& header)
{
    if (header.MinReaderVersion != 2) return false;

    //// uncomment to skip sequence point block parsing
    //return SkipBlock("SequencePoint");

    uint32_t blockSize = 0;

    // read the block and send it to the corresponding parser
    uint64_t blockOriginInFile = 0;
    if (!ExtractBlock("SequencePoint", blockSize, blockOriginInFile))
        return false;

    return _sequencePointParser.Parse(_pBlock, blockSize, blockOriginInFile);

}


bool EventPipeSession::ExtractBlock(const char* blockName, uint32_t& blockSize, uint64_t& blockOriginInFile)
{
    // get the block size
    if (!ReadBlockSize(blockName, blockSize))
        return false;

    // skip the block + final EndOfObject tag
    blockSize++;

    // check if it is needed to resize the block buffer
    if (_blockSize < blockSize)
    {
        // don't expect blocks larger than 100KB
        if (blockSize > MAX_BLOCK_SIZE)
            return false;

        delete [] _pBlock;
        _pBlock = new uint8_t[blockSize];
        ::ZeroMemory(_pBlock, blockSize);
        _blockSize = blockSize;
    }

    // keep track of the current position in file for padding
    blockOriginInFile = _position;
    if (!Read(_pBlock, blockSize))
    {
        Error = ::GetLastError();
        std::cout << "Error while extracting " << blockName << " block: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }
    std::cout << "\n" << blockName << " block (" << blockSize << " bytes)\n";
    DumpBuffer(_pBlock, blockSize);

    return true;
}

bool EventPipeSession::SkipBlock(const char* blockName)
{
    // get the block size
    uint32_t blockSize = 0;
    if (!ReadBlockSize(blockName, blockSize))
        return false;

    // skip the block + final EndOfObject tag
    blockSize++;
    uint8_t* pBuffer = new uint8_t[blockSize];
    if (!Read(pBuffer, blockSize))
    {
        Error = ::GetLastError();
        std::cout << "Error while reading " << blockName << " block: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }
    std::cout << "\n" << blockName << " block (" << blockSize << " bytes)\n";
    DumpBuffer(pBuffer, blockSize);
    delete[] pBuffer;

    return true;
}

bool EventPipeSession::SkipBytes(DWORD byteCount)
{
    // use the stack for small buffer (no need to delete)
    uint8_t* pBuffer = static_cast<uint8_t*>(_alloca(byteCount));
    auto success = Read(pBuffer, byteCount);
    if (success)
    {
        std::cout << "skip " << byteCount << " bytes\n";
        DumpBuffer(pBuffer, byteCount);
    }

    return success;
}

bool EventPipeSession::Read(LPVOID buffer, DWORD bufferSize)
{
    DWORD readBytes = 0;
    auto success = _pEndpoint->Read(buffer, bufferSize, &readBytes);
    if (success)
    {
        _position += readBytes;
    }

    return success;
}

bool EventPipeSession::ReadByte(uint8_t& byte)
{
    return Read(&byte, sizeof(uint8_t));
}

bool EventPipeSession::ReadDWord(uint32_t& dword)
{
    return Read(&dword, sizeof(uint32_t));
}

bool EventPipeSession::ReadHeader()
{
    NettraceHeader header;
    if (!Read(&header, sizeof(header)))
    {
        Error = ::GetLastError();
        std::cout << "Error while reading Nettrace header: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    return CheckNettraceHeader(header);
}

bool EventPipeSession::ReadTraceObjectHeader()
{
    TraceObjectHeader header;
    if (!Read(&header, sizeof(header)))
    {
        Error = ::GetLastError();
        std::cout << "Error while reading Trace Object header: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    return CheckTraceObjectHeader(header);
}

bool EventPipeSession::ReadObjectFields(ObjectFields& objectFields)
{
    if (!Read(&objectFields, sizeof(objectFields)))
    {
        Error = ::GetLastError();
        std::cout << "Error while reading Object fields: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    return true;
}

bool EventPipeSession::SkipPadding()
{
    if (_position % 4 != 0)
    {
        // need to skip the padding
        uint8_t paddingLength = 4 - (_position % 4);
        uint8_t padding[4];
        if (!Read(padding, paddingLength))
        {
            Error = ::GetLastError();
            std::cout << "Error while skipping padding (" << paddingLength << " bytes): 0x" << std::hex << Error << std::dec << "\n";
            return false;
        }
    }

    return true;
}

