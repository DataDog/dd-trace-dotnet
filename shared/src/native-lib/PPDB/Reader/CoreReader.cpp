// Copyright (c) 2019 Aaron R Robinson

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#include <cassert>
#include <iterator>
#include <fstream>
#include <PPDBReader.hpp>

using PPDB::ErrorCode;
using PPDB::Exception;
using PPDB::PdbStreamReader;
using PPDB::MetadataStreamReader;
using PPDB::MetadataTable;
using PPDB::PortablePdbReader;
using PPDB::RelativeLocation;

Exception::Exception(ErrorCode ec)
    : Exception{ ec, "Portable PDB" }
{
}

Exception::Exception(ErrorCode ec, MetadataTable table)
    : Error{ ec }
    , Table{ table }
{
    assert(Table != MetadataTable::Unknown);
}

Exception::Exception(ErrorCode ec, std::string name)
    : Error{ ec }
    , Table{ MetadataTable::Unknown }
    , Name{ std::move(name) }
{
    assert(!Name.empty());
}

std::shared_ptr<PortablePdbReader> PortablePdbReader::CreateReader(std::vector<uint8_t> data)
{
    assert(!data.empty());
    auto reader = std::shared_ptr<PortablePdbReader>(new PortablePdbReader{std::move(data)});

    // Create a weak pointer to the reader itself
    reader->_this = reader;
    return reader;
}

std::shared_ptr<PortablePdbReader> PortablePdbReader::CreateReader(const char *file)
{
    std::ifstream pdbFile{file, std::ios_base::in | std::ios_base::binary};

    if (!pdbFile) throw Exception{ErrorCode::FileReadFailure};

    // get the file size
    pdbFile.seekg(0, std::ios::end);
    auto length = pdbFile.tellg();

    pdbFile.seekg(0, std::ios::beg);

    // Read contents into buffer
    std::vector<uint8_t> fileContents;
    fileContents.resize(length);

    pdbFile.read(reinterpret_cast<char*>(fileContents.data()), length);

    return CreateReader(std::move(fileContents));
}

namespace
{
    struct PortablePdbMin
    {
        const uint32_t Magic;
        const uint16_t MajorVersion;
        const uint16_t MinorVersion;
        const uint32_t Reserved;
        const uint32_t Length;
        // const char Version[]
        // const uint16_t Flags
        // const uint16_t Streams
    };

    struct StreamHeader
    {
        const uint32_t Offset;
        const uint32_t Size;
        // const char Name[];
    };

    struct StreamResult
    {
        StreamHeader Header;
        std::string Name;
        const uint8_t *NewPos;
    };

    StreamResult ReadStreamHeader(const plat::data_view<uint8_t> s)
    {
        auto curr = std::begin(s);
        auto header = reinterpret_cast<const StreamHeader *>(curr);
        curr += sizeof(*header);
        if (curr >= std::end(s))
            throw Exception{ ErrorCode::CorruptFormat };

        // The name is UTF8 encoded and null terminated. Note that the
        // name length is padded with 0s on a 4 byte boundary.
        auto name = reinterpret_cast<const char *>(curr);

        // Look for a null at the next 4-byte boundary
        do
        {
            curr += 4;
            if (curr >= std::end(s))
                throw Exception{ ErrorCode::CorruptFormat };
        }
        while (*(curr - 1) != 0);

        return { *header, name, curr };
    }
}

PortablePdbReader::PortablePdbReader(std::vector<uint8_t> data)
    : _data{ std::move(data) }
    , _version{ "" }
{
    _data_view = plat::data_view<uint8_t>{ _data.size(), _data.data() };
    auto header = reinterpret_cast<const PortablePdbMin *>(std::begin(_data_view));

    // Check min size and magic number: 'BJSB'
    if (_data_view.size() < sizeof(*header)
        || header->Magic != 0x424A5342)
    {
        throw Exception{ ErrorCode::CorruptFormat };
    }

    // Store the version string offset - consumer may want it.
    // Specification indicates it is UTF-8 and null terminated,
    // so we can treat it as a C-string for now.
    auto versionOffset = std::begin(_data_view) + sizeof(*header);
    _version = reinterpret_cast<const char *>(versionOffset);

    // Move passed the version string and flags
    const uint8_t *streams = versionOffset + header->Length + sizeof(uint16_t);
    if (streams >= std::end(_data_view))
        throw Exception{ ErrorCode::CorruptFormat };

    // Determine how many streams exist
    auto streamCount = *reinterpret_cast<const uint16_t*>(streams);
    streams += sizeof(streamCount);
    if (streams >= std::end(_data_view))
        throw Exception{ ErrorCode::CorruptFormat };

    // Process streams
    for (int i = 0; i < streamCount; ++i)
    {
        size_t remain = std::end(_data_view) - streams;
        StreamResult result = ReadStreamHeader({ remain, streams });
        const uint32_t extent = result.Header.Offset + result.Header.Size;

        // Validate the returned stream extent
        if (_data_view.size() < extent)
            throw Exception{ ErrorCode::InvalidStreamExtent, result.Name };

        assert(_entries.find(result.Name) == std::end(_entries));
        _entries[std::move(result.Name)] = RelativeLocation{ result.Header.Size, result.Header.Offset };

        streams = result.NewPos;
    }
}

std::string PortablePdbReader::Version() const
{
    return { _version };
}

const uint8_t *PortablePdbReader::GetOffset(size_t offset) const
{
    if (_data_view.size() <= offset)
        return nullptr;

    return std::begin(_data_view) + offset;
}

RelativeLocation PortablePdbReader::GetLocationByName(const std::string &name) const
{
    auto iter = _entries.find(name);
    if (iter == std::end(_entries))
        return {};

    return iter->second;
}
