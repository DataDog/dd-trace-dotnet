#include "loader.h"

#ifdef _WIN32
#include "DllMain.h"
#endif

#include "il_rewriter_wrapper.h"

#ifdef MACOS
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

#define STRINGMAXSIZE 1024

namespace shared
{
    Loader* Loader::s_singletonInstance = nullptr;

#ifdef LINUX
    extern uint8_t dll_start[] asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_dll_start");
    extern uint8_t dll_end[]   asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_dll_end");

    extern uint8_t pdb_start[] asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_pdb_start");
    extern uint8_t pdb_end[]   asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_pdb_end");
#endif

    const WSTRING _managedLoaderAssemblyName = WStr("Datadog.AutoInstrumentation.ManagedLoader");

    // We exclude here the direct references of the loader to avoid a cyclic reference problem.
    // Also well-known assemblies we want to avoid.
    const WSTRING _assembliesExclusionList[] =
    {
        WStr("netstandard"),
        WStr("System"),
        WStr("System.Core"),
        WStr("Datadog.AutoInstrumentation.Profiler.Managed"),
    };

    constexpr const WCHAR* SpecificTypeToInjectName = WStr("System.AppDomain");
    constexpr const WCHAR* SpecificMethodToInjectName = WStr("IsCompatibilitySwitchSet");


    static Enumerator<mdMethodDef> EnumMethodsWithName(
        const ComPtr<IMetaDataImport2>& metadata_import,
        const mdToken& parent_token, LPCWSTR method_name) {
        return Enumerator<mdMethodDef>(
            [metadata_import, parent_token, method_name](HCORENUM* ptr, mdMethodDef arr[],
                ULONG max, ULONG* cnt) -> HRESULT {
                    return metadata_import->EnumMethodsWithName(ptr, parent_token, method_name, arr, max, cnt);
            },
            [metadata_import](HCORENUM ptr) -> void {
                metadata_import->CloseEnum(ptr);
            });
    }

    void Loader::CreateNewSingletonInstance(
        ICorProfilerInfo4* pCorProfilerInfo,
        bool logDebugIsEnabled,
        std::function<void(const std::string& str)> logDebugCallback,
        std::function<void(const std::string& str)> logInfoCallback,
        std::function<void(const std::string& str)> logErrorCallback,
        const LoaderResourceMonikerIDs& resourceMonikerIDs,
        const WCHAR* pNativeProfilerLibraryFilename,
        const std::vector<WSTRING>& nonIISAssemblyStringDefaultAppDomainVector,
        const std::vector<WSTRING>& nonIISAssemblyStringNonDefaultAppDomainVector,
        const std::vector<WSTRING>& iisAssemblyStringDefaultAppDomainVector,
        const std::vector<WSTRING>& iisAssemblyStringNonDefaultAppDomainVector)
    {
        Loader* newSingletonInstance = Loader::CreateNewLoaderInstance(pCorProfilerInfo,
                                                                       logDebugIsEnabled,
                                                                       logDebugCallback,
                                                                       logInfoCallback,
                                                                       logErrorCallback,
                                                                       resourceMonikerIDs,
                                                                       pNativeProfilerLibraryFilename,
                                                                       nonIISAssemblyStringDefaultAppDomainVector,
                                                                       nonIISAssemblyStringNonDefaultAppDomainVector,
                                                                       iisAssemblyStringDefaultAppDomainVector,
                                                                       iisAssemblyStringNonDefaultAppDomainVector);

        Loader::DeleteSingletonInstance();
        Loader::s_singletonInstance = newSingletonInstance;
    }

    Loader* Loader::GetSingletonInstance()
    {
        Loader* singletonInstance = Loader::s_singletonInstance;
        if (singletonInstance != nullptr)
        {
            return singletonInstance;
        }

        throw std::logic_error("No singleton instance of Loader has been created, or it has already been deleted.");
    }

    void Loader::DeleteSingletonInstance(void)
    {
        Loader* singletonInstance = Loader::s_singletonInstance;
        if (singletonInstance != nullptr)
        {
            Loader::s_singletonInstance = nullptr;
            delete singletonInstance;
        }
    }

    Loader* Loader::CreateNewLoaderInstance(
        ICorProfilerInfo4* pCorProfilerInfo,
        bool logDebugIsEnabled,
        std::function<void(const std::string& str)> logDebugCallback,
        std::function<void(const std::string& str)> logInfoCallback,
        std::function<void(const std::string& str)> logErrorCallback,
        const LoaderResourceMonikerIDs& resourceMonikerIDs,
        const WCHAR* pNativeProfilerLibraryFilename,
        const std::vector<WSTRING>& nonIISAssemblyStringDefaultAppDomainVector,
        const std::vector<WSTRING>& nonIISAssemblyStringNonDefaultAppDomainVector,
        const std::vector<WSTRING>& iisAssemblyStringDefaultAppDomainVector,
        const std::vector<WSTRING>& iisAssemblyStringNonDefaultAppDomainVector)
    {
        WSTRING processName = GetCurrentProcessName();

        const bool isIIS = processName == WStr("w3wp.exe") || processName == WStr("iisexpress.exe");

        if (logInfoCallback != nullptr)
        {
            logInfoCallback("Loader::InjectLoaderToModuleInitializer: Process name: " + ToString(processName));
            if (isIIS)
            {
                logInfoCallback("Loader::InjectLoaderToModuleInitializer: IIS process detected.");
            }
        }

        return new Loader(pCorProfilerInfo,
                          isIIS ? iisAssemblyStringDefaultAppDomainVector : nonIISAssemblyStringDefaultAppDomainVector,
                          isIIS ? iisAssemblyStringNonDefaultAppDomainVector : nonIISAssemblyStringNonDefaultAppDomainVector,
                          logDebugIsEnabled,
                          logDebugCallback,
                          logInfoCallback,
                          logErrorCallback,
                          resourceMonikerIDs,
                          pNativeProfilerLibraryFilename);
    }

    Loader::Loader(
        ICorProfilerInfo4* pCorProfilerInfo,
        const std::vector<WSTRING>& assemblyStringDefaultAppDomainVector,
        const std::vector<WSTRING>& assemblyStringNonDefaultAppDomainVector,
        bool logDebugIsEnabled,
        std::function<void(const std::string& str)> logDebugCallback,
        std::function<void(const std::string& str)> logInfoCallback,
        std::function<void(const std::string& str)> logErrorCallback,
        const LoaderResourceMonikerIDs& resourceMonikerIDs,
        const WCHAR* pNativeProfilerLibraryFilename)
        :
        _logDebugIsEnabled{ logDebugIsEnabled },
        _specificMethodToInjectFunctionId { 0 }
    {
        _resourceMonikerIDs = LoaderResourceMonikerIDs(resourceMonikerIDs);
        _pCorProfilerInfo = pCorProfilerInfo;
        _assemblyStringDefaultAppDomainVector = assemblyStringDefaultAppDomainVector;
        _assemblyStringNonDefaultAppDomainVector = assemblyStringNonDefaultAppDomainVector;
        _logDebugCallback = logDebugCallback;
        _logInfoCallback = logInfoCallback;
        _logErrorCallback = logErrorCallback;
        _runtimeInformation = GetRuntimeInformation();
        _pNativeProfilerLibraryFilename = pNativeProfilerLibraryFilename;

        if (pNativeProfilerLibraryFilename == nullptr)
        {
            Error("No native profiler library filename was provided. You must pass one to the loader.");
            throw std::runtime_error("No native profiler library filename was provided. You must pass one to the loader.");
        }
    }

