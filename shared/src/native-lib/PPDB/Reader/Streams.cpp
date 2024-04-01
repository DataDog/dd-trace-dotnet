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
#include <bitset>
#include <sstream>
#include <limits>
#include <cstring>
#include <PPDBReader.hpp>

using namespace PPDB;

const std::string GuidHeapReader::Name = "#GUID";

GuidHeapReader::GuidHeapReader(plat::data_view<uint8_t> view)
    : _heap{ std::move(view) }
{
    assert(_heap.data() != nullptr || _heap.empty());
}

GuidHeapReader::GuidHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc)
    : GuidHeapReader{ { loc.Length, reader->GetOffset(loc.Offset) } }
{
    _reader = std::move(reader);
}

size_t GuidHeapReader::Count() const
{
    return _heap.size();
}

const GUID &GuidHeapReader::Get(size_t index) const
{
    // Indices are 1 based
    if (index == 0 || Count() < index)
        throw Exception{ ErrorCode::InvalidIndex, Name };

    return _heap[index - 1];
}

namespace
{
    // ECMA-335 II.23.2
    uint32_t DecompressU32(const uint8_t **stream, size_t max)
    {
        assert(stream != nullptr);
        auto s = *stream;
        assert(s != nullptr);

        uint32_t val{};
        switch (*s & 0xc0)
        {
        case 0xc0:
            if (max < 4)
                throw Exception{ ErrorCode::CorruptFormat };

            val = ((*s++ & 0x1f) << 24);
            val |= (*s++ << 16);
            val |= (*s++ << 8);
            val |= *s++;
            break;

        case 0x80:
            if (max < 2)
                throw Exception{ ErrorCode::CorruptFormat };

            val = ((*s++ & 0x3f) << 8);
            val |= *s++;
            break;

        default:
            if (max < 1)
                throw Exception{ ErrorCode::CorruptFormat };

            val = *s++;
        }

        *stream = s;
        return val;
    }

    // ECMA-335 II.23.2
    int32_t DecompressS32(const uint8_t **stream, size_t max)
    {
        assert(stream != nullptr);
        auto initalPos = *stream;

        // Decompress the integer as unsigned
        uint32_t u32res = DecompressU32(stream, max);

        // Correct the signing
        int32_t val;
        ::memcpy(&val, &u32res, sizeof(u32res));

        bool isSigned = (val & 0x1) != 0;
        val >>= 1;

        if (isSigned)
        {
            // Based on compressed size, extend the sign
            switch (*stream - initalPos)
            {
            default:
                assert(false);
            case 4:
                val |= 0xf0000000;
                break;
            case 2:
                val |= 0xffffe000;
                break;
            case 1:
                val |= 0xffffffc0;
                break;
            }
        }

        return val;
    }

    // ECMA-335 II.23.2.8
    mdToken DecompressTypeDefOrRefOrSpecEncoded(const uint8_t **stream, size_t max)
    {
        uint32_t encodedToken = DecompressU32(stream, max);

        // The token type is encoded in the last two
        // least significant bits.
        uint32_t encodedType = encodedToken & 0x3;
        uint32_t val = encodedToken >> 2;

        uint32_t type;
        switch (encodedType)
        {
        default:
        case 0:
            type = static_cast<uint32_t>(MetadataTable::TypeDef);
            break;
        case 1:
            type = static_cast<uint32_t>(MetadataTable::TypeRef);
            break;
        case 2:
            type = static_cast<uint32_t>(MetadataTable::TypeSpec);
            break;
        }

        // Metadata types are stored in the most significant byte.
        type <<= 24;
        return mdToken{ type | val };
    }
}

const std::string StringsHeapReader::Name = "#Strings";

StringsHeapReader::StringsHeapReader(plat::data_view<uint8_t> view)
    : _view{ std::move(view) }
{
}

StringsHeapReader::StringsHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc)
    : StringsHeapReader{ { loc.Length, reader->GetOffset(loc.Offset) } }
{
    _reader = std::move(reader);
}

const char *StringsHeapReader::Get(size_t offset) const
{
    if (_view.size() <= offset)
        throw Exception{ ErrorCode::InvalidIndex, Name };

    return reinterpret_cast<const char *>(std::begin(_view) + offset);
}

const std::string UserStringsHeapReader::Name = "#US";

UserStringsHeapReader::UserStringsHeapReader(plat::data_view<uint8_t> view)
    : _view{ std::move(view) }
{
}

UserStringsHeapReader::UserStringsHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc)
    : UserStringsHeapReader{ { loc.Length, reader->GetOffset(loc.Offset) } }
{
    _reader = std::move(reader);
}

