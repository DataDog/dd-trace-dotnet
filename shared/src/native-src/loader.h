#ifndef DD_CLR_PROFILER_LOADER_H_
#define DD_CLR_PROFILER_LOADER_H_

#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <mutex>
#include <unordered_set>
#include <utility>

#include "com_ptr.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "string.h"
#include "pal.h"

namespace shared
{
    struct RuntimeInfo
    {
        COR_PRF_RUNTIME_TYPE RuntimeType;
        USHORT MajorVersion;
        USHORT MinorVersion;
        USHORT BuildVersion;
        USHORT QfeVersion;

        RuntimeInfo() :
            RuntimeType((COR_PRF_RUNTIME_TYPE)0x0),
            MajorVersion(0),
            MinorVersion(0),
            BuildVersion(0),
            QfeVersion(0) {}

        RuntimeInfo(COR_PRF_RUNTIME_TYPE runtimeType, USHORT majorVersion, USHORT minorVersion, USHORT buildVersion, USHORT qfeVersion) :
            RuntimeType(runtimeType),
            MajorVersion(majorVersion),
            MinorVersion(minorVersion),
            BuildVersion(buildVersion),
            QfeVersion(qfeVersion) {}

        RuntimeInfo& operator=(const RuntimeInfo& other)
        {
            RuntimeType = other.RuntimeType;
            MajorVersion = other.MajorVersion;
            MinorVersion = other.MinorVersion;
            BuildVersion = other.BuildVersion;
            QfeVersion = other.QfeVersion;
            return *this;
        }

        bool IsDesktop() const
        {
            return RuntimeType == COR_PRF_DESKTOP_CLR;
        }
        bool IsCore() const
        {
            return RuntimeType == COR_PRF_CORE_CLR;
        }
    };

    struct LoaderResourceMonikerIDs
    {
        public:
            LoaderResourceMonikerIDs()
                : Net45_Datadog_AutoInstrumentation_ManagedLoader_dll(0),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll(0),
                  Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb(0),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb(0)
            {}

