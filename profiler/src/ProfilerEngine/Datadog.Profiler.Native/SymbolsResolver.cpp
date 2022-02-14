// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef _WINDOWS
#include <atlcomcli.h>
#endif

#include <stdexcept>

#include "HResultConverter.h"
#include "Log.h"
#include "ResolvedSymbolsCache.h"
#include "StackFrameCodeKind.h"
#include "SymbolsResolver.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

SymbolsResolver* SymbolsResolver::s_singletonInstance = nullptr;
const ULONG SymbolsResolver::MethodNameBuffMaxSize = 200;
const ULONG SymbolsResolver::TypeNameBuffMaxSize = 300;
const ULONG SymbolsResolver::AssemblyNameBuffMaxSize = 400;
const ULONG32 SymbolsResolver::InitialTypeArgsBuffLen = 5;


void SymbolsResolver::CreateNewSingletonInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
    SymbolsResolver* newSingletonInstance = new SymbolsResolver(pCorProfilerInfo);

    SymbolsResolver::DeleteSingletonInstance();
    SymbolsResolver::s_singletonInstance = newSingletonInstance;
}

SymbolsResolver* SymbolsResolver::GetSingletonInstance()
{
    SymbolsResolver* singletonInstance = SymbolsResolver::s_singletonInstance;
    if (singletonInstance != nullptr)
    {
        return singletonInstance;
    }

    throw std::logic_error("No singleton instance of SymbolsResolver has been created, or it has already been deleted.");
}

void SymbolsResolver::DeleteSingletonInstance(void)
{
    SymbolsResolver* singletonInstance = SymbolsResolver::s_singletonInstance;
    if (singletonInstance != nullptr)
    {
        SymbolsResolver::s_singletonInstance = nullptr;
        delete singletonInstance;
    }
}

SymbolsResolver::SymbolsResolver(ICorProfilerInfo4* pCorProfilerInfo) :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pResolvedSymbolsCache{new ResolvedSymbolsCache()},
    _getResolveSymbols_Worker{pCorProfilerInfo, this}
{
    _pCorProfilerInfo->AddRef();
    _getResolveSymbols_Worker.Start();
}

SymbolsResolver::~SymbolsResolver()
{
    ResolvedSymbolsCache* pResolvedSymbolsCache = _pResolvedSymbolsCache;
    if (pResolvedSymbolsCache != nullptr)
    {
        delete pResolvedSymbolsCache;
        _pResolvedSymbolsCache = nullptr;
    }

    ICorProfilerInfo4* pCorProfilerInfo = _pCorProfilerInfo;
    if (pCorProfilerInfo != nullptr)
    {
        pCorProfilerInfo->Release();
        _pCorProfilerInfo = nullptr;
    }
}


bool SymbolsResolver::ResolveAppDomainInfoSymbols(AppDomainID appDomainId,
                                                  const std::uint32_t appDomainNameBuffSize,
                                                  std::uint32_t* pActualAppDomainNameLen,
                                                  WCHAR* pAppDomainNameBuff,
                                                  std::uint64_t* pAppDomainProcessId,
                                                  bool offloadToWorkerThread)
{
    if (offloadToWorkerThread)
    {
        Worker::Parameters params(appDomainId, appDomainNameBuffSize, pActualAppDomainNameLen, pAppDomainNameBuff, pAppDomainProcessId);
        Worker::Results results;

        if (_getResolveSymbols_Worker.ExecuteWorkItem(&params, &results))
        {
            return results.Success;
        }
        else
        {
            return false;
        }
    }
    else
    {
        return ResolveAppDomainInfoSymbols(appDomainId, appDomainNameBuffSize, pActualAppDomainNameLen, pAppDomainNameBuff, pAppDomainProcessId);
    }
}

bool SymbolsResolver::ResolveStackFrameSymbols(const StackSnapshotResultFrameInfo& capturedFrame,
                                               StackFrameInfo** ppResolvedFrame,
                                               bool offloadToWorkerThread)
{
    switch (capturedFrame.GetCodeKind())
    {
        case StackFrameCodeKind::ClrManaged:
        {
            FunctionID clrFunctionId = capturedFrame.GetClrFunctionId();

            // Look in the cache:
            if (_pResolvedSymbolsCache->TryGetFrameInfoForFunctionId(clrFunctionId, const_cast<const StackFrameInfo**>(ppResolvedFrame)))
            {
                // Found data in the cache. Can just return it.
                // The cache AddRef-ed the pointer before giving it to us, no need to do this again before exposing.
                return true;
            }

            // Did not find item in the cache. Need to do the work:
            if (offloadToWorkerThread)
            {
                Worker::Parameters params(clrFunctionId, ppResolvedFrame);
                Worker::Results results;

                return _getResolveSymbols_Worker.ExecuteWorkItem(&params, &results);
            }
            else
            {
                GetManagedMethodName(clrFunctionId, ppResolvedFrame);
                return (*ppResolvedFrame != nullptr);
            }
        }

        // For these we may add something smarter in the future:
        case StackFrameCodeKind::ClrNative:
        case StackFrameCodeKind::UserNative:
        case StackFrameCodeKind::UnknownNative:
        case StackFrameCodeKind::Kernel:
        case StackFrameCodeKind::MultipleMixed:
        {
            *ppResolvedFrame = const_cast<StackFrameInfo*>(_pResolvedSymbolsCache->GetNoInfoFrameForCodeKind(capturedFrame.GetCodeKind()));
            return (*ppResolvedFrame != nullptr);
        }

        // For these, there is not much more we can do:
        case StackFrameCodeKind::Dummy:
        case StackFrameCodeKind::NotDetermined:
        case StackFrameCodeKind::Unknown:
        default:
        {
            *ppResolvedFrame = const_cast<StackFrameInfo*>(_pResolvedSymbolsCache->GetNoInfoFrameForCodeKind(capturedFrame.GetCodeKind()));
            return (*ppResolvedFrame != nullptr);
        }
    }
}

