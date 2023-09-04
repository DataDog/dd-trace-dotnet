#include <iostream>
#include <unordered_map>

#include "DiagnosticsProtocol.h"
#include "NettraceFormat.h"
#include "BlockParser.h"


void DumpMetadataDefinition(EventCacheMetadata metadataDef)
{
    std::cout << "\nMetadata definition:\n";
    std::cout << "   Provider: ";
    std::wcout << metadataDef.ProviderName.c_str();
    std::cout << "\n";
    std::cout << "   Name    : ";
    std::wcout << metadataDef.EventName.c_str();
    std::cout << "\n";
    std::cout << "   ID      : " << metadataDef.EventId << "\n";
    std::cout << "   Version : " << metadataDef.Version << "\n";
    std::cout << "   Keywords: 0x" << std::hex << metadataDef.Keywords << std::dec << "\n";
    std::cout << "   Level   : " << metadataDef.Level << "\n";
}

MetadataParser::MetadataParser(std::unordered_map<uint32_t, EventCacheMetadata>& metadata)
    :
    EventParserBase(metadata)
{
}

bool MetadataParser::OnParseBlob(EventBlobHeader& header, bool isCompressed, DWORD& blobSize)
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

    // keep track of the only read bytes in the payload
    DWORD readBytesCount = 0;
    DWORD size = 0;

    // from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
    // A metadata blob is supposed to contain:
    //
    //  int MetaDataId;      // The Meta-Data ID that is being defined.
    //  string ProviderName; // The 2 byte Unicode, null terminated string representing the Name of the Provider (e.g. EventSource)
    //  int EventId;         // A small number that uniquely represents this Event within this provider.
    //  string EventName;    // The 2 byte Unicode, null terminated string representing the Name of the Event
    //  long Keywords;       // 64 bit set of groups (keywords) that this event belongs to.
    //  int Version          // The version number for this event.
    //  int Level;           // The verbosity (5 is verbose, 1 is only critical) for the event.
    //

    uint32_t metadataId;
    if (!ReadDWord(metadataId))
    {
        std::cout << "Error while reading metadata provider name\n";
        return false;
    }
    readBytesCount += sizeof(metadataId);

    auto& metadataDef = _metadata[metadataId];
    metadataDef.MetadataId = metadataId;

    // look for the provider name
    metadataDef.ProviderName.reserve(48);  // no provider name longer than 32+ characters
    if (!ReadWString(metadataDef.ProviderName, size))
    {
        std::cout << "Error while reading metadata provider name\n";
        return false;
    }
    readBytesCount += size;

    if (!ReadDWord(metadataDef.EventId))
    {
        std::cout << "Error while reading metadata event ID\n";
        return false;
    }
    readBytesCount += sizeof(metadataDef.EventId);

    // could be empty
    if (!ReadWString(metadataDef.EventName, size))
    {
        std::cout << "Error while reading metadata event name\n";
        return false;
    }
    readBytesCount += size;

    if (!ReadLong(metadataDef.Keywords))
    {
        std::cout << "Error while reading metadata keywords\n";
        return false;
    }
    readBytesCount += sizeof(metadataDef.Keywords);

    if (!ReadDWord(metadataDef.Version))
    {
        std::cout << "Error while reading metadata version\n";
        return false;
    }
    readBytesCount += sizeof(metadataDef.Version);

    if (!ReadDWord(metadataDef.Level))
    {
        std::cout << "Error while reading metadata level\n";
        return false;
    }
    readBytesCount += sizeof(metadataDef.Level);

    DumpMetadataDefinition(metadataDef);

    // skip remaining payload
    SkipBytes(header.PayloadSize - readBytesCount);

    blobSize += header.PayloadSize;
    return true;
}