    HRESULT Loader::InjectLoaderToModuleInitializer(const ModuleID moduleId)
    {
        //
        // global lock
        //
        std::lock_guard<std::mutex> guard(_loadersLoadedMutex);

        // **************************************************************************************************************
        //
        // retrieve AssemblyID from ModuleID
        //
        std::string moduleIdHex = "0x" + HexStr(moduleId);

        AssemblyID assemblyId = 0;
        HRESULT hr = this->_pCorProfilerInfo->GetModuleInfo2(moduleId, NULL, 0, NULL, NULL, &assemblyId, NULL);
        if (FAILED(hr))
        {
            Error("Loader::InjectLoaderToModuleInitializer: failed fetching AssemblyID for ModuleID=" + moduleIdHex);
            return hr;
        }
        std::string assemblyIdHex = "0x" + HexStr(assemblyId);


        // **************************************************************************************************************
        //
        // retrieve AppDomainID from AssemblyID
        //
        AppDomainID appDomainId = 0;
        WCHAR assemblyName[STRINGMAXSIZE];
        ULONG assemblyNameLength = 0;
        hr = this->_pCorProfilerInfo->GetAssemblyInfo(assemblyId, STRINGMAXSIZE, &assemblyNameLength, assemblyName, &appDomainId, NULL);
        if (FAILED(hr))
        {
            Error("Loader::InjectLoaderToModuleInitializer: failed fetching AppDomainID for AssemblyID=" + assemblyIdHex);
            return hr;
        }
        WSTRING assemblyNameString = WSTRING(assemblyName);
        std::string appDomainIdHex = "0x" + HexStr(appDomainId);


        // **************************************************************************************************************
        //
        // check if the module is not the loader itself
        //
        if (assemblyNameString == _managedLoaderAssemblyName)
        {
            if (_logDebugIsEnabled)
            {
                Debug("Loader::InjectLoaderToModuleInitializer: The module is the loader itself, skipping it.");
            }

            return S_OK;
        }

        //
        // check if the loader has been already loaded for this AppDomain
        //
        if (_loadersLoadedSet.find(appDomainId) != _loadersLoadedSet.end())
        {
            if (_logDebugIsEnabled)
            {
                Debug("Loader::InjectLoaderToModuleInitializer: The loader was already loaded in the AppDomain.  [AppDomainID=" + appDomainIdHex + "]");
            }

            return S_OK;
        }

        //
        // skip libraries from the exclusion list.
        //
        for (const auto asm_name : _assembliesExclusionList)
        {
            if (assemblyNameString == asm_name)
            {
                if (_logDebugIsEnabled)
                {
                    Debug("Loader::InjectLoaderToModuleInitializer: Skipping " + ToString(assemblyNameString) + " [AppDomainID=" + appDomainIdHex + "]");
                }

                return S_FALSE;
            }
        }

        // **************************************************************************************************************
        //
        // Retrieve the metadata interfaces for the ModuleID
        //
        ComPtr<IUnknown> metadataInterfaces;
        hr = this->_pCorProfilerInfo->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataImport2, metadataInterfaces.GetAddressOf());
        if (FAILED(hr))
        {
            Error("Loader::InjectLoaderToModuleInitializer: failed fetching metadata interfaces for ModuleID=" + moduleIdHex);
            return hr;
        }

