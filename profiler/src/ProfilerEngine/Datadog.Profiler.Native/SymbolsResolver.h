// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#ifdef _WINDOWS
#include <atlcomcli.h>
#endif

#include <thread>
#include <variant>

#include "cor.h"
#include "corprof.h"

#include "ResolvedSymbolsCache.h"
#include "StackSnapshotResultFrameInfo.h"
#include "SynchronousOffThreadWorkerBase.h"
#include "ISymbolsResolver.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

// forward declarations
class IThreadsCpuManager;

class SymbolsResolver : public ISymbolsResolver
{
private:
    static const ULONG MethodNameBuffMaxSize;
    static const ULONG TypeNameBuffMaxSize;
    static const ULONG AssemblyNameBuffMaxSize;
    static const ULONG32 InitialTypeArgsBuffLen;

public:
    SymbolsResolver(ICorProfilerInfo4* pCorProfilerInfo, IThreadsCpuManager* pThreadsCpuManager);

public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    bool ResolveAppDomainInfoSymbols(AppDomainID appDomainId,
                                     const std::uint32_t appDomainNameBuffSize,
                                     std::uint32_t* pActualAppDomainNameLen,
                                     WCHAR* pAppDomainNameBuff,
                                     std::uint64_t* pAppDomainProcessId,
                                     bool offloadToWorkerThread) override;

    bool ResolveStackFrameSymbols(const StackSnapshotResultFrameInfo& capturedFrame,
                                  StackFrameInfo** ppResolvedFrame,
                                  bool offloadToWorkerThread) override;

private:
    SymbolsResolver() = delete;
    ~SymbolsResolver() override;

    bool ResolveAppDomainInfoSymbols(AppDomainID appDomainId,
                                     const std::uint32_t appDomainNameBuffSize,
                                     std::uint32_t* pActualAppDomainNameLen,
                                     WCHAR* pAppDomainNameBuff,
                                     std::uint64_t* pAppDomainProcessId);

    void GetManagedMethodName(FunctionID functionId, StackFrameInfo** ppFrameInfo);

    bool GetManagedTypeName(ClassID classId,
                            ModuleID containingModuleId,
                            mdTypeDef mdTypeDefToken,
                            ComPtr<IMetaDataImport2> copModuleMetadataIface,
                            const ManagedTypeInfo** ppTypeInfo);

    void ReadTypeNameFromMetadata(ModuleID containingModuleId,
                                  mdTypeDef mdTypeDefToken,
                                  ComPtr<IMetaDataImport2> copModuleMetadataIface,
                                  ManagedTypeInfoMutable& typeInfo,
                                  bool readAssemblyName,
                                  bool* successTypeName,
                                  bool* successAssemblyName);

    void AppendGenericDefinition(IMetaDataImport2* pMetaDataImport,
                                 mdTypeDef mdType,
                                 shared::WSTRING* pTypeName);

    void GetMethodFlags(DWORD methodFlagsMask, std::vector<const shared::WSTRING*>& methodFlagMoikers);

    class Worker : public SynchronousOffThreadWorkerBase
    {
    public:
        Worker(ICorProfilerInfo4* pCorProfilerInfo, IThreadsCpuManager* pThreadsCpuManager, SymbolsResolver* pOwner);
        Worker() = delete;
        ~Worker() override;

    public:
        enum class WorkKinds : std::uint8_t
        {
            Unspecified = 0,
            GetManagedMethodName = 2,
            ResolveAppDomainInfoSymbols = 3
        };

        struct Parameters
        {
        public:
            Parameters(FunctionID functionId, StackFrameInfo** ppStackFrameInfo) :
                WorkKind{WorkKinds::GetManagedMethodName},
                FunctionId{functionId},
                PtrPtrStackFrameInfo{ppStackFrameInfo},
                AppDomainId{0},
                AppDomainNameBuffSize{0},
                PtrActualAppDomainNameLen{0},
                PtrAppDomainNameBuff{nullptr},
                PtrAppDomainProcessId{0}
            {
            }

            Parameters(AppDomainID adId, std::uint32_t adNameBuffSize, std::uint32_t* pActualADNameLen, WCHAR* pADNameBuff, std::uint64_t* pADProcessId) :
                WorkKind{WorkKinds::ResolveAppDomainInfoSymbols},
                FunctionId{0},
                PtrPtrStackFrameInfo{nullptr},
                AppDomainId{adId},
                AppDomainNameBuffSize{adNameBuffSize},
                PtrActualAppDomainNameLen{pActualADNameLen},
                PtrAppDomainNameBuff{pADNameBuff},
                PtrAppDomainProcessId{pADProcessId}
            {
            }

        public:
            WorkKinds WorkKind;

            // Ideally, this would be a union between the different Kind uses. But this is relatively short-lived and stack allocated.
            // We can refactor it in the future when we have more time.

            // Data required by GetManagedMethodName:
            const FunctionID FunctionId;
            StackFrameInfo** const PtrPtrStackFrameInfo;

            // Data required by ResolveAppDomainInfoSymbols:
            const AppDomainID AppDomainId;
            const std::uint32_t AppDomainNameBuffSize;
            std::uint32_t* const PtrActualAppDomainNameLen;
            WCHAR* const PtrAppDomainNameBuff;
            std::uint64_t* const PtrAppDomainProcessId;
        };

        struct Results
        {
        public:
            Results() :
                WorkKind{WorkKinds::Unspecified},
                Success{false}
            {
            }

        public:
            WorkKinds WorkKind;

            // Similarly to 'struct Parameters' this would be a union between the different Kind uses in the future.

            // No data required by GetManagedMethodName.
            // Data required by ResolveAppDomainInfoSymbols:
            bool Success;
        };

    protected:
        virtual bool ShouldInitializeCurrentThreadforManagedInteractions(ICorProfilerInfo4** ppCorProfilerInfo) override;
        virtual bool ShouldSetManagedThreadName(const char** managedThreadName) override;
        virtual bool ShouldSetNativeThreadName(const WCHAR** nativeThreadName) override;
        virtual void PerformWork(void* pWorkParameters, void* pWorkResults) override;

    private:
        static const char* ManagedThreadName;
        static const WCHAR* NativeThreadName;
        ICorProfilerInfo4* _pCorProfilerInfo;
        SymbolsResolver* _pOwner;
    };

private:
    const char* _serviceName = "SymbolsResolver";

    ResolvedSymbolsCache* _pResolvedSymbolsCache;
    ICorProfilerInfo4* _pCorProfilerInfo;
    Worker _getResolveSymbols_Worker;
};