const std::string BlobHeapReader::Name = "#Blob";

BlobHeapReader::BlobHeapReader(plat::data_view<uint8_t> view)
    : _view{ std::move(view) }
{
}

BlobHeapReader::BlobHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc)
    : BlobHeapReader{ { loc.Length, reader->GetOffset(loc.Offset) } }
{
    _reader = std::move(reader);
}

plat::data_view<uint8_t> BlobHeapReader::Get(size_t offset) const
{
    if (_view.size() <= offset)
        throw Exception{ ErrorCode::InvalidIndex, Name };

    auto curr = std::begin(_view) + offset;
    uint32_t val = DecompressU32(&curr, std::end(_view) - curr);
    return { val, curr };
}

std::string BlobHeapReader::GetAsStringUtf8(size_t offset) const
{
    auto nsBlob = Get(offset);
    return std::string{ std::begin(nsBlob), std::end(nsBlob) };
}

std::string BlobHeapReader::GetAsDocumentBlob(size_t offset) const
{
    auto docBlob = Get(offset);

    if (docBlob.empty())
        return{};

    auto iter = std::begin(docBlob);
    const char delim = *iter++;

    // [TODO] Add support for UTF-8 encoded delim
    assert((delim & 0x80) == 0);

    std::stringstream ss;
    bool isFirstPart = true;
    while (iter < std::end(docBlob))
    {
        // If the stream isn't empty add the delim
        if (!isFirstPart) ss << delim;

        size_t nextOffset = DecompressU32(&iter, std::end(docBlob) - iter);

        auto part = Get(nextOffset);

        isFirstPart = false;
        ss.write(reinterpret_cast<const char*>(part.data()), part.size());
    }

    return ss.str();
}

SequencePoints BlobHeapReader::GetAsSequencePoints(size_t initalDocumentIndex, size_t offset) const
{
    SequencePoints result{};
    result.InitialDocument = initalDocumentIndex;

    auto seqBlob = Get(offset);
    if (seqBlob.empty())
        return result;

    auto curr = std::begin(seqBlob);
    result.LocalSignature = DecompressU32(&curr, std::end(seqBlob) - curr);

    // If the initial document index is 0, then there is an
    // initial document index entry.
    if (result.InitialDocument == 0)
        result.InitialDocument = DecompressU32(&curr, std::end(seqBlob) - curr);

    assert(result.InitialDocument != 0);

    size_t docIdx = result.InitialDocument;

    // Index are used because when a vector
    // resizes all iterators are invalidated
    int prevNonHiddenIdx = -1;

    // Process records
    while (curr < std::end(seqBlob))
    {
        SeqPoint pt;
        pt.DocumentIndex = docIdx;

        // ILOffset
        pt.ILOffset = DecompressU32(&curr, std::end(seqBlob) - curr);
        assert(pt.ILOffset < 0x20000000);

        // Check if the method transitioned
        // into a new source file.
        if (pt.ILOffset == 0
            && !result.Points.empty())
        {
            docIdx = DecompressU32(&curr, std::end(seqBlob) - curr);
            continue;
        }

        // Compute IL offset
        if (!result.Points.empty())
            pt.ILOffset += result.Points.back().ILOffset;

        // Line range
        uint32_t lineRange = DecompressU32(&curr, std::end(seqBlob) - curr);

        // Column range
        int32_t colRange;
        if (lineRange == 0)
        {
            colRange = DecompressU32(&curr, std::end(seqBlob) - curr);
        }
        else
        {
            colRange = DecompressS32(&curr, std::end(seqBlob) - curr);
        }

        // Check for hidden point
        if (lineRange == 0 && colRange == 0)
        {
            pt.StartLine = pt.EndLine = 0xfeefee;
            pt.StartColumn = pt.EndColumn = 0;
            result.Points.push_back(pt);
            continue;
        }

        // Start line
        if (prevNonHiddenIdx == -1)
        {
            pt.StartLine = DecompressU32(&curr, std::end(seqBlob) - curr);
        }
        else
        {
            int32_t val = DecompressS32(&curr, std::end(seqBlob) - curr);
            pt.StartLine = result.Points[prevNonHiddenIdx].StartLine + val;
        }

        assert(pt.StartLine < 0x20000000 && pt.StartLine != 0xfeefee);

        // Start column
        if (prevNonHiddenIdx == -1)
        {
            pt.StartColumn = DecompressU32(&curr, std::end(seqBlob) - curr);
        }
        else
        {
            int32_t val = DecompressS32(&curr, std::end(seqBlob) - curr);
            pt.StartColumn = result.Points[prevNonHiddenIdx].StartColumn + val;
        }

        assert(pt.StartColumn < 0x10000);

        // Compute ends
        pt.EndLine = pt.StartLine + lineRange;
        assert(pt.EndLine < 0x20000000 && pt.EndLine != 0xfeefee);
        assert(pt.StartLine <= pt.EndLine);

        pt.EndColumn = pt.StartColumn + colRange;
        assert(pt.EndColumn < 0x10000);
        assert(pt.StartLine == pt.EndLine ? pt.StartColumn < pt.EndColumn : true);

        result.Points.push_back(std::move(pt));
        prevNonHiddenIdx = static_cast<int>(result.Points.size() - 1);
    }

    return result;
}

