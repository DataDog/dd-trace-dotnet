// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "IFrameStore.h"
#include <unordered_map>
#include <vector>

// Forward declarations
struct COR_FIELD_OFFSET;

// Build-time field information — used only inside BuildLayout, never stored in the cache.
struct FieldInfo
{
    ULONG offset = 0;
    bool isReferenceType = false;
    mdFieldDef fieldToken = 0;

    // For inline value type fields that contain reference sub-fields (e.g., the state machine
    // struct inside AsyncStateMachineBox<TStateMachine>). When true, use valueTypeClassID
    // with ClassLayoutCache::GetLayout() to enumerate the value type's sub-fields.
    bool isValueType = false;
    ClassID valueTypeClassID = 0;
};

// Cached class layout information
class ClassLayoutCache
{
public:
    struct ClassLayoutData
    {
        ULONG classSize = 0;
        bool isArray = false;
        CorElementType arrayElementType = ELEMENT_TYPE_END;
        ClassID arrayElementClassID = 0;
        ULONG arrayRank = 0;

        // Compact traversal-only field lists (empty for array entries).
        // Replaces the former std::vector<FieldInfo> fields.
        std::vector<ULONG> refFieldOffsets;
        std::vector<std::pair<ULONG, ClassID>> inlineVtFields;

        // True when this layout has direct reference fields (non-empty refFieldOffsets).
        // Used by EnqueueArrayChildren to skip VT arrays whose elements have no GC refs.
        // Does NOT include nested inline VTs — EnqueueValueTypeArrayChildren only
        // iterates refFieldOffsets today, so including inlineVtFields here would cause
        // wasteful iteration of large arrays with no useful work.
        bool elementHasReferenceFields = false;
    };

    ClassLayoutCache(ICorProfilerInfo12* pCorProfilerInfo, IFrameStore* pFrameStore);

    const ClassLayoutData* GetLayout(ClassID classID);

    void Clear()
    {
        _cache.clear();
        _referenceTypeCache.clear();
    }

    size_t GetMemorySize() const;
    size_t GetEntryCount() const { return _cache.size(); }

private:
    ClassLayoutData BuildLayout(ClassID classID);
    bool IsReferenceType(mdTypeDef typeDef, IMetaDataImport* pMetadataImport);
    bool IsClassIDReferenceType(ClassID classID);
    void MergeParentLayout(ClassID parentClassID,
                           std::vector<ULONG>& refFieldOffsets,
                           std::vector<std::pair<ULONG, ClassID>>& inlineVtFields);
    bool IsFieldReferenceType(
        mdFieldDef fieldToken,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    // For non-reference fields, try to detect inline value types whose layout contains
    // reference sub-fields.  Currently resolves ELEMENT_TYPE_VAR (generic type parameter)
    // fields — this covers async state machine structs inside AsyncStateMachineBox<T>.
    void ResolveValueTypeField(
        FieldInfo& fieldInfo,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    std::string GetClassName(ClassID classID) const;

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    std::unordered_map<ClassID, ClassLayoutData> _cache;
    std::unordered_map<ClassID, bool> _referenceTypeCache;
};
