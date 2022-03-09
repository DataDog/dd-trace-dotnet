// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FrameStore.h"
#include "HResultConverter.h"
#include "Log.h"
#include "OpSysTools.h"

#include "shared/src/native-src/com_ptr.h"


FrameStore::FrameStore(ICorProfilerInfo4* pCorProfilerInfo)
    :
    _pCorProfilerInfo{pCorProfilerInfo}
{
}


std::tuple<bool, std::string, std::string> FrameStore::GetFrame(uintptr_t instructionPointer)
{
    FunctionID functionId;
    HRESULT hr = _pCorProfilerInfo->GetFunctionFromIP((LPCBYTE)instructionPointer, &functionId);

    if (SUCCEEDED(hr))
    {
        auto [moduleName, frame] = GetManagedFrame(functionId);
        return { true, moduleName, frame };
    }
    else
    {
        auto [moduleName, frame] = GetNativeFrame(instructionPointer);
        return { false, moduleName, frame };
    }

}

// It should be possible to use dbghlp.dll on Windows (and something else on Linux?)
// to get function name + offset
// see https://docs.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-symfromaddr for more details
// However, today, no symbol resolution is done; only the module implementing the function is provided
std::pair<std::string, std::string> FrameStore::GetNativeFrame(uintptr_t instructionPointer)
{
    static const std::string UnknownNativeFrame("|lm:Unknown-Native-Module |ns:NativeCode |ct:Unknown-Native-Module |fn:Function");
    auto moduleName = OpSysTools::GetModuleName(reinterpret_cast<void*>(instructionPointer));
    if (moduleName.empty())
    {
        return { "Unknown-Native-Module", UnknownNativeFrame };
    }

    // moduleName contains the full path: keep only the filename
    moduleName = std::filesystem::path(moduleName).filename().string();
    std::stringstream builder;
    builder << "|lm:" << moduleName << " |ns:NativeCode |ct:" << moduleName << " |fn:Function";
    return { moduleName, builder.str() };
}

std::pair<std::string, std::string> FrameStore::GetManagedFrame(FunctionID functionId)
{
    // Look into the cache first
    auto element = _methods.find(functionId);
    if (element != _methods.end())
    {
        return element->second;
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
        return { UnknownManagedAssembly, UnknownManagedFrame };
    }

    // Use metadata API to get method name
    ComPtr<IMetaDataImport2> pMetadataImport;
    if (!GetMetadataApi(moduleId, functionId, pMetadataImport))
    {
        return { UnknownManagedAssembly, UnknownManagedFrame };
    }

    // method name is resolved first because we also get the mdDefToken of its class
    auto [methodName, mdTokenType] = GetMethodName(pMetadataImport.Get(), mdTokenFunc, genericParametersCount, genericParameters.get());
    if (methodName.empty())
    {
        return { UnknownManagedAssembly, UnknownManagedFrame };
    }

    // get type related description (assembly, namespace and type name)
    // look into the cache first
    TypeDesc typeDesc;
    bool typeInCache = false;
    if (classId != 0)  // classId could be 0 in case of generic type with a generic parameter that is a reference type
    {
        auto typeEntry = _types.find(classId);
        if (typeEntry != _types.end())
        {
            typeDesc = typeEntry->second;
            typeInCache = true;
        }
    }
    // TODO: would it be interesting to have a (moduleId + mdTokenDef) -> TypeDesc cache for the non cached generic types?

    if (!typeInCache)
    {
        // try to get the type description
        if (!GetTypeDesc(pMetadataImport.Get(), classId, moduleId, mdTokenType, typeDesc))
        {
            return { UnknownManagedAssembly, UnknownManagedType + " |fn:" + methodName };
        }

        if (classId != 0)
        {
            _types[classId] = typeDesc;
        }
        // TODO: would it be interesting to have a (moduleId + mdTokenDef) -> TypeDesc cache for the non cached generic types?
    }

    // build the frame from assembly, namespace, type and method names
    std::stringstream builder;
    if (!typeDesc.Assembly.empty())
    {
        builder << "|lm:" << typeDesc.Assembly;
    }
    builder << " |ns:" << typeDesc.Namespace;
    builder << " |ct:" << typeDesc.Type;
    builder << " |fn:" << methodName;

    std::string managedFrame = builder.str();

    // store it into the function cache
    _methods[functionId] = { typeDesc.Assembly, managedFrame };

    return { typeDesc.Assembly, managedFrame };
}