std::vector<Import> BlobHeapReader::GetAsImports(size_t offset) const
{
    auto importBlob = Get(offset);
    if (importBlob.empty())
        return{};

    std::vector<Import> imports;
    auto curr = std::begin(importBlob);
    while (curr < std::end(importBlob))
    {
        Import imp{};
        imp.Kind = static_cast<ImportKind>(DecompressU32(&curr, std::end(importBlob) - curr));

        switch (imp.Kind)
        {
        default:
        case ImportKind::Unknown:
            throw Exception{ ErrorCode::CorruptFormat, MetadataTable::ImportScope };

        case ImportKind::ImportFromNamespace:
        {
            uint32_t nsIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.TargetNamespace = GetAsStringUtf8(nsIdx);
            break;
        }
        case ImportKind::ImportFromNamespaceInAssembly:
        {
            imp.TargetAssembly = DecompressU32(&curr, std::end(importBlob) - curr);
            uint32_t nsIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.TargetNamespace = GetAsStringUtf8(nsIdx);
            break;
        }
        case ImportKind::ImportFromTargetType:
        {
            imp.TargetType = DecompressTypeDefOrRefOrSpecEncoded(&curr, std::end(importBlob) - curr);
            break;
        }
        case ImportKind::ImportFromNamespaceWithAlias:
        {
            uint32_t aliasIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.Alias = GetAsStringUtf8(aliasIdx);
            uint32_t nsIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.TargetNamespace = GetAsStringUtf8(nsIdx);
            break;
        }
        case ImportKind::ImportAssemblyAlias:
        {
            uint32_t aliasIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.Alias = GetAsStringUtf8(aliasIdx);
            break;
        }
        case ImportKind::DefineAssemblyAlias:
        {
            uint32_t aliasIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.Alias = GetAsStringUtf8(aliasIdx);
            imp.TargetAssembly = DecompressU32(&curr, std::end(importBlob) - curr);
            break;
        }
        case ImportKind::DefineNamespaceAlias:
        {
            uint32_t aliasIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.Alias = GetAsStringUtf8(aliasIdx);
            uint32_t nsIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.TargetNamespace = GetAsStringUtf8(nsIdx);
            break;
        }
        case ImportKind::DefineNamespaceAliasFromAssembly:
        {
            uint32_t aliasIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.Alias = GetAsStringUtf8(aliasIdx);
            imp.TargetAssembly = DecompressU32(&curr, std::end(importBlob) - curr);
            uint32_t nsIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.TargetNamespace = GetAsStringUtf8(nsIdx);
            break;
        }
        case ImportKind::DefineTargetTypeAlias:
            uint32_t aliasIdx = DecompressU32(&curr, std::end(importBlob) - curr);
            imp.Alias = GetAsStringUtf8(aliasIdx);
            imp.TargetType = DecompressTypeDefOrRefOrSpecEncoded(&curr, std::end(importBlob) - curr);
            break;
        }

        imports.push_back(std::move(imp));
    }

    return imports;
}

