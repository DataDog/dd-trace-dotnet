// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "InlineVTCache.h"
#include "CoreLibModuleProvider.h"
#include "Log.h"
#include "MemoryFaultGuard.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

InlineVTCache::InlineVTCache(
    ICorProfilerInfo12* pCorProfilerInfo,
    CoreLibModuleProvider* pCoreLibModuleProvider)
    :
    _pCorProfilerInfo(pCorProfilerInfo),
    _pCoreLibModuleProvider(pCoreLibModuleProvider)
{
}

InlineVTCache::InspectionStatus InlineVTCache::TryContainsGCPointers(
    ClassID classID,
    bool& containsPointers)
{
    containsPointers = false;
    MemoryFaultGuard::RunResult result = MemoryFaultGuard::Run(
        [&containsPointers, classID]
        {
            containsPointers = GCDesc::ContainsGCPointers(classID);
        });

    switch (result)
    {
        case MemoryFaultGuard::RunResult::Completed:
            return InspectionStatus::Completed;
        case MemoryFaultGuard::RunResult::Faulted:
            return InspectionStatus::Faulted;
        case MemoryFaultGuard::RunResult::Unavailable:
            return InspectionStatus::GuardUnavailable;
    }

    return InspectionStatus::GuardUnavailable;
}

const InlineVTCache::InlineVTInfo* InlineVTCache::GetInlineVTInfo(ClassID classID)
{
    if (classID == 0)
    {
        return nullptr;
    }

    // probably done before calling this helpers but just in case, avoid unneeded cache lookup
    if (!GCDesc::ContainsGCPointers(classID))
    {
        return nullptr;
    }

    auto it = _cache.find(classID);
    if (it != _cache.end())
    {
        return it->second.has_value() ? &it->second.value() : nullptr;
    }

    // Inspecting the type here would mean resolving its fields types while the runtime is
    // suspended for the dump: this is done later, once the dump is over.
    _pendingClassIDs.insert(classID);

    // Record a "nothing to attribute" placeholder so the remaining objects of this type
    // stop at the cache lookup above instead of probing the queue again, once per object,
    // while the runtime is suspended. ResolvePendingTypes drops it before inspecting the
    // type for real.
    _cache.emplace(classID, std::nullopt);

    return nullptr;
}

const InlineVTCache::InlineVTInfo* InlineVTCache::GetOrBuildInlineVTInfo(
    ClassID classID,
    InspectionStatus* pStatus)
{
    if (pStatus != nullptr)
    {
        *pStatus = InspectionStatus::Completed;
    }

    if (classID == 0)
    {
        return nullptr;
    }

    // Unlike GetInlineVTInfo, this runs after the dump, on a ClassID that may have died
    // in the meantime: reading its MethodTable to skip the types without GC pointers
    // early is exactly what must not be done before the runtime has vouched for it.
    // BuildInlineVTInfo does that check once the type has been validated.
    auto it = _cache.find(classID);
    if (it == _cache.end())
    {
        // BuildInlineVTInfo inspects the base types, so it can insert into _cache: it must be
        // called before emplace to avoid reentering it while it is being modified.
        BuildResult result = BuildInlineVTInfo(classID);
        if (pStatus != nullptr)
        {
            *pStatus = result.status;
        }

        if (result.status != InspectionStatus::Completed)
        {
            return nullptr;
        }

        it = _cache.emplace(classID, std::move(result.info)).first;
    }

    return it->second.has_value() ? &it->second.value() : nullptr;
}

bool InlineVTCache::DropCacheIfModuleUnloaded()
{
    if (!_moduleUnloaded.exchange(false, std::memory_order_acq_rel))
    {
        return false;
    }

    Clear();
    return true;
}