        //
        // Extract both IMetaDataImport2 and IMetaDataEmit2 interfaces
        //
        const ComPtr<IMetaDataImport2> metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        const ComPtr<IMetaDataEmit2> metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        const ComPtr<IMetaDataAssemblyImport> assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);

        //
        // Check and store assembly metadata if the corlib is found.
        //
        if (assemblyNameString == WStr("mscorlib") || assemblyNameString == WStr("System.Private.CoreLib"))
        {
            if (_logDebugIsEnabled)
            {
                Debug("Loader::InjectLoaderToModuleInitializer: extracting metadata of the corlib assembly: " + ToString(assemblyNameString));
            }

            mdAssembly corLibAssembly;
            assemblyImport->GetAssemblyFromScope(&corLibAssembly);

            _corlibMetadata.ModuleId = moduleId;
            _corlibMetadata.Id = assemblyId;
            _corlibMetadata.Token = corLibAssembly;
            _corlibMetadata.AppDomainId = appDomainId;

            WCHAR name[STRINGMAXSIZE];
            DWORD name_len = 0;

            hr = assemblyImport->GetAssemblyProps(
                _corlibMetadata.Token,
                &_corlibMetadata.pPublicKey,
                &_corlibMetadata.PublicKeyLength,
                &_corlibMetadata.HashAlgId,
                name,
                STRINGMAXSIZE,
                &name_len,
                &_corlibMetadata.Metadata,
                &_corlibMetadata.Flags);
            if (FAILED(hr) || assemblyNameString != WSTRING(name))
            {
                Error("Loader::InjectLoaderToModuleInitializer: failed fetching metadata for corlib assembly: " + ToString(assemblyNameString));
                return hr;
            }

            _corlibMetadata.Name = WSTRING(name);

            if (_logDebugIsEnabled)
            {
                Debug("Loader::InjectLoaderToModuleInitializer: [" +
                      ToString(_corlibMetadata.Name) + ", " +
                      ToString(_corlibMetadata.Metadata.usMajorVersion) + ", " +
                      ToString(_corlibMetadata.Metadata.usMinorVersion) + ", " +
                      ToString(_corlibMetadata.Metadata.usRevisionNumber) + ", " +
                      ToString(_corlibMetadata.Metadata.usBuildNumber) +
                      "]");
            }

            mdTypeDef appDomainTypeDef;
            hr = metadataImport->FindTypeDefByName(SpecificTypeToInjectName, mdTokenNil, &appDomainTypeDef);
            if (FAILED(hr))
            {
                Debug("Loader::InjectLoaderToModuleInitializer: " + ToString(SpecificTypeToInjectName) + " not found.");
                return S_FALSE;
            }

            auto enumMethods = EnumMethodsWithName(metadataImport, appDomainTypeDef, SpecificMethodToInjectName);
            auto enumIterator = enumMethods.begin();
            if (enumIterator != enumMethods.end()) {
                auto methodDef = *enumIterator;

                //
                // get a TypeRef for System.Object
                //
                mdTypeRef systemObjectTypeRef;
                hr = metadataEmit->DefineTypeRefByName(corLibAssembly, WStr("System.Object"), &systemObjectTypeRef);
                if (FAILED(hr))
                {
                    Error("Loader::InjectLoaderToModuleInitializer: failed to define typeref: System.Object");
                    return hr;
                }

                //
                // Define a new TypeDef DD_LoaderMethodsType that extends System.Object
                //
                mdTypeDef newTypeDef;
                hr = metadataEmit->DefineTypeDef(WStr("DD_LoaderMethodsType"), tdAbstract | tdSealed, systemObjectTypeRef, NULL, &newTypeDef);
                if (FAILED(hr)) {
                    Error("Loader::InjectLoaderToModuleInitializer: failed to define typedef: DD_LoaderMethodsType");
                    return hr;
                }

                //
                // Emit the DD_LoaderMethodsType.DD_LoadInitializationAssemblies() mdMethodDef
                //
                mdMethodDef loaderMethodDef;
                mdMemberRef securitySafeCriticalCtorMemberRef;
                hr = EmitDDLoadInitializationAssembliesMethod(moduleId, newTypeDef, assemblyNameString, &loaderMethodDef, &securitySafeCriticalCtorMemberRef);
                if (FAILED(hr))
                {
                    return hr;
                }

                //
                // Emit Call to the loader.
                //
                hr = EmitLoaderCallInMethod(moduleId, methodDef, loaderMethodDef);
                if (SUCCEEDED(hr))
                {
                    Info("Loader::InjectLoaderToModuleInitializer: Loader injected successfully (in " +
                         ToString(SpecificTypeToInjectName) + "." + ToString(SpecificMethodToInjectName) + "). [ModuleID=" + moduleIdHex +
                         ", AssemblyID=" + assemblyIdHex +
                         ", AssemblyName=" + ToString(assemblyNameString) +
                         ", AppDomainID=" + appDomainIdHex +
                         ", methodDef=" + HexStr(methodDef) +
                         ", loaderMethodDef=" + HexStr(loaderMethodDef) +
                         "]");
                }
            }

            return hr;
        }

        // **************************************************************************************************************
        //
        // We need to rewrite the <Module> to something like this:
        //
        //  using System;
        //  using System.Reflection;
        //  using System.Runtime.InteropServices;
        //
        //  [SecuritySafeCritical]
        //  class <Module> {
        //
        //      [DllImport("NativeProfilerFile.extension", CharSet = CharSet.Unicode)]
        //      static extern bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize, string moduleName);
        //
        //      static <Module>() {
        //          DD_LoadInitializationAssemblies();
        //      }
        //
        //      static void DD_LoadInitializationAssemblies()
        //      {
        //          if (GetAssemblyAndSymbolsBytes(out var assemblyPtr, out var assemblySize, out var symbolsPtr, out var symbolsSize, "[ModuleName]"))
        //          {
        //              byte[] assemblyBytes = new byte[assemblySize];
        //              Marshal.Copy(assemblyPtr, assemblyBytes, 0, assemblySize);
        //
        //              byte[] symbolsBytes = new byte[symbolsSize];
        //              Marshal.Copy(symbolsPtr, symbolsBytes, 0, symbolsSize);
        //
        //              Assembly loadedAssembly = Assembly.Load(assemblyBytes, symbolsBytes);
        //              loadedAssembly
        //                  .GetType("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader", true)
        //                  .GetMethod("Run")
        //                  .Invoke(null, new object[] {
        //                      new string[] { "Assembly01", "Assembly02" },
        //                      new string[] { "Assembly11", "Assembly12" }
        //                  });
        //          }
        //      }
        //
        //  }
        //
        // **************************************************************************************************************
        hr = EmitLoaderInModule(metadataImport, metadataEmit, moduleId, appDomainId, assemblyNameString);
        if (FAILED(hr))
        {
            return hr;
        }

        Info("Loader::InjectLoaderToModuleInitializer: Loader injected successfully. [ModuleID=" + moduleIdHex +
              ", AssemblyID=" + assemblyIdHex +
              ", AssemblyName=" + ToString(assemblyNameString) +
              ", AppDomainID=" + appDomainIdHex +
              "]");

        return S_OK;
    }

    HRESULT Loader::HandleJitCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction)
    {
        // Some of the logging in this method is a little too verbode even for debug.
        // Turn it on here if you need it for investigations.
        constexpr bool ExtraVerboseLogging = false;

        if (nullptr == pbUseCachedFunction)
        {
            return S_FALSE;
        }

        if (0 != _specificMethodToInjectFunctionId)
        {
            bool disableNGenForFunction = (functionId == _specificMethodToInjectFunctionId);

            if (ExtraVerboseLogging && _logDebugIsEnabled)
            {
                Debug("Loader::HandleJitCachedFunctionSearchStarted:"
                      " (functionId=" + ToString(functionId) + ")"
                      " executed the fast path and resulted in disableNGenForFunction=" + (disableNGenForFunction ? "True" : "False") + ".");
            }

            *pbUseCachedFunction = *pbUseCachedFunction && !disableNGenForFunction;
            return S_OK;
        }

        HRESULT hr;

        ModuleID moduleId;
        mdToken functionToken;
        hr = _pCorProfilerInfo->GetFunctionInfo(functionId, NULL, &moduleId, &functionToken);
        if (FAILED(hr))
        {
            Error("Loader::HandleJitCachedFunctionSearchStarted: Call to GetFunctionInfo(..) returned a FAILED HResult: " + ToString(hr) + ".");
            return S_FALSE;
        }

        ComPtr<IUnknown> metadataInterfaces;
        hr = _pCorProfilerInfo->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataImport2, metadataInterfaces.GetAddressOf());
        if (FAILED(hr))
        {
            Error("Loader::HandleJitCachedFunctionSearchStarted: Call to GetModuleMetaData(..) returned a FAILED HResult: " + ToString(hr) + ".");
            return S_FALSE;
        }

        const ComPtr<IMetaDataImport2> metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        const ComPtr<IMetaDataAssemblyImport> assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);

        constexpr DWORD NameBuffSize = 1024;

        mdToken functionParentToken;
        WCHAR functionName[NameBuffSize]{};
        DWORD functionNameLength = 0;
        hr = metadataImport->GetMemberProps(functionToken, &functionParentToken, functionName, NameBuffSize, &functionNameLength,
                nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr);
        if (FAILED(hr))
        {
            Error("Loader::HandleJitCachedFunctionSearchStarted: Call to GetMemberProps(..) returned a FAILED HResult: " + ToString(hr) + ".");
            return S_FALSE;
        }

        mdToken typeParentToken = mdTokenNil;
        WCHAR typeName[NameBuffSize]{};
        DWORD typeNameLength = 0;
        DWORD typeFlags;
        hr = metadataImport->GetTypeDefProps(functionParentToken, typeName, NameBuffSize, &typeNameLength, &typeFlags, NULL);
        if (FAILED(hr))
        {
            Error("Loader::HandleJitCachedFunctionSearchStarted: Call to GetTypeDefProps(..) returned a FAILED HResult: " + ToString(hr) + ".");
            return S_FALSE;
        }

        bool disableNGenForFunction = (0 == memcmp(functionName, SpecificMethodToInjectName, sizeof(WCHAR) * (std::min)(NameBuffSize, functionNameLength)))
                                        && (0 == memcmp(typeName, SpecificTypeToInjectName, sizeof(WCHAR) * (std::min)(NameBuffSize, typeNameLength)));

        if (disableNGenForFunction)
        {
            _specificMethodToInjectFunctionId = functionId;
        }

        if ((ExtraVerboseLogging || disableNGenForFunction) && _logDebugIsEnabled)
        {
            Debug("Loader::HandleJitCachedFunctionSearchStarted:"
                  " (functionId=" + ToString(functionId) + ","
                  " functionMoniker=\"" + ToString(typeName) + "." + ToString(functionName) + "\")"
                  " resulted in disableNGenForFunction=" + (disableNGenForFunction ? "True" : "False") + ".");
        }

        *pbUseCachedFunction = *pbUseCachedFunction && !disableNGenForFunction;
        return S_OK;
    }

    //

    HRESULT Loader::EmitLoaderCallInMethod(ModuleID moduleId, mdMethodDef methodDef, mdMethodDef loaderMethodDef) {
        HRESULT hr;
        std::string moduleIdHex = "0x" + HexStr(moduleId);

        //
        // rewrite method IsCompatibilitySwitchSet to call the startup loader.
        //
        ILRewriter rewriter(this->_pCorProfilerInfo, nullptr, moduleId, methodDef);
        hr = rewriter.Import();
        if (FAILED(hr))
        {
            Error("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Import() failed for ModuleID=" + moduleIdHex +
                ", methodDef=0x" + HexStr(methodDef));
            return hr;
        }

        ILRewriterWrapper rewriterWrapper(&rewriter);
        rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);
        rewriterWrapper.CallMember(loaderMethodDef, false);

        hr = rewriter.Export();
        if (FAILED(hr))
        {
            Error("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Export() failed for ModuleID=" + moduleIdHex +
                ", methodDef=0x" + HexStr(methodDef));
        }
        return hr;
    }

    HRESULT Loader::EmitLoaderInModule(
        const ComPtr<IMetaDataImport2> metadataImport,
        const ComPtr<IMetaDataEmit2> metadataEmit,
        ModuleID moduleId,
        AppDomainID appDomainId,
        WSTRING assemblyNameString) {

        HRESULT hr;
        std::string moduleIdHex = "0x" + HexStr(moduleId);

        //
        // Gets the mdTypeDef of <Module> type
        //
        const LPCWSTR moduleTypeName = WStr("<Module>");
        mdTypeDef moduleTypeDef = mdTypeDefNil;
        hr = metadataImport->FindTypeDefByName(moduleTypeName, mdTokenNil, &moduleTypeDef);
        if (FAILED(hr))
        {
            Error("Loader::EmitLoaderInModule: failed fetching " + ToString(moduleTypeName) + " (module type) typedef for ModuleID=" + moduleIdHex);
            return hr;
        }


        //
        // Emit the <Module>.DD_LoadInitializationAssemblies() mdMethodDef
        //
        mdMethodDef loaderMethodDef;
        mdMemberRef securitySafeCriticalCtorMemberRef;
        hr = EmitDDLoadInitializationAssembliesMethod(moduleId, moduleTypeDef, assemblyNameString, &loaderMethodDef, &securitySafeCriticalCtorMemberRef);
        if (FAILED(hr))
        {
            return hr;
        }


        //
        // Set SecuritySafeCriticalAttribute to the loader method.
        //
        mdCustomAttribute securityCriticalAttribute;
        hr = metadataEmit->DefineCustomAttribute(moduleTypeDef, securitySafeCriticalCtorMemberRef, nullptr, 0, &securityCriticalAttribute);
        if (FAILED(hr))
        {
            Error("Loader::EmitLoaderInModule: Error creating the security critical attribute for the module type.");
            return hr;
        }

        //
        // Emit the <Module>.cctor() mdMethodDef
        //
        hr = EmitModuleCCtorMethod(moduleId, moduleTypeDef, appDomainId, loaderMethodDef);
        if (FAILED(hr))
        {
            return hr;
        }

        return S_OK;
    }

    HRESULT Loader::EmitDDLoadInitializationAssembliesMethod(
        const ModuleID moduleId,
        mdTypeDef typeDef,
        WSTRING assemblyName,
        mdMethodDef* pLoaderMethodDef,
        mdMemberRef* pSecuritySafeCriticalCtorMemberRef)
    {
        std::string moduleIdHex = "0x" + HexStr(moduleId);

        // **************************************************************************************************************
        //
        // Retrieve the metadata interfaces for the ModuleID
        //
        ComPtr<IUnknown> metadataInterface;
        HRESULT hr = this->_pCorProfilerInfo->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataImport2, metadataInterface.GetAddressOf());
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: failed fetching metadata interfaces for ModuleID=" + moduleIdHex);
            return hr;
        }

        //
        // Extract both IMetaDataImport2 and IMetaDataEmit2 interfaces
        //
        const ComPtr<IMetaDataImport2> metadataImport = metadataInterface.As<IMetaDataImport2>(IID_IMetaDataImport);
        const ComPtr<IMetaDataEmit2> metadataEmit = metadataInterface.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        const ComPtr<IMetaDataAssemblyEmit> assmeblyEmit = metadataInterface.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

        //
        // Get type name
        //
        WCHAR typeName[STRINGMAXSIZE];
        hr = metadataImport->GetTypeDefProps(typeDef, typeName, STRINGMAXSIZE, NULL, nullptr, NULL);
        if (FAILED(hr))
        {
            Info("Loader::EmitDDLoadInitializationAssemblies: Error loading the name for the type. ModuleID=" + moduleIdHex + ", TypeDef=" + ToString(HexStr(typeDef)));
            return hr;
        }
        WSTRING typeNameString = WSTRING(typeName);

        //
        // Check if the static void [Type].DD_LoadInitializationAssemblies() mdMethodDef has been already injected.
        //
        WSTRING loaderMethodName = WSTRING(WStr("DD_LoadInitializationAssemblies_")) + ReplaceString(assemblyName, (WSTRING)WStr("."), (WSTRING)WStr(""));
        COR_SIGNATURE loaderMethodSignature[] =
        {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
        };

        hr = metadataImport->FindMethod(typeDef, loaderMethodName.c_str(), loaderMethodSignature, sizeof(loaderMethodSignature), pLoaderMethodDef);
        if (SUCCEEDED(hr))
        {
            Info("Loader::EmitDDLoadInitializationAssemblies: Loader was already injected in ModuleID=" + moduleIdHex + ", LoaderMethodName=" + ToString(loaderMethodName));
            return hr;
        }

        // **************************************************************************************************************
        //
        //  Emit all definitions required before writing loader method.
        //

        //
        // create assembly ref to mscorlib
        //
        if (_corlibMetadata.Token == mdAssemblyNil)
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: Loader cannot be injected, corlib was not found.");
            return E_FAIL;
        }

        mdAssemblyRef corlibAssemblyRef = mdAssemblyRefNil;
        hr = assmeblyEmit->DefineAssemblyRef(
            _corlibMetadata.pPublicKey,
            _corlibMetadata.PublicKeyLength,
            _corlibMetadata.Name.c_str(),
            &_corlibMetadata.Metadata,
            NULL,
            0,
            _corlibMetadata.Flags,
            &corlibAssemblyRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: Error creating assembly reference to mscorlib.");
            return hr;
        }

        // **************************************************************************************************************
        //
        // TypeRefs
        //

        //
        // get a TypeRef for System.Object
        //
        mdTypeRef systemObjectTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Object"), &systemObjectTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Type
        //
        mdTypeRef systemTypeTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Type"), &systemTypeTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Byte
        //
        mdTypeRef systemByteTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Byte"), &systemByteTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.String
        //
        mdTypeRef systemStringTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.String"), &systemStringTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.Assembly
        //
        mdTypeRef systemReflectionAssemblyTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Reflection.Assembly"), &systemReflectionAssemblyTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.MethodInfo
        //
        mdTypeRef systemReflectionMethodInfoTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Reflection.MethodInfo"), &systemReflectionMethodInfoTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.MethodBase
        //
        mdTypeRef systemReflectionMethodBaseTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Reflection.MethodBase"), &systemReflectionMethodBaseTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Runtime.InteropServices.Marshal
        //
        mdTypeRef systemRuntimeMarshalTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Runtime.InteropServices.Marshal"), &systemRuntimeMarshalTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Security.SecuritySafeCriticalAttribute
        //
        mdTypeRef securitySafeCriticalAttributeTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.Security.SecuritySafeCriticalAttribute"), &securitySafeCriticalAttributeTypeRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineTypeRefByName failed");
            return hr;
        }

        // **************************************************************************************************************
        //
        // MemberRefs
        //
        ULONG offset = 0;

        //
        // get a MemberRef for System.Runtime.InteropServices.Marshal.Copy(IntPtr, Byte[], int, int)
        //
        mdMemberRef marshalCopyMemberRef;
        COR_SIGNATURE marshalCopySignature[] =
        {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                4,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
                ELEMENT_TYPE_I,                 // List of parameter types
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_I4
        };

        hr = metadataEmit->DefineMemberRef(systemRuntimeMarshalTypeRef, WStr("Copy"), marshalCopySignature, sizeof(marshalCopySignature), &marshalCopyMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for Assembly.Load(byte[], byte[])
        //
        BYTE systemReflectionAssemblyTypeRefCompressedToken[10];
        ULONG systemReflectionAssemblyTypeRefCompressedTokenLength = CorSigCompressToken(systemReflectionAssemblyTypeRef, systemReflectionAssemblyTypeRefCompressedToken);

        COR_SIGNATURE assemblyLoadSignature[50];
        offset = 0;
        assemblyLoadSignature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        assemblyLoadSignature[offset++] = 2;
        assemblyLoadSignature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&assemblyLoadSignature[offset], systemReflectionAssemblyTypeRefCompressedToken, systemReflectionAssemblyTypeRefCompressedTokenLength);
        offset += systemReflectionAssemblyTypeRefCompressedTokenLength;
        assemblyLoadSignature[offset++] = ELEMENT_TYPE_SZARRAY;
        assemblyLoadSignature[offset++] = ELEMENT_TYPE_U1;
        assemblyLoadSignature[offset++] = ELEMENT_TYPE_SZARRAY;
        assemblyLoadSignature[offset++] = ELEMENT_TYPE_U1;

        mdMemberRef assemblyLoadMemberRef;
        hr = metadataEmit->DefineMemberRef(systemReflectionAssemblyTypeRef, WStr("Load"), assemblyLoadSignature, offset, &assemblyLoadMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: Assembly.Load(byte[], byte[]) DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for System.Type System.Reflection.Assembly.GetType(string name, bool throwOnError)
        //
        BYTE systemTypeTypeRefCompressedToken[10];
        ULONG systemTypeTypeRefCompressedTokenLength = CorSigCompressToken(systemTypeTypeRef, systemTypeTypeRefCompressedToken);

        COR_SIGNATURE assemblyGetTypeSignature[50];
        offset = 0;
        assemblyGetTypeSignature[offset++] = IMAGE_CEE_CS_CALLCONV_HASTHIS;
        assemblyGetTypeSignature[offset++] = 2;
        assemblyGetTypeSignature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&assemblyGetTypeSignature[offset], systemTypeTypeRefCompressedToken, systemTypeTypeRefCompressedTokenLength);
        offset += systemTypeTypeRefCompressedTokenLength;
        assemblyGetTypeSignature[offset++] = ELEMENT_TYPE_STRING;
        assemblyGetTypeSignature[offset++] = ELEMENT_TYPE_BOOLEAN;

        mdMemberRef assemblyGetTypeMemberRef;
        hr = metadataEmit->DefineMemberRef(systemReflectionAssemblyTypeRef, WStr("GetType"), assemblyGetTypeSignature, offset, &assemblyGetTypeMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: System.Reflection.Assembly.GetType(string name, bool throwOnError) DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for System.Reflection.MethodInfo System.Type.GetMethod(string name)
        //
        BYTE systemReflectionMethodInfoTypeRefCompressedToken[10];
        ULONG systemReflectionMethodInfoTypeRefCompressedTokenLength = CorSigCompressToken(systemReflectionMethodInfoTypeRef, systemReflectionMethodInfoTypeRefCompressedToken);

        COR_SIGNATURE typeGetMethodSignature[50];
        offset = 0;
        typeGetMethodSignature[offset++] = IMAGE_CEE_CS_CALLCONV_HASTHIS;
        typeGetMethodSignature[offset++] = 1;
        typeGetMethodSignature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&typeGetMethodSignature[offset], systemReflectionMethodInfoTypeRefCompressedToken, systemReflectionMethodInfoTypeRefCompressedTokenLength);
        offset += systemReflectionMethodInfoTypeRefCompressedTokenLength;
        typeGetMethodSignature[offset++] = ELEMENT_TYPE_STRING;

        mdMemberRef typeGetMethodMemberRef;
        hr = metadataEmit->DefineMemberRef(systemTypeTypeRef, WStr("GetMethod"), typeGetMethodSignature, offset, &typeGetMethodMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: System.Type.GetMethod(string name) DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for object System.Reflection.MethodBase.Invoke(object instance, object[] args)
        //
        COR_SIGNATURE methodBaseInvokeSignature[] =
        {
            IMAGE_CEE_CS_CALLCONV_HASTHIS,
            2,
            ELEMENT_TYPE_OBJECT,
            ELEMENT_TYPE_OBJECT,
            ELEMENT_TYPE_SZARRAY,
            ELEMENT_TYPE_OBJECT,
        };
        mdMemberRef methodBaseInvokeMemberRef;
        hr = metadataEmit->DefineMemberRef(systemReflectionMethodBaseTypeRef, WStr("Invoke"), methodBaseInvokeSignature, 6, &methodBaseInvokeMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: System.Reflection.MethodBase.Invoke(object instance, object[] args) DefineMemberRef failed");
            return hr;
        }

        //
        // Create method member ref for System.Security.SecuritySafeCriticalAttribute..ctor()
        //
        COR_SIGNATURE ctorSignature[] =
        {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
        };

        hr = metadataEmit->DefineMemberRef(securitySafeCriticalAttributeTypeRef, WStr(".ctor"), ctorSignature, sizeof(ctorSignature), pSecuritySafeCriticalCtorMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: System.Security.SecuritySafeCriticalAttribute..ctor() DefineMemberRef failed");
            return hr;
        }

        // **************************************************************************************************************
        //
        // UserStrings definitions
        //

        //
        // Create a string representing
        // "Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader"
        //
        const LPCWSTR managedLoaderAssemblyLoaderTypeName = WStr("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader");
        mdString managedLoaderAssemblyLoaderTypeNameStringToken;
        hr = metadataEmit->DefineUserString(managedLoaderAssemblyLoaderTypeName, (ULONG)WStrLen(managedLoaderAssemblyLoaderTypeName), &managedLoaderAssemblyLoaderTypeNameStringToken);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: 'Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader' DefineUserString failed");
            return hr;
        }

        //
        // Create a string representing
        // "Run"
        //
        const LPCWSTR runName = WStr("Run");
        mdString runNameStringToken;
        hr = metadataEmit->DefineUserString(runName, (ULONG)WStrLen(runName), &runNameStringToken);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: 'Run' DefineUserString failed");
            return hr;
        }

        //
        // Create a string representing the assembly name
        //
        mdString assemblyNameStringToken;
        hr = metadataEmit->DefineUserString(assemblyName.c_str(), (ULONG)assemblyName.length(), &assemblyNameStringToken);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: AssemblyName DefineUserString failed");
            return hr;
        }


        // **************************************************************************************************************
        //
        // create PInvoke method definition to the native GetAssemblyAndSymbolsBytes method.
        //
        mdMethodDef getAssemblyAndSymbolsBytesPInvokeMethodDef = mdMethodDefNil;
        hr = GetGetAssemblyAndSymbolsBytesMethodDef(metadataEmit, typeDef, &getAssemblyAndSymbolsBytesPInvokeMethodDef);
        if (FAILED(hr))
        {
            return hr;
        }

        if (_logDebugIsEnabled)
        {
            Debug("Loader::EmitDDLoadInitializationAssemblies: Creating " + ToString(typeNameString) + "." + ToString(loaderMethodName) + "() in ModuleID = " + moduleIdHex);
        }

        //
        // If the loader method cannot be found we create [Type].DD_LoadInitializationAssemblies() mdMethodDef
        //
        hr = metadataEmit->DefineMethod(typeDef, loaderMethodName.c_str(), mdStatic | mdPublic, loaderMethodSignature, sizeof(loaderMethodSignature), 0, 0, pLoaderMethodDef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: Error creating the loader method.");
            return hr;
        }

        // **************************************************************************************************************
        //
        // Generate a locals signature defined in the following way:
        //   [0] System.IntPtr ("assemblyPtr" - address of assembly bytes)
        //   [1] System.Int32  ("assemblySize" - size of assembly bytes)
        //   [2] System.IntPtr ("symbolsPtr" - address of symbols bytes)
        //   [3] System.Int32  ("symbolsSize" - size of symbols bytes)
        //   [4] System.Byte[] ("assemblyBytes" - managed byte array for assembly)
        //   [5] System.Byte[] ("symbolsBytes" - managed byte array for symbols)
        //
        mdSignature loaderLocalsSignatureToken;
        COR_SIGNATURE loaderLocalsSignature[] =
        {
                IMAGE_CEE_CS_CALLCONV_LOCAL_SIG,  // Calling convention
                6,                                // Number of variables
                ELEMENT_TYPE_I,                   // List of variable types
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_I,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
        };
        hr = metadataEmit->GetTokenFromSig(loaderLocalsSignature, sizeof(loaderLocalsSignature), &loaderLocalsSignatureToken);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: Unable to generate locals signature. ModuleID=0x" + moduleIdHex);
            return hr;
        }

        // **************************************************************************************************************
        //
        // Write method body
        //
        ILRewriter rewriter(this->_pCorProfilerInfo, nullptr, moduleId, *pLoaderMethodDef);
        rewriter.InitializeTiny();
        rewriter.SetTkLocalVarSig(loaderLocalsSignatureToken);

        ILRewriterWrapper rewriterWrapper(&rewriter);
        rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);

        // Step 1) Call bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize, string moduleName)

        // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index 0)
        rewriterWrapper.LoadLocalAddress(0);
        // ldloca.s 1 : Load the address of the "assemblySize" variable (locals index 1)
        rewriterWrapper.LoadLocalAddress(1);
        // ldloca.s 2 : Load the address of the "symbolsPtr" variable (locals index 2)
        rewriterWrapper.LoadLocalAddress(2);
        // ldloca.s 3 : Load the address of the "symbolsSize" variable (locals index 3)
        rewriterWrapper.LoadLocalAddress(3);
        // ldstr assembly name
        rewriterWrapper.LoadStr(assemblyNameStringToken);
        // call bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize, string moduleName)
        rewriterWrapper.CallMember(getAssemblyAndSymbolsBytesPInvokeMethodDef, false);
        // check if the return of the method call is true or false
        ILInstr* pBranchFalseInstr = rewriterWrapper.CreateInstr(CEE_BRFALSE);

        // Step 2) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the managed assembly bytes

        // ldloc.1 : Load the "assemblySize" variable (locals index 1)
        rewriterWrapper.LoadLocal(1);
        // newarr System.Byte : Create a new Byte[] to hold a managed copy of the assembly data
        rewriterWrapper.CreateArray(systemByteTypeRef);
        // stloc.s 4 : Assign the Byte[] to the "assemblyBytes" variable (locals index 4)
        rewriterWrapper.StLocal(4);
        // ldloc.0 : Load the "assemblyPtr" variable (locals index 0)
        rewriterWrapper.LoadLocal(0);
        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4)
        rewriterWrapper.LoadLocal(4);
        // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
        rewriterWrapper.LoadInt32(0);
        // ldloc.1 : Load the "assemblySize" variable (locals index 1) for the Marshal.Copy length parameter
        rewriterWrapper.LoadLocal(1);
        // call Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
        rewriterWrapper.CallMember(marshalCopyMemberRef, false);

        // Step 3) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the symbols bytes

        // ldloc.3 : Load the "symbolsSize" variable (locals index 3)
        rewriterWrapper.LoadLocal(3);
        // newarr System.Byte : Create a new Byte[] to hold a managed copy of the symbols data
        rewriterWrapper.CreateArray(systemByteTypeRef);
        // stloc.s 5 : Assign the Byte[] to the "symbolsBytes" variable (locals index 5)
        rewriterWrapper.StLocal(5);
        // ldloc.2 : Load the "symbolsPtr" variables (locals index 2)
        rewriterWrapper.LoadLocal(2);
        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5)
        rewriterWrapper.LoadLocal(5);
        // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
        rewriterWrapper.LoadInt32(0);
        // ldloc.3 : Load the "symbolsSize" variable (locals index 3) for the Marshal.Copy length parameter
        rewriterWrapper.LoadLocal(3);
        // call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
        rewriterWrapper.CallMember(marshalCopyMemberRef, false);

        // Step 4) Call System.Reflection.Assembly System.Reflection.Assembly.Load(byte[], byte[]))

        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the first byte[] parameter of AppDomain.Load(byte[], byte[])
        rewriterWrapper.LoadLocal(4);
        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the second byte[] parameter of AppDomain.Load(byte[], byte[])
        rewriterWrapper.LoadLocal(5);
        // callvirt System.Reflection.Assembly System.AppDomain.Load(uint8[], uint8[])
        rewriterWrapper.CallMember(assemblyLoadMemberRef, false);

        // Step 5) Call instance method Assembly.GetType("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader", true)

        // ldstr "Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader"
        rewriterWrapper.LoadStr(managedLoaderAssemblyLoaderTypeNameStringToken);
        // ldc.i4.1 load true for boolean type
        rewriterWrapper.LoadInt32(1);
        // callvirt System.Type System.Reflection.Assembly.GetType(string, bool)
        rewriterWrapper.CallMember(assemblyGetTypeMemberRef, true);

        // Step 6) Call instance method System.Type.GetMethod("Run");

        // ldstr "Run"
        rewriterWrapper.LoadStr(runNameStringToken);
        // callvirt instance class System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string)
        rewriterWrapper.CallMember(typeGetMethodMemberRef, true);

        // Step 7) Call instance method System.Reflection.MethodBase.Invoke(null, new object[] { new string[] { "Assembly1" }, new string[] { "Assembly2" } });

        // ldnull
        rewriterWrapper.LoadNull();
        // create an array of 2 elements with two string[] parameter
        // ldc.i4.2 = const int 2 => array length
        // newarr System.Object
        rewriterWrapper.CreateArray(systemObjectTypeRef, 2);

        // *************************************************************************************************************** FIRST PARAMETER

        // dup
        // ldc.i4.0 = const int 0 => array index 0
        rewriterWrapper.BeginLoadValueIntoArray(0);
        // write string array
        hr = WriteAssembliesStringArray(rewriterWrapper, metadataEmit, _assemblyStringDefaultAppDomainVector, systemStringTypeRef);
        if (FAILED(hr))
        {
            return hr;
        }
        // stelem.ref
        rewriterWrapper.EndLoadValueIntoArray();

        // *************************************************************************************************************** SECOND PARAMETER

        // dup
        // ldc.i4.1 = const int 1 => array index 1
        rewriterWrapper.BeginLoadValueIntoArray(1);
        // write string array
        hr = WriteAssembliesStringArray(rewriterWrapper, metadataEmit, _assemblyStringNonDefaultAppDomainVector, systemStringTypeRef);
        if (FAILED(hr))
        {
            return hr;
        }
        // stelem.ref
        rewriterWrapper.EndLoadValueIntoArray();

        // ***************************************************************************************************************

        // callvirt instance class object System.Reflection.MethodBase::Invoke(object, object[])
        rewriterWrapper.CallMember(methodBaseInvokeMemberRef, true);

        // Step 8) Pop and return

        // pop the returned object
        rewriterWrapper.Pop();
        // return
        pBranchFalseInstr->m_pTarget = rewriterWrapper.Return();

        hr = rewriter.Export();
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: Call to ILRewriter.Export() failed for ModuleID=" + moduleIdHex);
            return hr;
        }

        return hr;
    }

    HRESULT Loader::GetGetAssemblyAndSymbolsBytesMethodDef(
        const ComPtr<IMetaDataEmit2> metadataEmit,
        mdTypeDef typeDef,
        mdMethodDef* pGetAssemblyAndSymbolsBytesMethodDef)
    {
        //
        // create PInvoke method definition to the native GetAssemblyAndSymbolsBytes method (interop.cpp)
        //
        // Define a method on the managed side that will PInvoke into the profiler
        // method:
        // C++: bool GetAssemblyAndSymbolsBytes(void** pAssemblyArray, int* assemblySize, void** pSymbolsArray, int* symbolsSize, string moduleName)
        // C#: static extern void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize, string moduleName)
        const LPCWSTR getAssemblyAndSymbolsBytesName = WStr("GetAssemblyAndSymbolsBytes");
        COR_SIGNATURE getAssemblyAndSymbolsBytesSignature[] =
        {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                5,                              // Number of parameters
                ELEMENT_TYPE_BOOLEAN,           // Return type
                ELEMENT_TYPE_BYREF,             // List of parameter types
                ELEMENT_TYPE_I,
                ELEMENT_TYPE_BYREF,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_BYREF,
                ELEMENT_TYPE_I,
                ELEMENT_TYPE_BYREF,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_STRING
        };

        HRESULT hr = metadataEmit->DefineMethod(typeDef, getAssemblyAndSymbolsBytesName, mdStatic | mdPinvokeImpl | mdHideBySig,
            getAssemblyAndSymbolsBytesSignature, sizeof(getAssemblyAndSymbolsBytesSignature), 0, miPreserveSig, pGetAssemblyAndSymbolsBytesMethodDef);
        if (FAILED(hr))
        {
            Error("Loader::GetGetAssemblyAndSymbolsBytesMethodDef: DefineMethod for GetAssemblyAndSymbolsBytes failed.");
            return hr;
        }

        WSTRING nativeProfilerLibraryFilename = WSTRING(_pNativeProfilerLibraryFilename);

        mdModuleRef nativeProfilerModuleRef;
        hr = metadataEmit->DefineModuleRef(nativeProfilerLibraryFilename.c_str(), &nativeProfilerModuleRef);
        if (FAILED(hr))
        {
            Error("Loader::GetGetAssemblyAndSymbolsBytesMethodDef: Failed! DefineModuleRef for " + ToString(nativeProfilerLibraryFilename));
            return hr;
        }

        hr = metadataEmit->DefinePinvokeMap(*pGetAssemblyAndSymbolsBytesMethodDef, pmCharSetUnicode, getAssemblyAndSymbolsBytesName, nativeProfilerModuleRef);
        if (FAILED(hr))
        {
            Error("Loader::GetGetAssemblyAndSymbolsBytesMethodDef: DefinePinvokeMap for GetAssemblyAndSymbolsBytes failed");
            return hr;
        }

        return hr;
    }

    HRESULT Loader::WriteAssembliesStringArray(
        ILRewriterWrapper& rewriterWrapper,
        const ComPtr<IMetaDataEmit2> metadataEmit,
        const std::vector<WSTRING>& assemblyStringVector,
        mdTypeRef stringTypeRef)
    {
        // ldc.i4 = const int (array length)
        // newarr System.String
        rewriterWrapper.CreateArray(stringTypeRef, (INT32)assemblyStringVector.size());

        // loading array index
        for (ULONG i = 0; i < assemblyStringVector.size(); i++)
        {
            // dup
            // ldc.i4 = const int array index 0
            rewriterWrapper.BeginLoadValueIntoArray(i);

            // Create a string token
            mdString stringToken;
            auto hr = metadataEmit->DefineUserString(assemblyStringVector[i].c_str(), (ULONG)assemblyStringVector[i].size(), &stringToken);
            if (FAILED(hr))
            {
                Error("Loader::WriteAssembliesStringArray: DefineUserString for string array failed");
                return hr;
            }

            // ldstr assembly index value
            rewriterWrapper.LoadStr(stringToken);

            // stelem.ref
            rewriterWrapper.EndLoadValueIntoArray();
        }

        return S_OK;
    }

    HRESULT Loader::EmitModuleCCtorMethod(
        const ModuleID moduleId,
        mdTypeDef typeDef,
        AppDomainID appDomainId,
        mdMethodDef loaderMethodDef)
    {
        std::string moduleIdHex = "0x" + HexStr(moduleId);
        std::string appDomainIdHex = "0x" + HexStr(appDomainId);

        // **************************************************************************************************************
        //
        // Retrieve the metadata interfaces for the ModuleID
        //
        ComPtr<IUnknown> metadataInterfaces;
        HRESULT hr = this->_pCorProfilerInfo->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataImport2, metadataInterfaces.GetAddressOf());
        if (FAILED(hr))
        {
            Error("Loader::EmitModuleCCtor: failed fetching metadata interfaces for ModuleID=" + moduleIdHex);
            return hr;
        }

        //
        // Extract both IMetaDataImport2 and IMetaDataEmit2 interfaces
        //
        const ComPtr<IMetaDataImport2> metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        const ComPtr<IMetaDataEmit2> metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);

        //
        // Check if the <Module> type has already a ..ctor, if not we create an empty one.
        //
        const LPCWSTR cctorName = WStr(".cctor");
        COR_SIGNATURE cctorSignature[] =
        {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
        };

        mdMethodDef cctorMethodDef = mdMethodDefNil;
        hr = metadataImport->FindMethod(typeDef, cctorName, cctorSignature, sizeof(cctorSignature), &cctorMethodDef);
        if (FAILED(hr))
        {
            if (_logDebugIsEnabled)
            {
                Debug("Loader::EmitModuleCCtor: failed fetching <Module>..ctor mdMethodDef, creating new .ctor [ModuleID=" +
                    moduleIdHex + ", AppDomainID=" + appDomainIdHex + "]");
            }

            //
            // Define a new ..ctor for the <Module> type
            //
            hr = metadataEmit->DefineMethod(typeDef, cctorName,
                mdPublic | mdStatic | mdRTSpecialName | mdSpecialName, cctorSignature,
                sizeof(cctorSignature), 0, 0, &cctorMethodDef);

            if (FAILED(hr))
            {
                Error("Loader::EmitModuleCCtor: Error creating .cctor for <Module> [ModuleID=" +
                    moduleIdHex + ", AppDomainID=" + appDomainIdHex + "]");
                return hr;
            }

            //
            // Create a simple method body with only the `ret` opcode instruction.
            //
            ILRewriter rewriter(this->_pCorProfilerInfo, nullptr, moduleId, cctorMethodDef);
            rewriter.InitializeTiny();

            ILRewriterWrapper rewriterWrapper(&rewriter);
            rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);
            rewriterWrapper.Return();

            hr = rewriter.Export();
            if (FAILED(hr))
            {
                Error("Loader::EmitModuleCCtor: ILRewriter.Export failed creating .cctor for <Module> [ModuleID=" +
                    moduleIdHex + ", AppDomainID=" + appDomainIdHex + "]");
                return hr;
            }
        }

        //
        // At this point we have a mdTypeDef for <Module> and a mdMethodDef for the ..ctor
        // that we can rewrite to load the loader
        //
        return EmitLoaderCallInMethod(moduleId, cctorMethodDef, loaderMethodDef);
    }

    //

    bool Loader::GetAssemblyAndSymbolsBytes(void** ppAssemblyArray, int* pAssemblySize, void** ppSymbolsArray, int* pSymbolsSize, WCHAR* pModuleName)
    {
        //
        // global lock
        //
        std::lock_guard<std::mutex> guard(_loadersLoadedMutex);

        //
        // gets module name
        //
        WSTRING moduleName = WSTRING(pModuleName);

        //
        // gets the current thread id
        //
        ThreadID threadId;
        HRESULT hr = this->_pCorProfilerInfo->GetCurrentThreadID(&threadId);
        if (FAILED(hr))
        {
            Error("Loader::GetAssemblyAndSymbolsBytes: ThreadId could not be retrieved.");
        }
        std::string threadIdHex = "0x" + HexStr(threadId);

        //
        // gets the current appdomain id
        //
        AppDomainID appDomainId;
        hr = this->_pCorProfilerInfo->GetThreadAppDomain(threadId, &appDomainId);
        if (FAILED(hr))
        {
            Error("Loader::GetAssemblyAndSymbolsBytes: AppDomainID could not be retrieved.");
        }
        std::string appDomainIdHex = "0x" + HexStr(appDomainId);

        std::string trait = "[AppDomainId=" + appDomainIdHex + ", ThreadId=" + threadIdHex + ", ModuleName=" + ToString(moduleName) + "]";

        //
        // check if the loader has been already loaded for this AppDomain
        //
        if (_loadersLoadedSet.find(appDomainId) != _loadersLoadedSet.end())
        {
            if (_logDebugIsEnabled)
            {
                Debug("Loader::GetAssemblyAndSymbolsBytes: The loader was already loaded. " + trait);
            }

            return false;
        }

        Info("Loader::GetAssemblyAndSymbolsBytes: Loading loader data. " + trait);
        _loadersLoadedSet.insert(appDomainId);

#ifdef _WIN32
        HINSTANCE hInstance = DllHandle;
        LPCWSTR dllLpName;
        LPCWSTR symbolsLpName;

        if (_runtimeInformation.IsDesktop())
        {
          dllLpName = MAKEINTRESOURCE(_resourceMonikerIDs.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll);
          symbolsLpName = MAKEINTRESOURCE(_resourceMonikerIDs.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb);
        }
        else
        {
          dllLpName = MAKEINTRESOURCE(_resourceMonikerIDs.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll);
          symbolsLpName = MAKEINTRESOURCE(_resourceMonikerIDs.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb);
        }

        HRSRC hResAssemblyInfo = FindResource(hInstance, dllLpName, L"ASSEMBLY");
        HGLOBAL hResAssembly = LoadResource(hInstance, hResAssemblyInfo);
        *pAssemblySize = SizeofResource(hInstance, hResAssemblyInfo);
        *ppAssemblyArray = (LPBYTE)LockResource(hResAssembly);

        HRSRC hResSymbolsInfo = FindResource(hInstance, symbolsLpName, L"SYMBOLS");
        HGLOBAL hResSymbols = LoadResource(hInstance, hResSymbolsInfo);
        *pSymbolsSize = SizeofResource(hInstance, hResSymbolsInfo);
        *ppSymbolsArray = (LPBYTE)LockResource(hResSymbols);

        if (_logDebugIsEnabled)
        {
            Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=_WIN32)."
                  " *assemblySize=" + ToString(*pAssemblySize) + ","
                  " *pAssemblyArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppAssemblyArray)) + ","
                  " *symbolsSize=" + ToString(*pSymbolsSize) + ","
                  " *pSymbolsArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppSymbolsArray)) + ".");

            Debug("Loader::GetAssemblyAndSymbolsBytes: resourceMonikerIDs_: _.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll=" + ToString(_resourceMonikerIDs.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll) + ","
                " _.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb=" + ToString(_resourceMonikerIDs.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb) + ","
                " _.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll=" + ToString(_resourceMonikerIDs.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll) + ","
                " _.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb=" + ToString(_resourceMonikerIDs.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb) + ".");
        }