LocalConstantSig BlobHeapReader::GetAsLocalConstantSig(size_t offset) const
{
    auto sigBlob = Get(offset);
    if (sigBlob.empty())
        return{};

    auto curr = std::begin(sigBlob);

    LocalConstantSig sig{};
    sig.CustomModToken = mdTokenNil;

    // We need to peak at this value _not_ increment it
    ElementType tmp = static_cast<ElementType>(*curr);

    // Check for custom modifier
    if (tmp == ElementType::CModOpt
        || tmp == ElementType::CModReq)
    {
        // Consume the type
        curr++;
        sig.CustomModToken = DecompressTypeDefOrRefOrSpecEncoded(&curr, std::end(sigBlob) - curr);
    }

    sig.Type = static_cast<ElementType>(*curr++);
    sig.TypeToken = mdTokenNil;

    int sz;
    switch (sig.Type)
    {
    default:
        throw Exception{ ErrorCode::CorruptFormat, MetadataTable::LocalConstant };

    case ElementType::Boolean:
    case ElementType::I1:
    case ElementType::U1:
        sz = 1;
        break;

    case ElementType::Char:
    case ElementType::I2:
    case ElementType::U2:
        sz = 2;
        break;

    case ElementType::I4:
    case ElementType::U4:
    case ElementType::R4:
        sz = 4;
        break;

    case ElementType::I8:
    case ElementType::U8:
    case ElementType::R8:
        sz = 8;
        break;

    case ElementType::String:
        sz = -1;
        break;

    case ElementType::Class:
    case ElementType::ValueType:
        sig.TypeToken = DecompressTypeDefOrRefOrSpecEncoded(&curr, std::end(sigBlob) - curr);
        sz = -1;
        break;

    case ElementType::Object:
        sz = 0;
        break;
    }

    size_t remaining = std::end(sigBlob) - curr;

    // Consume fixed sized primitive/enum value
    if (sz > 0)
    {
        if (remaining < static_cast<size_t>(sz))
            throw Exception{ ErrorCode::CorruptFormat, Name };

        // At this point we know the value is a primitive or enum type
        sig.RawValue = { static_cast<size_t>(sz), curr };

        // Check if enum type exists
        curr += sz;
        remaining -= sz;
        if (0 < remaining)
            sig.TypeToken = DecompressTypeDefOrRefOrSpecEncoded(&curr, remaining);
    }
    else if (sz < 0)
    {
        if (sig.Type == ElementType::String
            && remaining == 1)
        {
            // Null string
            assert(*curr == 0xff);
            sig.RawValue = {};
        }
        else
        {
            sig.RawValue = { remaining, curr };
        }

        curr += remaining;
    }

    if (curr != std::end(sigBlob))
        throw Exception{ ErrorCode::CorruptFormat, MetadataTable::LocalConstant };

    return sig;
}

namespace
{
    struct PdbStreamMin
    {
        const uint8_t Id[20];
        const mdToken Entry;
        const uint64_t ReferencedTypeSystemTables;
        // const uint32_t TypeSystemTableRows[];
    };

    bool setTableRowCounts(
        uint64_t bitVector,
        const plat::data_view<uint32_t> setBitValues,
        AllTables<uint32_t> &allValues)
    {
        auto value = std::begin(setBitValues);

        // Key invariant in this function is that allValues is equal in
        // length to the number of bits in the bit vector data type.
        for (size_t i = 0; bitVector != 0; ++i)
        {
            if (bitVector & 1)
            {
                if (std::end(setBitValues) < value)
                    return false;

                allValues[i] = *value++;
            }

            bitVector >>= 1;
        }

        return true;
    }
}

const std::string PdbStreamReader::Name = "#Pdb";

PdbStreamReader::PdbStreamReader(plat::data_view<uint8_t> view)
    : _view{ std::move(view) }
    , _tableRowCounts{ 0 }
{
    if (_view.size() < sizeof(PdbStreamMin))
        throw Exception{ ErrorCode::CorruptFormat, Name };

    auto pdbStreamMin = reinterpret_cast<const PdbStreamMin *>(std::begin(_view));
    _id = pdbStreamMin->Id;
    _entry = pdbStreamMin->Entry;

    auto currPos = std::begin(_view) + sizeof(*pdbStreamMin);

    const std::bitset<64> tableRefBits{ pdbStreamMin->ReferencedTypeSystemTables };
    auto tableRefs = tableRefBits.count();
    if (tableRefs > 0)
    {
        // Verify the remaining space matches the computed count
        if ((std::end(_view) - currPos) < static_cast<int>(tableRefs * sizeof(_tableRowCounts[0])))
            throw Exception{ ErrorCode::CorruptFormat, Name };

        // Using the system tables reference bit flags set the appropriate row counts
        bool didSet = setTableRowCounts(
            pdbStreamMin->ReferencedTypeSystemTables,
            { tableRefs, reinterpret_cast<const uint32_t*>(currPos) },
            _tableRowCounts);
        if (!didSet)
            throw Exception{ ErrorCode::CorruptFormat, Name };
    }
}

PdbStreamReader::PdbStreamReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc)
    : PdbStreamReader{ { loc.Length, reader->GetOffset(loc.Offset) } }
{
    _reader = std::move(reader);
}

mdToken PdbStreamReader::EntryPoint() const
{
    return _entry;
}

