// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

#include <atomic>
#include <mutex>
#include <unordered_map>

// Tracks the core library module (System.Private.CoreLib or mscorlib) and resolves
// types defined in it.
//
// Several components need to look up well known types such as System.Exception or the
// primitive types backing value type fields. Resolving them requires the module that
// DEFINES them: ICorProfilerInfo::GetClassFromTokenAndTypeArgs only accepts an mdTypeDef
// belonging to the module it is given, and feeding it a TypeRef from another module makes
// the CLR attempt a type load that throws EETypeLoadException.
//
// This provider is fed by CorProfilerCallback::ModuleLoadFinished and is safe to query
// from any thread, including during a GC callback: the core library types it resolves are
// always already loaded, so no type load is triggered.
class CoreLibModuleProvider
{
public:
    explicit CoreLibModuleProvider(ICorProfilerInfo4* pCorProfilerInfo);

    CoreLibModuleProvider(const CoreLibModuleProvider&) = delete;
    CoreLibModuleProvider& operator=(const CoreLibModuleProvider&) = delete;

    // Records the module id the first time the core library is seen.
    // Returns true only for the call that identified it.
    bool OnModuleLoaded(ModuleID moduleId);

    // Returns 0 until the core library has been loaded.
    ModuleID GetModuleId() const;

    // Lazily opens the core library metadata. The returned ComPtr owns an AddRef.
    // Returns an empty ComPtr when the core library is not loaded yet or metadata
    // is unavailable.
    ComPtr<IMetaDataImport2> GetMetadata();

    // Resolves a type defined in the core library (e.g. WStr("System.Int32")) to its ClassID.
    // Returns 0 when the core library is not loaded yet or when the type cannot be resolved.
    ClassID ResolveTypeInCoreLib(const WCHAR* fullTypeName);

private:
    // Both helpers require _lock to be held.
    IMetaDataImport2* GetMetadataNoLock();
    ClassID ResolveTypeNoLock(ModuleID moduleId, const WCHAR* fullTypeName);

    ICorProfilerInfo4* _pCorProfilerInfo;

    // Set once, then never changes: read without taking the lock.
    std::atomic<ModuleID> _moduleId;

    std::mutex _lock;
    ComPtr<IMetaDataImport2> _pMetadataImport;
    std::unordered_map<shared::WSTRING, ClassID> _resolvedTypes;
};
