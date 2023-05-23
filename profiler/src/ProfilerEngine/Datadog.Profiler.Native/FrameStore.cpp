// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FrameStore.h"

#include "COMHelpers.h"
#include "DebugInfoStore.h"
#include "IConfiguration.h"
#include "Log.h"
#include "OpSysTools.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

FrameStore::FrameStore(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration* pConfiguration, IDebugInfoStore* debugInfoStore) :
    _pCorProfilerInfo{pCorProfilerInfo},
    _resolveNativeFrames{pConfiguration->IsNativeFramesEnabled()},
    _pDebugInfoStore{debugInfoStore}
{
}

std::pair<bool, FrameInfoView> FrameStore::GetFrame(uintptr_t instructionPointer)
{
    static const std::string NotResolvedModuleName("NotResolvedModule");
    static const std::string NotResolvedFrame("NotResolvedFrame");

    FunctionID functionId;
    HRESULT hr = _pCorProfilerInfo->GetFunctionFromIP((LPCBYTE)instructionPointer, &functionId);

    if (SUCCEEDED(hr))
    {
        auto frameInfo = GetManagedFrame(functionId);
        return {true, frameInfo};
    }
    else
    {
        if (!_resolveNativeFrames)
        {
            return {false, {NotResolvedModuleName, NotResolvedFrame, "", 0}};
        }

        auto [moduleName, frame] = GetNativeFrame(instructionPointer);
        return {true, {moduleName, frame, "", 0}};
    }
}

// It should be possible to use dbghlp.dll on Windows (and something else on Linux?)
// to get function name + offset
// see https://docs.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-symfromaddr for more details
// However, today, no symbol resolution is done; only the module implementing the function is provided
std::pair<std::string_view, std::string_view> FrameStore::GetNativeFrame(uintptr_t instructionPointer)
{
    static const std::string UnknownNativeFrame("|lm:Unknown-Native-Module |ns:NativeCode |ct:Unknown-Native-Module |fn:Function");
    static const std::string UnknowNativeModule = "Unknown-Native-Module";

    auto moduleName = OpSysTools::GetModuleName(reinterpret_cast<void*>(instructionPointer));
    if (moduleName.empty())
    {
        return {UnknowNativeModule, UnknownNativeFrame};
    }

    {
        std::lock_guard<std::mutex> lock(_nativeLock);

        auto it = _framePerNativeModule.find(moduleName);
        if (it != _framePerNativeModule.cend())
        {
            return {it->first, it->second};
        }
    }

    // moduleName contains the full path: keep only the filename
    auto moduleFilename = fs::path(moduleName).filename().string();
    std::stringstream builder;
    builder << "|lm:" << moduleFilename << " |ns:NativeCode |ct:" << moduleFilename << " |fn:Function";

    {
        std::lock_guard<std::mutex> lock(_nativeLock);
        // emplace returns a pair<iterator, bool>. It returns false if the element was already there
        // we use the iterator (first element of the pair) to get a reference to the key and the value
        auto [it, _] = _framePerNativeModule.emplace(std::move(moduleName), builder.str());
        return {it->first, it->second};
    }
}

FrameInfoView FrameStore::GetManagedFrame(FunctionID functionId)
{
    {
        std::lock_guard<std::mutex> lock(_methodsLock);

        // Look into the cache first
        auto element = _methods.find(functionId);
        if (element != _methods.end())
        {
            return element->second;
        }
    }

    // Get the method generic parameters if any + metadata token + class ID + module ID
    // Next, get the method name et type token from metadata API
    // Finally, get the type/namespace names
    mdToken mdTokenFunc;
    ClassID classId;
    ModuleID moduleId;
    std::unique_ptr<ClassID[]> genericParameters;
    ULONG32 genericParametersCount;
    if (!GetFunctionInfo(functionId, mdTokenFunc, classId, moduleId, genericParametersCount, genericParameters))
    {
        return {UnknownManagedAssembly, UnknownManagedFrame, {}, 0};
    }

    // Use metadata API to get method name
    ComPtr<IMetaDataImport2> pMetadataImport;
    if (!GetMetadataApi(moduleId, functionId, pMetadataImport))
    {
        return {UnknownManagedAssembly, UnknownManagedFrame, {}, 0};
    }

    // method name is resolved first because we also get the mdDefToken of its class
    auto [methodName, mdTokenType] = GetMethodName(functionId, pMetadataImport.Get(), mdTokenFunc, genericParametersCount, genericParameters.get());
    if (methodName.empty())
    {
        return {UnknownManagedAssembly, UnknownManagedFrame, {}, 0};
    }

    // get type related description (assembly, namespace and type name)
    // look into the cache first
    TypeDesc* pTypeDesc = nullptr;  // if already in the cache
    TypeDesc typeDesc;              // if needed to be built from a given classId
    bool isEncoded = true;
    bool typeInCache = GetCachedTypeDesc(classId, pTypeDesc, isEncoded);
    // TODO: would it be interesting to have a (moduleId + mdTokenDef) -> TypeDesc cache for the non cached generic types?

    if (!typeInCache)
    {
        // try to get the type description
        if (!BuildTypeDesc(pMetadataImport.Get(), classId, moduleId, mdTokenType, typeDesc, false, nullptr, isEncoded))
        {
            // This should never happen but in case it happens, we cache the module/frame value.
            // It's safe to cache, because there is no reason that the next calls to
            // BuildTypeDesc will succeed.
            auto& value = _methods[functionId];
            value = {UnknownManagedAssembly, UnknownManagedType + " |fn:" + std::move(methodName), "", 0};
            return value;
        }

        if (classId != 0)
        {
            std::lock_guard<std::mutex> lock(_encodedTypesLock);
            pTypeDesc = &typeDesc;
            _encodedTypes[classId] = typeDesc;
        }
        else
        {
            pTypeDesc = &typeDesc;
        }
        // TODO: would it be interesting to have a (moduleId + mdTokenDef) -> TypeDesc cache for the non cached generic types?
    }

    // build the frame from assembly, namespace, type and method names
    std::stringstream builder;
    if (!pTypeDesc->Assembly.empty())
    {
        builder << "|lm:" << pTypeDesc->Assembly;
    }
    builder << " |ns:" << pTypeDesc->Namespace;
    builder << " |ct:" << pTypeDesc->Type;
    builder << " |fn:" << methodName;

    auto debugInfo = _pDebugInfoStore->Get(moduleId, mdTokenFunc);

    std::string managedFrame = builder.str();

    {
        std::lock_guard<std::mutex> lock(_methodsLock);

        // store it into the function cache and return an iterator to the stored elements
        auto [it, _] = _methods.emplace(functionId, FrameInfo{pTypeDesc->Assembly, managedFrame, debugInfo.File, debugInfo.StartLine});
        // first is the key, second is the associated value
        return it->second;
    }
}

