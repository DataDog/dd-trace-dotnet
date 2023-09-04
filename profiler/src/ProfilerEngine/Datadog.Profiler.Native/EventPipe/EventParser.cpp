#include <iostream>

#include "DiagnosticsProtocol.h"
#include "BlockParser.h"


EventParser::EventParser(std::unordered_map<uint32_t, EventCacheMetadata>& metadata)
    :
    EventParserBase(metadata)
{
}


// look at:
//  EventpipeEventBlock.ReadBlockContent()
bool EventParser::OnParseBlob(EventBlobHeader& header, bool isCompressed, DWORD& blobSize)
{
    if (isCompressed)
    {
        if (!ReadCompressedHeader(header, blobSize))
        {
            return false;
        }
    }
    else
    {
        if (!ReadUncompressedHeader(header, blobSize))
        {
            return false;
        }
    }

    // TODO: uncomment to show blob header
    DumpBlobHeader(header);

    auto& metadataDef = _metadata[header.MetadataId];
    if (metadataDef.MetadataId == 0)
    {
        // this should never occur: no definition was previously received

        uint8_t* pBuffer = new uint8_t[header.PayloadSize];
        if (!Read(pBuffer, header.PayloadSize))
        {
            std::cout << "Error while reading EventBlob payload: 0x" << std::hex << Error << std::dec << "\n";

            delete[] pBuffer;
            return false;
        }

        std::cout << "Event blob\n";
        DumpBuffer(pBuffer, header.PayloadSize);
        delete[] pBuffer;
    }

    switch (metadataDef.EventId)
    {
        case EventIDs::AllocationTick:
            if (!OnAllocationTick(header.PayloadSize, metadataDef))
            {
                return false;
            }
            break;

        // TODO: check in which version of the CLR, the ContentionStop_V1 is available
        //       before that, it is needed to compute, per thread, the difference between Start and Stop
        case EventIDs::ContentionStop:
            if (!OnContentionStop(header.ThreadId, header.PayloadSize, metadataDef))
            {
                return false;
            }
            break;

        case EventIDs::ExceptionThrown:
            if (!OnExceptionThrown(header.PayloadSize, metadataDef))
            {
                return false;
            }
            break;

        default:  // skip events we are not interested in
        {
            std::cout << "Event = " << metadataDef.EventId << "\n";
            SkipBytes(header.PayloadSize);
        }
    }

    blobSize += header.PayloadSize;

    return true;
}


