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
#include <limits>
#include <cstring>
#include <PPDBReader.hpp>

using namespace PPDB;

#if defined(_MSC_VER)
// Disable warning about multiple inheritance.
// This is okay because we want a single interface inheritance chain.
#pragma warning( disable : 4250 )
#endif

namespace
{
    enum class ColumnType
    {
        Width_2,
        Width_4,
    };

    constexpr ColumnType IndexType(const AllTables<uint32_t> &counts, MetadataTable t)
    {
        return counts[static_cast<size_t>(t)] >= std::numeric_limits<uint16_t>::max()
            ? ColumnType::Width_4
            : ColumnType::Width_2;
    }

    constexpr ColumnType CodedIndex(
                    const AllTables<uint32_t>& counts,
                    const plat::data_view<MetadataTable>& targets,
                    uint32_t reserveMask)
    {
        // Compute the maximum index value that can be used
        // given the supplied mask.
        auto maxWidth_2 = std::numeric_limits<uint16_t>::max();
        while (reserveMask & 0x1)
        {
            maxWidth_2 >>= 1;
            reserveMask >>= 1;
        }

        // If any target table exceeds this value, then a 2-byte
        // column size will be insufficient.
        for (auto t : targets)
        {
            if (counts[static_cast<size_t>(t)] >= maxWidth_2)
                return ColumnType::Width_4;
        }

        return ColumnType::Width_2;
    }

    struct TableReaderConfig
    {
        static TableReaderConfig Create(
            const plat::data_view<uint8_t> &view,
            uint32_t rowCount,
            std::initializer_list<ColumnType> schema)
        {
            TableReaderConfig cfg{ 0, rowCount };

            // Determine column sizes and accumulate the total row size
            for (ColumnType type : schema)
            {
                auto colSize = (type == ColumnType::Width_2) ? sizeof(uint16_t) : sizeof(uint32_t);
                cfg.ColumnSizes.push_back(colSize);
                cfg.RowSize += colSize;
            }

            // Verify the total table size
            if (view.size() < (cfg.RowCount * cfg.RowSize))
                throw Exception{ ErrorCode::InvalidTableExtent };

            cfg.View = { (cfg.RowCount * cfg.RowSize), view.data() };
            return std::move(cfg);
        }

        size_t RowSize;
        size_t RowCount;
        plat::data_view<uint8_t> View;
        std::vector<size_t> ColumnSizes;
    };

    class TableReaderBase : virtual public TableReader
    {
    public:
        TableReaderBase(
                MetadataTable tableId,
                TableReaderConfig cfg)
            : _config{ std::move(cfg) }
            , _currPos{}
            , _tableId{ tableId }
        {
            _currPos = std::begin(_config.View);
        }

        virtual ~TableReaderBase() = default;

        MetadataTable GetTableId() const override { return _tableId; }

        size_t TableSizeInBytes() const override { return _config.RowCount * _config.RowSize; }

        size_t RowCount() const override { return _config.RowCount; }

        void SetRow(size_t index) override
        {
            if (index == 0 || _config.RowCount < index)
                throw Exception{ ErrorCode::InvalidIndex, GetTableId() };

            // 1 base indexing
            _currPos = std::begin(_config.View) + ((index - 1) * _config.RowSize);
        }

        bool NextRow(std::vector<uint32_t> &rowValues) override
        {
            // Ensure input vector can hold data
            rowValues.resize(_config.ColumnSizes.size());

            return NextRowRaw(rowValues.size(), rowValues.data());
        }

    protected:
        bool NextRowRaw(size_t count, uint32_t *currCol)
        {
            if (std::end(_config.View) <= _currPos)
                return false;

            assert(currCol != nullptr && count == _config.ColumnSizes.size());
            auto rowPos = _currPos;
            for (size_t cs : _config.ColumnSizes)
            {
                // Extract row data
                // Ensure always initialized to 0. This is done since
                // the reading size may be less than 4 bytes.
                uint32_t val{};
                ::memcpy(&val, rowPos, cs);
                rowPos += cs;
                assert(rowPos <= std::end(_config.View));

                // Set row data
                *currCol++ = val;
            }

            _currPos = rowPos;
            return true;
        }