bool FrameStore::GetTypeName(ClassID classId, std::string& name)
{
    // no backend encoding --> C# like
    bool isEncoded = false;

    TypeDesc* pTypeDesc = nullptr;
    if (!GetTypeDesc(classId, pTypeDesc, isEncoded))
    {
        return false;
    }

    if (pTypeDesc->Namespace.empty())
    {
        name = pTypeDesc->Type;
    }
    else
    {
        name = pTypeDesc->Namespace + "." + pTypeDesc->Type;
    }

    return true;
}

// This method is supposed to return a string_view over a string in the types cache
// It is used by the allocations recorder to avoid duplicating type name strings
// For example if 4 instances of MyType are allocated, the string_view for these 4 allocations
// will point to the same "MyType" string.
// This is why it is needed to get a pointer to the TypeDesc held by the cache
bool FrameStore::GetTypeName(ClassID classId, std::string_view& name)
{
    // no backend encoding --> C# like
    bool isEncoded = false;

    TypeDesc* pTypeDesc = nullptr;
    if (!GetTypeDesc(classId, pTypeDesc, isEncoded))
    {
        return false;
    }

    if (!GetCachedTypeDesc(classId, pTypeDesc, isEncoded))
    {
        return false;
    }

    // ensure that the string_view is pointing to the string in the cache
    name = pTypeDesc->Type;

    return true;
}

bool FrameStore::GetCachedTypeDesc(ClassID classId, TypeDesc*& typeDesc, bool isEncoded)
{
    if (classId != 0)
    {
        if (isEncoded)
        {
            std::lock_guard<std::mutex> lock(_encodedTypesLock);

            auto typeEntry = _encodedTypes.find(classId);
            if (typeEntry != _encodedTypes.end())
            {
                typeDesc = &(_encodedTypes.at(classId));
                return true;
            }
        }
        else
        {
            std::lock_guard<std::mutex> lock(_typesLock);

            auto typeEntry = _types.find(classId);
            if (typeEntry != _types.end())
            {
                typeDesc = &(_types.at(classId));
                return true;
            }
        }
    }

    return false;
}

void AppendArrayRank(std::string& arrayBuilder, ULONG rank)
{
    if (rank == 1)
    {
        arrayBuilder = "[]" + arrayBuilder;
    }
    else
    {
        std::stringstream builder;
        builder << "[";
        for (size_t i = 0; i < rank - 1; i++)
        {
            builder << ",";
        }
        builder << "]";

        arrayBuilder = builder.str() + arrayBuilder;
    }
}

bool FrameStore::GetTypeDesc(ClassID classId, TypeDesc*& pTypeDesc, bool isEncoded)
{
    // get type related description (assembly, namespace and type name)
    // look into the cache first
    bool typeInCache = GetCachedTypeDesc(classId, pTypeDesc, isEncoded);
    // TODO: would it be interesting to have a (moduleId + mdTokenDef) -> TypeDesc cache for the non cached generic types?

    if (!typeInCache)
    {
        ClassID originalClassId = classId;

        // deal with class[]/[,...,]
        // read https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo-isarrayclass-method for more details
        bool isArray = false;
        std::string arrayBuilder;

        CorElementType baseElementType;
        ClassID itemClassId;
        ULONG rank = 0;
        if (_pCorProfilerInfo->IsArrayClass(classId, &baseElementType, &itemClassId, &rank) == S_OK)
        {
            classId = itemClassId;
            isArray = true;
            AppendArrayRank(arrayBuilder, rank);

            // in case of matrices, it is needed to look for the last "good" item class ID
            // because all others might be array of array of ...
            for (size_t i = 0; i < rank; i++)
            {
                HRESULT hr = _pCorProfilerInfo->IsArrayClass(classId, &baseElementType, &itemClassId, &rank);
                if ((hr == S_FALSE) || FAILED(hr))
                {
                    itemClassId = classId;

                    break;
                }

                AppendArrayRank(arrayBuilder, rank);
                classId = itemClassId;
            }
        }

        ModuleID moduleId;
        mdTypeDef typeDefToken;
        INVOKE(_pCorProfilerInfo->GetClassIDInfo(classId, &moduleId, &typeDefToken));

        // for some types, it is not possible to find the moduleId ???  --> could be arrays...
        if (moduleId == 0)
        {
            INVOKE(_pCorProfilerInfo->GetClassIDInfo2(classId, &moduleId, &typeDefToken, nullptr, 0, nullptr, nullptr));
        }

        ComPtr<IMetaDataImport2> metadataImport;
        INVOKE(_pCorProfilerInfo->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(metadataImport.GetAddressOf())));

        // try to get the type description
        TypeDesc typeDesc;
        if (!BuildTypeDesc(metadataImport.Get(), classId, moduleId, typeDefToken, typeDesc, isArray, arrayBuilder.c_str(), isEncoded))
        {
            return false;
        }

        if (originalClassId != 0)
        {
            if (isEncoded)
            {
                std::lock_guard<std::mutex> lock(_encodedTypesLock);

                _encodedTypes[originalClassId] = typeDesc;
                pTypeDesc = &(_encodedTypes.at(originalClassId));
            }
            else
            {
                std::lock_guard<std::mutex> lock(_typesLock);

                _types[originalClassId] = typeDesc;
                pTypeDesc = &(_types.at(originalClassId));
            }
        }
        else
        {
            // TODO: check the number of times this happens because a TypeDefs has been constructed but
            // it is not possible to return a pointer to it (the object is on the stack)!!!
            return false;
        }
    }

    return true;
}

// More explanations in https://chnasarre.medium.com/dealing-with-modules-assemblies-and-types-with-clr-profiling-apis-a7522a5abaa9?source=friends_link&sk=3e010ab991456db0394d4cca29cb8cb2
bool FrameStore::BuildTypeDesc(
    IMetaDataImport2* pMetadataImport,
    ClassID classId,
    ModuleID moduleId,
    mdTypeDef mdTokenType,
    TypeDesc& typeDesc,
    bool isArray,
    const char* arraySuffix,
    bool isEncoded)
{
    // 1. Get the assembly from the module
    if (!GetAssemblyName(_pCorProfilerInfo, moduleId, typeDesc.Assembly))
    {
        return false;
    }

    // 2. Look for the type name including namespace (need to take into account nested types and generic types)
    auto [ns, ct] = GetManagedTypeName(_pCorProfilerInfo, pMetadataImport, moduleId, classId, mdTokenType, isArray, arraySuffix, isEncoded);
    typeDesc.Namespace = ns;
    typeDesc.Type = ct;

    return true;
}