// from https://docs.microsoft.com/en-us/dotnet/framework/performance/garbage-collection-etw-events#gcallocationtick_v3-event
//  AllocationAmount    UInt32          The allocation size, in bytes.
//                                      This value is accurate for allocations that are less than the length of a ULONG(4,294,967,295 bytes).
//                                      If the allocation is greater, this field contains a truncated value.
//                                      Use AllocationAmount64 for very large allocations.
//  AllocationKind      UInt32          0x0 - Small object allocation(allocation is in small object heap).
//                                      0x1 - Large object allocation(allocation is in large object heap).
//  ClrInstanceID       UInt16          Unique ID for the instance of CLR or CoreCLR.
//  AllocationAmount64  UInt64          The allocation size, in bytes.This value is accurate for very large allocations.
//  TypeId              Pointer         The address of the MethodTable.When there are several types of objects that were allocated during this event,
//                                      this is the address of the MethodTable that corresponds to the last object allocated (the object that caused the 100 KB threshold to be exceeded).
//  TypeName            UnicodeString   The name of the type that was allocated.When there are several types of objects that were allocated during this event,
//                                      this is the type of the last object allocated (the object that caused the 100 KB threshold to be exceeded).
//  HeapIndex           UInt32          The heap where the object was allocated.This value is 0 (zero)when running with workstation garbage collection.
//  Address             Pointer         The address of the last allocated object.
//
bool EventParser::OnAllocationTick(DWORD payloadSize, EventCacheMetadata& metadataDef)
{
    DWORD readBytesCount = 0;
    DWORD size = 0;
    std::cout << "\nAllocation Tick:\n";

    // get common fields
    uint32_t dword = 0;
    if (!ReadDWord(dword))
    {
        std::cout << "Error while reading allocation tick amount\n";
        return false;
    }
    readBytesCount += sizeof(dword);
    std::cout << "   Amount        = " << dword << " bytes\n";

    if (!ReadDWord(dword))
    {
        std::cout << "Error while reading allocation tick kind\n";
        return false;
    }
    readBytesCount += sizeof(dword);
    std::cout << "   Kind          = " << ((dword == 1) ? "LOH" : "small") << " bytes\n";

    uint16_t word = 0;
    if (!ReadWord(word))
    {
        std::cout << "Error while reading allocation tick CLR instance ID\n";
        return false;
    }
    readBytesCount += sizeof(word);
    std::cout << "   CLR ID        = " << word << "\n";

    uint64_t ulong = 0;
    if (!ReadLong(ulong))
    {
        std::cout << "Error while reading allocation tick amount64\n";
        return false;
    }
    readBytesCount += sizeof(ulong);
    std::cout << "   Amount64      = " << ulong << " bytes\n";

    // skip useless MT address
    // Note: handle 32/64 bit difference
    if (_is64Bit)
    {
        if (!ReadLong(ulong))
        {
            std::cout << "Error while reading allocation tick MT address\n";
            return false;
        }
        readBytesCount += sizeof(ulong);
    }
    else
    {
        if (!ReadDWord(dword))
        {
            std::cout << "Error while reading allocation tick MT address\n";
            return false;
        }
        readBytesCount += sizeof(dword);
    }

    std::wstring strBuffer;
    strBuffer.reserve(128);
    if (!ReadWString(strBuffer, size))
    {
        std::cout << "Error while reading allocation tick type name\n";
        return false;
    }
    readBytesCount += size;
    if (strBuffer.empty())
        std::wcout << L"   Type          = ''\n";
    else
        std::wcout << L"   Type          = " << strBuffer.c_str() << L"\n";

    if (!ReadDWord(dword))
    {
        std::cout << "Error while reading allocation tick heap index\n";
        return false;
    }
    readBytesCount += sizeof(dword);
    std::cout << "   Heap index    = " << dword << "\n";

    // get additional fields if any
    if (metadataDef.Version >= 3)
    {
        // Note: handle 32/64 bit difference
        if (_is64Bit)
        {
            if (!ReadLong(ulong))
            {
                std::cout << "Error while reading allocation tick object address\n";
                return false;
            }
            readBytesCount += sizeof(ulong);
            std::cout << "   Object addr   = 0x" << std::hex << ulong << std::dec << "\n";
        }
        else
        {
            if (!ReadDWord(dword))
            {
                std::cout << "Error while reading allocation tick object address\n";
                return false;
            }
            readBytesCount += sizeof(dword);
            std::cout << "   Object addr   = 0x" << std::hex << dword << std::dec << "\n";
        }
    }

    // skip the rest of the payload
    return SkipBytes(payloadSize - readBytesCount);
}

// from https://docs.microsoft.com/en-us/dotnet/framework/performance/contention-etw-events
//    + https://github.com/dotnet/runtime/blob/main/src/coreclr/vm/ClrEtwAll.man#L1720 for V1
//  ContentionFlags win:UInt8   0 = Managed and 1 = Native
//  ClrInstanceID   win:UInt16
//  DurationNs      win:Double  duration of the contention in nanoseconds (only in V1)
//
bool EventParser::OnContentionStop(uint64_t threadId, DWORD payloadSize, EventCacheMetadata& metadataDef)
{
    DWORD readBytesCount = 0;
    DWORD size = 0;
    std::cout << "\nContention:\n";
    std::cout << "   Thread ID  = " << threadId << "\n";

    uint8_t flags = 0;
    if (!ReadByte(flags))
    {
        std::cout << "Error while reading contention end flags ID\n";
        return false;
    }
    readBytesCount += sizeof(flags);
    std::cout << "   Lock type  = " << ((flags == 0) ? "Managed" : "Native") << "\n";

    uint16_t word = 0;
    if (!ReadWord(word))
    {
        std::cout << "Error while reading contention end CLR instance ID\n";
        return false;
    }
    readBytesCount += sizeof(word);
    std::cout << "   CLR ID     = " << word << "\n";

    double d = 0;
    if (!ReadDouble(d))
    {
        std::cout << "Error while reading contention end duration\n";
        return false;
    }
    readBytesCount += sizeof(d);
    std::cout << "   Duration   = " << d / 1000000 << " ms\n";

    // skip the rest of the payload
    return SkipBytes(payloadSize - readBytesCount);
}


