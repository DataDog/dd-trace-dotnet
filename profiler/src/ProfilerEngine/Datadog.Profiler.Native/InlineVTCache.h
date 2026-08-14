// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "GCDescReader.h"

#include "shared/src/native-src/com_ptr.h"

#include <atomic>
#include <optional>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

struct COR_FIELD_OFFSET;
class CoreLibModuleProvider;

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
//
// Inspecting a type requires the runtime to resolve the types of its fields, which is not
// allowed while it is suspended for the dump GC. This is why the lookups done during a
// traversal (GetInlineVTInfo) only queue the types they don't know yet, and the inspection
// itself (ResolvePendingTypes) happens once the dump is over.
class InlineVTCache
{
public:
    struct InlineVTField
    {
        ULONG offset = 0;
        ClassID classID = 0;
        ULONG size = 0;
    };

    struct InlineVTInfo
    {
        std::vector<InlineVTField> fields;
    };

    InlineVTCache(
        ICorProfilerInfo12* pCorProfilerInfo,
        CoreLibModuleProvider* pCoreLibModuleProvider);

    // Returns non-null only for types with inline VT fields containing GC refs.
    // Returns nullptr for the vast majority of types (no inline VTs or already
    // checked and found to have none).
    //
    // This is a pure cache lookup: it is called while the runtime is suspended for the heap
    // dump GC, where inspecting a type is forbidden (see ResolvePendingTypes). A type met for
    // the first time is queued and gets its attribution starting with the next snapshot.
    const InlineVTInfo* GetInlineVTInfo(ClassID classID);

    // Inspects the types queued by GetInlineVTInfo and returns how many were inspected.
    //
    // MUST be called outside of the heap dump (i.e. once the EventPipe session is stopped so
    // that no more GC callback can run): resolving a type calls GetClassFromTokenAndTypeArgs,
    // which may trigger a type load and a garbage collection. The runtime forbids it from a
    // callback that runs with the EE suspended: it throws EETypeLoadException or crashes in
    // the type loader instead of failing with CORPROF_E_UNSUPPORTED_CALL_SEQUENCE.
    //
    // The flip side of deferring is that a queued ClassID is no longer guaranteed to be
    // alive: the runtime reported it during the dump, but the module defining it may have
    // been collected since (a collectible AssemblyLoadContext going away), which frees its
    // MethodTables. Reading one from here would fault on a thread the CLR knows nothing
    // about, i.e. take the application down. Three things keep that from happening:
    //   - a module unload drops everything held here (see OnModuleUnloaded),
    //   - the first thing done with a queued type is GetClassIDInfo2, which the runtime
    //     validates, instead of a raw MethodTable read,
    //   - each type is inspected under MemoryFaultGuard, so a fault costs that one type's
    //     attribution rather than the process.
    // The window left open is an unload starting after the queue has been checked and
    // while this loop is running: the guard is what covers it.
    size_t ResolvePendingTypes();

    // Records that a module has been unloaded, i.e. that the ClassIDs held here may now
    // point to freed MethodTables.
    //
    // Called from ModuleUnloadStarted, on whichever thread triggered the unload, so it
    // only raises a flag: the containers are read and written by the snapshot threads and
    // clearing them from here would be a data race. DropCacheIfModuleUnloaded consumes it.
    void OnModuleUnloaded()
    {
        _moduleUnloaded.store(true, std::memory_order_release);
    }

    // Drops everything when a module has been unloaded since the last call, and returns
    // whether it did. Coarse (a single unload throws away types that are still alive) but
    // unloads are rare, whereas the cache is kept across dumps for the life of the process.
    //
    // MUST be called from the snapshot thread, outside a dump.
    bool DropCacheIfModuleUnloaded();

    size_t GetPendingTypeCount() const
    {
        return _pendingClassIDs.size();
    }

    void Clear()
    {
        _cache.clear();
        _pendingClassIDs.clear();
        _primitiveClassIDs.clear();
    }

    size_t GetMemorySize() const;

    // Number of cached types that actually have inline value types to attribute.
    size_t GetEntryCount() const;

    // Number of types the cache can answer for without touching the pending queue,
    // including the ones whose verdict is "nothing to attribute".
    size_t GetCachedTypeCount() const
    {
        return _cache.size();
    }

private:
    enum class InspectionStatus
    {
        Completed,
        Faulted,
        GuardUnavailable
    };

    struct BuildResult
    {
        std::optional<InlineVTInfo> info;
        InspectionStatus status = InspectionStatus::Completed;
    };

    BuildResult BuildInlineVTInfo(ClassID classID);

    // Same as GetInlineVTInfo but inspects the type when it is not cached yet.
    // Only callable outside of the heap dump GC (see ResolvePendingTypes).
    const InlineVTInfo* GetOrBuildInlineVTInfo(
        ClassID classID,
        InspectionStatus* pStatus = nullptr);

    // Fault-guarded raw MethodTable read. The guarded body owns no RAII object.
    static InspectionStatus TryContainsGCPointers(ClassID classID, bool& containsPointers);