bool FrameStore::GetFunctionInfo(
    FunctionID functionId,
    mdToken& mdTokenFunc,
    ClassID& classId,
    ModuleID& moduleId,
    ULONG32& genericParametersCount,
    std::unique_ptr<ClassID[]>& genericParameters)
{
    // Call GetFunctionInfo2 to get the method's class and module, its metadata token, and
    // its generic type parameters if any
    //
    // Note that the class ID may be 0 in the case of a generic type with at least 1 reference type as parameter.
    // The solution is to use metadata API to rebuild the un-instanciated definition of the generic type:
    //    class MyClass<K, V>  --> MyClass<K, V>
    // Note: it is not possible to get more details about K and V types so the ct: recursive syntax cannot be used
    //       and the name will be "ct:MyClass<K, V>"
    //
    // Even when class ID is 0, the generic parameters of the method (not the type) are still available
    // and typeArgsCount should not be 0.
    //
    // Unlike what the GetClassIDInfo2 documentation states, GetFunctionInfo2 must always be called
    // with 0 as number of arguments to get the real generic parameters count
    // https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo2-getfunctioninfo2-method
    //

    // GetFunctionInfo2 is called with 0 to get the actual number of the method generic parameters
    HRESULT hr = _pCorProfilerInfo->GetFunctionInfo2(
        functionId,
        (COR_PRF_FRAME_INFO)(nullptr), /* clrFrameInfo */
        &classId,
        &moduleId,
        &mdTokenFunc,
        0,
        &genericParametersCount,
        nullptr);

    if (FAILED(hr))
    {
        classId = 0;
        moduleId = 0;
        mdTokenFunc = 0;
        genericParameters = nullptr;
        genericParametersCount = 0;
        return false;
    }

    if (genericParametersCount > 0)
    {
        // in case of generic function, it's time to allocate the array
        // that will receive the ClassID for each generic parameter
        genericParameters = std::make_unique<ClassID[]>(genericParametersCount); // move
        hr = _pCorProfilerInfo->GetFunctionInfo2(
            functionId,
            (COR_PRF_FRAME_INFO)(nullptr), /* clrFrameInfo */
            nullptr,                       //
            nullptr,                       // these parameters have already been retrieved in the first call
            nullptr,                       //
            genericParametersCount,
            &genericParametersCount,
            genericParameters.get());

        if (FAILED(hr))
        {
            // This is not supposed to happen but just in case, since generic parameters are not available,
            // let's say that this is not a generic function
            genericParameters = nullptr;
            genericParametersCount = 0;

            return false;
        }
    }

    return true;
}

bool FrameStore::GetMetadataApi(ModuleID moduleId, FunctionID functionId, ComPtr<IMetaDataImport2>& pMetadataImport)
{
    HRESULT hr = _pCorProfilerInfo->GetModuleMetaData(moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport2, (IUnknown**)&pMetadataImport);
    if (FAILED(hr))
    {
        Log::Debug("GetModuleMetaData() failed with HRESULT = ", HResultConverter::ToStringWithCode(hr));
        mdToken mdTokenFunc; // not used
        hr = _pCorProfilerInfo->GetTokenAndMetaDataFromFunction(
            functionId, IID_IMetaDataImport2, (IUnknown**)&pMetadataImport, &mdTokenFunc);
        if (FAILED(hr))
        {
            Log::Debug("GetTokenAndMetaDataFromFunction() failed with HRESULT = ", HResultConverter::ToStringWithCode(hr));
            return false;
        }
    }

    return true;
}

std::pair<std::string, mdTypeDef> FrameStore::GetMethodName(
    FunctionID functionId,
    IMetaDataImport2* pMetadataImport,
    mdMethodDef mdTokenFunc,
    ULONG32 genericParametersCount,
    ClassID* genericParameters)
{
    auto [methodName, mdTokenType] = GetMethodNameFromMetadata(pMetadataImport, mdTokenFunc);
    if ((methodName.empty()) || (genericParametersCount == 0))
    {
        // get the method signature
        std::string signature = GetMethodSignature(_pCorProfilerInfo, pMetadataImport, functionId, mdTokenFunc);

        return std::make_pair(std::move(methodName + signature), mdTokenType);
    }

    // Append generic parameters if any
    //
    // bool LongGenericParameterList<MT1, MT2, MT3, MT4, MT5, MT6, MT7, MT8>(K key, out V val)
    // called as: LongGenericParameterList<byte, bool, bool, bool, string, bool, bool, bool>(i, out _)
    // -->
    // |fn:LongGenericParameterList{ |ns:System |ct:Byte,  |ns:System |ct:Boolean,  |ns:System |ct:Boolean,  |ns:System |ct:Boolean,  |ns: |ct:T5,  |ns:System |ct:Boolean,  |ns:System |ct:Boolean,  |ns:System |ct:Boolean}
    // since string is a reference type, the __canon implementation is used and we can't know it is a string
    // --> this is why T5 (from the metadata) is used
    std::stringstream builder;
    builder << "{";
    for (ULONG32 i = 0; i < genericParametersCount; i++)
    {
        auto [ns, typeName] = GetManagedTypeName(_pCorProfilerInfo, genericParameters[i]);

        // deal with System.__Canon case
        if (typeName == "__Canon")
        {
            builder << "|ns: |ct:T" << i;
        }
        else // normal namespace.type case
        {
            builder << "|ns:";
            if (!ns.empty())
            {
                builder << ns;
            }

            builder << " |ct:" << typeName;
        }

        if (i < genericParametersCount - 1)
        {
            builder << ", ";
        }
    }
    builder << "}";

    // get the method signature
    std::string signature = GetMethodSignature(_pCorProfilerInfo, pMetadataImport, functionId, mdTokenFunc);

    return std::make_pair(methodName + builder.str() + signature, mdTokenType);
}

bool FrameStore::GetAssemblyName(ICorProfilerInfo4* pInfo, ModuleID moduleId, std::string& assemblyName)
{
    assemblyName = std::string("");

    AssemblyID assemblyId;
    INVOKE(pInfo->GetModuleInfo(moduleId, nullptr, 0, nullptr, nullptr, &assemblyId));

    // 2 steps way to get the assembly name (get the buffer size first and then fill it up with the name)
    ULONG nameCharCount = 0;
    INVOKE(pInfo->GetAssemblyInfo(assemblyId, nameCharCount, &nameCharCount, nullptr, nullptr, nullptr));

    auto buffer = std::make_unique<WCHAR[]>(nameCharCount);
    INVOKE(pInfo->GetAssemblyInfo(assemblyId, nameCharCount, &nameCharCount, buffer.get(), nullptr, nullptr));

    // convert from UTF16 to UTF8
    assemblyName = shared::ToString(shared::WSTRING(buffer.get()));
    return true;
}

