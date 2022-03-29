// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <functional>
#include <mutex>
#include <unordered_map>
#include <vector>

#include "cor.h"
#include "corprof.h"

#include "StackFrameCodeKind.h"
#include "StackFrameInfo.h"

#include "shared/src/native-src/string.h"

class ResolvedSymbolsCache
{
public:
    static const shared::WSTRING UnknownThreadName;

    static const shared::WSTRING MethodFlagMoniker_UnknownAccess;
    static const shared::WSTRING MethodFlagMoniker_PrivateScope;
    static const shared::WSTRING MethodFlagMoniker_Private;
    static const shared::WSTRING MethodFlagMoniker_FamANDAssem;
    static const shared::WSTRING MethodFlagMoniker_Assem;
    static const shared::WSTRING MethodFlagMoniker_Family;
    static const shared::WSTRING MethodFlagMoniker_FamORAssem;
    static const shared::WSTRING MethodFlagMoniker_Public;
    static const shared::WSTRING MethodFlagMoniker_Static;
    static const shared::WSTRING MethodFlagMoniker_Final;
    static const shared::WSTRING MethodFlagMoniker_Virtual;
    static const shared::WSTRING MethodFlagMoniker_Abstract;
    static const shared::WSTRING MethodFlagMoniker_SpecialName;
    static const shared::WSTRING MethodFlagMoniker_PinvokeImpl;
    static const shared::WSTRING MethodFlagMoniker_UnmanagedExport;
    static const shared::WSTRING NamespacePrefix;
    static const shared::WSTRING TypeNamePrefix;
    static const shared::WSTRING NestedTypeSeparator;
    static const shared::WSTRING EmptyNamespaceMoniker;
    static const shared::WSTRING UnknownFunctionNameMoniker;
    static const shared::WSTRING UnknownTypeNameMoniker;
    static const shared::WSTRING UnknownAssemblyNameMoniker;
    static const shared::WSTRING EmptyNamespaceWithPrefixMoniker;
    static const shared::WSTRING UnknownTypeNameWithPrefixMoniker;
    static const shared::WSTRING SpecialTypeMoniker_Array;
    static const shared::WSTRING SpecialTypeMoniker_NonArrayComposite;
    static const shared::WSTRING SpecialTypeMoniker_SystemDotCanon;
    static const shared::WSTRING UnknownTypeAndNamespaceWithPrefixMoniker;

    static const WCHAR* GenericParamList_Open;
    static const WCHAR* GenericParamList_Close;
    static const WCHAR* GenericParamList_Separator;
    static const WCHAR* GenericParamList_MultipleUnknownItemsPlaceholder;
    static const WCHAR* GenericParamList_SingleUnknownItemPlaceholderBase;
    static const WCHAR* ReservedApiNameChars;
    static const WCHAR ReservedCharReplacement;

public:
    ResolvedSymbolsCache();
    ~ResolvedSymbolsCache();
    ResolvedSymbolsCache(ResolvedSymbolsCache const&) = delete;
    ResolvedSymbolsCache& operator=(ResolvedSymbolsCache const&) = delete;

    static bool IsSharedStaticConstant(const shared::WSTRING* str);
    inline static bool IsSharedStaticConstant(const shared::WSTRING& str)
    {
        return IsSharedStaticConstant(&str);
    }
    static void ReleaseWStringIfNotShared(const shared::WSTRING** ppWString);
    static void ReplaceReservedChars(WCHAR* wcharBuff, std::int32_t wcharCount);
    inline static void AdjustCharBuffLenToRemoveTrailingNulls(const WCHAR* wcharBuff, std::int32_t* wcharCount)
    {
        if (wcharBuff != nullptr && wcharCount != nullptr)
        {
            while (*wcharCount > 0 && wcharBuff[*wcharCount - 1] == '\0')
            {
                (*wcharCount)--;
            }
        }
    }

public:
    // Note: the frameinfo is addrefed if added/found
    bool TryGetFrameInfoForFunctionId(FunctionID clrFunctionId, const StackFrameInfo** ppFrameInfo);
    bool AddFrameInfoForFunctionId(FunctionID clrFunctionId, const StackFrameInfo* pFrameInfo);
    bool TryGetTypeInfoForClassId(ClassID clrClassId, const ManagedTypeInfo** ppTypeInfo);
    bool AddTypeInfoForClassId(ClassID clrClassId, const ManagedTypeInfo* pTypeInfo);
    bool TryGetTypeInfoForTypeDefToken(mdTypeDef mdTypeDefToken, ModuleID clrModuleId, const ManagedTypeInfo** ppTypeInfo);
    bool AddTypeInfoForForTypeDefToken(mdTypeDef mdTypeDefToken, ModuleID clrModuleId, const ManagedTypeInfo* pTypeInfo);
    const StackFrameInfo* GetNoInfoFrameForCodeKind(StackFrameCodeKind codeKind);

private:
    /// <summary>
    /// Shared code by the destructor to delete the contents of a cache map (we have several such maps):
    /// </summary>
    template <typename TMap, typename TItemNonConst>
    void DeleteTrackedMap(TMap** ppTrackedMapInstanceVar)
    {
        TMap* ppTrackedMap = *ppTrackedMapInstanceVar;
        if (ppTrackedMap != nullptr)
        {
            *ppTrackedMapInstanceVar = nullptr;

            typename TMap::iterator iter = ppTrackedMap->begin();
            while (iter != ppTrackedMap->end())
            {
                const_cast<TItemNonConst>(iter->second)->Release();
                ++iter;
            }

            delete ppTrackedMap;
        }
    }