    // Get the type definition of the given type and the metadata of the module defining it.
    // The parent class and the generic arguments are also returned when asked for.
    // Returns false for types without a type definition (e.g. arrays) or when the metadata
    // cannot be read (e.g. dynamic modules).
    bool TryGetTypeDefAndMetadata(
        ClassID classID,
        ModuleID& moduleID,
        mdTypeDef& typeDef,
        ComPtr<IMetaDataImport>& pMetadataImport,
        ClassID* pParentClassID = nullptr,
        std::vector<ClassID>* pTypeArgs = nullptr);

    // Detect inline value type fields. Handles:
    //  - ELEMENT_TYPE_VAR (generic type parameters, e.g. AsyncStateMachineBox<T>)
    //  - ELEMENT_TYPE_VALUETYPE (non-generic embedded structs)
    //  - ELEMENT_TYPE_GENERICINST with ELEMENT_TYPE_VALUETYPE base (generic VTs)
    InspectionStatus ResolveValueTypeField(
        VTFieldInfo& fieldInfo,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    InspectionStatus MergeParentInlineVTs(
        ClassID parentClassID,
        std::vector<InlineVTField>& fields);

    // Check whether a ClassID resolves to a value type (as opposed to a reference type).
    // Uses the GCDesc::ContainsGCPointers flag and metadata inspection.
    bool IsClassIDValueType(ClassID classID);

    // Resolve a single generic type argument from a metadata signature to its ClassID.
    // Advances idx past the consumed bytes. Returns 0 on failure.
    ClassID ResolveGenericArgClassID(
        PCCOR_SIGNATURE pSignature, ULONG signatureSize, ULONG& idx,
        ModuleID moduleID, IMetaDataImport* pMetadataImport,
        const std::vector<ClassID>& typeArgs);

    // Returns the well-known ECMA type name for a primitive CorElementType, or nullptr.
    static const WCHAR* GetPrimitiveTypeName(CorElementType elementType);

// Exposed to the tests: they check that no TypeRef/TypeSpec token ever reaches the runtime.
#ifdef DD_TEST
public:
#endif
    // Bounds-checked signature helpers. Public in tests to exercise malformed blobs.
    static bool TryUncompressToken(
        PCCOR_SIGNATURE pSignature,
        ULONG signatureSize,
        ULONG& idx,
        mdToken& token);

    static bool TryUncompressData(
        PCCOR_SIGNATURE pSignature,
        ULONG signatureSize,
        ULONG& idx,
        ULONG& value);

    // The single place where a signature token is handed to the runtime.
    // Only mdTypeDef tokens are accepted: GetClassFromTokenAndTypeArgs expects a type
    // DEFINED in the given module and triggers a failing type load (raising the CLR
    // EETypeLoadException in the middle of a heap dump) for anything else.
    // Returns 0 when the token is not usable or the resolution fails.
    ClassID ResolveClassIDFromToken(
        ModuleID moduleID,
        mdToken token,
        ULONG32 typeArgCount,
        ClassID* pTypeArgs);

    // Map a primitive CorElementType (e.g. ELEMENT_TYPE_I4) to its ClassID: it is looked up
    // in the given module when it defines it (i.e. the core library) and in the core library
    // module otherwise.
    ClassID ResolvePrimitiveClassID(
        CorElementType elementType,
        ModuleID moduleID,
        IMetaDataImport* pMetadataImport);

#ifdef DD_TEST
    void SetInlineVTInfoForTests(ClassID classID, InlineVTInfo info)
    {
        _cache.insert_or_assign(classID, std::move(info));
    }
#endif

#ifdef DD_TEST
private:
#endif
    ICorProfilerInfo12* _pCorProfilerInfo;
    CoreLibModuleProvider* _pCoreLibModuleProvider;

    // Cache: ClassID -> optional<InlineVTInfo>.
    // std::nullopt = nothing to attribute for this type, either because it was inspected
    // and has no inline VTs, or because it is still queued in _pendingClassIDs and has
    // not been inspected yet. Both cases behave identically during a traversal, and
    // ResolvePendingTypes erases the queued ones before inspecting them.
    // InlineVTInfo with non-empty fields = type has inline VTs.
    std::unordered_map<ClassID, std::optional<InlineVTInfo>> _cache;

    // Types met during a traversal that are not in _cache yet: they are inspected by
    // ResolvePendingTypes once the dump is over. Filled from the GC callbacks and drained
    // from the HeapSnapshotManager loop thread; the two never overlap because the draining
    // happens after the EventPipe session has been stopped.
    std::unordered_set<ClassID> _pendingClassIDs;

    // Cache: CorElementType -> ClassID for primitive types (e.g. int → System.Int32).
    std::unordered_map<CorElementType, ClassID> _primitiveClassIDs;

    // Set from ModuleUnloadStarted, consumed by the snapshot thread (see OnModuleUnloaded).
    std::atomic<bool> _moduleUnloaded{false};
};