// Remove `xx at the end of the given string
// ex: List`1 --> List
void FrameStore::FixTrailingGeneric(WCHAR* name)
{
    ULONG currentCharPos = 0;
    while (name[currentCharPos] != WStr('\0'))
    {
        if (name[currentCharPos] == WStr('`'))
        {
            // skip `xx
            name[currentCharPos] = WStr('\0');
            return; // this is a generic type
        }
        currentCharPos++;
    }

    // this is not a generic type
}

std::string FrameStore::GetTypeNameFromMetadata(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType)
{
    ULONG nameCharCount = 0;
    HRESULT hr = pMetadata->GetTypeDefProps(mdTokenType, nullptr, 0, &nameCharCount, nullptr, nullptr);
    if (FAILED(hr))
    {
        return std::string("");
    }

    auto buffer = std::make_unique<WCHAR[]>(nameCharCount);
    hr = pMetadata->GetTypeDefProps(mdTokenType, buffer.get(), nameCharCount, &nameCharCount, nullptr, nullptr);
    if (FAILED(hr))
    {
        return std::string("");
    }

    auto pBuffer = buffer.get();
    FixTrailingGeneric(pBuffer);

    // convert from UTF16 to UTF8
    return shared::ToString(shared::WSTRING(pBuffer));
}

std::pair<std::string, std::string> FrameStore::GetTypeWithNamespace(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType, bool isArray, const char* arraySuffix)
{
    mdTypeDef mdEnclosingType = 0;
    HRESULT hr = pMetadata->GetNestedClassProps(mdTokenType, &mdEnclosingType);
    bool isNested = SUCCEEDED(hr) && pMetadata->IsValidToken(mdEnclosingType);

    std::string enclosingType;
    std::string ns;
    if (isNested)
    {
        std::tie(ns, enclosingType) = GetTypeWithNamespace(pMetadata, mdEnclosingType, false, nullptr);
    }

    // Get type name
    // Note: in case of nested type (i.e. type defined in another type), the namespace is not present in the name
    auto typeName = GetTypeNameFromMetadata(pMetadata, mdTokenType);
    if (typeName.empty())
    {
        // TODO: check if this is what we really want
        typeName = "?";
    }

    if (isNested)
    {
        return std::make_pair(std::move(ns), enclosingType + "." + typeName);
    }
    else
    {
        // the namespace is only given for a non nested type
        // --> look for the last '.': what is after is the type name and what is before is the namespace
        std::string separated;

        auto const pos = typeName.find_last_of('.');
        if (pos == std::string::npos)
        {
            // no namespace
            return std::make_pair("", std::move(typeName));
        }

        // need to split to get the namespace and type name
        return std::make_pair(typeName.substr(0, pos), typeName.substr(pos + 1));
    }
}

std::string FrameStore::FormatGenericTypeParameters(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType, bool isEncoded)
{
    std::stringstream builder;

    // Get all generic parameters definition (ex: "{|ct:K, |ct:V}" for Dictionary<K,V>)
    // --> need to iterate on the generic arguments definition with metadata API
    HCORENUM hEnum = nullptr;

    // NOTE: unlike other COM iterators, it is not possible to get the real count in a first call
    //       and then allocate to get them all in a second call.
    //       128 type parameters sounds more than enough: no need to detect the case where 128
    //       were retrieved and add ... before >
    const ULONG MaxGenericParametersCount = 128;
    ULONG genericParamsCount = MaxGenericParametersCount;
    mdGenericParam genericParams[MaxGenericParametersCount];
    HRESULT hr = pMetadata->EnumGenericParams(&hEnum, mdTokenType, genericParams, MaxGenericParametersCount, &genericParamsCount);

    if (SUCCEEDED(hr))
    {
        WCHAR paramName[64];
        ULONG paramNameLen = 64;

        builder << (isEncoded ? "{" : "<");
        for (size_t currentParam = 0; currentParam < genericParamsCount; currentParam++)
        {
            if (isEncoded)
            {
                builder << "|ns: |ct:";
            }

            ULONG index;
            DWORD flags;
            hr = pMetadata->GetGenericParamProps(genericParams[currentParam], &index, &flags, nullptr, nullptr, paramName, paramNameLen, &paramNameLen);
            if (SUCCEEDED(hr))
            {
                // need to convert from UTF16 to UTF8
                builder << shared::ToString(shared::WSTRING(paramName));
            }
            else
            {
                // this should never happen if the enum succeeded: no need to count the parameters
                builder << "T";
            }

            if (currentParam < genericParamsCount - 1)
            {
                builder << ", ";
            }
        }
        builder << (isEncoded ? "}" : ">");
        pMetadata->CloseEnum(hEnum);
    }

    return builder.str();
}

void FrameStore::ConcatUnknownGenericType(std::stringstream& builder, bool isEncoded)
{
    if (isEncoded)
    {
        builder << "|ns: |ct:T";
    }
    else
    {
        builder << "T";
    }
}

std::string FrameStore::FormatGenericParameters(
    ICorProfilerInfo4* pInfo,
    ULONG32 numGenericTypeArgs,
    ClassID* genericTypeArgs,
    bool isEncoded)
{
    std::stringstream builder;
    builder << (isEncoded ? "{" : "<");

    for (size_t currentGenericArg = 0; currentGenericArg < numGenericTypeArgs; currentGenericArg++)
    {
        ClassID argClassId = genericTypeArgs[currentGenericArg];
        if (argClassId == 0)
        {
            ConcatUnknownGenericType(builder, isEncoded);
        }
        else
        {
            ModuleID argModuleId;
            mdTypeDef mdType;
            HRESULT hr = pInfo->GetClassIDInfo2(argClassId, &argModuleId, &mdType, nullptr, 0, nullptr, nullptr);
            if (FAILED(hr))
            {
                ConcatUnknownGenericType(builder, isEncoded);
            }
            else
            {
                ComPtr<IMetaDataImport2> pMetadata;
                hr = pInfo->GetModuleMetaData(argModuleId, ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(&pMetadata));
                if (FAILED(hr))
                {
                    ConcatUnknownGenericType(builder, isEncoded);
                }
                else
                {
                    auto [ns, ct] = GetManagedTypeName(pInfo, pMetadata.Get(), argModuleId, argClassId, mdType, false, nullptr, isEncoded);
                    if (isEncoded)
                    {
                        builder << "|ns:" << ns << " |ct:" << ct;
                    }
                    else
                    {
                        if (ns.empty())
                        {
                            builder << ct;
                        }
                        else
                        {
                            builder << ns << "." << ct;
                        }
                    }
                }
            }
        }

        if (currentGenericArg < numGenericTypeArgs - 1)
        {
            builder << ", ";
        }
    }

    builder << (isEncoded ? "}" : ">");

    return builder.str();
}