size_t InlineVTCache::ResolvePendingTypes()
{
    // A module unloaded since the last dump: the queued ClassIDs may be dangling and
    // there is no way to tell which ones, so none of them gets inspected.
    if (DropCacheIfModuleUnloaded())
    {
        return 0;
    }

    if (_pendingClassIDs.empty())
    {
        return 0;
    }

    // Inspecting a type also inspects its base types, which adds entries to _cache:
    // iterate over a copy of the queue instead of the queue itself.
    std::unordered_set<ClassID> pendingClassIDs;
    pendingClassIDs.swap(_pendingClassIDs);

    // Drop the placeholders GetInlineVTInfo left behind before inspecting anything: a
    // type inspected below can pull in a base type that is queued too, and it must see
    // that base type rebuilt rather than its "nothing to attribute" placeholder.
    for (ClassID classID : pendingClassIDs)
    {
        _cache.erase(classID);
    }

    size_t faultedCount = 0;
    size_t unavailableCount = 0;

    for (ClassID classID : pendingClassIDs)
    {
        // Types without inline VTs are cached as "nothing to attribute" so they don't
        // come back in the queue at the next dump.
        //
        // Raw MethodTable reads are guarded individually inside BuildInlineVTInfo so
        // ComPtr/vector destructors and cache mutations never sit across a longjmp/SEH
        // recovery boundary.
        InspectionStatus status = InspectionStatus::Completed;
        GetOrBuildInlineVTInfo(classID, &status);

        if (status == InspectionStatus::Faulted)
        {
            faultedCount++;

            // Cached as "nothing to attribute" so the next dumps stop at the lookup
            // instead of faulting on this type again, dump after dump.
            _cache.insert_or_assign(classID, std::nullopt);
        }
        else if (status == InspectionStatus::GuardUnavailable)
        {
            unavailableCount++;

            // Keep the placeholder and retry once fault recovery becomes available.
            _cache.insert_or_assign(classID, std::nullopt);
            _pendingClassIDs.insert(classID);
        }
    }

    // Logged after the loop: a Log call inside the guard would keep its lock on a fault.
    if (faultedCount > 0)
    {
        Log::Warn("InlineVTCache: ", faultedCount, " type(s) could not be inspected because "
                  "their description was no longer readable. Their inline value types will "
                  "not be attributed in the reference tree.");
    }

    if (unavailableCount > 0)
    {
        Log::Warn("InlineVTCache: deferred ", unavailableCount, " type inspection(s) because "
                  "memory fault recovery is unavailable.");
    }

    return pendingClassIDs.size() - unavailableCount;
}

ClassID InlineVTCache::ResolveClassIDFromToken(
    ModuleID moduleID,
    mdToken token,
    ULONG32 typeArgCount,
    ClassID* pTypeArgs)
{
    // GetClassFromTokenAndTypeArgs may trigger a type load, so it can only be called outside
    // of the dump GC (see ResolvePendingTypes).
    //
    // A TypeRef or a TypeSpec does not identify a type defined in moduleID: the runtime would
    // start a type load that always fails, so don't even ask.
    if (TypeFromToken(token) != mdtTypeDef)
    {
        return 0;
    }

    ClassID classID = 0;
    HRESULT hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(
        moduleID, token, typeArgCount, pTypeArgs, &classID);

    return SUCCEEDED(hr) ? classID : 0;
}

bool InlineVTCache::TryGetTypeDefAndMetadata(
    ClassID classID,
    ModuleID& moduleID,
    mdTypeDef& typeDef,
    ComPtr<IMetaDataImport>& pMetadataImport,
    ClassID* pParentClassID,
    std::vector<ClassID>* pTypeArgs)
{
    moduleID = 0;
    typeDef = mdTokenNil;

    if (classID == 0)
    {
        return false;
    }

    // arrays are not described by a type definition
    CorElementType elementType;
    ClassID elementClassID;
    ULONG rank = 0;
    if (_pCorProfilerInfo->IsArrayClass(classID, &elementType, &elementClassID, &rank) == S_OK)
    {
        return false;
    }

    ClassID parentClassID = 0;
    ULONG32 typeArgCount = 0;
    HRESULT hr = _pCorProfilerInfo->GetClassIDInfo2(
        classID, &moduleID, &typeDef, &parentClassID, 0, &typeArgCount, nullptr);

    if (FAILED(hr) || moduleID == 0)
    {
        return false;
    }

    if (pParentClassID != nullptr)
    {
        *pParentClassID = parentClassID;
    }

    if (pTypeArgs != nullptr && typeArgCount > 0)
    {
        pTypeArgs->resize(typeArgCount);
        hr = _pCorProfilerInfo->GetClassIDInfo2(
            classID, nullptr, nullptr, nullptr, typeArgCount, &typeArgCount, pTypeArgs->data());
        if (FAILED(hr))
        {
            pTypeArgs->clear();
        }
    }

    hr = _pCorProfilerInfo->GetModuleMetaData(
        moduleID, ofRead, IID_IMetaDataImport,
        reinterpret_cast<IUnknown**>(pMetadataImport.GetAddressOf()));

    return SUCCEEDED(hr) && pMetadataImport.Get() != nullptr;
}