bool SymbolsResolver::ResolveAppDomainInfoSymbols(AppDomainID appDomainId,
                                                  const std::uint32_t appDomainNameBuffSize,
                                                  std::uint32_t* pActualAppDomainNameLen,
                                                  WCHAR* pAppDomainNameBuff,
                                                  std::uint64_t* pAppDomainProcessId)
{
    ULONG adNameLen;
    ProcessID adProcessId;
    HRESULT hr = _pCorProfilerInfo->GetAppDomainInfo(appDomainId, appDomainNameBuffSize, &adNameLen, pAppDomainNameBuff, &adProcessId);
    if (FAILED(hr))
    {
        Log::Info("GetAppDomainInfo failed for ", appDomainId, " : ", HResultConverter::ToStringWithCode(hr));

        *pActualAppDomainNameLen = 0;
        pAppDomainNameBuff[0] = WStr('\0');
        *pAppDomainProcessId = 0;
    }
    else
    {
        *pActualAppDomainNameLen = adNameLen;
        *pAppDomainProcessId = static_cast<std::uint64_t>(adProcessId);
    }

    return SUCCEEDED(hr);
}

/// <summary>
/// ppMethodName, pppMethodFlags, ppTypeName, ppAssemblyName must be non-null and point to valid pointer locations;
/// this is a private method, we do not re-validate it here.
///
/// After this method (*ppMethodName), (*ppTypeName), (*ppAssemblyName) will point either to newly allocated wstring objects,
/// or to objects previously allocated and now held in cache.
///
/// The array of string pointers (*pppMethodFlags) will always be newly allocated to be null-terminated. The pointers contained
/// there point to static consts.
/// </summary>
void SymbolsResolver::GetManagedMethodName(FunctionID functionId, StackFrameInfo** ppFrameInfo)
{
    *ppFrameInfo = new StackFrameInfo(StackFrameCodeKind::ClrManaged);

    // Used in deciding whether or not we will add the eventual result to the cache.
    bool success = false;

    // We AddRef this instance right away so that we do not forget it later and since it is exposed via the out parameter.
    // The caller must release it when done (same as the COM pattern).
    // If we decide to abandon (avoid returning) the object *ppFrameInfo, then we need to call Resease() before we loose the pointer.
    (*ppFrameInfo)->AddRef();

    // Call GetFunctionInfo2 to get the containing class and module, the metadata token, and
    // the generic type parameters of the method (not the containing type).
    //
    // Note that containingClassId may be 0 in the case of a generic type with at least 1 reference type as parameter.
    // A valid clrFrameInfo would help in that case, but it is only available in the context of enter/leave callbacks.
    // (The StackSnapshotCallbackHandler in StackSamplerLoop.cpp should have a comment detailing this.)
    //
    // The solution is to use metadata API to rebuild the un-instanciated definition of the generic type:
    //    class MyClass<K, V>  --> MyClass<K, V>
    // Note: it is not possible to get more details about K and V types so the ct: recursive syntax cannot be used
    //       and the name will be "ct:MyClass<K, V>"
    //
    // Even when containingClassId is 0, the generic parameters of the method (not the type) are still available
    // and typeArgsCount should not be 0.
    //
    // Unlike what the GetClassIDInfo2 documentation states, GetFunctionInfo2 must always be called
    // with 0 as number of arguments to get the real generic parameters count
    // https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo2-getfunctioninfo2-method
    //
    ClassID containingClassId;
    ModuleID containingModuleId;
    mdToken mdTokenFunc;

    // GetFunctionInfo2 is called with 0 to get the actual number of the method generic parameters
    ClassID* functionGenericParameters = nullptr;
    ULONG32 functionGenericParametersCount = 0;

    HRESULT hr = _pCorProfilerInfo->GetFunctionInfo2(
        functionId,
        static_cast<COR_PRF_FRAME_INFO>(0), /* clrFrameInfo */
        &containingClassId,
        &containingModuleId,
        &mdTokenFunc,
        0,
        &functionGenericParametersCount,
        nullptr);

    if (FAILED(hr))
    {
        containingClassId = 0;
        containingModuleId = 0;
        mdTokenFunc = 0;
        functionGenericParameters = nullptr;
        functionGenericParametersCount = 0;

        *ppFrameInfo = nullptr;
        return;
    }

    if (functionGenericParametersCount > 0)
    {
        // in case of generic function, it's time to allocate the array
        // that will receive the ClassID for each type parameter
        functionGenericParameters = new ClassID[functionGenericParametersCount];
        hr = _pCorProfilerInfo->GetFunctionInfo2(
            functionId,
            static_cast<COR_PRF_FRAME_INFO>(0), /* clrFrameInfo */
            nullptr,                            /* &containingClassId */
            nullptr,                            /* &containingModuleId */
            nullptr,                            /* &mdTokenFunc */
            functionGenericParametersCount,
            &functionGenericParametersCount,
            functionGenericParameters);

        if (FAILED(hr))
        {
            Log::Debug("GetManagedMethodName: The 1st call to GetFunctionInfo2() succeeded, but the 2nd call failed with HR=",
                       HResultConverter::ToStringWithCode(hr), ".");

            // This is not supposed to happen but just in case, since generic parameters are not available,
            // let's say that this is not a generic function
            delete[] functionGenericParameters;
            functionGenericParameters = nullptr;
            functionGenericParametersCount = 0;
        }
    }

    // Get the pointer to metadata for the module that contains the method pointed by the functionId.
    // If we previously got the containingModuleId, we use GetModuleMetaData(), otherwise we have to use GetTokenAndMetaDataFromFunction().

    // version 2 is needed for generics support
    ComPtr<IMetaDataImport2> copModuleMetadataIface;
    if (containingModuleId != 0)
    {
        hr = _pCorProfilerInfo->GetModuleMetaData(
            containingModuleId,
            CorOpenFlags::ofRead /* | CorOpenFlags::ofNoTransform */,
            IID_IMetaDataImport2,
            reinterpret_cast<IUnknown**>(copModuleMetadataIface.GetAddressOf()));

        if (FAILED(hr) || copModuleMetadataIface.Get() == nullptr)
        {
            Log::Debug("GetManagedMethodName: GetModuleMetaData() failed with result ", HResultConverter::ToStringWithCode(hr),
                       " or returned a null metadata import pointer");
            copModuleMetadataIface.Reset(); // also sets wrapped pointer to nullptr
        }
    }

    if (copModuleMetadataIface.Get() == nullptr)
    {
        hr = _pCorProfilerInfo->GetTokenAndMetaDataFromFunction(
            functionId,
            IID_IMetaDataImport,
            reinterpret_cast<IUnknown**>(copModuleMetadataIface.GetAddressOf()),
            &mdTokenFunc);

        if (FAILED(hr) || copModuleMetadataIface.Get() == nullptr)
        {
            // If we cannot access the method metadata, we cannot get its name.
            Log::Debug("GetManagedMethodName: GetTokenAndMetaDataFromFunction() failed with result ",
                       HResultConverter::ToStringWithCode(hr),
                       " or returned a null metadata import pointer");

            copModuleMetadataIface.Reset(); // also sets wrapped pointer to nullptr
        }
    }

    mdTypeDef mdTypeDefTokenContainingType;

    if (copModuleMetadataIface.Get() == nullptr)
    {
        // If we cannot access the method metadata, we cannot get its name.
        (*ppFrameInfo)->UseFunctionName(&ResolvedSymbolsCache::UnknownFunctionNameMoniker);
        mdTypeDefTokenContainingType = 0;
    }
    else
    {
        // Get the method metadata and extract the method name and the TypeDef token for the containing type:
        WCHAR methodNameBuff[SymbolsResolver::MethodNameBuffMaxSize];
        ULONG methodNameLen;
        DWORD methodFlagsMask;

        hr = copModuleMetadataIface->GetMethodProps(
            mdTokenFunc,
            &mdTypeDefTokenContainingType,
            methodNameBuff,
            SymbolsResolver::MethodNameBuffMaxSize,
            &methodNameLen,
            &methodFlagsMask,
            nullptr,  /* point to the blob value of meta data */
            nullptr,  /* actual size of signature blob */
            nullptr,  /* codeRVA */
            nullptr); /* method implementation flags */

        if (FAILED(hr))
        {
            Log::Debug("GetManagedMethodName: GetMethodProps() failed with result ",
                       HResultConverter::ToStringWithCode(hr), ".");
            (*ppFrameInfo)->UseFunctionName(&ResolvedSymbolsCache::UnknownFunctionNameMoniker);
            mdTypeDefTokenContainingType = 0;
        }
        else
        {
            // We got the metadata.
            // Properly turn the name buffer into a string:
            methodNameLen = (std::min)(methodNameLen, SymbolsResolver::MethodNameBuffMaxSize);
            (*ppFrameInfo)->CreateNewFunctionName(methodNameBuff, methodNameLen);

            // Allocate a local vector array of string pointers and fill it with pointers to static constants denoting method flags:
            std::vector<const shared::WSTRING*> methodFlagMoikers;
            GetMethodFlags(methodFlagsMask, methodFlagMoikers);
            (*ppFrameInfo)->SetManagedMethodFlags(methodFlagMoikers);

            success = true;
        }
    }

    // Get the name of the type:
    const ManagedTypeInfo* pContainingTypeInfo;
    bool successContainingType =
        GetManagedTypeName(containingClassId,
                           containingModuleId,
                           mdTypeDefTokenContainingType,
                           copModuleMetadataIface,
                           &pContainingTypeInfo);

    success = success && successContainingType;

    (*ppFrameInfo)->SetContainingTypeInfo(pContainingTypeInfo);

    const_cast<ManagedTypeInfo*>(pContainingTypeInfo)->Release();
    pContainingTypeInfo = nullptr;

    // Append generic type parameters in if any
    if (functionGenericParametersCount > 0)
    {
        // Earlier we may have created a new method name string or used a constant (i.e. global static).
        // We are about to append to the method name, so if we used a global static before, we need to copy
        // it into a separate string instance. The for-modifying getter should do that:
        shared::WSTRING* pGenericMethodName = (*ppFrameInfo)->GetFunctionNameForModifying();

        pGenericMethodName->append(ResolvedSymbolsCache::GenericParamList_Open);

        // Call GetManagedTypeName for each generic type argument and append the type name as required.
        // The ManagedTypeInfo instances potentially created by these recursive calls will end up in cache like
        // all other resolved type names too.
        for (ULONG32 currentGenericParameter = 0; currentGenericParameter < functionGenericParametersCount; currentGenericParameter++)
        {
            ClassID typeParamClassId = functionGenericParameters[currentGenericParameter];

            if (currentGenericParameter > 0)
            {
                pGenericMethodName->append(ResolvedSymbolsCache::GenericParamList_Separator);
            }

            const ManagedTypeInfo* pTypeParamInfo;
            bool successTypeParam =
                GetManagedTypeName(typeParamClassId,
                                   static_cast<ModuleID>(0),
                                   static_cast<mdTypeDef>(0),
                                   ComPtr<IMetaDataImport2>(),
                                   &pTypeParamInfo);

            success = success && successTypeParam;

            if (pTypeParamInfo == nullptr || ResolvedSymbolsCache::SpecialTypeMoniker_SystemDotCanon == *(pTypeParamInfo->GetTypeName()))
            {
                // Type params that are non-value types may come back as "System.__Canon".
                // StackSnapshotCallbackHandler(..) in StackSamplerLoop.cpp should have a comment detailing the reasons.
                // Listing this as such is not very readable, so we will replace it with T1, T2, etc.:
                // __Canon (or something else like T?) should not be replaced by Txx because Txx probably exists in the method definition
                //       but not at the same position.
                //       For example if in a MyClass.Do<K, V, T1>(), one of the parameter is a reference type, it should not be shown as T1.
                //          Do<string, int, string>  --> Do<T1, int, T2>   (Tuple<T?, int, T?> sounds less confusing)
                //
                pGenericMethodName->append(ResolvedSymbolsCache::NamespacePrefix);
                pGenericMethodName->append(ResolvedSymbolsCache::TypeNamePrefix);
                pGenericMethodName->append(ResolvedSymbolsCache::GenericParamList_SingleUnknownItemPlaceholderBase);
                pGenericMethodName->append(shared::ToWSTRING(currentGenericParameter + 1));
            }
            else
            {
                pGenericMethodName->append(*(pTypeParamInfo->GetTypeName()));
            }

            if (pTypeParamInfo != nullptr)
            {
                const_cast<ManagedTypeInfo*>(pTypeParamInfo)->Release();
                pTypeParamInfo = nullptr;
            }
        }

        pGenericMethodName->append(ResolvedSymbolsCache::GenericParamList_Close);
    } // if (functionGenericParametersCount > 0)

    // Don't forget to delete generic function parameters if any
    if (functionGenericParameters != nullptr)
    {
        delete[] functionGenericParameters;
        functionGenericParameters = nullptr;
    }

    // If we get all the symbols successfully, we will add them to the cache,
    // but if we fail, we will not, because failing may be due to synchronization and concurrency
    // and trying again later may succeed.
    // Drawback: if failing is permanent, we will keep trying and everyting will slow down.
    // Mitigation: we are caching the results on the managed sides again anyway.
    // To-do: need to bubble up success information to managed to that it can make better caching decisions.
    // Perhaps, retry failures next time this function id is encountered, but not indefinetly.
    if (success)
    {
        _pResolvedSymbolsCache->AddFrameInfoForFunctionId(functionId, *ppFrameInfo);
    }
}

