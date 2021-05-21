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
    Loader* Loader::s_singeltonInstance = nullptr;

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


    void Loader::CreateNewSingeltonInstance(
        ICorProfilerInfo4* pCorProfilerInfo,
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
        Loader* newSingeltonInstance = Loader::CreateNewLoaderInstance(pCorProfilerInfo,
            logDebugCallback,
            logInfoCallback,
            logErrorCallback,
            resourceMonikerIDs,
            pNativeProfilerLibraryFilename,
            nonIISAssemblyStringDefaultAppDomainVector,
            nonIISAssemblyStringNonDefaultAppDomainVector,
            iisAssemblyStringDefaultAppDomainVector,
            iisAssemblyStringNonDefaultAppDomainVector);

        Loader::DeleteSingeltonInstance();
        Loader::s_singeltonInstance = newSingeltonInstance;
    }

    Loader* Loader::GetSingeltonInstance()
    {
        Loader* singeltonInstance = Loader::s_singeltonInstance;
        if (singeltonInstance != nullptr)
        {
            return singeltonInstance;
        }

        throw std::logic_error("No singelton instance of Loader has been created, or it has already been deleted.");
    }

    void Loader::DeleteSingeltonInstance(void)
    {
        Loader* singeltonInstance = Loader::s_singeltonInstance;
        if (singeltonInstance != nullptr)
        {
            Loader::s_singeltonInstance = nullptr;
            delete singeltonInstance;
        }
    }

    Loader* Loader::CreateNewLoaderInstance(
        ICorProfilerInfo4* pCorProfilerInfo,
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
        std::function<void(const std::string& str)> logDebugCallback,
        std::function<void(const std::string& str)> logInfoCallback,
        std::function<void(const std::string& str)> logErrorCallback,
        const LoaderResourceMonikerIDs& resourceMonikerIDs,
        const WCHAR* pNativeProfilerLibraryFilename)
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
            Debug("Loader::InjectLoaderToModuleInitializer: The module is the loader itself, skipping it.");
            return E_FAIL;
        }

        //
        // check if the loader has been already loaded for this AppDomain
        //
        if (_loadersLoadedSet.find(appDomainId) != _loadersLoadedSet.end())
        {
            Debug("Loader::InjectLoaderToModuleInitializer: The loader was already loaded in the AppDomain.  [AppDomainID=" + appDomainIdHex + "]");
            return S_OK;
        }

        //
        // skip libraries from the exclusion list.
        //
        for (const auto asm_name : _assembliesExclusionList)
        {
            if (assemblyNameString == asm_name)
            {
                Debug("Loader::InjectLoaderToModuleInitializer: Skipping " + ToString(assemblyNameString) + " [AppDomainID=" + appDomainIdHex + "]");
                return E_FAIL;
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
            Debug("Loader::InjectLoaderToModuleInitializer: extracting metadata of the corlib assembly.");

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
                Error("Loader::InjectLoaderToModuleInitializer: failed fetching metadata for corlib assembly.");
                return hr;
            }

            _corlibMetadata.Name = WSTRING(name);
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
        //              Assembly loadedAssembly = AppDomain.CurrentDomain.Load(assemblyBytes, symbolsBytes);
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

        //
        // Gets the mdTypeDef of <Module> type
        //
        const LPCWSTR moduleTypeName = WStr("<Module>");
        mdTypeDef moduleTypeDef = mdTypeDefNil;
        hr = metadataImport->FindTypeDefByName(moduleTypeName, mdTokenNil, &moduleTypeDef);
        if (FAILED(hr))
        {
            Error("Loader::InjectLoaderToModuleInitializer: failed fetching " + ToString(moduleTypeName) + " (module type) typedef for ModuleID=" + moduleIdHex);
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
            Error("Loader::InjectLoaderToModuleInitializer: Error creating the security critical attribute for the module type.");
            return hr;
        }

        // ***************************************************************************************************************

        //
        // Emit the <Module>.cctor() mdMethodDef
        //
        hr = EmitModuleCCtorMethod(moduleId, moduleTypeDef, appDomainId, loaderMethodDef);
        if (FAILED(hr))
        {
            return hr;
        }

        Info("Loader::InjectLoaderToModuleInitializer: Loader injected successfully. [ModuleID=" + moduleIdHex +
              ", AssemblyID=" + assemblyIdHex +
              ", AssemblyName=" + ToString(assemblyNameString) +
              ", AppDomainID=" + appDomainIdHex +
              ", LoaderMethodDef=0x" + HexStr(loaderMethodDef) +
              ", SecuritySafeCriticalAttributeMemberRef=0x" + HexStr(securitySafeCriticalCtorMemberRef) +
              "]");

        return S_OK;
    }

    //

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
        // Check if the static void <Module>.DD_LoadInitializationAssemblies() mdMethodDef has been already injected.
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
        // get a TypeRef for System.AppDomain
        //
        mdTypeRef systemAppDomainTypeRef;
        hr = metadataEmit->DefineTypeRefByName(corlibAssemblyRef, WStr("System.AppDomain"), &systemAppDomainTypeRef);
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
        // get a MemberRef for System.AppDomain.get_CurrentDomain()
        //
        BYTE systemAppDomainTypeRefCompressedToken[10];
        ULONG systemAppDomainTypeRefCompressedTokenLength = CorSigCompressToken(systemAppDomainTypeRef, systemAppDomainTypeRefCompressedToken);

        COR_SIGNATURE appDomainGetCurrentDomainSignature[50];
        offset = 0;
        appDomainGetCurrentDomainSignature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        appDomainGetCurrentDomainSignature[offset++] = 0;
        appDomainGetCurrentDomainSignature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&appDomainGetCurrentDomainSignature[offset], systemAppDomainTypeRefCompressedToken, systemAppDomainTypeRefCompressedTokenLength);
        offset += systemAppDomainTypeRefCompressedTokenLength;

        mdMemberRef appDomainGetCurrentDomainMemberRef;
        hr = metadataEmit->DefineMemberRef(systemAppDomainTypeRef, WStr("get_CurrentDomain"), appDomainGetCurrentDomainSignature, offset, &appDomainGetCurrentDomainMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: System.AppDomain.get_CurrentDomain() DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for AppDomain.Load(byte[], byte[])
        //
        BYTE systemReflectionAssemblyTypeRefCompressedToken[10];
        ULONG systemReflectionAssemblyTypeRefCompressedTokenLength = CorSigCompressToken(systemReflectionAssemblyTypeRef, systemReflectionAssemblyTypeRefCompressedToken);

        COR_SIGNATURE appDomainLoadSignature[50];
        offset = 0;
        appDomainLoadSignature[offset++] = IMAGE_CEE_CS_CALLCONV_HASTHIS;
        appDomainLoadSignature[offset++] = 2;
        appDomainLoadSignature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&appDomainLoadSignature[offset], systemReflectionAssemblyTypeRefCompressedToken, systemReflectionAssemblyTypeRefCompressedTokenLength);
        offset += systemReflectionAssemblyTypeRefCompressedTokenLength;
        appDomainLoadSignature[offset++] = ELEMENT_TYPE_SZARRAY;
        appDomainLoadSignature[offset++] = ELEMENT_TYPE_U1;
        appDomainLoadSignature[offset++] = ELEMENT_TYPE_SZARRAY;
        appDomainLoadSignature[offset++] = ELEMENT_TYPE_U1;

        mdMemberRef appDomainLoadMemberRef;
        hr = metadataEmit->DefineMemberRef(systemAppDomainTypeRef, WStr("Load"), appDomainLoadSignature, offset, &appDomainLoadMemberRef);
        if (FAILED(hr))
        {
            Error("Loader::EmitDDLoadInitializationAssemblies: AppDomain.Load(byte[], byte[]) DefineMemberRef failed");
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

        Debug("Loader::EmitDDLoadInitializationAssemblies: Creating <Module>." + ToString(loaderMethodName) + "() in ModuleID=" + moduleIdHex);

        //
        // If the loader method cannot be found we create <Module>.DD_LoadInitializationAssemblies() mdMethodDef
        //
        hr = metadataEmit->DefineMethod(typeDef, loaderMethodName.c_str(), mdStatic, loaderMethodSignature, sizeof(loaderMethodSignature), 0, 0, pLoaderMethodDef);
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

        // Step 4) Call System.Reflection.Assembly System.AppDomain.CurrentDomain.Load(byte[], byte[]))

        // call System.AppDomain System.AppDomain.CurrentDomain property
        rewriterWrapper.CallMember(appDomainGetCurrentDomainMemberRef, false);
        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the first byte[] parameter of AppDomain.Load(byte[], byte[])
        rewriterWrapper.LoadLocal(4);
        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the second byte[] parameter of AppDomain.Load(byte[], byte[])
        rewriterWrapper.LoadLocal(5);
        // callvirt System.Reflection.Assembly System.AppDomain.Load(uint8[], uint8[])
        rewriterWrapper.CallMember(appDomainLoadMemberRef, true);

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
            Debug("Loader::EmitModuleCCtor: failed fetching <Module>..ctor mdMethodDef, creating new .ctor [ModuleID=" +
                moduleIdHex + ", AppDomainID=" + appDomainIdHex + "]");

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

        //
        // rewrite ..ctor to call the startup loader.
        //
        ILRewriter rewriter(this->_pCorProfilerInfo, nullptr, moduleId, cctorMethodDef);
        hr = rewriter.Import();
        if (FAILED(hr))
        {
            Error("Loader::EmitModuleCCtor: Call to ILRewriter.Import() failed for ModuleID=" + moduleIdHex +
                ", CCTORMethodDef=0x" + HexStr(cctorMethodDef));
            return hr;
        }

        ILRewriterWrapper rewriterWrapper(&rewriter);
        rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);

        rewriterWrapper.CallMember(loaderMethodDef, false);

        hr = rewriter.Export();
        if (FAILED(hr))
        {
            Error("Loader::EmitModuleCCtor: Call to ILRewriter.Export() failed for ModuleID=" + moduleIdHex +
                ", CCTORMethodDef=0x" + HexStr(cctorMethodDef));
            return hr;
        }

        return hr;
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
            Debug("Loader::GetAssemblyAndSymbolsBytes: The loader was already loaded. " + trait);
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

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=_WIN32)."
              " *assemblySize=" + ToString(*pAssemblySize) + ","
              " *pAssemblyArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppAssemblyArray)) + ","
              " *symbolsSize=" + ToString(*pSymbolsSize) + ","
              " *pSymbolsArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppSymbolsArray)) + ".");

        Debug("Loader::GetAssemblyAndSymbolsBytes: resourceMonikerIDs_: _.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll=" + ToString(_resourceMonikerIDs.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll) + ","
            " _.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb=" + ToString(_resourceMonikerIDs.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb) + ","
            " _.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll=" + ToString(_resourceMonikerIDs.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll) + ","
            " _.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb=" + ToString(_resourceMonikerIDs.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb) + ".");


#elif LINUX
        *pAssemblySize = dll_end - dll_start;
        *ppAssemblyArray = (void*)dll_start;

        *pSymbolsSize = pdb_end - pdb_start;
        *ppSymbolsArray = (void*)pdb_start;

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=LINUX)."
            " *assemblySize=" + ToString(*pAssemblySize) + ", "
            " *pAssemblyArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppAssemblyArray)) + ", "
            " *symbolsSize=" + ToString(*pSymbolsSize) + ", "
            " *pSymbolsArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppSymbolsArray)) + ".");
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

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=MACOS)."
            " *assemblySize=" + ToString(*pAssemblySize) + ", "
            " *pAssemblyArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppAssemblyArray)) + ", "
            " *symbolsSize=" + ToString(*pSymbolsSize) + ", "
            " *pSymbolsArray=" + HexStr(reinterpret_cast<std::uint64_t>(*ppSymbolsArray)) + ".");
#else
        Error("Loader::GetAssemblyAndSymbolsBytes. Platform not supported.");
        return false;
#endif
        return true;
    }

}