InlineVTCache::BuildResult InlineVTCache::BuildInlineVTInfo(ClassID classID)
{
    ModuleID moduleID = 0;
    mdTypeDef typeDef = mdTokenNil;
    ClassID parentClassID = 0;
    std::vector<ClassID> typeArgs;
    ComPtr<IMetaDataImport> pMetadataImport;

    // Validate the type through profiling APIs before the raw MethodTable read below.
    // Profiling APIs report invalid ClassIDs through HRESULT, while the raw read would
    // fault on freed memory.
    if (!TryGetTypeDefAndMetadata(classID, moduleID, typeDef, pMetadataImport, &parentClassID, &typeArgs))
    {
        return {std::nullopt, InspectionStatus::Completed};
    }

    bool containsPointers = false;
    InspectionStatus status = TryContainsGCPointers(classID, containsPointers);
    if (status != InspectionStatus::Completed)
    {
        return {std::nullopt, status};
    }

    if (!containsPointers)
    {
        return {std::nullopt, InspectionStatus::Completed};
    }

    // Parent fields sit at lower offsets in the object layout, so collect them first.
    std::vector<InlineVTField> inlineVtFields;
    if (parentClassID != 0)
    {
        status = MergeParentInlineVTs(parentClassID, inlineVtFields);
        if (status != InspectionStatus::Completed)
        {
            return {std::nullopt, status};
        }
    }

    ULONG fieldCount = 0;
    ULONG classSize = 0;
    HRESULT hr = _pCorProfilerInfo->GetClassLayout(
        classID, nullptr, 0, &fieldCount, &classSize);

    if (SUCCEEDED(hr) && fieldCount > 0)
    {
        std::vector<COR_FIELD_OFFSET> fieldOffsets(fieldCount);
        hr = _pCorProfilerInfo->GetClassLayout(
            classID, fieldOffsets.data(), fieldCount, &fieldCount, &classSize);

        if (SUCCEEDED(hr))
        {
            for (ULONG i = 0; i < fieldCount; i++)
            {
                VTFieldInfo fieldInfo;
                fieldInfo.offset = fieldOffsets[i].ulOffset;
                fieldInfo.fieldToken = fieldOffsets[i].ridOfField;

                status = ResolveValueTypeField(fieldInfo, moduleID, pMetadataImport.Get(), typeArgs);
                if (status != InspectionStatus::Completed)
                {
                    return {std::nullopt, status};
                }

                if (fieldInfo.isValueType && fieldInfo.valueTypeClassID != 0)
                {
                    // Keep profiling API calls outside MemoryFaultGuard: on Linux,
                    // siglongjmp across CLR-owned frames could skip locks or cleanup.
                    // This ClassID was just resolved by the runtime; invalidation is
                    // reported through HRESULT.
                    ULONG valueTypeFieldCount = 0;
                    ULONG valueTypeSize = 0;
                    HRESULT valueTypeLayoutHr = _pCorProfilerInfo->GetClassLayout(
                        fieldInfo.valueTypeClassID,
                        nullptr,
                        0,
                        &valueTypeFieldCount,
                        &valueTypeSize);

                    if (SUCCEEDED(valueTypeLayoutHr) && valueTypeSize != 0)
                    {
                        inlineVtFields.push_back(
                            {fieldInfo.offset, fieldInfo.valueTypeClassID, valueTypeSize});
                    }
                }
            }
        }
    }

    if (inlineVtFields.empty())
    {
        return {std::nullopt, InspectionStatus::Completed};
    }

    InlineVTInfo info;
    info.fields = std::move(inlineVtFields);
    return {std::optional<InlineVTInfo>{std::move(info)}, InspectionStatus::Completed};
}