/// <summary>
/// Clear and fill the specifie vector of string pointers with pointers to static constants that
/// represent human readable monikers for method flags (e.g. public, internal, virtual, etc..).
/// </summary>
void SymbolsResolver::GetMethodFlags(DWORD methodFlagsMask, std::vector<const shared::WSTRING*>& methodFlagMoikers)
{
    methodFlagMoikers.clear();

    DWORD memberAccessFlag = methodFlagsMask & CorMethodAttr::mdMemberAccessMask;
    switch (memberAccessFlag)
    {
        case CorMethodAttr::mdPublic:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Public);
            break;

        case CorMethodAttr::mdFamORAssem:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_FamORAssem);
            break;

        case CorMethodAttr::mdFamily:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Family);
            break;

        case CorMethodAttr::mdAssem:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Assem);
            break;

        case CorMethodAttr::mdFamANDAssem:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_FamANDAssem);
            break;

        case CorMethodAttr::mdPrivate:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Private);
            break;

        case CorMethodAttr::mdPrivateScope:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_PrivateScope);
            break;

        default:
            methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_UnknownAccess);
            break;
    }

    if (methodFlagsMask & CorMethodAttr::mdStatic)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Static);
    }
    if (methodFlagsMask & CorMethodAttr::mdFinal)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Final);
    }
    if (methodFlagsMask & CorMethodAttr::mdVirtual)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Virtual);
    }

    if (methodFlagsMask & CorMethodAttr::mdAbstract)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_Abstract);
    }

    if (methodFlagsMask & CorMethodAttr::mdSpecialName)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_SpecialName);
    }

    if (methodFlagsMask & CorMethodAttr::mdPinvokeImpl)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_PinvokeImpl);
    }
    if (methodFlagsMask & CorMethodAttr::mdUnmanagedExport)
    {
        methodFlagMoikers.push_back(&ResolvedSymbolsCache::MethodFlagMoniker_UnmanagedExport);
    }
}