    template <typename TMap>
    bool TryGetFromMap(const TMap* pMap, typename TMap::key_type key, typename TMap::mapped_type* pValue)
    {
        typename TMap::const_iterator elem = pMap->find(key);

        if (elem == pMap->end())
        {
            return false;
        }
        else
        {
            *pValue = elem->second;
            return true;
        }
    }

    template <typename TMap>
    bool TryInsertIntoMap(TMap* pMap,
                          typename TMap::key_type key,
                          const typename TMap::mapped_type& newValue,
                          typename TMap::mapped_type* pValueInTable)
    {
        std::pair<typename TMap::iterator, bool> res = pMap->insert(std::make_pair(key, newValue));
        bool inserted = res.second;

        if (pValueInTable != nullptr)
        {
            if (inserted) // key was not in use, value was inserted. Make *pValueInTable point to the new value.
            {
                *pValueInTable = newValue;
            }
            else // key was already in use, value was not inserted. Make *pValueInTable point to the existing value.
            {
                *pValueInTable = res.first->second;
            }
        }

        return inserted;
    }

    template <typename TMap>
    bool TryInsertIntoMap(TMap* pMap, typename TMap::key_type key, const typename TMap::mapped_type& newValue)
    {
        return TryInsertIntoMap(pMap, key, newValue, nullptr);
    }

private:
    struct TypeDefTokenAndModuleId
    {
    public:
        TypeDefTokenAndModuleId(mdTypeDef typeDefToken, ModuleID moduleId) :
            TypeDefToken(typeDefToken), ModuleId(moduleId)
        {
        }
        mdTypeDef TypeDefToken;
        ModuleID ModuleId;

        bool operator==(const TypeDefTokenAndModuleId& keyVal) const
        {
            return ((this->TypeDefToken == keyVal.TypeDefToken) && (this->ModuleId == keyVal.ModuleId));
        }

    public:
        struct Hasher
        {
        private:
            static const std::hash<mdTypeDef> TokenHasher;   // should this be std::hash<std::uint32_t> ?
            static const std::hash<ModuleID> ModuleIdHasher; // should this be std::hash<std::uint64_t> ?

        public:
            std::size_t operator()(const TypeDefTokenAndModuleId& keyVal) const
            {
                size_t hashVal = 17;
                hashVal = (hashVal << 5) - hashVal + TokenHasher(keyVal.TypeDefToken); // 31 * hashVal + Hasher(token)
                hashVal = (hashVal << 5) - hashVal + ModuleIdHasher(keyVal.ModuleId);  // 31 * hashVal + Hasher(module)
                return hashVal;
            }
        };
    };

private:
    std::unordered_map<ClassID, const ManagedTypeInfo*>* _pTypeInfosByClassId;
    std::unordered_map<TypeDefTokenAndModuleId, const ManagedTypeInfo*, TypeDefTokenAndModuleId::Hasher>* _pTypeInfosByTypeDefAndModuleId;
    std::unordered_map<FunctionID, const StackFrameInfo*>* _pStackFrameInfosByFunctionId;
    std::unordered_map<StackFrameCodeKind, const StackFrameInfo*>* _pNoInfoStackFrameInfosByCodeKind;

    std::mutex _lockTypeInfosByClassId;
    std::mutex _lockTypeInfosByTypeDefAndModuleId;
    std::mutex _lockStackFrameInfosByFunctionId;
    std::mutex _lockNoInfoStackFrameInfosByCodeKind;
};