// from https://docs.microsoft.com/en-us/dotnet/framework/performance/exception-thrown-v1-etw-event
//
// Type             wstring     Exception type
// Message          wstring     Exception message
// EIPCodeThrow     win:Pointer Instruction pointer where exception occurred.
// ExceptionHR      win:UInt32  Exception HRESULT.
// ExceptionFlags   win:UInt16
//      0x01: HasInnerException (see CLR ETW Events in the Visual Basic documentation).
//      0x02: IsNestedException.
//      0x04: IsRethrownException.
//      0x08: IsCorruptedStateException (indicates that the process state is corrupt; see Handling Corrupted State Exceptions).
//      0x10: IsCLSCompliant (an exception that derives from Exception is CLS-compliant; otherwise, it is not CLS-compliant).
// ClrInstanceID	win:UInt16	Unique ID for the instance of CLR or CoreCLR.
//
bool EventParser::OnExceptionThrown(DWORD payloadSize, EventCacheMetadata& metadataDef)
{
    DWORD readBytesCount = 0;
    DWORD size = 0;

    // string: exception type
    // string: exception message
    std::wstring strBuffer;
    strBuffer.reserve(128);
    std::cout << "\nException thrown:\n";

    if (!ReadWString(strBuffer, size))
    {
        std::cout << "Error while reading exception thrown type name\n";
        return false;
    }
    readBytesCount += size;
    if (strBuffer.empty())
        std::wcout << L"   type    = ''\n";
    else
        std::wcout << L"   type    = " << strBuffer.c_str() << L"\n";

    strBuffer.clear();

    // Size of the ExceptionThrown payload AFTER the Message field
    uint16_t exceptionRemainingPayloadSize = (_is64Bit ? 8 : 4) + 4 + 2 + 2;

    // In case of "empty" message, it might not be even visible as "\0" before .NET Core 6 (and after, will be "NULL")
    // so it is needed to check if the remaining payload contains such a string
    if ((payloadSize - readBytesCount) == exceptionRemainingPayloadSize)
    {
        std::wcout << L"   message = ''\n";
    }
    else
    {
        if (!ReadWString(strBuffer, size))
        {
            std::cout << "Error while reading exception thrown message text\n";
            return false;
        }
        readBytesCount += size;

        // handle empty string case (check for "NULL" in case of .NET 6+)
        if (strBuffer.empty() || (wcscmp(strBuffer.c_str(), L"NULL") == 0))
            std::wcout << L"   message = ''\n";
        else
        {
            std::wcout << L"   message = " << strBuffer.c_str() << L"\n";
        }
    }

    // skip the rest of the payload
    return SkipBytes(payloadSize - readBytesCount);
}



void DumpBlobHeader(EventBlobHeader& header)
{
    std::cout << "\nblob header:\n";
    std::cout << "   IsSorted          = " << header.IsSorted << "\n";
    std::cout << "   PayloadSize       = " << header.PayloadSize << "\n";
    std::cout << "   MetadataId        = " << header.MetadataId << "\n";
    std::cout << "   SequenceNumber    = " << header.SequenceNumber << "\n";
    std::cout << "   ThreadId          = " << header.ThreadId << "\n";
    std::cout << "   CaptureThreadId   = " << header.CaptureThreadId << "\n";
    std::cout << "   ProcessorNumber   = " << header.ProcessorNumber << "\n";
    std::cout << "   StackId           = " << header.StackId << "\n";
    std::cout << "   Timestamp         = " << header.Timestamp << "\n";
    std::cout << "   ActivityId        = "; DumpGuid(header.ActivityId); std::cout << "\n";
    std::cout << "   RelatedActivityId = "; DumpGuid(header.RelatedActivityId); std::cout << "\n";
}