InlineVTCache::InspectionStatus InlineVTCache::ResolveValueTypeField(
    VTFieldInfo& fieldInfo,
    ModuleID moduleID,
    IMetaDataImport* pMetadataImport,
    const std::vector<ClassID>& typeArgs)
{
    if (pMetadataImport == nullptr || fieldInfo.fieldToken == 0)
    {
        return InspectionStatus::Completed;
    }

    PCCOR_SIGNATURE pSignature = nullptr;
    ULONG signatureSize = 0;

    HRESULT hr = pMetadataImport->GetFieldProps(
        fieldInfo.fieldToken, nullptr, nullptr, 0, nullptr,
        nullptr, &pSignature, &signatureSize,
        nullptr, nullptr, nullptr);

    if (FAILED(hr) || pSignature == nullptr || signatureSize < 2)
    {
        return InspectionStatus::Completed;
    }

    ULONG idx = 0;
    idx++; // Skip IMAGE_CEE_CS_CALLCONV_FIELD

    if (idx >= signatureSize)
    {
        return InspectionStatus::Completed;
    }

    while (idx < signatureSize)
    {
        CorElementType prefix = static_cast<CorElementType>(pSignature[idx]);
        if (prefix == ELEMENT_TYPE_CMOD_OPT || prefix == ELEMENT_TYPE_CMOD_REQD)
        {
            idx++;
            mdToken token = mdTokenNil;
            if (!TryUncompressToken(pSignature, signatureSize, idx, token))
            {
                return InspectionStatus::Completed;
            }
            continue;
        }
        if (prefix == ELEMENT_TYPE_PINNED || prefix == ELEMENT_TYPE_BYREF)
        {
            idx++;
            continue;
        }
        break;
    }

    if (idx >= signatureSize)
    {
        return InspectionStatus::Completed;
    }

    CorElementType elementType = static_cast<CorElementType>(pSignature[idx]);
    ClassID valueTypeClassID = 0;

    if (elementType == ELEMENT_TYPE_VAR)
    {
        idx++;
        if (typeArgs.empty())
        {
            return InspectionStatus::Completed;
        }

        ULONG varIndex = 0;
        if (!TryUncompressData(pSignature, signatureSize, idx, varIndex))
        {
            return InspectionStatus::Completed;
        }

        if (varIndex < static_cast<ULONG>(typeArgs.size()))
        {
            ClassID argClassID = typeArgs[varIndex];
            if (IsClassIDValueType(argClassID))
            {
                valueTypeClassID = argClassID;
            }
        }
    }
    else if (elementType == ELEMENT_TYPE_VALUETYPE)
    {
        idx++;
        mdToken vtToken = mdTokenNil;
        if (!TryUncompressToken(pSignature, signatureSize, idx, vtToken))
        {
            return InspectionStatus::Completed;
        }

        valueTypeClassID = ResolveClassIDFromToken(moduleID, vtToken, 0, nullptr);
    }
    else if (elementType == ELEMENT_TYPE_GENERICINST)
    {
        // only embedded generic structs are of interest here:
        // a field of generic reference type is a reference, not an inline value type
        if ((signatureSize - idx < 2) ||
            (static_cast<CorElementType>(pSignature[idx + 1]) != ELEMENT_TYPE_VALUETYPE))
        {
            return InspectionStatus::Completed;
        }

        valueTypeClassID = ResolveGenericArgClassID(
            pSignature, signatureSize, idx, moduleID, pMetadataImport, typeArgs);
    }

    if (valueTypeClassID == 0)
    {
        return InspectionStatus::Completed;
    }

    bool containsPointers = false;
    InspectionStatus status = TryContainsGCPointers(valueTypeClassID, containsPointers);
    if (status != InspectionStatus::Completed)
    {
        return status;
    }

    if (!containsPointers)
    {
        return InspectionStatus::Completed;
    }

    fieldInfo.isValueType = true;
    fieldInfo.valueTypeClassID = valueTypeClassID;
    return InspectionStatus::Completed;
}

bool InlineVTCache::TryUncompressToken(
    PCCOR_SIGNATURE pSignature,
    ULONG signatureSize,
    ULONG& idx,
    mdToken& token)
{
    if (pSignature == nullptr || idx >= signatureSize)
    {
        return false;
    }

    uint32_t consumed = 0;
    uint32_t remaining = static_cast<uint32_t>(signatureSize - idx);
    HRESULT hr = CorSigUncompressToken(
        &pSignature[idx],
        remaining,
        &token,
        &consumed);

    if (FAILED(hr) || consumed == 0 || consumed > remaining)
    {
        return false;
    }

    idx += consumed;
    return true;
}

bool InlineVTCache::TryUncompressData(
    PCCOR_SIGNATURE pSignature,
    ULONG signatureSize,
    ULONG& idx,
    ULONG& value)
{
    if (pSignature == nullptr || idx >= signatureSize)
    {
        return false;
    }

    uint32_t decoded = 0;
    uint32_t consumed = 0;
    uint32_t remaining = static_cast<uint32_t>(signatureSize - idx);
    HRESULT hr = CorSigUncompressData(
        &pSignature[idx],
        remaining,
        &decoded,
        &consumed);

    if (FAILED(hr) || consumed == 0 || consumed > remaining)
    {
        return false;
    }

    value = static_cast<ULONG>(decoded);
    idx += consumed;
    return true;
}