#elif LINUX
        *pAssemblySize = dll_end - dll_start;
        *ppAssemblyArray = (void*)dll_start;

        *pSymbolsSize = pdb_end - pdb_start;
        *ppSymbolsArray = (void*)pdb_start;

        if (_logDebugIsEnabled)
        {
            Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=LINUX)."
                  " *assemblySize=" + ToString(*pAssemblySize) + ", "
                  " *pAssemblyArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppAssemblyArray)) + ", "
                  " *symbolsSize=" + ToString(*pSymbolsSize) + ", "
                  " *pSymbolsArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppSymbolsArray)) + ".");
        }
#elif MACOS
        const unsigned int imgCount = _dyld_image_count();

        std::string nativeProfilerFileMacOS = _pNativeProfilerLibraryFilename;

        for(auto i = 0; i < imgCount; i++)
        {
            const std::string name = std::string(_dyld_get_image_name(i));

            if (name.rfind(nativeProfilerFileMacOS) != std::string::npos)
            {
                const mach_header_64* header = (const struct mach_header_64 *) _dyld_get_image_header(i);

                unsigned long dllSize;
                const auto dllData = getsectiondata(header, "binary", "dll", &dllSize);
                *pAssemblySize = dllSize;
                *ppAssemblyArray = (void*)dllData;

                unsigned long pdbSize;
                const auto pdbData = getsectiondata(header, "binary", "pdb", &pdbSize);
                *pSymbolsSize = pdbSize;
                *ppSymbolsArray = (void*)pdbData;
                break;
            }
        }

        if (_logDebugIsEnabled)
        {
            Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=MACOS)."
                " *assemblySize=" + ToString(*pAssemblySize) + ", "
                " *pAssemblyArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppAssemblyArray)) + ", "
                " *symbolsSize=" + ToString(*pSymbolsSize) + ", "
                " *pSymbolsArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppSymbolsArray)) + ".");
        }
#else
        Error("Loader::GetAssemblyAndSymbolsBytes. Platform not supported.");
        return false;
#endif
        return true;
    }

}