// Use metadata API to get the name of a type and its un-instanciated generic parameter types
// ex: MyTuple<K, V>
void SymbolsResolver::AppendGenericDefinition(IMetaDataImport2* pMetaDataImport, mdTypeDef mdType, shared::WSTRING* pTypeName)
{
    // The name of the type parameters are used with no namespace
    // MyTuple<K, V> --> MyTuple{ |ct:K, |ct:V}
    pTypeName->append(ResolvedSymbolsCache::GenericParamList_Open);

    // need to iterate on the generic arguments definition with metadata API
    HCORENUM hEnum = NULL;

    // NOTE: unlike other COM iterators, it is not possible to get the real count in a first call
    //       and then allocate to get them all in a second call.
    //       128 type parameters sounds more than enough: no need to detect the case where 128
    //       were retrieved and add ... before >
    const ULONG MaxGenericParametersCount = 128;
    ULONG genericParamsCount = MaxGenericParametersCount;
    mdGenericParam genericParams[MaxGenericParametersCount];
    HRESULT hr = pMetaDataImport->EnumGenericParams(&hEnum, mdType, genericParams, MaxGenericParametersCount, &genericParamsCount);
    if (SUCCEEDED(hr))
    {
        WCHAR paramName[SymbolsResolver::TypeNameBuffMaxSize];
        ULONG paramNameLen = SymbolsResolver::TypeNameBuffMaxSize;
        for (size_t currentParam = 0; currentParam < genericParamsCount; currentParam++)
        {
            pTypeName->append(ResolvedSymbolsCache::TypeNamePrefix);

            ULONG index;
            DWORD flags;
            hr = pMetaDataImport->GetGenericParamProps(genericParams[currentParam], &index, &flags, nullptr, nullptr, paramName, paramNameLen, &paramNameLen);
            if (SUCCEEDED(hr))
            {
                pTypeName->append(paramName);
            }
            else
            {
                // this should never happen if the enum succeeded: no need to count the parameters
                pTypeName->append(WStr("T"));
            }

            if (currentParam < genericParamsCount - 1)
                pTypeName->append(ResolvedSymbolsCache::GenericParamList_Separator);
        }
        pTypeName->append(ResolvedSymbolsCache::GenericParamList_Close);

        pMetaDataImport->CloseEnum(hEnum);
    }
}