ClassID InlineVTCache::ResolveGenericArgClassID(
    PCCOR_SIGNATURE pSignature, ULONG signatureSize, ULONG& idx,
    ModuleID moduleID, IMetaDataImport* pMetadataImport,
    const std::vector<ClassID>& typeArgs)
{
    if (idx >= signatureSize)
    {
        return 0;
    }

    CorElementType argType = static_cast<CorElementType>(pSignature[idx]);

    if (argType == ELEMENT_TYPE_VAR)
    {
        idx++;
        ULONG varIndex = 0;
        if (!TryUncompressData(pSignature, signatureSize, idx, varIndex))
        {
            return 0;
        }

        if (varIndex >= static_cast<ULONG>(typeArgs.size()) || typeArgs[varIndex] == 0)
        {
            return 0;
        }
        return typeArgs[varIndex];
    }

    if (argType == ELEMENT_TYPE_VALUETYPE || argType == ELEMENT_TYPE_CLASS)
    {
        idx++;
        mdToken argToken = mdTokenNil;
        if (!TryUncompressToken(pSignature, signatureSize, idx, argToken))
        {
            return 0;
        }

        return ResolveClassIDFromToken(moduleID, argToken, 0, nullptr);
    }

    if (argType == ELEMENT_TYPE_GENERICINST)
    {
        idx++;
        if (idx >= signatureSize)
        {
            return 0;
        }
        CorElementType base = static_cast<CorElementType>(pSignature[idx]);
        if (base != ELEMENT_TYPE_VALUETYPE && base != ELEMENT_TYPE_CLASS)
        {
            return 0;
        }

        idx++;
        mdToken token = mdTokenNil;
        if (!TryUncompressToken(pSignature, signatureSize, idx, token))
        {
            return 0;
        }

        ULONG nestedArgCount = 0;
        if (!TryUncompressData(pSignature, signatureSize, idx, nestedArgCount) ||
            nestedArgCount > signatureSize - idx)
        {
            return 0;
        }

        std::vector<ClassID> nestedArgs;
        nestedArgs.reserve(nestedArgCount);
        for (ULONG i = 0; i < nestedArgCount && idx < signatureSize; i++)
        {
            ClassID nested = ResolveGenericArgClassID(
                pSignature, signatureSize, idx, moduleID, pMetadataImport, typeArgs);
            if (nested == 0)
            {
                return 0;
            }
            nestedArgs.push_back(nested);
        }

        if (nestedArgs.size() != nestedArgCount)
        {
            return 0;
        }

        return ResolveClassIDFromToken(moduleID, token, nestedArgCount, nestedArgs.data());
    }

    // Primitive / well-known types (single-byte element types with no trailing token).
    const WCHAR* name = GetPrimitiveTypeName(argType);
    if (name != nullptr)
    {
        idx++;
        return ResolvePrimitiveClassID(argType, moduleID, pMetadataImport);
    }

    return 0;
}

const WCHAR* InlineVTCache::GetPrimitiveTypeName(CorElementType elementType)
{
    switch (elementType)
    {
        case ELEMENT_TYPE_BOOLEAN: return WStr("System.Boolean");
        case ELEMENT_TYPE_CHAR:    return WStr("System.Char");
        case ELEMENT_TYPE_I1:      return WStr("System.SByte");
        case ELEMENT_TYPE_U1:      return WStr("System.Byte");
        case ELEMENT_TYPE_I2:      return WStr("System.Int16");
        case ELEMENT_TYPE_U2:      return WStr("System.UInt16");
        case ELEMENT_TYPE_I4:      return WStr("System.Int32");
        case ELEMENT_TYPE_U4:      return WStr("System.UInt32");
        case ELEMENT_TYPE_I8:      return WStr("System.Int64");
        case ELEMENT_TYPE_U8:      return WStr("System.UInt64");
        case ELEMENT_TYPE_R4:      return WStr("System.Single");
        case ELEMENT_TYPE_R8:      return WStr("System.Double");
        case ELEMENT_TYPE_I:       return WStr("System.IntPtr");
        case ELEMENT_TYPE_U:       return WStr("System.UIntPtr");
        case ELEMENT_TYPE_STRING:  return WStr("System.String");
        case ELEMENT_TYPE_OBJECT:  return WStr("System.Object");
        default:                   return nullptr;
    }
}