// for a given classId/mdTypeDef, get:
//      the namespace (if any)
//      outer types (if any) without generic information
//      inner type with generic information (if any)
std::pair<std::string, std::string> FrameStore::GetManagedTypeName(
    ICorProfilerInfo4* pInfo,
    IMetaDataImport2* pMetadata,
    ModuleID moduleId,
    ClassID classId,
    mdTypeDef mdTokenType,
    bool isArray,
    const char* arraySuffix,
    bool isEncoded)
{
    auto [ns, typeName] = GetTypeWithNamespace(pMetadata, mdTokenType, isArray, arraySuffix);
    // we have everything we need if not a generic type

    // if classId == 0 (i.e. one generic parameter is a reference type), no way to get the exact generic parameters
    // but we can get the original generic parameter type definition (i.e. "T" instead of "string")
    if (classId == 0)
    {
        // concat the generic parameter types from metadata based on mdTokenType
        auto genericParameters = FormatGenericTypeParameters(pMetadata, mdTokenType, isEncoded);

        if (isArray)
        {
            return std::make_pair(std::move(ns), typeName + genericParameters + arraySuffix);
        }
        else
        {
            return std::make_pair(std::move(ns), typeName + genericParameters);
        }
    }

    // figure out the instanciated generic parameters if any
    mdTypeDef mdType;
    ClassID parentClassId; // useful if we need parent type
    ULONG32 numGenericTypeArgs = 0;

    HRESULT hr = pInfo->GetClassIDInfo2(classId, nullptr, &mdType, &parentClassId, 0, &numGenericTypeArgs, nullptr);
    if (FAILED(hr))
    {
        // this happens when the given classId is 0 so should not occur
        if (isArray)
        {
            typeName += arraySuffix;
        }

        return std::make_pair(std::move(ns), std::move(typeName));
    }

    // nothing else to do if not a generic
    if (FAILED(hr) || (numGenericTypeArgs == 0))
    {
        if (isArray)
        {
            typeName += arraySuffix;
        }

        return std::make_pair(std::move(ns), std::move(typeName));
    }

    // list generic parameters
    auto genericTypeArgs = std::make_unique<ClassID[]>(numGenericTypeArgs);
    hr = pInfo->GetClassIDInfo2(classId, nullptr, &mdType, &parentClassId, numGenericTypeArgs, &numGenericTypeArgs, genericTypeArgs.get());
    if (FAILED(hr))
    {
        // why would it fail?
        assert(SUCCEEDED(hr));

        if (isArray)
        {
            typeName += arraySuffix;
        }

        return std::make_pair(std::move(ns), std::move(typeName));
    }

    // concat the generic parameter types
    auto genericParameters = FormatGenericParameters(pInfo, numGenericTypeArgs, genericTypeArgs.get(), isEncoded);

    if (isArray)
    {
        return std::make_pair(std::move(ns), std::move(typeName + genericParameters + arraySuffix));
    }
    else
    {
        return std::make_pair(std::move(ns), std::move(typeName + genericParameters));
    }
}

std::pair<std::string, mdTypeDef> FrameStore::GetMethodNameFromMetadata(IMetaDataImport2* pMetadataImport, mdMethodDef mdTokenFunc)
{
    // get the method name
    ULONG nameCharCount = 0;
    HRESULT hr = pMetadataImport->GetMethodProps(mdTokenFunc, nullptr, nullptr, 0, &nameCharCount, nullptr, nullptr, nullptr, nullptr, nullptr);
    if (FAILED(hr))
    {
        return std::make_pair(std::string(), mdTokenNil);
    }

    auto buffer = std::make_unique<WCHAR[]>(nameCharCount);
    mdTypeDef mdTokenType;

    hr = pMetadataImport->GetMethodProps(mdTokenFunc, &mdTokenType, buffer.get(), nameCharCount, &nameCharCount, nullptr, nullptr, nullptr, nullptr, nullptr);
    if (FAILED(hr))
    {
        return std::make_pair(std::string(), mdTokenNil);
    }

    // convert from UTF16 to UTF8
    return std::make_pair(shared::ToString(shared::WSTRING(buffer.get())), mdTokenType);
}