bool SymbolsResolver::GetManagedTypeName(ClassID classId,
                                         ModuleID containingModuleId,
                                         mdTypeDef mdTypeDefToken,
                                         ComPtr<IMetaDataImport2> copModuleMetadataIface,
                                         const ManagedTypeInfo** ppTypeInfo
                                         )
{
    // First, try the cache. We prefer looking up by classId over metadata tokens becasue it can have resolved generic type params.
    if (classId != 0 && _pResolvedSymbolsCache->TryGetTypeInfoForClassId(classId, const_cast<const ManagedTypeInfo**>(ppTypeInfo)))
    {
        // The cache AddRef-ed the pointer before giving it to us, no need to do this again before exposing.
        return true;
    }

    // Ok. Type data is not in cache. Let's do the work.
    bool success = false;
    ManagedTypeInfoMutable mutableTypeInfo;

    ClassID* typeArgsBuff = nullptr;   // array with ClassIDs of generic type params
    ULONG32 typeArgsCount = 0;         // number of type params for which we have details
    bool hasMissingTypeParams = false; // are there more genereic type params than we have details for?
    bool isClassIdValid = false;       // class id lookup did not fail

    // If classId is set, try resolving the class from it. This will include the generic instantiation if applicable.
    // If classId is not set (or if GetClassIDInfo2 will fail) we will fall back to containingModuleId/mdTypeDefToken later.

    if (classId != 0)
    {
        // We will get the type name from GetTypeDefProps(..).
        // For that we need the mdTypeDefToken and the containingModuleId/pModuleMetadataIface.
        // If we came from GetManagedMethodName(..) we already have those. But if we are being called recursively to get the name
        // for a type used as a generic type param, then we ONLY have the classId.
        // So, we call GetClassIDInfo2(..) to get the the mdTypeDefToken and the containingModuleId/pModuleMetadataIface.
        // Note: even if we came from GetManagedMethodName(..) and we alreadyhave the mdTypeDefToken etc, we stil want to
        // call GetClassIDInfo2(..), becasue the type may be generic and we want to obtain the generic type params.
        bool inputHasAllTokenData = (containingModuleId != 0) && (mdTypeDefToken != 0);

        // If containingModuleId and mdTypeDefToken are specified, then the metadata iface pointer (pModuleMetadataIface) is expected
        // to also be specified, valid, and matching those values.
        // Otherwise, we will retrieve it later.
        if (false == inputHasAllTokenData)
        {
            copModuleMetadataIface.Reset(); // also sets wrapped pointer to nullptr
        }

        // Unlike what the GetClassIDInfo2 documentation states, it must always be called
        // with 0 as number of arguments to get the real generic parameters count
        // https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo2-getclassidinfo2-method
        HRESULT hr = _pCorProfilerInfo->GetClassIDInfo2(
            classId,
            inputHasAllTokenData ? nullptr : &containingModuleId,
            inputHasAllTokenData ? nullptr : &mdTypeDefToken,
            nullptr, /* parentTypeClassId */
            0,
            &typeArgsCount,
            nullptr);

        if (FAILED(hr))
        {
            typeArgsBuff = nullptr;
            typeArgsCount = 0;
            hasMissingTypeParams = false;

            if (CORPROF_E_CLASSID_IS_ARRAY == hr)
            {
                mutableTypeInfo.UseTypeName(&ResolvedSymbolsCache::SpecialTypeMoniker_Array);
            }
            else if (CORPROF_E_CLASSID_IS_COMPOSITE == hr)
            {
                mutableTypeInfo.UseTypeName(&ResolvedSymbolsCache::SpecialTypeMoniker_NonArrayComposite);
            }
            else
            {
                mutableTypeInfo.UseTypeName(&ResolvedSymbolsCache::UnknownTypeNameMoniker);
            }
        }
        else
        {
            isClassIdValid = true;

            // retrieve generic parameter types if any
            if (typeArgsCount > 0)
            {
                typeArgsBuff = new ClassID[typeArgsCount];
                hr = _pCorProfilerInfo->GetClassIDInfo2(
                    classId,
                    nullptr, nullptr, nullptr,
                    typeArgsCount,
                    &typeArgsCount,
                    typeArgsBuff);

                if (FAILED(hr))
                {
                    Log::Debug("GetManagedTypeName: The 1st call to GetClassIDInfo2() succeeded, but the 2nd call failed with HR=",
                               HResultConverter::ToStringWithCode(hr), ".");

                    // If for some weird reason we first succeeded, and then failed, we skip the generic parameters.
                    delete[] typeArgsBuff;
                    typeArgsBuff = nullptr;
                    typeArgsCount = 0;
                    hasMissingTypeParams = true;
                }
                else
                {
                    hasMissingTypeParams = false;
                }
            }
        }
    } // if (classId != 0)

    // Get the type and the assembly name from the metadata and store the results in mutableTypeInfo:

    const ManagedTypeInfo* pCachedByTokeTypeInfo;
    if (_pResolvedSymbolsCache->TryGetTypeInfoForTypeDefToken(mdTypeDefToken, containingModuleId, &pCachedByTokeTypeInfo))
    {
        // The type info is in the by-TypeDefToken-cache.

        // We looked in the by-classId-cache at the beginning and did not find the type info here.
        // But it is in the by-TypeDefToken-cache. That cache cannot have resolved generic params.
        // So, if we have NO generic params detected earlier:
        //  We can just use the info from the cache. Do some clean-up and return.
        // Otherwise: we will copy the data we received from the cache and proceed resolving generic type params.
        if (typeArgsCount == 0)
        {
            // So we will be using the type info from the cache without modifications.

            // Type info was already in the by-TypeDefToken-cache, and if the classId
            // is valid, then we can also add it into the by-classId-cache:
            if (isClassIdValid)
            {
                _pResolvedSymbolsCache->AddTypeInfoForClassId(classId, pCachedByTokeTypeInfo);
            }

            // At the very start we may have allocated a buffer for a call to GetClassIDInfo2(..). If so, release it now:
            if (typeArgsBuff != nullptr)
            {
                delete[] typeArgsBuff;
                typeArgsBuff = nullptr;
            }

            // The cache AddRef-ed the pointer before giving it to us, No need to do this again before exposing.
            // We can just return it:
            *ppTypeInfo = pCachedByTokeTypeInfo;
            return true;
        }

        // So it must be that (typeArgsCount > 0).

        // We will use the the data from the cache to attach type params. For that we need to copy it:
        mutableTypeInfo.CopyFrom(*pCachedByTokeTypeInfo);

        // We will no longer use the data from the cache, so release:
        const_cast<ManagedTypeInfo*>(pCachedByTokeTypeInfo)->Release();
        pCachedByTokeTypeInfo = nullptr;
    }
    else // Type info is NOT in the by-metadata cache. We need to look it up via the token:
    {
        bool successTypeName;
        bool successAssemblyName;
        ReadTypeNameFromMetadata(containingModuleId, mdTypeDefToken, copModuleMetadataIface, mutableTypeInfo, true, &successTypeName, &successAssemblyName);

        // The overall lookup for the type succeeded, if both, the class name AND the assembly name components succeeded.
        // Note: this may be modified towards negative during a possible type-param lookup below.
        success = successTypeName && successAssemblyName;
    }

    // Ok. We have the type name and the assembly. Now we need to add the generic type params, if any.
    // Earlier, we called GetClassIDInfo2() and it may have given us ClassIDs of type params, if any.
    // If there are any, we will now dd then to the type name.
    // if classID is 0, it means that it is a generic type with at least 1 reference type
    // as instanciated type parameter (ex: MyClass<...,string,...>)
    if (typeArgsCount > 0)
    {
        // Earlier we may have created a new type name string or used a constant (i.e. global static).
        // We are about to append to the type name, so if we used a global static before, we need to copy
        // it into a separate string instance. The for-modifying getter should do that:
        shared::WSTRING* pGenericTypeName = mutableTypeInfo.GetTypeNameForModifying();
        pGenericTypeName->append(ResolvedSymbolsCache::GenericParamList_Open);

        // Recursively call GetManagedTypeName() for each generic type argument and append the type name as required.
        // The ManagedTypeInfo instances potentially created by these recursive calls will end up in cache like
        // all other resolved type names too.
        for (ULONG32 typeArgIndex = 0; typeArgIndex < typeArgsCount; typeArgIndex++)
        {
            ClassID typeParamClassId = typeArgsBuff[typeArgIndex];

            const ManagedTypeInfo* pTypeParamInfo;
            bool successTypeParam =
                GetManagedTypeName(typeParamClassId,
                                   static_cast<ModuleID>(0),
                                   static_cast<mdTypeDef>(0),
                                   ComPtr<IMetaDataImport2>(),
                                   &pTypeParamInfo);
            success = success && successTypeParam;

            if (typeArgIndex > 0)
            {
                pGenericTypeName->append(ResolvedSymbolsCache::GenericParamList_Separator);
            }

            if (pTypeParamInfo == nullptr || ResolvedSymbolsCache::SpecialTypeMoniker_SystemDotCanon == *(pTypeParamInfo->GetTypeName()))
            {
                // Type params that are non-value types may come back as "System.__Canon".
                // StackSnapshotCallbackHandler() in StackSamplerLoop.cpp should have a comment detailing the reasons.
                // Listing this as such is not very readable, so we will replace it with T1, T2, etc.:
                pGenericTypeName->append(ResolvedSymbolsCache::NamespacePrefix);
                pGenericTypeName->append(ResolvedSymbolsCache::TypeNamePrefix);
                pGenericTypeName->append(ResolvedSymbolsCache::GenericParamList_SingleUnknownItemPlaceholderBase);
                pGenericTypeName->append(shared::ToWSTRING((std::int64_t)typeArgIndex + 1));
            }
            else
            {
                pGenericTypeName->append(*(pTypeParamInfo->GetTypeName()));
            }

            if (pTypeParamInfo != nullptr)
            {
                const_cast<ManagedTypeInfo*>(pTypeParamInfo)->Release();
                pTypeParamInfo = nullptr;
            }
        }

        if (hasMissingTypeParams)
        {
            if (typeArgsCount > 1)
            {
                pGenericTypeName->append(ResolvedSymbolsCache::GenericParamList_Separator);
            }

            pGenericTypeName->append(ResolvedSymbolsCache::GenericParamList_MultipleUnknownItemsPlaceholder);
        }

        pGenericTypeName->append(ResolvedSymbolsCache::GenericParamList_Close);
    } // if (typeArgsCount > 0)
    else
    {
        // In case of generic type with at least 1 reference type parameter, classId and typeArgsCount would be 0.
        // It is needed to rebuild the generic parameters list from the metadata
        if (classId == 0)
        {
            shared::WSTRING* pGenericTypeName = mutableTypeInfo.GetTypeNameForModifying();
            AppendGenericDefinition(copModuleMetadataIface.Get(), mdTypeDefToken, pGenericTypeName);
        }
    }

    // At the very start we may have allocated a buffer for a call to GetClassIDInfo2(..). If so, release it now:
    if (typeArgsBuff != nullptr)
    {
        delete[] typeArgsBuff;
        typeArgsBuff = nullptr;
    }

    // We are done. Generate immutable result and cache if required.
    *ppTypeInfo = mutableTypeInfo.ConvertToNewImmutable();

    // The caller must release the type info we just generated when done (same as the COM pattern).
    (*const_cast<ManagedTypeInfo**>(ppTypeInfo))->AddRef();

    // Note that classId is 0 for generic types with at least 1 reference type as generic parameter
    if (isClassIdValid && (classId != 0) && success)
    {
        // Cache by classId if classId was found to be valid earlier:
        _pResolvedSymbolsCache->AddTypeInfoForClassId(classId, *ppTypeInfo);
    }

    // The by-token cache is purely metadata based and cannot account for generic params.
    // So, if we enrich this type name with reneric param info, we cannot store it into the by-token cache.
    // Note that classId is 0 for generic types with at least 1 reference type as generic parameter
    if ((classId != 0) && (typeArgsCount == 0) && success)
    {
        _pResolvedSymbolsCache->AddTypeInfoForForTypeDefToken(mdTypeDefToken, containingModuleId, *ppTypeInfo);
    }

    return success;
}