    private:
        plat::data_view<uint8_t>::const_iterator _currPos;
        const MetadataTable _tableId;
        const TableReaderConfig _config;
    };

    class ModuleTableReaderImpl :
        public TableReaderBase,
        public ModuleTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto stringIdxType = HasFlag(flags, HeapSizeFlags::String) ? ColumnType::Width_4 : ColumnType::Width_2;
            const auto guidIdxType = HasFlag(flags, HeapSizeFlags::Guid) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                { ColumnType::Width_2, stringIdxType, guidIdxType, guidIdxType, guidIdxType });

            return std::make_unique<ModuleTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        ModuleTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _strReader{ reader->GetNamedEntry<StringsHeapReader>() }
            , _guidReader{ reader->GetNamedEntry<GuidHeapReader>() }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 5> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            assert(cols[0] == 0);
            r.Name = std::move(_strReader->Get(cols[1]));
            r.Mvid = _guidReader->Get(cols[2]);
            assert(cols[3] == 0);
            assert(cols[4] == 0);
            return true;
        }

    private:
        std::unique_ptr<StringsHeapReader> _strReader;
        std::unique_ptr<GuidHeapReader> _guidReader;
    };

    class DocumentTableReaderImpl :
        public TableReaderBase,
        public DocumentTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto guidIdxType = HasFlag(flags, HeapSizeFlags::Guid) ? ColumnType::Width_4 : ColumnType::Width_2;
            const auto blobIdxType = HasFlag(flags, HeapSizeFlags::Blob) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                { blobIdxType, guidIdxType, blobIdxType, guidIdxType });

            return std::make_unique<DocumentTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        DocumentTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _blobReader{ reader->GetNamedEntry<BlobHeapReader>() }
            , _guidReader{ reader->GetNamedEntry<GuidHeapReader>() }
        { }

        bool NextRow(Row &r) override
        {
            std::array<uint32_t, 4> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            r.Name  = _blobReader->GetAsDocumentBlob(cols[0]);
            r.HashAlgorithm = _guidReader->Get(cols[1]);

            auto hash = _blobReader->Get(cols[2]);
            r.Hash.assign(std::begin(hash), std::end(hash));

            r.Language = _guidReader->Get(cols[3]);
            return true;
        }

    private:
        std::unique_ptr<BlobHeapReader> _blobReader;
        std::unique_ptr<GuidHeapReader> _guidReader;
    };

    class MethodDebugInformationTableReaderImpl :
        public TableReaderBase,
        public MethodDebugInformationTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto blobIdxType = HasFlag(flags, HeapSizeFlags::Blob) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                { IndexType(rowCounts, MetadataTable::Document), blobIdxType });

            return std::make_unique<MethodDebugInformationTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        MethodDebugInformationTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _blobReader{ reader->GetNamedEntry<BlobHeapReader>() }
        { }

        bool NextRow(Row &r) override
        {
            std::array<uint32_t, 2> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            auto initDocIdx = cols[0];
            auto seqIdx = cols[1];
            if (seqIdx == 0)
            {
                r = {};
                r.InitialDocument = initDocIdx;
            }
            else
            {
                r = std::move(_blobReader->GetAsSequencePoints(initDocIdx, seqIdx));
            }

            return true;
        }

    private:
        std::unique_ptr<BlobHeapReader> _blobReader;
    };

    class LocalScopeTableReaderImpl :
        public TableReaderBase,
        public LocalScopeTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                {
                    IndexType(rowCounts, MetadataTable::MethodDef),
                    IndexType(rowCounts, MetadataTable::ImportScope),
                    IndexType(rowCounts, MetadataTable::LocalVariable),
                    IndexType(rowCounts, MetadataTable::LocalConstant),
                    ColumnType::Width_4,
                    ColumnType::Width_4
                });

            return std::make_unique<LocalScopeTableReaderImpl>(std::move(cfg));
        }

    public:
        LocalScopeTableReaderImpl(TableReaderConfig cfg)
            : TableReaderBase{ TableId, std::move(cfg) }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 6> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            r.MethodIndex = cols[0];
            r.ImportScopeIndex = cols[1];
            r.VariableListIndex = cols[2];
            r.ConstantListIndex = cols[3];
            r.StartOffset = cols[4];
            assert(r.StartOffset < 0x80000000);
            r.Length = cols[5];
            assert(r.Length != 0 && r.Length < 0x80000000);
            assert((r.StartOffset + r.Length) < 0x80000000);

            return true;
        }
    };

    class LocalVariableTableReaderImpl :
        public TableReaderBase,
        public LocalVariableTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto stringIdxType = HasFlag(flags, HeapSizeFlags::String) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                {
                    ColumnType::Width_2,
                    ColumnType::Width_2,
                    stringIdxType
                });

            return std::make_unique<LocalVariableTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        LocalVariableTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _strReader{ reader->GetNamedEntry<StringsHeapReader>() }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 3> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            r.Attr = static_cast<LocalVariableAttr>(cols[0]);
            r.SlotIndex = static_cast<uint16_t>(cols[1]);
            r.Name = _strReader->Get(cols[2]);

            return true;
        }

    private:
        std::unique_ptr<StringsHeapReader> _strReader;
    };

    class LocalConstantTableReaderImpl :
        public TableReaderBase,
        public LocalConstantTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto stringIdxType = HasFlag(flags, HeapSizeFlags::String) ? ColumnType::Width_4 : ColumnType::Width_2;
            const auto blobIdxType = HasFlag(flags, HeapSizeFlags::Blob) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                { stringIdxType, blobIdxType });

            return std::make_unique<LocalConstantTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        LocalConstantTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _strReader{ reader->GetNamedEntry<StringsHeapReader>() }
            , _blobReader{ reader->GetNamedEntry<BlobHeapReader>() }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 2> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            r.Name = _strReader->Get(cols[0]);
            r.Signature = std::move(_blobReader->GetAsLocalConstantSig(cols[1]));

            return true;
        }

    private:
        std::unique_ptr<StringsHeapReader> _strReader;
        std::unique_ptr<BlobHeapReader> _blobReader;
    };

    class ImportScopeTableReaderImpl :
        public TableReaderBase,
        public ImportScopeTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto blobIdxType = HasFlag(flags, HeapSizeFlags::Blob) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                { IndexType(rowCounts, MetadataTable::ImportScope), blobIdxType });

            return std::make_unique<ImportScopeTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        ImportScopeTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _blobReader{ reader->GetNamedEntry<BlobHeapReader>() }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 2> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            r.ParentIndex = cols[0];
            r.Imports = std::move(_blobReader->GetAsImports(cols[1]));

            return true;
        }

    private:
        std::unique_ptr<BlobHeapReader> _blobReader;
    };

    class StateMachineMethodTableReaderImpl :
        public TableReaderBase,
        public StateMachineMethodTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                {
                    IndexType(rowCounts, MetadataTable::MethodDef),
                    IndexType(rowCounts, MetadataTable::MethodDef)
                });

            return std::make_unique<StateMachineMethodTableReaderImpl>(std::move(cfg));
        }

    public:
        StateMachineMethodTableReaderImpl(TableReaderConfig cfg)
            : TableReaderBase{ TableId, std::move(cfg) }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 2> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            r.MoveNextMethodIndex = cols[0];
            r.KickoffMethodIndex = cols[1];
            return true;
        }
    };

    const MetadataTable CustomDebugInformationTableTargetTables[] =
    {
        MetadataTable::MethodDef,
        MetadataTable::Field,
        MetadataTable::TypeRef,
        MetadataTable::TypeDef,
        MetadataTable::Param,
        MetadataTable::InterfaceImpl,
        MetadataTable::MemberRef,
        MetadataTable::Module,
        MetadataTable::DeclSecurity,
        MetadataTable::Property,
        MetadataTable::Event,
        MetadataTable::StandAloneSig,
        MetadataTable::ModuleRef,
        MetadataTable::TypeSpec,
        MetadataTable::Assembly,
        MetadataTable::AssemblyRef,
        MetadataTable::File,
        MetadataTable::ExportedType,
        MetadataTable::ManifestResource,
        MetadataTable::GenericParam,
        MetadataTable::GenericParamConstraint,
        MetadataTable::MethodSpec,
        MetadataTable::Document,
        MetadataTable::LocalScope,
        MetadataTable::LocalVariable,
        MetadataTable::LocalConstant,
        MetadataTable::ImportScope,
    };

    class CustomDebugInformationTableReaderImpl :
        public TableReaderBase,
        public CustomDebugInformationTableReader
    {
    public: // static
        static std::unique_ptr<TableReader> Create(
            std::shared_ptr<PortablePdbReader> reader,
            plat::data_view<uint8_t> view,
            const AllTables<uint32_t> &rowCounts,
            HeapSizeFlags flags)
        {
            const auto parentIdxType = CodedIndex(rowCounts, CustomDebugInformationTableTargetTables, HasCustomDebugInformationMask);
            const auto guidIdxType = HasFlag(flags, HeapSizeFlags::Guid) ? ColumnType::Width_4 : ColumnType::Width_2;
            const auto blobIdxType = HasFlag(flags, HeapSizeFlags::Blob) ? ColumnType::Width_4 : ColumnType::Width_2;

            auto cfg = TableReaderConfig::Create(
                view,
                rowCounts[static_cast<size_t>(TableId)],
                { parentIdxType, guidIdxType, blobIdxType });

            return std::make_unique<CustomDebugInformationTableReaderImpl>(std::move(cfg), reader.get());
        }

    public:
        CustomDebugInformationTableReaderImpl(TableReaderConfig cfg, PortablePdbReader *reader)
            : TableReaderBase{ TableId, std::move(cfg) }
            , _blobReader{ reader->GetNamedEntry<BlobHeapReader>() }
            , _guidReader{ reader->GetNamedEntry<GuidHeapReader>() }
        { }

        virtual bool NextRow(Row &r)
        {
            std::array<uint32_t, 3> cols;
            if (!NextRowRaw(cols.size(), cols.data()))
                return false;

            // Enumeration is represented by the first 5 bits
            // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#customdebuginformation-table-0x37
            r.Parent = static_cast<HasCustomDebugInformation>(cols[0] & HasCustomDebugInformationMask);
            r.Kind = _guidReader->Get(cols[1]);

            auto value = _blobReader->Get(cols[2]);
            r.Value.assign(std::begin(value), std::end(value));

            return true;
        }

    private:
        std::unique_ptr<BlobHeapReader> _blobReader;
        std::unique_ptr<GuidHeapReader> _guidReader;
    };
}

std::unique_ptr<TableReader> PPDB::CreateTableReader(
    MetadataTable tableId,
    std::shared_ptr<PortablePdbReader> reader,
    plat::data_view<uint8_t> view,
    const AllTables<uint32_t> &rowCounts,
    HeapSizeFlags flags)
{
    switch (tableId)
    {
    default:
        assert(false && "Unexpected table in Portable PDB");
        return nullptr;

    case MetadataTable::Module:
        return ModuleTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::Document:
        return DocumentTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::MethodDebugInformation:
        return MethodDebugInformationTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::LocalScope:
        return LocalScopeTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::LocalVariable:
        return LocalVariableTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::LocalConstant:
        return LocalConstantTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::ImportScope:
        return ImportScopeTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::StateMachineMethod:
        return StateMachineMethodTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);

    case MetadataTable::CustomDebugInformation:
        return CustomDebugInformationTableReaderImpl::Create(std::move(reader), view, rowCounts, flags);
    }
}