std::string FrameStore::GetMethodSignature(ICorProfilerInfo4* pInfo, IMetaDataImport2* pMetaData, FunctionID functionId, mdMethodDef mdTokenFunc)
{
    PCCOR_SIGNATURE pSigBlob;
    ULONG blobSize, attributes;
    DWORD flags;
    ULONG codeRva;

    // get the coded signature from metadata
    ULONG nameCharCount = 0;
    HRESULT hr = pMetaData->GetMethodProps(mdTokenFunc, nullptr, nullptr, 0, nullptr, &attributes, &pSigBlob, &blobSize, &codeRva, &flags);
    if (FAILED(hr))
    {
        return "(?)";
    }

    // use Peter Sollich way in ClrProfiler to parse the binary signature
    // https://github.com/microsoftarchive/clrprofiler/blob/master/CLRProfiler/profilerOBJ/ProfilerInfo.cpp#L1838
    // read https://chnasarre.medium.com/decyphering-method-signature-with-clr-profiling-api-8328a72a216e for more details
    ULONG elementType;
    ULONG callConv;
    char buffer[2 * 260];

    // get the calling convention
    pSigBlob += CorSigUncompressData(pSigBlob, &callConv);

    ULONG argCount = 0;
    ClassID* methodTypeArgs = NULL;
    ClassID* classTypeArgs = NULL;
    ModuleID moduleId;
    // for generic support
    ULONG genericArgCount = 0;
    UINT32 methodTypeArgCount = 0;
    ULONG32 classTypeArgCount = 0;
    ClassID classId = 0;
    if ((callConv & IMAGE_CEE_CS_CALLCONV_GENERIC) != 0)
    {
        //
        // Grab the generic type argument count
        //
        pSigBlob += CorSigUncompressData(pSigBlob, &genericArgCount);

        // get the generic details for the function
        // TODO replace with unique_ptr methodTypeArgs = std::make_unique<ClassID[]>(genericArgCount);
        methodTypeArgs = new ClassID[genericArgCount];
        hr = pInfo->GetFunctionInfo2(functionId, NULL, &classId, &moduleId, NULL, genericArgCount, &methodTypeArgCount, methodTypeArgs);
        assert(!SUCCEEDED(hr) || (genericArgCount == methodTypeArgCount));
        if (FAILED(hr))
        {
            delete[] methodTypeArgs;
            methodTypeArgs = NULL;
        }

        // TODO: why would we need these for the method signature?
        // get the generic details for the type implementing the function
        hr = pInfo->GetClassIDInfo2(classId, NULL, NULL, NULL, 0, &classTypeArgCount, NULL);
        if (SUCCEEDED(hr) && classTypeArgCount > 0)
        {
            // TODO replace with unique_ptr classTypeArgs = std::make_unique<ClassID[]>(classTypeArgCount);
            classTypeArgs = new ClassID[classTypeArgCount];
            hr = pInfo->GetClassIDInfo2(classId, NULL, NULL, NULL, classTypeArgCount, &classTypeArgCount, classTypeArgs);

            if (FAILED(hr))
            {
                delete[] classTypeArgs;
                classTypeArgs = NULL;
            }
        }

        hr = S_OK;
    }
    else
    {
        hr = pInfo->GetFunctionInfo(functionId, NULL, &moduleId, NULL);
    }

    //
    // Grab the argument count
    //
    pSigBlob += CorSigUncompressData(pSigBlob, &argCount);

    //
    // Get the return type
    //
    mdToken returnTypeToken;
    buffer[0] = '\0';
    pSigBlob = ParseElementType(pMetaData, pSigBlob, classTypeArgs, methodTypeArgs, &elementType, buffer, ARRAY_LEN(buffer) - 1, &returnTypeToken);
    // if the return type returned back empty, should correspond to "void"
    // NOTE: elementType should be ELEMENT_TYPE_VOID in that case

    if (argCount == 0)
    {
        // TODO: should go away with unique_ptr
        if (methodTypeArgs != NULL)
        {
            delete[] methodTypeArgs;
            methodTypeArgs = NULL;
        }
        if (classTypeArgs != NULL)
        {
            delete[] classTypeArgs;
            classTypeArgs = NULL;
        }

        return "()";
    }


    // To get the parameters types and name, it is needed to:
    // - decypher the function binary blob signature to get each parameter type
    // - fetch each parameter properties to get its name
    // NOTE: in case of non static method, the implicit 'this' parameter is not part of the parameters
    //       neither in the signature nor in the metadata enumeration EnumParams
    hr = S_OK;
    HCORENUM hEnum = 0;
    ULONG paramCount;
    // TODO: use unique_ptr
    mdParamDef* paramDefs = new mdParamDef[argCount];
    hr = pMetaData->EnumParams(&hEnum, mdTokenFunc, paramDefs, argCount, &paramCount);
    pMetaData->CloseEnum(hEnum);

    // sanity checks
    //assert(paramCount == argCount);
    if (paramCount != argCount)
    {
        printf("paramCount=%u  argCount=%u\r\n", paramCount, argCount);
    }

    ULONG pos;
    WCHAR name[260];
    ULONG length;

    // attributes values from CorParamAttr in CorHdr.h
    /*
    typedef enum CorParamAttr
    {
       pdIn                        =   0x0001,     // Param is [In]
       pdOut                       =   0x0002,     // Param is [out]
       pdOptional                  =   0x0010,     // Param is optional
       ...
    } CorParamAttr;

    // Macros for accessing the members of CorParamAttr.
    #define IsPdIn(x)                           ((x) & pdIn)
    #define IsPdOut(x)                          ((x) & pdOut)
    #define IsPdOptional(x)                     ((x) & pdOptional)
    */

    DWORD bIsValueType;
    ULONG currentGenericParam = 0;
    std::stringstream builder;
    builder << "(";
    for (ULONG i = 0;
         (SUCCEEDED(hr) && (pSigBlob != NULL) && (i < (argCount)));
         i++)
    {
        // get the parameter name
        hr = pMetaData->GetParamProps(paramDefs[i], NULL, &pos, name, ARRAY_LEN(name) - 1, &length, &attributes, &bIsValueType, NULL, NULL);
        // note that we need to convert from WCHAR* to char* for the name

        // get the parameter type
        buffer[0] = '\0';

        // TODO: in case of generic function, get the type details from the runtime and not from the metadata
        // !! we don't know in advance which parameter is a generic parameter and this is given by elementType == MVAR
        mdToken parameterTypeToken = mdTypeDefNil;
        pSigBlob = ParseElementType(pMetaData, pSigBlob, classTypeArgs, methodTypeArgs, &elementType, buffer, ARRAY_LEN(buffer) - 1, &parameterTypeToken);
        if ((methodTypeArgs != NULL) && (elementType == ELEMENT_TYPE_MVAR))
        {
            // TODO: check that currentGenericParam < methodTypeArgCount
            ModuleID moduleId;
            mdTypeDef mdType;
            hr = pInfo->GetClassIDInfo2(methodTypeArgs[currentGenericParam], &moduleId, &mdType, NULL, 0, NULL, NULL);
            if (SUCCEEDED(hr))
            {
                WCHAR paramTypeName[260];
                IMetaDataImport2* pImport2;
                hr = pInfo->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(&pImport2));
                ULONG sigBlobLen = 0;

                // get elementType from type name because the metadata can't give us the instanciated generic signature
                hr = pImport2->GetTypeDefProps(mdType, paramTypeName, ARRAY_LEN(paramTypeName) - 1, 0, NULL, NULL);
                if (FAILED(hr))
                {
                    builder << "?";
                }
                else
                {
                    // convert from UTF16 to UTF8
                    builder << shared::ToString(shared::WSTRING(paramTypeName));
                    builder << " ";
                    // convert from UTF16 to UTF8
                    builder << shared::ToString(shared::WSTRING(name));
                }

                pImport2->Release();
            }

            currentGenericParam++;
        }
        else
        {
            builder << buffer;
            builder << " ";
            // convert from UTF16 to UTF8
            builder << shared::ToString(shared::WSTRING(name));
        }

        if (i < argCount - 1)
        {
            builder << ", ";
        }
    }
    builder << ")";

    // TODO: remove it once unique_ptr is used
    // don't forget to cleanup
    if (paramDefs != NULL)
    {
        delete[] paramDefs;
        paramDefs = NULL;
    }
    if (methodTypeArgs != NULL)
    {
        delete[] methodTypeArgs;
        methodTypeArgs = NULL;
    }
    if (classTypeArgs != NULL)
    {
        delete[] classTypeArgs;
        classTypeArgs = NULL;
    }

    return builder.str();
}



