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

#ifndef _PPDB_INC_PPDBREADER_HPP_
#define _PPDB_INC_PPDBREADER_HPP_

#include <cstdint>
#include <vector>
#include <memory>
#include <string>
#include <map>
#include <cassert>
#include <array>
#include <type_traits>
#include "platform.hpp"

namespace PPDB
{
    struct RelativeLocation
    {
        size_t Length;
        size_t Offset;
    };

    // Forward declaration
    class PortablePdbReader;

    // GUID heap reader
    // ECMA-335 II.24.2.5
    class GuidHeapReader
    {
    public: // static
        static const std::string Name;

    public:
        GuidHeapReader(plat::data_view<uint8_t> view);
        GuidHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc);

        size_t Count() const;

        const GUID &Get(size_t index) const;

    private:
        const plat::data_view<GUID> _heap;
        std::shared_ptr<PortablePdbReader> _reader;
    };

    // Strings heap reader
    // ECMA-335 II.24.2.3
    class StringsHeapReader
    {
    public: // static
        static const std::string Name;

    public:
        StringsHeapReader(plat::data_view<uint8_t> view);
        StringsHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc);

        const char * Get(size_t offset) const;

    private:
        const plat::data_view<uint8_t> _view;
        std::shared_ptr<PortablePdbReader> _reader;
    };

    // User Strings heap reader
    // ECMA-335 II.24.2.4
    class UserStringsHeapReader
    {
    public: // static
        static const std::string Name;

    public:
        UserStringsHeapReader(plat::data_view<uint8_t> view);
        UserStringsHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc);

    private:
        const plat::data_view<uint8_t> _view;
        std::shared_ptr<PortablePdbReader> _reader;
    };

    struct SeqPoint
    {
        uint32_t ILOffset;
        uint32_t StartLine;
        uint32_t StartColumn;
        uint32_t EndLine;
        uint32_t EndColumn;

        // Document table index
        size_t DocumentIndex;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#sequence-points-blob
    struct SequencePoints
    {
        // StandAloneSig table index
        size_t LocalSignature;

        // Document table index
        size_t InitialDocument;

        std::vector<SeqPoint> Points;
    };

    enum class ImportKind
    {
        Unknown = 0,
        ImportFromNamespace = 1,
        ImportFromNamespaceInAssembly = 2,
        ImportFromTargetType = 3,
        ImportFromNamespaceWithAlias = 4,
        ImportAssemblyAlias = 5,
        DefineAssemblyAlias = 6,
        DefineNamespaceAlias = 7,
        DefineNamespaceAliasFromAssembly = 8,
        DefineTargetTypeAlias = 9
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#ImportsBlob
    struct Import
    {
        ImportKind Kind;

        // AssemblyRef table index
        size_t TargetAssembly;

        mdToken TargetType;

        std::string Alias;
        std::string TargetNamespace;
    };

    enum class ElementType
    {
        Void = 0x1,
        Boolean = 0x2,
        Char = 0x3,
        I1 = 0x4,
        U1 = 0x5,
        I2 = 0x6,
        U2 = 0x7,
        I4 = 0x8,
        U4 = 0x9,
        I8 = 0xa,
        U8 = 0xb,
        R4 = 0xc,
        R8 = 0xd,
        String = 0xe,
        Ptr = 0xf,
        ByReg = 0x10,
        ValueType = 0x11,
        Class = 0x12,
        Var = 0x13,
        Array = 0x14,
        GenericInst = 0x15,
        TypedByRef = 0x16,
        IntPtr = 0x18,
        UIntPtr = 0x19,
        FnPtr = 0x1b,
        Object = 0x1c,
        SZArray = 0x1d,
        MVar = 0x1e,
        CModReq = 0x1f,
        CModOpt = 0x20,
        Internal = 0x21,

        Modifier = 0x40,
        Sentinal = 0x41,
        Pinned = 0x45,

        // = 0x50,
        // = 0x51,
        // = 0x52,
        // = 0x53,
        // = 0x54,
        // = 0x55,
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localconstantsig-blob
    struct LocalConstantSig
    {
        ElementType Type;
        mdToken TypeToken;
        mdToken CustomModToken;
        plat::data_view<uint8_t> RawValue;
    };

    // Blob heap reader
    // ECMA-335 II.24.2.4
    class BlobHeapReader
    {
    public: // static
        static const std::string Name;

    public:
        BlobHeapReader(plat::data_view<uint8_t> view);
        BlobHeapReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc);

        plat::data_view<uint8_t> Get(size_t offset) const;

        std::string GetAsStringUtf8(size_t offset) const;
        std::string GetAsDocumentBlob(size_t offset) const;
        SequencePoints GetAsSequencePoints(size_t initalDocumentIndex, size_t offset) const;
        std::vector<Import> GetAsImports(size_t offset) const;
        LocalConstantSig GetAsLocalConstantSig(size_t offset) const;

    private:
        const plat::data_view<uint8_t> _view;
        std::shared_ptr<PortablePdbReader> _reader;
    };

    enum class MetadataTable
    {
        Unknown = -1,
        Module = 0x0,
        TypeRef = 0x01,
        TypeDef = 0x02,

        Field = 0x04,

        MethodDef = 0x06,

        Param = 0x08,
        InterfaceImpl = 0x09,
        MemberRef = 0x0a,
        Constant = 0x0b,
        CustomAttribute = 0x0c,
        FieldMarshal = 0x0d,
        DeclSecurity = 0x0e,
        ClassLayout = 0x0f,
        FieldLayout = 0x10,
        StandAloneSig = 0x11,
        EventMap = 0x12,

        Event = 0x14,
        PropertyMap = 0x15,

        Property = 0x17,
        MethodSemantics = 0x18,
        MethodImpl = 0x19,
        ModuleRef = 0x1a,
        TypeSpec = 0x1b,
        ImplMap = 0x1c,
        FieldRva = 0x1d,

        Assembly = 0x20,
        AssemblyProcessor = 0x21,
        AssemblyOS = 0x22,
        AssemblyRef = 0x23,
        AssemblyRefProcessor = 0x24,
        AssemblyRefOS = 0x25,
        File = 0x26,
        ExportedType = 0x27,
        ManifestResource = 0x28,
        NestedClass = 0x29,
        GenericParam = 0x2a,
        MethodSpec = 0x2b,
        GenericParamConstraint = 0x2c,

        Document = 0x30,
        MethodDebugInformation = 0x31,
        LocalScope = 0x32,
        LocalVariable = 0x33,
        LocalConstant = 0x34,
        ImportScope = 0x35,
        StateMachineMethod = 0x36,
        CustomDebugInformation = 0x37,

        Max = 0x40
    };

    template<typename T>
    using AllTables = std::array<T, static_cast<size_t>(MetadataTable::Max)>;

    // PDB stream reader '#PDB'
    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#pdb-stream
    class PdbStreamReader
    {
    public: // static
        static const std::string Name;

    public:
        PdbStreamReader(plat::data_view<uint8_t> view);
        PdbStreamReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc);

        mdToken EntryPoint() const;

        uint32_t GetTableRowCount(MetadataTable table) const;

    private:
        mdToken _entry;
        const uint8_t *_id;
        const plat::data_view<uint8_t> _view;
        std::shared_ptr<PortablePdbReader> _reader;
        AllTables<uint32_t> _tableRowCounts;
    };

    // Table reader interface.
    // All tables should implement this interface.
    class TableReader
    {
    public:
        virtual ~TableReader() = default;

        virtual MetadataTable GetTableId() const = 0;

        virtual size_t TableSizeInBytes() const = 0;

        virtual size_t RowCount() const = 0;

        virtual void SetRow(size_t index) = 0;

        virtual bool NextRow(std::vector<uint32_t> &rowValues) = 0;
    };

    // See metadata stream
    enum class HeapSizeFlags
    {
        None = 0x0,
        String = 0x1,
        Guid = 0x2,
        Blob = 0x4,
    };

    template<typename T>
    constexpr bool HasFlag(const T &flags, T f)
    {
        return 0 != (static_cast<typename std::underlying_type<T>::type>(flags)
            & static_cast<typename std::underlying_type<T>::type>(f));
    }

    // Construct a table reader for the defined table ID
    std::unique_ptr<TableReader> CreateTableReader(
        MetadataTable tableId,
        std::shared_ptr<PortablePdbReader> reader,
        plat::data_view<uint8_t> view,
        const AllTables<uint32_t> &counts,
        HeapSizeFlags flags);

    // Module table reader
    // ECMA-335 II.22.30
    class ModuleTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::Module;

    public:
        using TableReader::NextRow;

        struct Row
        {
            std::string Name;
            GUID Mvid;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#document-table-0x30
    class DocumentTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::Document;

    public:
        using TableReader::NextRow;

        struct Row
        {
            std::string Name;
            GUID HashAlgorithm;
            std::vector<uint8_t> Hash;
            GUID Language;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#methoddebuginformation-table-0x31
    class MethodDebugInformationTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::MethodDebugInformation;

    public:
        using TableReader::NextRow;

        using Row = SequencePoints;
        virtual bool NextRow(Row &r) = 0;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localscope-table-0x32
    class LocalScopeTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::LocalScope;

    public:
        using TableReader::NextRow;

        struct Row
        {
            // MethodDef table index
            size_t MethodIndex;

            // ImportScope table index
            size_t ImportScopeIndex;

            // LocalVariable table index
            size_t VariableListIndex;

            // LocalConstant table index
            size_t ConstantListIndex;

            uint32_t StartOffset;
            uint32_t Length;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    enum class LocalVariableAttr
    {
        None = 0,
        DebuggerHidden = 0x1
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localvariable-table-0x33
    class LocalVariableTableReader : virtual public TableReader
    {
    public:
        static const MetadataTable TableId = MetadataTable::LocalVariable;

    public: // static
        using TableReader::NextRow;

        struct Row
        {
            std::string Name;
            LocalVariableAttr Attr;
            uint16_t SlotIndex;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localconstant-table-0x34
    class LocalConstantTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::LocalConstant;

    public:
        using TableReader::NextRow;

        struct Row
        {
            std::string Name;
            LocalConstantSig Signature;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#importscope-table-0x35
    class ImportScopeTableReader : virtual public TableReader
    {
    public:
        static const MetadataTable TableId = MetadataTable::ImportScope;

    public:
        using TableReader::NextRow;

        struct Row
        {
            // ImportScope table index
            size_t ParentIndex;
            std::vector<Import> Imports;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#statemachinemethod-table-0x36
    class StateMachineMethodTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::StateMachineMethod;

    public:
        using TableReader::NextRow;

        struct Row
        {
            // MethodDef table index
            size_t MoveNextMethodIndex;

            // MethodDef table index
            size_t KickoffMethodIndex;
        };
        virtual bool NextRow(Row &r) = 0;
    };

    enum class HasCustomDebugInformation
    {
        MethodDef = 0,
        Field = 1,
        TypeRef = 2,
        TypeDef = 3,
        Param = 4,
        InterfaceImpl = 5,
        MemberRef = 6,
        Module = 7,
        DeclSecurity = 8,
        Property = 9,
        Event = 10,
        StandAloneSig = 11,
        ModuleRef = 12,
        TypeSpec = 13,
        Assembly = 14,
        AssemblyRef = 15,
        File = 16,
        ExportedType = 17,
        ManifestResource = 18,
        GenericParam = 19,
        GenericParamConstraint = 20,
        MethodSpec = 21,
        Document = 22,
        LocalScope = 23,
        LocalVariable = 24,
        LocalConstant = 25,
        ImportScope = 26
    };

    const uint32_t HasCustomDebugInformationMask = 0x1f;

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#customdebuginformation-table-0x37
    class CustomDebugInformationTableReader : virtual public TableReader
    {
    public: // static
        static const MetadataTable TableId = MetadataTable::CustomDebugInformation;

    public:
        struct Row
        {
            HasCustomDebugInformation Parent;
            GUID Kind;
            std::vector<uint8_t> Value;
        };

        using TableReader::NextRow;
        virtual bool NextRow(Row &r) = 0;
    };

    // Metadata stream reader '#~'
    // ECMA-335 II.24.2.6
    class MetadataStreamReader
    {
    public: // static
        static const std::string Name;

    public:
        MetadataStreamReader(std::shared_ptr<PortablePdbReader> reader, plat::data_view<uint8_t> view);
        MetadataStreamReader(std::shared_ptr<PortablePdbReader> reader, RelativeLocation loc);

        HeapSizeFlags GetHeapFlags() const;

        std::shared_ptr<TableReader> GetTableReader(MetadataTable table) const;

        template<typename R>
        std::shared_ptr<R> GetTableReader() const
        {
            auto tableReader = GetTableReader(R::TableId);
            return  std::dynamic_pointer_cast<R>(tableReader);
        }

    private:
        HeapSizeFlags _heapFlags;
        plat::data_view<uint8_t> _allTables;
        const plat::data_view<uint8_t> _view;
        std::shared_ptr<PortablePdbReader> _reader;
        std::unique_ptr<PdbStreamReader> _pdbStream;
        AllTables<std::shared_ptr<TableReader>> _tableReaders;
    };

    enum class ErrorCode
    {
        FileReadFailure = 1,
        CorruptFormat,
        InvalidStreamExtent,
        InvalidTableExtent,
        InvalidIndex
    };

    class Exception
    {
    public:
        Exception(ErrorCode ec);
        Exception(ErrorCode ec, MetadataTable table);
        Exception(ErrorCode ec, std::string name);

        const ErrorCode Error;
        const MetadataTable Table;
        const std::string Name;
    };

    // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#portable-pdb-v10-format-specification
    class PortablePdbReader
    {
    public: // static
        static std::shared_ptr<PortablePdbReader> CreateReader(std::vector<uint8_t> data);
        static std::shared_ptr<PortablePdbReader> CreateReader(const char *file);

    private:
        PortablePdbReader(std::vector<uint8_t> data);

    public:
        std::string Version() const;

        const uint8_t *GetOffset(size_t offset) const;

        RelativeLocation GetLocationByName(const std::string &name) const;

        template<typename T>
        std::unique_ptr<T> GetNamedEntry() const
        {
            RelativeLocation loc = GetLocationByName(T::Name);
            if (loc.Offset == 0)
                return {};

            assert(loc.Length > 0);
            return std::make_unique<T>(_this.lock(), loc);
        }

    private:
        const char *_version;
        plat::data_view<uint8_t> _data_view;
        const std::vector<uint8_t> _data;
        std::weak_ptr<PortablePdbReader> _this;
        std::map<std::string, RelativeLocation> _entries;
    };
}

#endif // _PPDB_INC_PPDBREADER_HPP_