uint32_t PdbStreamReader::GetTableRowCount(MetadataTable table) const
{
    return _tableRowCounts[static_cast<size_t>(table)];
}

namespace
{
    struct MetadataStreamMin
    {
        const uint32_t Reserved1;
        const uint8_t MajorVersion;
        const uint8_t MinorVersion;
        const uint8_t HeapSizeFlags;
        const uint8_t Reserved2;
        const uint64_t ValidTables;
        const uint64_t SortedTables;
        // const uint32_t TableRows[];
        // const T Tables[];
    };
}

const std::string MetadataStreamReader::Name = "#~";

MetadataStreamReader::MetadataStreamReader(std::shared_ptr<PortablePdbReader> reader, plat::data_view<uint8_t> view)
    : _view{ view }
    , _reader{ std::move(reader) }
    , _tableReaders{}
{
    if (_view.size() < sizeof(MetadataStreamMin))
        throw Exception{ ErrorCode::CorruptFormat, Name };

    auto metadataStreamMin = reinterpret_cast<const MetadataStreamMin *>(std::begin(_view));
    _heapFlags = static_cast<HeapSizeFlags>(metadataStreamMin->HeapSizeFlags);
    auto currPos = std::begin(_view) + sizeof(*metadataStreamMin);

    AllTables<uint32_t> debugTableRowCounts{};
    AllTables<uint32_t> allTableRowCounts{};
    const uint64_t validTables = metadataStreamMin->ValidTables;
    const auto validTableBits = std::bitset<std::numeric_limits<decltype(validTables)>::digits>{ validTables };
    auto validTableCount = validTableBits.count();
    if (validTableCount > 0)
    {
        // Verify the remaining space matches the computed count
        if ((std::end(_view) - currPos) < static_cast<int>(validTableCount * sizeof(debugTableRowCounts[0])))
            throw Exception{ ErrorCode::CorruptFormat, Name };

        // Using the valid tables bit flags set the appropriate row counts
        bool didSet = setTableRowCounts(
            validTables,
            { validTableCount, reinterpret_cast<const uint32_t*>(currPos) },
            debugTableRowCounts);
        if (!didSet)
            throw Exception{ ErrorCode::CorruptFormat, Name };

        // Copy the debug tables
        allTableRowCounts = debugTableRowCounts;

        // Advance beyond the table row counts
        currPos += (validTableCount * sizeof(debugTableRowCounts[0]));
    }

    // If the PDB stream exists, merge the system tables' row
    // counts with the debug tables' row counts.
    auto pdbReader = _reader->GetNamedEntry<PdbStreamReader>();
    if (pdbReader != nullptr)
    {
        for (size_t i = 0; i < allTableRowCounts.size(); ++i)
            allTableRowCounts[i] += pdbReader->GetTableRowCount(static_cast<MetadataTable>(i));
    }

    if (currPos > std::end(_view))
        throw Exception{ ErrorCode::CorruptFormat, Name };

    size_t remain = std::end(_view) - currPos;
    _allTables = plat::data_view<uint8_t>{ remain, currPos };

    plat::data_view<uint8_t> streamExtent = _allTables;
    for (size_t i = 0; i < debugTableRowCounts.size(); ++i)
    {
        uint32_t rowCount = debugTableRowCounts[i];
        if (rowCount == 0)
            continue;

        // Create the reader for this table
        assert(!_allTables.empty());

        auto tableReader = CreateTableReader(
            static_cast<MetadataTable>(i),
            _reader,
            streamExtent,
            allTableRowCounts,
            _heapFlags);

        // If we fail to create a table, we _have_ to stop since it isn't possible
        // to know where any subsequent tables occur.
        if (tableReader == nullptr)
            break;

        // Update the current postion based on what the table consumed
        currPos += tableReader->TableSizeInBytes();

        if (currPos > std::end(_view))
            throw Exception{ ErrorCode::CorruptFormat, Name };

        remain = std::end(view) - currPos;
        streamExtent = plat::data_view<uint8_t>{ remain, currPos };

        _tableReaders[i] = std::move(tableReader);
    }
}

MetadataStreamReader::MetadataStreamReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc)
    : MetadataStreamReader{ reader, { loc.Length, reader->GetOffset(loc.Offset) } }
{
}

HeapSizeFlags MetadataStreamReader::GetHeapFlags() const
{
    return _heapFlags;
}

std::shared_ptr<TableReader> MetadataStreamReader::GetTableReader(MetadataTable table) const
{
    auto reader = _tableReaders[static_cast<size_t>(table)];

    assert(reader == nullptr || reader->GetTableId() == table);
    return reader;
}