std::pair<std::string, std::string> FrameStore::GetManagedTypeName(ICorProfilerInfo4* pInfo, ClassID classId)
{
    // only get the type name (no generic)
    ModuleID moduleId;
    mdTypeDef mdTypeToken;
    HRESULT hr = pInfo->GetClassIDInfo(classId, &moduleId, &mdTypeToken);
    if (FAILED(hr))
    {
        return std::make_pair("", "T");
    }

    IMetaDataImport2* pMetadata;
    hr = pInfo->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(&pMetadata));
    if (FAILED(hr))
    {
        return std::make_pair("", "T");
    }

    std::string typeName = GetTypeNameFromMetadata(pMetadata, mdTypeToken);
    pMetadata->Release();
    if (typeName.empty())
    {
        return std::make_pair("", "T");
    }

    // look for the namespace
    auto const pos = typeName.find_last_of('.');
    if (pos == std::string::npos)
    {
        // no namespace
        return std::make_pair("", std::move(typeName));
    }

    // need to split to get the namespace and type name
    return std::make_pair(typeName.substr(0, pos), typeName.substr(pos + 1));
}


void FixGenericSyntax(WCHAR* name)
{
    ULONG currentCharPos = 0;
    while (name[currentCharPos] != L'\0')
    {
        if (name[currentCharPos] == L'`')
        {
            // skip `xx
            name[currentCharPos] = L'\0';
            return;
        }
        currentCharPos++;
    }
}

void FixGenericSyntax(char* name)
{
    ULONG currentCharPos = 0;
    while (name[currentCharPos] != '\0')
    {
        if (name[currentCharPos] == '`')
        {
            // skip `xx
            name[currentCharPos] = '\0';
            return;
        }
        currentCharPos++;
    }
}

void StrAppend(__out_ecount(cchBuffer) char* buffer, const char* str, size_t cchBuffer)
{
    size_t bufLen = strlen(buffer) + 1;
    if (bufLen <= cchBuffer)
        strncat_s(buffer, cchBuffer, str, cchBuffer - bufLen);
}

PCCOR_SIGNATURE ParseByte(PCCOR_SIGNATURE pbSig, BYTE* pByte)
{
    *pByte = *pbSig++;
    return pbSig;
}