// More explanations in https://chnasarre.medium.com/dealing-with-modules-assemblies-and-types-with-clr-profiling-apis-a7522a5abaa9?source=friends_link&sk=3e010ab991456db0394d4cca29cb8cb2
bool FrameStore::GetTypeDesc(IMetaDataImport2* pMetadataImport, ClassID classId, ModuleID moduleId, mdTypeDef mdTokenType, TypeDesc& typeDesc)
{
    // 1. Get the assembly from the module
    if (!GetAssemblyName(_pCorProfilerInfo, moduleId, typeDesc.Assembly))
    {
        return false;
    }

    // 2. Look for the type name including namespace (need to take into account nested types and generic types)
    auto [ns, ct] = GetManagedTypeName(_pCorProfilerInfo, pMetadataImport, moduleId, classId, mdTokenType);
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
    IMetaDataImport2* pMetadataImport,
    mdMethodDef mdTokenFunc,
    ULONG32 genericParametersCount,
    ClassID* genericParameters)
{
    auto [methodName, mdTokenType] = GetMethodNameFromMetadata(pMetadataImport, mdTokenFunc);
    if ((methodName.empty()) || (genericParametersCount == 0))
    {
        return std::make_pair(std::move(methodName), mdTokenType);
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

    return std::make_pair(methodName + builder.str(), mdTokenType);
}


bool FrameStore::GetAssemblyName(ICorProfilerInfo4* pInfo, ModuleID moduleId, std::string& assemblyName)
{
    assemblyName = std::string("");

    AssemblyID assemblyId;
    HRESULT hr = pInfo->GetModuleInfo(moduleId, nullptr, 0, nullptr, nullptr, &assemblyId);
    if (FAILED(hr)) { return false; }

    // 2 steps way to get the assembly name (get the buffer size first and then fill it up with the name)
    ULONG nameCharCount = 0;
    hr = pInfo->GetAssemblyInfo(assemblyId, nameCharCount, &nameCharCount, nullptr, nullptr, nullptr);
    if (FAILED(hr)) { return false; }

    auto buffer = std::make_unique<WCHAR[]>(nameCharCount);
    hr = pInfo->GetAssemblyInfo(assemblyId, nameCharCount, &nameCharCount, buffer.get(), nullptr, nullptr);
    if (FAILED(hr)) { return false; }

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
            return;  // this is a generic type
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

std::pair<std::string, std::string> FrameStore::GetTypeWithNamespace(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType)
{
    mdTypeDef mdEnclosingType = 0;
    HRESULT hr = pMetadata->GetNestedClassProps(mdTokenType, &mdEnclosingType);
    bool isNested = SUCCEEDED(hr) && pMetadata->IsValidToken(mdEnclosingType);

    std::string enclosingType;
    std::string ns;
    if (isNested)
    {
        std::tie(ns, enclosingType) = GetTypeWithNamespace(pMetadata, mdEnclosingType);
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

std::string FrameStore::FormatGenericTypeParameters(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType)
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
        builder << "{";
        for (size_t currentParam = 0; currentParam < genericParamsCount; currentParam++)
        {
            builder << "|ns: |ct:";

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
        builder << "}";
        pMetadata->CloseEnum(hEnum);
    }

    return builder.str();
}

std::string FrameStore::FormatGenericParameters(ICorProfilerInfo4* pInfo, ULONG32 numGenericTypeArgs, ClassID* genericTypeArgs)
{
    std::stringstream builder;
    builder << "{";

    for (size_t currentGenericArg = 0; currentGenericArg < numGenericTypeArgs; currentGenericArg++)
    {

        ClassID argClassId = genericTypeArgs[currentGenericArg];
        ModuleID argModuleId;
        mdTypeDef mdType;
        pInfo->GetClassIDInfo2(argClassId, &argModuleId, &mdType, nullptr, 0, nullptr, nullptr);

        ComPtr<IMetaDataImport2> pMetadata;
        HRESULT hr = pInfo->GetModuleMetaData(argModuleId, ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(&pMetadata));
        if (FAILED(hr))
        {
            builder << "|ns: |ct:T";
        }
        else
        {
            auto [ns, ct] = GetManagedTypeName(pInfo, pMetadata.Get(), argModuleId, argClassId, mdType);
            builder << "|ns:" << ns << " |ct:" << ct;
        }

        if (currentGenericArg < numGenericTypeArgs - 1)
        {
            builder << ", ";
        }
    }

    builder << "}";

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
    mdTypeDef mdTokenType
    )
{
    auto [ns, typeName] = GetTypeWithNamespace(pMetadata, mdTokenType);
    // we have everything we need if not a generic type

    // if classId == 0 (i.e. one generic parameter is a reference type), no way to get the exact generic parameters
    // but we can get the original generic parameter type definition (i.e. "T" instead of "string")
    if (classId == 0)
    {
        // concat the generic parameter types from metadata based on mdTokenType
        auto genericParameters = FormatGenericTypeParameters(pMetadata, mdTokenType);
        return std::make_pair(std::move(ns), typeName + genericParameters);
    }

    // figure out the instanciated generic parameters if any
    mdTypeDef mdType;
    ClassID parentClassId; //useful if we need parent type
    ULONG32 numGenericTypeArgs = 0;

    HRESULT hr = pInfo->GetClassIDInfo2(classId, nullptr, &mdType, &parentClassId, 0, &numGenericTypeArgs, nullptr);
    if (FAILED(hr))
    {
        // this happens when the given classId is 0 so should not occur
        return std::make_pair(std::move(ns), std::move(typeName));
    }

    // nothing else to do if not a generic
    if (FAILED(hr) || (numGenericTypeArgs == 0))
    {
        return std::make_pair(std::move(ns), std::move(typeName));
    }

    // list generic parameters
    auto genericTypeArgs = std::make_unique<ClassID[]>(numGenericTypeArgs);
    hr = pInfo->GetClassIDInfo2(classId, nullptr, &mdType, &parentClassId, numGenericTypeArgs, &numGenericTypeArgs, genericTypeArgs.get());
    if (FAILED(hr))
    {
        // why would it fail?
        assert(SUCCEEDED(hr));
        return std::make_pair(std::move(ns), std::move(typeName));
        //return std::make_pair(ns, typeName);
    }

    // concat the generic parameter types
    auto genericParameters = FormatGenericParameters(pInfo, numGenericTypeArgs, genericTypeArgs.get());
    return std::make_pair(std::move(ns), std::move(typeName + genericParameters));
}

std::pair<std::string, mdTypeDef> FrameStore::GetMethodNameFromMetadata(IMetaDataImport2* pMetadataImport, mdMethodDef mdTokenFunc)
{
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