// Remove `xx at the end of the given string and
// return the resulting string length (including final \0)
// ex: List`1 --> List and returns 5
// Note that charCount counts the final \0
ULONG FixTrailingGenericCount(WCHAR* name, ULONG charCount)
{
    ULONG currentCharPos = 0;
    while ((currentCharPos < charCount) && (name[currentCharPos] != WStr('\0')))
    {
        if (name[currentCharPos] == WStr('`'))
        {
            // skip `xx
            name[currentCharPos] = WStr('\0');
            return currentCharPos + 1;
        }
        currentCharPos++;
    }

    return currentCharPos + 1;
}

/// <summary>
/// Helper method used by GetManagedTypeName().
/// containingModuleId and mdTypeDefToken are assumed to be valid.
/// pModuleMetadataIface must either be valid and matching the token and the module, or it must be null.
///
/// Results will be stored in typeInfo.
/// </summary>
void SymbolsResolver::ReadTypeNameFromMetadata(ModuleID containingModuleId,
                                               mdTypeDef mdTypeDefToken,
                                               ComPtr<IMetaDataImport2> copModuleMetadataIface,
                                               ManagedTypeInfoMutable& typeInfo,
                                               bool readAssemblyName,
                                               bool* successTypeName,
                                               bool* successAssemblyName)
{
    if (successTypeName != nullptr)
    {
        *successTypeName = false;
    }

    if (successAssemblyName != nullptr)
    {
        *successAssemblyName = false;
    }

    // In GetManagedTypeName(..), if we have the token info passed in, we should also have the metadata iface.
    // However, if we just fetched the token info, we need to also get the metadata iface:
    if (copModuleMetadataIface.Get() == nullptr)
    {
        HRESULT hr = _pCorProfilerInfo->GetModuleMetaData(
            containingModuleId,
            CorOpenFlags::ofRead,
            IID_IMetaDataImport,
            reinterpret_cast<IUnknown**>(copModuleMetadataIface.GetAddressOf()));

        if (FAILED(hr) || copModuleMetadataIface.Get() == nullptr)
        {
            Log::Debug("GetTypeNameFromMetadata: Call to GetModuleMetaData() failed with HR=",
                       HResultConverter::ToStringWithCode(hr), ".");
            copModuleMetadataIface.Reset(); // also sets wrapped pointer to nullptr
        }
    }

    // Get the type name from the metadata:
    if (copModuleMetadataIface.Get() == nullptr)
    {
        // If we cannot have a valid metadata iface, there is not much we can do.
        if (nullptr == typeInfo.GetTypeName())
        {
            typeInfo.UseTypeName(&ResolvedSymbolsCache::UnknownTypeNameMoniker);
        }
    }
    else
    {
        // This type may be an enclosed type.
        // In that case GetTypeDefProps(..) will not get us anything about the encosing type or the namespace.
        // We will recursively call ReadTypeNameFromMetadata(..) and then attach the name if this enclosed type.
        mdTypeDef mdEnclosingType = 0;
        HRESULT hr = copModuleMetadataIface->GetNestedClassProps(mdTypeDefToken, &mdEnclosingType);
        bool isEnclosedType = SUCCEEDED(hr) && copModuleMetadataIface->IsValidToken(mdEnclosingType);

        if (isEnclosedType)
        {
            ReadTypeNameFromMetadata(containingModuleId, mdEnclosingType, copModuleMetadataIface, typeInfo, false, successTypeName, nullptr);

            // If the enclosing type reading failed, we will tread this type as non-enclosed:
            isEnclosedType = isEnclosedType && successTypeName;
        }

        WCHAR typeNameBuff[SymbolsResolver::TypeNameBuffMaxSize];
        ULONG typeNameLen;

        hr = copModuleMetadataIface->GetTypeDefProps(
            mdTypeDefToken,
            typeNameBuff,
            SymbolsResolver::TypeNameBuffMaxSize,
            &typeNameLen,
            nullptr,
            nullptr);

        if (FAILED(hr))
        {
            Log::Debug("GetTypeNameFromMetadata: Call to GetTypeDefProps() failed with HR=",
                       HResultConverter::ToStringWithCode(hr), ".");

            if (nullptr == typeInfo.GetTypeName())
            {
                typeInfo.UseTypeName(&ResolvedSymbolsCache::UnknownTypeNameMoniker);
            }
        }
        else
        {
            // GetTypeDefProps() succeeded and typeNameBuff contains both the namespace and
            // the type name (without enclosing type name if any)
            //      namespace.typename`xx
            // In case of generic type, the name ends with `xx where xx is the number of generic parameters
            // that needs to be removed
            typeNameLen = FixTrailingGenericCount(typeNameBuff, typeNameLen);

            // If this is a non-enclosed type, we need to split namespace from type name based on the
            // last dot (.) we can find. Then we set the new name for the type.
            // If this is an enclosed types, we assume that the namespace was not encuded into the buffer
            // by GetTypeDefProps(..). We attach the type name to the info already available.
            typeNameLen = (std::min)(typeNameLen, SymbolsResolver::TypeNameBuffMaxSize);

            if (isEnclosedType)
            {
                typeInfo.AttachEnclosedTypeName(typeNameBuff, typeNameLen);
            }
            else
            {
                const WCHAR separator = WStr('.');
                WCHAR* typeNameBuffEnd = (typeNameLen == 0) ? typeNameBuff : typeNameBuff + typeNameLen - 1;

                WCHAR* pActualTypeNameStart = typeNameBuffEnd;
                while ((pActualTypeNameStart > typeNameBuff) && (*pActualTypeNameStart != separator))
                {
                    pActualTypeNameStart--;
                }

                // If we found the dot that separates namespace from type name, then we put a 0 into the buffer
                // on that position to separate it in 2 strings, and then construct the qualified type name.
                // Otherwise we assume there in no namespace.

                if (*pActualTypeNameStart == separator && pActualTypeNameStart > typeNameBuff && pActualTypeNameStart < typeNameBuffEnd)
                {
                    *pActualTypeNameStart = WStr('\0');
                    std::int32_t namespaceLen = static_cast<std::int32_t>(pActualTypeNameStart - typeNameBuff);

                    pActualTypeNameStart++;
                    std::int32_t actualTypeLen = static_cast<std::int32_t>(typeNameBuffEnd - pActualTypeNameStart);
                    typeInfo.CreateNewTypeName(typeNameBuff, namespaceLen, pActualTypeNameStart, actualTypeLen);
                }
                else
                {
                    typeInfo.CreateNewTypeName(nullptr, 0, typeNameBuff, typeNameLen);
                }
            }

            if (successTypeName != nullptr)
            {
                *successTypeName = true;
            }
        }
    }

    // Now we will get the assembly name, if that was requested.
    // First we fetch the assemblyId, then we use it to get the name:
    if (readAssemblyName)
    {
        AssemblyID assemblyId;
        HRESULT hr = _pCorProfilerInfo->GetModuleInfo(
            containingModuleId,
            nullptr, /* ppBaseLoadAddress */
            0,       /* cchName */
            nullptr, /* pcchName */
            nullptr, /* szName */
            &assemblyId);
        if (FAILED(hr))
        {
            Log::Debug("GetTypeNameFromMetadata: Call to GetModuleInfo() failed with HR=",
                       HResultConverter::ToStringWithCode(hr), ".");
            typeInfo.UseAssemblyName(&ResolvedSymbolsCache::UnknownAssemblyNameMoniker);
        }
        else
        {
            WCHAR assemblyNameBuff[SymbolsResolver::AssemblyNameBuffMaxSize];
            ULONG assemblyNameLen;
            HRESULT hr = _pCorProfilerInfo->GetAssemblyInfo(
                assemblyId,
                SymbolsResolver::AssemblyNameBuffMaxSize,
                &assemblyNameLen,
                assemblyNameBuff,
                nullptr,  /* pAppDomainId */
                nullptr); /* pModuleId */

            if (FAILED(hr))
            {
                Log::Debug("GetTypeNameFromMetadata: Call to GetAssemblyInfo() failed with HR=",
                           HResultConverter::ToStringWithCode(hr), ".");
                typeInfo.UseAssemblyName(&ResolvedSymbolsCache::UnknownAssemblyNameMoniker);
            }
            else
            {
                assemblyNameLen = (std::min)(assemblyNameLen, SymbolsResolver::AssemblyNameBuffMaxSize);
                typeInfo.CreateNewAssemblyName(assemblyNameBuff, assemblyNameLen);

                if (successAssemblyName != nullptr)
                {
                    *successAssemblyName = true;
                }
            }
        }
    }
}