ClassID InlineVTCache::ResolvePrimitiveClassID(
    CorElementType elementType,
    ModuleID moduleID,
    IMetaDataImport* pMetadataImport)
{
    auto cached = _primitiveClassIDs.find(elementType);
    if (cached != _primitiveClassIDs.end())
    {
        return cached->second;
    }

    const WCHAR* typeName = GetPrimitiveTypeName(elementType);
    if (typeName == nullptr)
    {
        return 0;
    }

    ClassID classID = 0;

    // Fast path: the module defines the type (i.e. it IS the core library).
    mdTypeDef typeDef = mdTokenNil;
    if ((pMetadataImport != nullptr) &&
        SUCCEEDED(pMetadataImport->FindTypeDefByName(typeName, mdTokenNil, &typeDef)))
    {
        classID = ResolveClassIDFromToken(moduleID, typeDef, 0, nullptr);
    }

    if (classID == 0 && _pCoreLibModuleProvider != nullptr)
    {
        // Primitives are defined in the core library: it is the only module able to resolve them.
        // Looking for a TypeRef in the current module instead would give a token that the runtime
        // refuses (it expects a TypeDef of the module it is given).
        classID = _pCoreLibModuleProvider->ResolveTypeInCoreLib(typeName);
    }

    if (classID != 0)
    {
        _primitiveClassIDs[elementType] = classID;
    }

    return classID;
}

InlineVTCache::InspectionStatus InlineVTCache::MergeParentInlineVTs(
    ClassID parentClassID,
    std::vector<InlineVTField>& fields)
{
    InspectionStatus status = InspectionStatus::Completed;
    const InlineVTInfo* parentInfo = GetOrBuildInlineVTInfo(parentClassID, &status);
    if (status != InspectionStatus::Completed)
    {
        return status;
    }

    if (parentInfo == nullptr)
    {
        return InspectionStatus::Completed;
    }

    fields.insert(fields.end(),
                  parentInfo->fields.begin(),
                  parentInfo->fields.end());
    return InspectionStatus::Completed;
}

bool InlineVTCache::IsClassIDValueType(ClassID classID)
{
    ModuleID moduleID = 0;
    mdTypeDef typeDef = mdTokenNil;
    ComPtr<IMetaDataImport> pMetadataImport;

    if (!TryGetTypeDefAndMetadata(classID, moduleID, typeDef, pMetadataImport))
    {
        return false;
    }

    DWORD flags = 0;
    mdToken extendsToken = mdTokenNil;
    HRESULT hr = pMetadataImport->GetTypeDefProps(
        typeDef, nullptr, 0, nullptr, &flags, &extendsToken);

    if (FAILED(hr))
    {
        return false;
    }

    if (TypeFromToken(extendsToken) == mdtTypeRef ||
        TypeFromToken(extendsToken) == mdtTypeDef)
    {
        WCHAR baseTypeName[256];
        ULONG baseTypeNameLen = 0;

        if (TypeFromToken(extendsToken) == mdtTypeRef)
        {
            hr = pMetadataImport->GetTypeRefProps(
                extendsToken, nullptr, baseTypeName, 256, &baseTypeNameLen);
        }
        else
        {
            hr = pMetadataImport->GetTypeDefProps(
                extendsToken, baseTypeName, 256, &baseTypeNameLen, nullptr, nullptr);
        }

        if (SUCCEEDED(hr))
        {
            if (WStrCmp(baseTypeName, WStr("System.ValueType")) == 0 ||
                WStrCmp(baseTypeName, WStr("System.Enum")) == 0)
            {
                return true;
            }
        }
    }

    return false;
}

size_t InlineVTCache::GetMemorySize() const
{
    size_t total = sizeof(InlineVTCache);

    total += _cache.bucket_count() * sizeof(void*);
    for (const auto& [id, opt] : _cache)
    {
        total += sizeof(ClassID) + sizeof(std::optional<InlineVTInfo>);
        if (opt.has_value())
        {
            total += opt->fields.capacity() * sizeof(InlineVTField);
        }
    }

    total += _pendingClassIDs.bucket_count() * sizeof(void*);
    total += _pendingClassIDs.size() * sizeof(ClassID);

    total += _primitiveClassIDs.bucket_count() * sizeof(void*);
    total += _primitiveClassIDs.size() * (sizeof(CorElementType) + sizeof(ClassID));

    return total;
}

size_t InlineVTCache::GetEntryCount() const
{
    size_t count = 0;
    for (const auto& [_, opt] : _cache)
    {
        if (opt.has_value())
        {
            count++;
        }
    }
    return count;
}
