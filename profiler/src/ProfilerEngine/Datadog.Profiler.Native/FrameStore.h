// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>
#include <mutex>
#include <unordered_map>
#include <string>
#include <string_view>
#include "IFrameStore.h"

#include "shared/src/native-src/com_ptr.h"

class IConfiguration;

class FrameStore : public IFrameStore
{
private:
    const std::string UnknownManagedFrame = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |fn:Unknown-Method";
    const std::string UnknownManagedType = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type ";
    const std::string UnknownManagedAssembly = "Unknown-Assembly";

private:
    class TypeDesc
    {
    public:
        std::string Assembly;
        std::string Namespace;
        std::string Type;
    };

public:
    FrameStore(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration* pConfiguration);

public :
    std::tuple<bool, std::string_view, std::string_view> GetFrame(uintptr_t instructionPointer) override;

private:
    bool GetFunctionInfo(
        FunctionID functionId,
        mdToken& functionToken,
        ClassID& classId,
        ModuleID& moduleId,
        ULONG32& genericParametersCount,
        std::unique_ptr<ClassID[]>& genericParameters
        );
    bool GetMetadataApi(ModuleID moduleId, FunctionID functionId, ComPtr<IMetaDataImport2>& pMetadataImport);
    std::pair<std::string, mdTypeDef> GetMethodName(
        IMetaDataImport2* pMetadataImport,
        mdMethodDef mdTokenFunc,
        ULONG32 genericParametersCount,
        ClassID* genericParameters
        );
    bool GetTypeDesc(IMetaDataImport2* pMetadataImport, ClassID classId, ModuleID moduleId, mdTypeDef mdTokenType, TypeDesc& typeDesc);
    std::pair <std::string_view, std::string_view> GetManagedFrame(FunctionID functionId);
    std::pair <std::string_view, std::string_view> GetNativeFrame(uintptr_t instructionPointer);

public:   // global helpers
    static bool GetAssemblyName(ICorProfilerInfo4* pInfo, ModuleID moduleId, std::string& assemblyName);

private:  // global helpers
    static void FixTrailingGeneric(WCHAR* name);
    static std::string GetTypeNameFromMetadata(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType);
    static std::pair<std::string, std::string> GetTypeWithNamespace(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType);
    static std::string FormatGenericTypeParameters(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType);
    static std::string FormatGenericParameters(ICorProfilerInfo4* pInfo, ULONG32 numGenericTypeArgs, ClassID* genericTypeArgs);
    static std::pair<std::string, std::string> GetManagedTypeName(
        ICorProfilerInfo4* pInfo,
        IMetaDataImport2* pMetadata,
        ModuleID moduleId,
        ClassID classId,
        mdTypeDef mdTokenType
        );
    static std::pair<std::string, mdTypeDef> GetMethodNameFromMetadata(
        IMetaDataImport2* pMetadataImport,
        mdMethodDef mdTokenFunc
        );
    static std::pair<std::string, std::string> GetManagedTypeName(ICorProfilerInfo4* pInfo, ClassID classId);

private:
    ICorProfilerInfo4* _pCorProfilerInfo;

    std::mutex _methodsLock;
    std::mutex _typesLock;
    std::mutex _nativeLock;
    // caches functions                      V-- module    V-- full frame
    std::unordered_map<FunctionID, std::pair<std::string, std::string>> _methods;
    std::unordered_map<ClassID, TypeDesc> _types;
    std::unordered_map<std::string, std::string> _framePerNativeModule;

    bool _resolveNativeFrames;
    // TODO: dump stats about caches size at the end of the application

    // TODO: would it be needed to have a cache (moduleId + mdTypeDef) -> TypeDesc?
};