const char* SymbolsResolver::Worker::ManagedThreadName = "DD.Profiler.ResolveSymbolsWorker";
const WCHAR* SymbolsResolver::Worker::NativeThreadName = WStr("DD.Profiler.ResolveSymbolsWorker");

SymbolsResolver::Worker::Worker(ICorProfilerInfo4* pCorProfilerInfo, SymbolsResolver* pOwner) :
    SynchronousOffThreadWorkerBase(),
    _pCorProfilerInfo{nullptr},
    _pOwner{pOwner}
{
    _pCorProfilerInfo = pCorProfilerInfo;

    if (pCorProfilerInfo != nullptr)
    {
        _pCorProfilerInfo->AddRef();
    }
}

SymbolsResolver::Worker::~Worker()
{
    ICorProfilerInfo4* pCorProfilerInfo = _pCorProfilerInfo;
    if (pCorProfilerInfo != nullptr)
    {
        _pCorProfilerInfo = nullptr;
        pCorProfilerInfo->Release();
    }

    _pOwner = nullptr;
}

bool SymbolsResolver::Worker::ShouldInitializeCurrentThreadforManagedInteractions(ICorProfilerInfo4** ppCorProfilerInfo)
{
    if (_pCorProfilerInfo != nullptr)
    {
        if (ppCorProfilerInfo != nullptr)
        {
            *ppCorProfilerInfo = _pCorProfilerInfo;
        }

        return true;
    }

    return false;
}