// TODO: move the signature parsing helpers into another file
//			so it could be called from different places in a more
//			consistent way
PCCOR_SIGNATURE ParseElementType(IMetaDataImport* pMDImport,
                                 PCCOR_SIGNATURE signature,
                                 ClassID* classTypeArgs,
                                 ClassID* methodTypeArgs,
                                 ULONG* elementType,
                                 __out_ecount(cchBuffer) char* buffer,
                                 size_t cchBuffer,
                                 mdToken* typeToken // type ref/def token for reference and value types
)
{
    ULONG eType = *signature++;
    *elementType = eType;
    switch (*elementType)
    {
        case ELEMENT_TYPE_VOID:
            StrAppend(buffer, "void", cchBuffer);
            break;

        case ELEMENT_TYPE_BOOLEAN:
            StrAppend(buffer, "bool", cchBuffer);
            break;

        case ELEMENT_TYPE_CHAR:
            StrAppend(buffer, "char", cchBuffer);
            break;

        case ELEMENT_TYPE_I1:
            StrAppend(buffer, "int8", cchBuffer);
            break;

        case ELEMENT_TYPE_U1:
            StrAppend(buffer, "unsigned int8", cchBuffer);
            break;

        case ELEMENT_TYPE_I2:
            StrAppend(buffer, "int16", cchBuffer);
            break;

        case ELEMENT_TYPE_U2:
            StrAppend(buffer, "unsigned int16", cchBuffer);
            break;

        case ELEMENT_TYPE_I4:
            StrAppend(buffer, "int32", cchBuffer);
            break;

        case ELEMENT_TYPE_U4:
            StrAppend(buffer, "unsigned int32", cchBuffer);
            break;

        case ELEMENT_TYPE_I8:
            StrAppend(buffer, "int64", cchBuffer);
            break;

        case ELEMENT_TYPE_U8:
            StrAppend(buffer, "unsigned int64", cchBuffer);
            break;

        case ELEMENT_TYPE_R4:
            StrAppend(buffer, "float32", cchBuffer);
            break;

        case ELEMENT_TYPE_R8:
            StrAppend(buffer, "float64", cchBuffer);
            break;

        case ELEMENT_TYPE_U:
            StrAppend(buffer, "unsigned int_ptr", cchBuffer);
            break;

        case ELEMENT_TYPE_I:
            StrAppend(buffer, "int_ptr", cchBuffer);
            break;

        case ELEMENT_TYPE_OBJECT:
            StrAppend(buffer, "Object", cchBuffer);
            break;

        case ELEMENT_TYPE_STRING:
            StrAppend(buffer, "String", cchBuffer);
            break;

        case ELEMENT_TYPE_TYPEDBYREF:
            StrAppend(buffer, "refany", cchBuffer);
            break;

        case ELEMENT_TYPE_CLASS:
        case ELEMENT_TYPE_VALUETYPE:
        case ELEMENT_TYPE_CMOD_REQD:
        case ELEMENT_TYPE_CMOD_OPT:
        {
            mdToken token;
            char classname[260];

            classname[0] = '\0';
            signature += CorSigUncompressToken(signature, &token);
            if (typeToken != NULL)
            {
                *typeToken = token;
            }

            HRESULT hr;
            WCHAR zName[260];
            if (TypeFromToken(token) == mdtTypeRef)
            {
                mdToken resScope;
                ULONG length;
                hr = pMDImport->GetTypeRefProps(token,
                                                &resScope,
                                                zName,
                                                260,
                                                &length);
            }
            else
            {
                hr = pMDImport->GetTypeDefProps(token,
                                                zName,
                                                260,
                                                NULL,
                                                NULL,
                                                NULL);
            }
            if (SUCCEEDED(hr))
            {
                size_t convertedChars;
                wcstombs_s(&convertedChars, classname, sizeof(classname) / sizeof(char), zName, sizeof(zName) / sizeof(WCHAR));
            }

            StrAppend(buffer, classname, cchBuffer);
        }
        break;

        case ELEMENT_TYPE_SZARRAY:
            signature = ParseElementType(pMDImport, signature, classTypeArgs, methodTypeArgs, &eType, buffer, cchBuffer, typeToken);
            StrAppend(buffer, "[]", cchBuffer);
            break;

        case ELEMENT_TYPE_ARRAY:
        {
            ULONG rank;
            signature = ParseElementType(pMDImport, signature, classTypeArgs, methodTypeArgs, &eType, buffer, cchBuffer, typeToken);
            rank = CorSigUncompressData(signature);

            // The second condition is to guard against overflow bugs & shut up PREFAST
            if (rank == 0 || rank >= 65536)
                StrAppend(buffer, "[?]", cchBuffer);

            else
            {
                ULONG* lower;
                ULONG* sizes;
                ULONG numsizes;
                ULONG arraysize = (sizeof(ULONG) * 2 * rank);

                lower = (ULONG*)_alloca(arraysize);
                memset(lower, 0, arraysize);
                sizes = &lower[rank];

                numsizes = CorSigUncompressData(signature);
                if (numsizes <= rank)
                {
                    ULONG numlower;
                    ULONG i;

                    for (i = 0; i < numsizes; i++)
                        sizes[i] = CorSigUncompressData(signature);

                    numlower = CorSigUncompressData(signature);
                    if (numlower <= rank)
                    {
                        for (i = 0; i < numlower; i++)
                            lower[i] = CorSigUncompressData(signature);

                        StrAppend(buffer, "[", cchBuffer);
                        for (i = 0; i < rank; i++)
                        {
                            if ((sizes[i] != 0) && (lower[i] != 0))
                            {
                                char sizeBuffer[100];
                                if (lower[i] == 0)
                                    sprintf_s(sizeBuffer, sizeof(sizeBuffer) / sizeof(char), "%d", sizes[i]);
                                else
                                {
                                    sprintf_s(sizeBuffer, sizeof(sizeBuffer) / sizeof(char), "%d...", lower[i]);

                                    if (sizes[i] != 0)
                                        sprintf_s(sizeBuffer, sizeof(sizeBuffer) / sizeof(char), "%d...%d", lower[i], (lower[i] + sizes[i] + 1));
                                }
                                StrAppend(buffer, sizeBuffer, cchBuffer);
                            }

                            if (i < (rank - 1))
                                StrAppend(buffer, ",", cchBuffer);
                        }

                        StrAppend(buffer, "]", cchBuffer);
                    }
                }
            }
        }
        break;

        case ELEMENT_TYPE_PINNED:
            // TODO: I'm not sure what to do with this...
            signature = ParseElementType(pMDImport, signature, classTypeArgs, methodTypeArgs, &eType, buffer, cchBuffer, typeToken);
            StrAppend(buffer, "pinned ", cchBuffer);
            break;

        case ELEMENT_TYPE_PTR:
            // TODO: I'm not sure this is something that can happen in C#
            signature = ParseElementType(pMDImport, signature, classTypeArgs, methodTypeArgs, &eType, buffer, cchBuffer, typeToken);
            StrAppend(buffer, "*", cchBuffer);
            break;

        case ELEMENT_TYPE_BYREF:
            // TODO: keep in mind that it is a parameter passed by reference; i.e. its value is the address of the variable
            // that contains the real object address in case of reference type.
            // --> we need to return if it is a BYREF parameter as an 'isReference' out parameter in this method!!!
            // Note that the "real" type just follows
            signature = ParseElementType(pMDImport, signature, classTypeArgs, methodTypeArgs, &eType, buffer, cchBuffer, typeToken);
            StrAppend(buffer, "&", cchBuffer);
            break;

            // handle generics
        case ELEMENT_TYPE_VAR: // for type
        {
            // read the number
            StrAppend(buffer, "T", cchBuffer);
            ULONG n = CorSigUncompressData(signature);
            char number[16];
            sprintf_s(number, ARRAY_LEN(number) - 1, "%u", n);
            StrAppend(buffer, number, cchBuffer);
        }
        break;
        case ELEMENT_TYPE_MVAR: // for method
        {
            StrAppend(buffer, "M", cchBuffer);
            ULONG n = CorSigUncompressData(signature);
            char number[16];
            sprintf_s(number, ARRAY_LEN(number) - 1, "%u", n);
            StrAppend(buffer, number, cchBuffer);
        }
        break;

        case ELEMENT_TYPE_GENERICINST:
            // https://docs.microsoft.com/en-us/archive/blogs/davbr/sigparse-cpp line 834
            //
            {
                // is it value/reference type?
                ULONG eType = CorSigUncompressData(signature);
                BYTE elementType = (BYTE)eType;
                // signature = ParseByte(signature, &elementType);
                if ((elementType != ELEMENT_TYPE_CLASS) && (elementType != ELEMENT_TYPE_VALUETYPE))
                {
                    StrAppend(buffer, "<T???>", cchBuffer);
                    break;
                }

                // get the mdToken corresponding to the generic type and get it's name
                // Note that the name ends with `xx where xx is the count of generic parameters
                mdToken token;
                signature += CorSigUncompressToken(signature, &token);

                // TODO extract this code (same as case ELEMENT_TYPE_CLASS)
                HRESULT hr;
                char classname[260];
                classname[0] = '\0';
                WCHAR zName[260];
                if (TypeFromToken(token) == mdtTypeRef)
                {
                    mdToken resScope;
                    ULONG length;
                    hr = pMDImport->GetTypeRefProps(token,
                                                    &resScope,
                                                    zName,
                                                    260,
                                                    &length);
                }
                else
                {
                    hr = pMDImport->GetTypeDefProps(token,
                                                    zName,
                                                    260,
                                                    NULL,
                                                    NULL,
                                                    NULL);
                }
                if (SUCCEEDED(hr))
                {
                    size_t convertedChars;
                    wcstombs_s(&convertedChars, classname, sizeof(classname) / sizeof(char), zName, sizeof(zName) / sizeof(WCHAR));
                }

                FixGenericSyntax((char*)classname);
                StrAppend(buffer, classname, cchBuffer);

                StrAppend(buffer, "<", cchBuffer);

                // get generic parameters count
                ULONG genericParameterCount = CorSigUncompressData(signature);

                // get each generic parameter
                for (ULONG current = 1; current <= genericParameterCount; current++)
                {
                    signature = ParseElementType(pMDImport, signature, classTypeArgs, methodTypeArgs, &eType, buffer, cchBuffer, NULL);

                    if (current != genericParameterCount)
                    {
                        StrAppend(buffer, ", ", cchBuffer);
                    }
                }
                StrAppend(buffer, ">", cchBuffer);
                break;
            }

        default:
        case ELEMENT_TYPE_END:
        case ELEMENT_TYPE_SENTINEL:
            StrAppend(buffer, "<UNKNOWN>", cchBuffer);
            break;

    } // switch

    return signature;
}