            LoaderResourceMonikerIDs(const LoaderResourceMonikerIDs& ids)
                : Net45_Datadog_AutoInstrumentation_ManagedLoader_dll(ids.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll(ids.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll),
                     Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb(ids.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb(ids.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb)
            {}

            std::int32_t Net45_Datadog_AutoInstrumentation_ManagedLoader_dll;
            std::int32_t NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll;
            std::int32_t Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb;
            std::int32_t NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb;
    };

    struct AssemblyMetadata
    {
        AppDomainID AppDomainId;
        ModuleID ModuleId;
        AssemblyID Id;
        mdAssembly Token = mdAssemblyNil;
        ASSEMBLYMETADATA Metadata{};
        WSTRING Name;
        DWORD Flags;
        const void* pPublicKey;
        ULONG PublicKeyLength;
        ULONG HashAlgId;
    };

    struct LoaderOptions
    {
        // Rewrite options
        bool RewriteModulesInitializers = true;
        bool RewriteMSCorLibMethods = true;
        bool RewriteModulesEntrypoint = true;

        // Log options
        bool LogDebugIsEnabled = true;
        std::function<void(const std::string& str)> LogDebugCallback = nullptr;
        std::function<void(const std::string& str)> LogInfoCallback = nullptr;
        std::function<void(const std::string& str)> LogErrorCallback = nullptr;

        // .NET Framework
        bool IsNet46OrGreater = true;

        // NGEN support
        bool DisableNGENImagesSupport = false;
    };

    struct EntryPointItem
    {
        ModuleID ModuleId;
        mdToken Token;
    };

    class Loader {
    private:
        RuntimeInfo _runtimeInformation;
        ICorProfilerInfo4* _pCorProfilerInfo;

        std::mutex _loadersLoadedMutex;
        std::unordered_set<AppDomainID> _loadersLoadedSet;
        std::vector<EntryPointItem> _processedEntryPoints;
        AssemblyMetadata _corlibMetadata{};

        std::vector<WSTRING> _assemblyStringDefaultAppDomainVector;
        std::vector<WSTRING> _assemblyStringNonDefaultAppDomainVector;

        const LoaderOptions _loaderOptions;

        FunctionID _specificMethodToInjectFunctionId;

        LoaderResourceMonikerIDs _resourceMonikerIDs;

        const WCHAR* _pNativeProfilerLibraryFilename;

        static Loader* s_singletonInstance;

        static Loader* CreateNewLoaderInstance(
                    ICorProfilerInfo4* pCorProfilerInfo,
                    const LoaderOptions& loaderOptions,
                    const LoaderResourceMonikerIDs& resourceMonikerIDs,
                    const WCHAR* pNativeProfilerLibraryFilename,
                    const std::vector<WSTRING>& nonIISAssemblyStringDefaultAppDomainVector,
                    const std::vector<WSTRING>& nonIISAssemblyStringNonDefaultAppDomainVector,
                    const std::vector<WSTRING>& iisAssemblyStringDefaultAppDomainVector,
                    const std::vector<WSTRING>& iisAssemblyStringNonDefaultAppDomainVector);

        Loader(
                    ICorProfilerInfo4* pCorProfilerInfo,
                    const std::vector<WSTRING>& assemblyStringDefaultAppDomainVector,
                    const std::vector<WSTRING>& assemblyStringNonDefaultAppDomainVector,
                    const LoaderOptions& loaderOptions,
                    const LoaderResourceMonikerIDs& resourceMonikerIDs,
                    const WCHAR* pNativeProfilerLibraryFilename);

        inline void Debug(const std::string& value) {
            if (_loaderOptions.LogDebugIsEnabled && _loaderOptions.LogDebugCallback != nullptr)
            {
                _loaderOptions.LogDebugCallback(value);
            }
        }

        inline void Info(const std::string& value) {
            if (_loaderOptions.LogInfoCallback != nullptr)
            {
                _loaderOptions.LogInfoCallback(value);
            }
        }

        inline void Error(const std::string& value) {
            if (_loaderOptions.LogErrorCallback != nullptr)
            {
                _loaderOptions.LogErrorCallback(value);
            }
        }

        HRESULT EmitLoaderCallInMethod(ModuleID moduleId, mdMethodDef methodDef, mdMethodDef loaderMethodDef);

        HRESULT EmitLoaderInModule(
            const ComPtr<IMetaDataImport2> metadataImport,
            const ComPtr<IMetaDataEmit2> metadataEmit,
            ModuleID moduleId,
            AppDomainID appDomainId,
            WSTRING assemblyNameString);

        HRESULT EmitDDLoadInitializationAssembliesMethod(
            const ModuleID moduleId,
            mdTypeDef typeDef,
            WSTRING assemblyName,
            mdMethodDef* pLoaderMethodDef,
            mdMemberRef* pSecuritySafeCriticalCtorMemberRef);

        HRESULT GetGetAssemblyAndSymbolsBytesMethodDef(
            const ComPtr<IMetaDataEmit2> metadataEmit,
            mdTypeDef typeDef,
            mdMethodDef* pGetAssemblyAndSymbolsBytesMethodDef);

        HRESULT WriteAssembliesStringArray(
            ILRewriterWrapper& rewriterWrapper,
            const ComPtr<IMetaDataEmit2> metadataEmit,
            const std::vector<WSTRING>& assemblyStringVector,
            mdTypeRef stringTypeRef);

        HRESULT EmitModuleCCtorMethod(
            const ModuleID moduleId,
            mdTypeDef typeDef,
            AppDomainID appDomainId,
            mdMethodDef loaderMethodDef);

        mdToken GetModuleEntryPointToken(LPCBYTE moduleBaseLoadAddress, DWORD moduleFlags);

        inline RuntimeInfo GetRuntimeInformation() {
            COR_PRF_RUNTIME_TYPE runtimeType;
            USHORT majorVersion;
            USHORT minorVersion;
            USHORT buildVersion;
            USHORT qfeVersion;

            HRESULT hr = _pCorProfilerInfo->GetRuntimeInformation(nullptr, &runtimeType, &majorVersion, &minorVersion, &buildVersion, &qfeVersion, 0, nullptr, nullptr);
            if (FAILED(hr)) {
                return {};
            }

            return { runtimeType, majorVersion, minorVersion, buildVersion, qfeVersion };
        }

    public:
        static void CreateNewSingletonInstance(
                    ICorProfilerInfo4* pCorProfilerInfo,
                    const LoaderOptions& loaderOptions,
                    const LoaderResourceMonikerIDs& resourceMonikerIDs,
                    const WCHAR* pNativeProfilerLibraryFilename,
                    const std::vector<WSTRING>& nonIISAssemblyStringDefaultAppDomainVector,
                    const std::vector<WSTRING>& nonIISAssemblyStringNonDefaultAppDomainVector,
                    const std::vector<WSTRING>& iisAssemblyStringDefaultAppDomainVector,
                    const std::vector<WSTRING>& iisAssemblyStringNonDefaultAppDomainVector);

        static Loader* GetSingletonInstance();
        static void DeleteSingletonInstance();

        HRESULT InjectLoaderToModuleInitializer(const ModuleID moduleId);
        HRESULT HandleJitCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction);

        bool GetAssemblyAndSymbolsBytes(void** ppAssemblyArray, int* pAssemblySize, void** ppSymbolsArray, int* pSymbolsSize, WCHAR* pModuleName);

        DWORD GetLoaderProfilerEventMask()
        {
            DWORD eventMask = COR_PRF_MONITOR_MODULE_LOADS | COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST;
            if (_loaderOptions.DisableNGENImagesSupport || !_loaderOptions.IsNet46OrGreater)
            {
                eventMask |= COR_PRF_DISABLE_ALL_NGEN_IMAGES;
            }
            else
            {
                eventMask |= COR_PRF_MONITOR_CACHE_SEARCHES;
            }

            return eventMask;
        }
    };

}  // namespace shared

#endif // DD_CLR_PROFILER_LOADER_H_