bool SymbolsResolver::Worker::ShouldSetManagedThreadName(const char** managedThreadName)
{
    // Seems that once we call into managed from this thread, metadata APIs start returning CORPROF_E_UNSUPPORTED_CALL_SEQUENCE.
    // This needs to be investigated (@ToDo), in the meantime we avoid setting the managed thread name.
    // Once investigated, we should either re-instate the code commented below, or get rid of the commented code.
    //
    //if (managedThreadName != nullptr)
    //{
    //    *managedThreadName = SymbolsResolver::Worker::ManagedThreadName;
    //}
    //
    //return true;

    return false;
}

bool SymbolsResolver::Worker::ShouldSetNativeThreadName(const WCHAR** nativeThreadName)
{
    if (nativeThreadName != nullptr)
    {
        *nativeThreadName = SymbolsResolver::Worker::NativeThreadName;
    }

    return true;
}

void SymbolsResolver::Worker::PerformWork(void* pWorkParameters, void* pWorkResults)
{
    Parameters* pParams = static_cast<Parameters*>(pWorkParameters);
    Results* pResults = static_cast<Results*>(pWorkResults);

    switch (pParams->WorkKind)
    {
        case WorkKinds::GetManagedMethodName:
        {
            _pOwner->GetManagedMethodName(pParams->FunctionId, pParams->PtrPtrStackFrameInfo);
            pResults->WorkKind = Worker::WorkKinds::GetManagedMethodName;
            break;
        }

        case WorkKinds::ResolveAppDomainInfoSymbols:
        {
            bool success = _pOwner->ResolveAppDomainInfoSymbols(pParams->AppDomainId,
                                                                pParams->AppDomainNameBuffSize,
                                                                pParams->PtrActualAppDomainNameLen,
                                                                pParams->PtrAppDomainNameBuff,
                                                                pParams->PtrAppDomainProcessId);

            pResults->WorkKind = Worker::WorkKinds::ResolveAppDomainInfoSymbols;
            pResults->Success = success;
            break;
        }

        default:
        {
            pResults->WorkKind = pParams->WorkKind;
            break;
        }
    }
}