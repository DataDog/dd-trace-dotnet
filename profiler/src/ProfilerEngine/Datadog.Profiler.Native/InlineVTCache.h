// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "IFrameStore.h"
#include "GCDescReader.h"
#include <optional>
#include <unordered_map>
#include <vector>

struct COR_FIELD_OFFSET;

// Build-time field information used only inside BuildInlineVTInfo.
struct VTFieldInfo
{
    ULONG offset = 0;
    mdFieldDef fieldToken = 0;
    bool isValueType = false;
    ClassID valueTypeClassID = 0;
};

// Small cache that stores inline value type field info ONLY for types whose
// embedded structs contain GC-traceable references. For the vast majority of
// types this cache returns nullptr (no inline VTs).
//
// Reference field enumeration is handled by GCDesc reads at traversal time
// (the fast path). This cache exists solely to preserve inline VT tree
// attribution in the TypeReferenceTree (the slow path).
//
// Without this, all GC references in an object with inline VTs would be attributed
// to the class instead of the embedded struct that owns them, losing important
// type-level insights in the reference tree (see how GCDesc are storing reference offsets)
class InlineVTCache
{
public:
    struct InlineVTInfo
    {
        std::vector<std::pair<ULONG, ClassID>> fields;
    };

    InlineVTCache(ICorProfilerInfo12* pCorProfilerInfo, IFrameStore* pFrameStore);

    // Returns non-null only for types with inline VT fields containing GC refs.
    // Returns nullptr for the vast majority of types (no inline VTs or already
    // checked and found to have none).
    const InlineVTInfo* GetInlineVTInfo(ClassID classID);

    void Clear()
    {
        _cache.clear();
        _primitiveClassIDs.clear();
    }

    size_t GetMemorySize() const;
    size_t GetEntryCount() const;

private:
    std::optional<InlineVTInfo> BuildInlineVTInfo(ClassID classID);

    // Detect inline value type fields. Handles:
    //  - ELEMENT_TYPE_VAR (generic type parameters, e.g. AsyncStateMachineBox<T>)
    //  - ELEMENT_TYPE_VALUETYPE (non-generic embedded structs)
    //  - ELEMENT_TYPE_GENERICINST with ELEMENT_TYPE_VALUETYPE base (generic VTs)
    void ResolveValueTypeField(
        VTFieldInfo& fieldInfo,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    void MergeParentInlineVTs(ClassID parentClassID,
                              std::vector<std::pair<ULONG, ClassID>>& fields);

    // Check whether a ClassID resolves to a value type (as opposed to a reference type).
    // Uses the GCDesc::ContainsGCPointers flag and metadata inspection.
    bool IsClassIDValueType(ClassID classID);

    // Resolve a single generic type argument from a metadata signature to its ClassID.
    // Advances idx past the consumed bytes. Returns 0 on failure.
    ClassID ResolveGenericArgClassID(
        PCCOR_SIGNATURE pSignature, ULONG signatureSize, ULONG& idx,
        ModuleID moduleID, IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    // Map a primitive CorElementType (e.g. ELEMENT_TYPE_I4) to its ClassID
    // by finding the corresponding TypeRef/TypeDef in the module's metadata.
    ClassID ResolvePrimitiveClassID(
        CorElementType elementType,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport);

    // Returns the well-known ECMA type name for a primitive CorElementType, or nullptr.
    static const WCHAR* GetPrimitiveTypeName(CorElementType elementType);

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;

    // Cache: ClassID -> optional<InlineVTInfo>.
    // std::nullopt = type was inspected but has no inline VTs (sentinel to avoid re-checking).
    // InlineVTInfo with non-empty fields = type has inline VTs.
    std::unordered_map<ClassID, std::optional<InlineVTInfo>> _cache;

    // Cache: CorElementType -> ClassID for primitive types (e.g. int → System.Int32).
    std::unordered_map<CorElementType, ClassID> _primitiveClassIDs;
};
