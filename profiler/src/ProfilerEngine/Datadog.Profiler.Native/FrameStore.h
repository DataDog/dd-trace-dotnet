// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>
#include <mutex>
#include <unordered_map>
#include <string>
#include <vector>
#include "IFrameStore.h"
#include "IDebugInfoStore.h"

#include "shared/src/native-src/com_ptr.h"

class IConfiguration;

class FrameStore : public IFrameStore
{
private:
    const std::string UnknownManagedFrame = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: |fn:Unknown-Method |fg: |sg:(?)";
    const std::string UnknownManagedType = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: ";
    const std::string UnknownManagedAssembly = "Unknown-Assembly";

private:
    class TypeDesc
    {
    public:
        std::string Assembly;
        std::string Namespace;
        std::string Type;
        std::string Parameters;
    };

public:
    FrameStore(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration* pConfiguration, IDebugInfoStore* pDebugInfoStore);

public :
    std::pair<bool, FrameInfoView> GetFrame(uintptr_t instructionPointer) override;
    bool GetTypeName(ClassID classId, std::string& name) override;
    bool GetTypeName(ClassID classId, std::string_view& name) override;


private:
    bool GetFunctionInfo(
        FunctionID functionId,
        mdToken& mdTokenFunc,
        ClassID& classId,
        ModuleID& moduleId,
        ULONG32& genericParametersCount,
        std::unique_ptr<ClassID[]>& genericParameters
        );
    bool GetMetadataApi(ModuleID moduleId, FunctionID functionId, ComPtr<IMetaDataImport2>& pMetadataImport);
    std::tuple<std::string, std::string, mdTypeDef> GetMethodName(
        FunctionID functionId,
        IMetaDataImport2* pMetadataImport,
        mdMethodDef mdTokenFunc,
        ULONG32 genericParametersCount,
        ClassID* genericParameters
        );
    bool BuildTypeDesc(
        IMetaDataImport2* pMetadataImport,
        ClassID classId,
        ModuleID moduleId,
        mdTypeDef mdTokenType,
        TypeDesc& typeDesc,
        bool isArray,
        const char* arraySuffix);
    bool GetTypeDesc(ClassID classId, TypeDesc*& typeDesc);
    bool GetCachedTypeDesc(ClassID classId, TypeDesc*& typeDesc);
    FrameInfoView GetManagedFrame(FunctionID functionId);
    std::pair <std::string_view, std::string_view> GetNativeFrame(uintptr_t instructionPointer);

public:   // global helpers
    static bool GetAssemblyName(ICorProfilerInfo4* pInfo, ModuleID moduleId, std::string& assemblyName);

private:  // global helpers
    static void FixTrailingGeneric(WCHAR* name);
    static std::string GetTypeNameFromMetadata(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType);
    static std::pair<std::string, std::string> GetTypeWithNamespace(
        IMetaDataImport2* pMetadata,
        mdTypeDef mdTokenType);
    static std::string FormatGenericTypeParameters(IMetaDataImport2* pMetadata, mdTypeDef mdTokenType);
    static std::string FormatGenericParameters(
        ICorProfilerInfo4* pInfo,
        ULONG32 numGenericTypeArgs,
        ClassID* genericTypeArgs);
    static std::tuple<std::string, std::string, std::string> GetManagedTypeName(
        ICorProfilerInfo4* pInfo,
        IMetaDataImport2* pMetadata,
        ModuleID moduleId,
        ClassID classId,
        mdTypeDef mdTokenType,
        bool isArray,
        const char* arraySuffix);
    std::string GetMethodSignature(
        ICorProfilerInfo4* pInfo,
        IMetaDataImport2* pMetadataImport,
        mdTypeDef mdTokenType,
        FunctionID functionId,
        mdMethodDef mdTokenFunc
        );
    PCCOR_SIGNATURE ParseElementType(
        IMetaDataImport* pMDImport,
        PCCOR_SIGNATURE signature,
        std::vector<std::string>& classTypeArgs,
        ClassID* methodTypeArgs,
        ULONG* elementType,
        std::stringstream& builder,
        mdToken* typeToken);
    static std::pair<std::string, mdTypeDef> GetMethodNameFromMetadata(
        IMetaDataImport2* pMetadataImport,
        mdMethodDef mdTokenFunc
        );
    static std::pair<std::string, std::string> GetManagedTypeName(ICorProfilerInfo4* pInfo, ClassID classId);
    static void ConcatUnknownGenericType(std::stringstream& builder);

private:
    struct FrameInfo
    {
    public:
        std::string ModuleName;
        std::string Frame;
        std::string_view Filename;
        std::uint32_t StartLine;

        operator FrameInfoView() const
        {
            return {ModuleName, Frame, Filename, StartLine};
        }
    };

    ICorProfilerInfo4* _pCorProfilerInfo;
    IDebugInfoStore* _pDebugInfoStore;

    std::mutex _methodsLock;
    std::mutex _nativeLock;

    // frame relate caches functions
    std::unordered_map<FunctionID, FrameInfo> _methods;
    std::mutex _typesLock;
    std::unordered_map<ClassID, TypeDesc> _types;
    std::unordered_map<std::string, std::string> _framePerNativeModule;

    // for allocation recorder
    std::mutex _fullTypeNamesLock;
    std::unordered_map<ClassID, std::string> _fullTypeNames;

    bool _resolveNativeFrames;
    // TODO: dump stats about caches size at the end of the application

    // TODO: would it be needed to have a cache (moduleId + mdTypeDef) -> TypeDesc?
};
