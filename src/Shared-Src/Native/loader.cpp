#include "loader.h"

#ifdef _WIN32
#include "DllMain.h"
#endif

#include "il_rewriter_wrapper.h"
//#include "resource.h"

#ifdef MACOS    
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

#define stringMaxSize 1024

namespace shared {
    
    Loader* Loader::s_singeltonInstance = nullptr;
    
#ifdef LINUX
    extern uint8_t dll_start[]                                  asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_dll_start");
    extern uint8_t dll_end[]                                    asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_dll_end");

    extern uint8_t pdb_start[]                                  asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_pdb_start");
    extern uint8_t pdb_end[]                                    asm("_binary_Datadog_AutoInstrumentation_ManagedLoader_pdb_end");
#endif
    
    const WSTRING managed_loader_assembly_name                  = WStr("Datadog.AutoInstrumentation.ManagedLoader");

    const LPCWSTR managed_loader_startup_type                   = WStr("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader");
    const LPCWSTR module_type_name                              = WStr("<Module>");
    const LPCWSTR constructor_name                              = WStr(".cctor");
    const LPCWSTR get_assembly_and_symbols_bytes_name           = WStr("GetAssemblyAndSymbolsBytes");
    const LPCWSTR mscorlib_name                                 = WStr("mscorlib");
    const LPCWSTR system_byte_name                              = WStr("System.Byte");
    const LPCWSTR system_string_name                            = WStr("System.String");
    const LPCWSTR system_runtime_interopservices_marshal_name   = WStr("System.Runtime.InteropServices.Marshal");
    const LPCWSTR copy_name                                     = WStr("Copy");
    const LPCWSTR system_reflection_assembly_name               = WStr("System.Reflection.Assembly");
    const LPCWSTR system_object_name                            = WStr("System.Object");
    const LPCWSTR system_type_name                              = WStr("System.Type");
    const LPCWSTR system_appdomain_name                         = WStr("System.AppDomain");
    const LPCWSTR get_currentdomain_name                        = WStr("get_CurrentDomain");
    const LPCWSTR load_name                                     = WStr("Load");
    const LPCWSTR gettype_name                                  = WStr("GetType");
    const LPCWSTR loader_method_name                            = WStr("DD_LoadInitializationAssemblies");

    const LPCWSTR system_reflection_methodinfo_name             = WStr("System.Reflection.MethodInfo");
    const LPCWSTR system_reflection_methodbase_name             = WStr("System.Reflection.MethodBase");
    const LPCWSTR getmethod_name                                = WStr("GetMethod");
    const LPCWSTR invoke_name                                   = WStr("Invoke");
    const LPCWSTR run_name                                      = WStr("Run");
    
    // We exclude here the direct references of the loader to avoid a cyclic reference problem.
    // Also well-known assemblies we want to avoid.
    const WSTRING assemblies_exclusion_list_[] = {
            WStr("mscorlib"),
            WStr("netstandard"),
            WStr("System.Private.CoreLib"),
            WStr("System"),
            WStr("System.Core"),
            WStr("System.Configuration"),
            WStr("System.Data"),
            WStr("System.EnterpriseServices"),
            WStr("System.Numerics"),
            WStr("System.Runtime.Caching"),
            WStr("System.Security"),
            WStr("System.Transactions"),
            WStr("System.Xml"),
            WStr("System.Web"),
            WStr("System.Web.ApplicationServices"),
    };

    void Loader::CreateNewSingeltonInstance(ICorProfilerInfo4* pCorProfilerInfo,
                                            std::function<void(const std::string& str)> log_debug_callback,
                                            std::function<void(const std::string& str)> log_info_callback,
                                            std::function<void(const std::string& str)> log_warn_callback,
                                            const LoaderResourceMonikerIDs& resource_moniker_ids,
                                            WCHAR const * native_profiler_library_filename)
    {
        Loader* newSingeltonInstance = Loader::CreateNewLoaderInstance(pCorProfilerInfo,
                                                                       log_debug_callback,
                                                                       log_info_callback, 
                                                                       log_warn_callback, 
                                                                       resource_moniker_ids, 
                                                                       native_profiler_library_filename);

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
        std::function<void(const std::string& str)> log_debug_callback,
        std::function<void(const std::string& str)> log_info_callback,
        std::function<void(const std::string& str)> log_warn_callback,
        const LoaderResourceMonikerIDs& resourceMonikerIDs,
        WCHAR const* native_profiler_library_filename) {

        std::vector<WSTRING> assembly_string_default_appdomain_vector;
        std::vector<WSTRING> assembly_string_nondefault_appdomain_vector;

        WSTRING process_name = GetCurrentProcessName();
        const bool is_iis = process_name == WStr("w3wp.exe") ||
            process_name == WStr("iisexpress.exe");

        if (is_iis) {

            assembly_string_default_appdomain_vector = {
                WStr("Datadog.AutoInstrumentation.Profiler.Managed"),
            };
            assembly_string_nondefault_appdomain_vector = {
                WStr("Datadog.AutoInstrumentation.Profiler.Managed"),
            };

        }
        else {

            assembly_string_default_appdomain_vector = {
                WStr("Datadog.AutoInstrumentation.Profiler.Managed"),
            };
            assembly_string_nondefault_appdomain_vector = {
                WStr("Datadog.AutoInstrumentation.Profiler.Managed"),
            };

        }

        return new Loader(pCorProfilerInfo,
            assembly_string_default_appdomain_vector,
            assembly_string_nondefault_appdomain_vector,
            log_debug_callback,
            log_info_callback,
            log_warn_callback,
            resourceMonikerIDs,
            native_profiler_library_filename);
    }

    Loader::Loader(
        ICorProfilerInfo4* info,
        std::vector<WSTRING> assembly_string_default_appdomain_vector,
        std::vector<WSTRING> assembly_string_nondefault_appdomain_vector,
        std::function<void(const std::string& str)> log_debug_callback,
        std::function<void(const std::string& str)> log_info_callback,
        std::function<void(const std::string& str)> log_warn_callback,
        const LoaderResourceMonikerIDs& resourceMonikerIDs,
        WCHAR const * native_profiler_library_filename) {
        resourceMonikerIDs_ = LoaderResourceMonikerIDs(resourceMonikerIDs);
        info_ = info;
        assembly_string_default_appdomain_vector_ = assembly_string_default_appdomain_vector;
        assembly_string_nondefault_appdomain_vector_ = assembly_string_nondefault_appdomain_vector;
        log_debug_callback_ = log_debug_callback;
        log_info_callback_ = log_info_callback;
        log_warn_callback_ = log_warn_callback;
        runtime_information_ = GetRuntimeInformation();
        native_profiler_library_filename_ = native_profiler_library_filename;

        if (native_profiler_library_filename == nullptr)
        {
            Warn("No native profiler library filename was provided. You must pass one to the loader.");
            throw std::runtime_error("No native profiler library filename was provided. You must pass one to the loader.");
        }
    }

    HRESULT Loader::InjectLoaderToModuleInitializer(const ModuleID module_id) {
        //
        // global lock
        //
        std::lock_guard<std::mutex> guard(loaders_loaded_mutex_);

        //
        // retrieve AssemblyID from ModuleID
        //
        AssemblyID assembly_id = 0;
        HRESULT hr = this->info_->GetModuleInfo2(module_id, NULL, 0, NULL, NULL, &assembly_id, NULL);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching AssemblyID for ModuleID=" + module_id);
            return hr;
        }

        //
        // retrieve AppDomainID from AssemblyID
        //
        AppDomainID app_domain_id = 0;
        WCHAR assembly_name[stringMaxSize];
        ULONG assembly_name_len = 0;
        hr = this->info_->GetAssemblyInfo(assembly_id, stringMaxSize, &assembly_name_len, assembly_name, &app_domain_id, NULL);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching AppDomainID for AssemblyID=" + assembly_id);
            return hr;
        }

        auto assembly_name_string = WSTRING(assembly_name);

        //
        // check if the module is not the loader itself
        //
        if (assembly_name_string == managed_loader_assembly_name) {
            Debug("Loader::InjectLoaderToModuleInitializer: The module is the loader itself, skipping it.");
            return E_FAIL;
        }

        //
        // check if the loader has been already loaded for this AppDomain
        //
        if (loaders_loaded_.find(app_domain_id) != loaders_loaded_.end()) {
            return E_FAIL;
        }

        //
        // skip libraries from the exclusion list.
        //
        for (const auto asm_name : assemblies_exclusion_list_) {
            if (assembly_name_string == asm_name) {
                Debug("Loader::InjectLoaderToModuleInitializer: Skipping " + 
                    ToString(assembly_name_string) + 
                    " [AppDomain=" + ToString(app_domain_id) + "]");
                return E_FAIL;
            }
        }

        //
        // the loader is not loaded yet for this AppDomain
        // so we will rewrite the <Module>..ctor to load the loader.
        //

        //
        // Retrieve the metadata interfaces for the ModuleID
        //
        ComPtr<IUnknown> metadata_interfaces;
        hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2, metadata_interfaces.GetAddressOf());
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching metadata interfaces for ModuleID=" + module_id);
            return hr;
        }

        //
        // Extract both IMetaDataImport2 and IMetaDataEmit2 interfaces
        //
        const ComPtr<IMetaDataImport2> metadata_import = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        const ComPtr<IMetaDataEmit2> metadata_emit = metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        const ComPtr<IMetaDataAssemblyImport> assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        const ComPtr<IMetaDataAssemblyEmit> assembly_emit = metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

        //
        // Get the mdTypeDef of <Module> type
        //
        mdTypeDef module_type_def = mdTypeDefNil;
        hr = metadata_import->FindTypeDefByName(module_type_name, mdTokenNil, &module_type_def);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching " + ToString(module_type_name) + " (module type) typedef for ModuleID=" + ToString(module_id));
            return hr;
        }

        //
        // Check if the <Module> type has already a ..ctor, if not we create an empty one.
        //
        COR_SIGNATURE cctor_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
                ELEMENT_TYPE_OBJECT             // List of parameter types
        };

        mdMethodDef cctor_method_def = mdMethodDefNil;
        hr = metadata_import->FindMethod(module_type_def, constructor_name, cctor_signature, sizeof(cctor_signature), &cctor_method_def);
        if (FAILED(hr)) {
            Debug("Loader::InjectLoaderToModuleInitializer: failed fetching <Module>..ctor methoddef, creating new .ctor for ModuleID=" + ToString(module_id));

            //
            // Define a new ..ctor for the <Module> type
            //
            hr = metadata_emit->DefineMethod(module_type_def, constructor_name,
                                             mdPublic | mdStatic | mdRTSpecialName | mdSpecialName, cctor_signature,
                                             sizeof(cctor_signature), 0, 0, &cctor_method_def);

            if (FAILED(hr)) {
              Warn("Loader::InjectLoaderToModuleInitializer: Error creating .cctor for <Module> ModuleID=" + ToString(module_id));
                return hr;
            }

            //
            // Create a simple method body with only the `ret` opcode instruction.
            //
            ILRewriter rewriter(this->info_, nullptr, module_id, cctor_method_def);
            rewriter.InitializeTiny();
            ILInstr* pFirstInstr = rewriter.GetILList()->m_pNext;
            ILInstr* pNewInstr = NULL;

            pNewInstr = rewriter.NewILInstr();
            pNewInstr->m_opcode = CEE_RET;
            rewriter.InsertBefore(pFirstInstr, pNewInstr);

            hr = rewriter.Export();
            if (FAILED(hr)) {
                Warn("Loader::InjectLoaderToModuleInitializer: ILRewriter.Export failed creating .cctor for <Module> ModuleID=" + ToString(module_id));
                return hr;
            }
        }

        //
        // At this point we have a mdTypeDef for <Module> and a mdMethodDef for the ..ctor
        // that we can rewrite to load the loader
        //

        ///////////////////////////////////////////////////////////////////////

        //
        // create PInvoke method definition to the native GetAssemblyAndSymbolsBytes method (interop.cpp)
        //
        // Define a method on the managed side that will PInvoke into the profiler
        // method: C++: bool GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int*
        // assemblySize, BYTE** pSymbolsArray, int* symbolsSize, AppDomainID
        // appDomainId) C#: static extern void GetAssemblyAndSymbolsBytes(out IntPtr
        // assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int
        // symbolsSize, ulong appDomainId)
        mdMethodDef pinvoke_method_def = mdMethodDefNil;
        COR_SIGNATURE get_assembly_bytes_signature[] = {
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
                ELEMENT_TYPE_U8,
        };
        hr = metadata_emit->DefineMethod(module_type_def, get_assembly_and_symbols_bytes_name,
                                         mdStatic | mdPinvokeImpl | mdHideBySig, get_assembly_bytes_signature,
                                         sizeof(get_assembly_bytes_signature), 0, 0, &pinvoke_method_def);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMethod for GetAssemblyAndSymbolsBytes failed.");
            return hr;
        }

        metadata_emit->SetMethodImplFlags(pinvoke_method_def, miPreserveSig);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: SetMethodImplFlags for GetAssemblyAndSymbolsBytes failed.");
            return hr;
        }

        WSTRING native_profiler_file = native_profiler_library_filename_;

        Debug("Loader::InjectLoaderToModuleInitializer: Setting the PInvoke native profiler library path to " + ToString(native_profiler_file));

        mdModuleRef profiler_ref;
        hr = metadata_emit->DefineModuleRef(native_profiler_file.c_str(), &profiler_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Failed! DefineModuleRef for " + ToString(native_profiler_file));
            return hr;
        }

        hr = metadata_emit->DefinePinvokeMap(pinvoke_method_def, 0, get_assembly_and_symbols_bytes_name, profiler_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefinePinvokeMap for GetAssemblyAndSymbolsBytes failed");
            return hr;
        }

        //
        // create assembly ref to mscorlib
        //
        mdAssemblyRef mscorlib_ref = mdAssemblyRefNil;
        ASSEMBLYMETADATA metadata{ 4, 0, 0, 0};
        COR_SIGNATURE public_key[] = {0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89};
        hr = assembly_emit->DefineAssemblyRef(public_key, sizeof(public_key), mscorlib_name, &metadata, NULL, 0, 0, &mscorlib_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Error creating assembly reference to mscorlib.");
            return hr;
        }

        //
        // get a TypeRef for System.Byte
        //
        mdTypeRef byte_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_byte_name, &byte_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }
        
        //
        // get a TypeRef for System.String
        //
        mdTypeRef string_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_string_name, &string_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Runtime.InteropServices.Marshal
        //
        mdTypeRef marshal_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_runtime_interopservices_marshal_name, &marshal_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a MemberRef for System.Runtime.InteropServices.Marshal.Copy(IntPtr, Byte[], int, int)
        //
        mdMemberRef marshal_copy_member_ref;
        COR_SIGNATURE marshal_copy_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                4,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
                ELEMENT_TYPE_I,                 // List of parameter types
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_I4};
        hr = metadata_emit->DefineMemberRef(marshal_type_ref, copy_name, marshal_copy_signature, sizeof(marshal_copy_signature), &marshal_copy_member_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.Assembly
        //
        mdTypeRef system_reflection_assembly_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_reflection_assembly_name, &system_reflection_assembly_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Object
        //
        mdTypeRef system_object_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_object_name, &system_object_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.AppDomain
        //
        mdTypeRef system_appdomain_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_appdomain_name, &system_appdomain_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Type
        //
        mdTypeRef system_type_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_type_name, &system_type_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }
        
        //
        // get a TypeRef for System.Reflection.MethodInfo
        //
        mdTypeRef system_reflection_methodinfo_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_reflection_methodinfo_name, &system_reflection_methodinfo_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }
                
        //
        // get a TypeRef for System.Reflection.MethodBase
        //
        mdTypeRef system_reflection_methodbase_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_reflection_methodbase_name, &system_reflection_methodbase_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }


        ULONG offset = 0;

        //
        // get a MemberRef for System.AppDomain.get_CurrentDomain()
        //
        BYTE system_appdomain_type_ref_compressed_token[4];
        ULONG system_appdomain_type_ref_compressed_token_length = 
            CorSigCompressToken(system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

        COR_SIGNATURE appdomain_get_current_domain_signature[50];
        offset = 0;
        appdomain_get_current_domain_signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        appdomain_get_current_domain_signature[offset++] = 0;
        appdomain_get_current_domain_signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&appdomain_get_current_domain_signature[offset],
               system_appdomain_type_ref_compressed_token,
               system_appdomain_type_ref_compressed_token_length);
        offset += system_appdomain_type_ref_compressed_token_length;

        mdMemberRef appdomain_get_current_domain_member_ref;
        hr = metadata_emit->DefineMemberRef(system_appdomain_type_ref, get_currentdomain_name,
                                            appdomain_get_current_domain_signature, offset,
                                            &appdomain_get_current_domain_member_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for AppDomain.Load(byte[], byte[])
        //
        BYTE system_reflection_assembly_type_ref_compressed_token[4];
        ULONG system_reflection_assembly_type_ref_compressed_token_length =
                CorSigCompressToken(system_reflection_assembly_type_ref,
                                    system_reflection_assembly_type_ref_compressed_token);

        COR_SIGNATURE appdomain_load_signature[50];
        offset = 0;
        appdomain_load_signature[offset++] = IMAGE_CEE_CS_CALLCONV_HASTHIS;
        appdomain_load_signature[offset++] = 2;
        appdomain_load_signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&appdomain_load_signature[offset],
               system_reflection_assembly_type_ref_compressed_token,
               system_reflection_assembly_type_ref_compressed_token_length);
        offset += system_reflection_assembly_type_ref_compressed_token_length;
        appdomain_load_signature[offset++] = ELEMENT_TYPE_SZARRAY;
        appdomain_load_signature[offset++] = ELEMENT_TYPE_U1;
        appdomain_load_signature[offset++] = ELEMENT_TYPE_SZARRAY;
        appdomain_load_signature[offset++] = ELEMENT_TYPE_U1;

        mdMemberRef appdomain_load_member_ref;
        hr = metadata_emit->DefineMemberRef(system_appdomain_type_ref,
                                            load_name, appdomain_load_signature, 
                                            offset, &appdomain_load_member_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
            return hr;
        }

        //
        // Create method signature for System.Type System.Reflection.Assembly.GetType(string name, bool throwOnError)
        //
        BYTE system_type_type_ref_compressed_token[4];
        ULONG system_type_type_ref_compressed_token_length =
                CorSigCompressToken(system_type_type_ref, system_type_type_ref_compressed_token);

        COR_SIGNATURE assembly_get_type_signature[50];
        offset = 0;
        assembly_get_type_signature[offset++] = IMAGE_CEE_CS_CALLCONV_HASTHIS;
        assembly_get_type_signature[offset++] = 2;
        assembly_get_type_signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&assembly_get_type_signature[offset],
               system_type_type_ref_compressed_token,
               system_type_type_ref_compressed_token_length);
        offset += system_type_type_ref_compressed_token_length;
        assembly_get_type_signature[offset++] = ELEMENT_TYPE_STRING;
        assembly_get_type_signature[offset++] = ELEMENT_TYPE_BOOLEAN;

        mdMemberRef assembly_get_type_member_ref;
        hr = metadata_emit->DefineMemberRef(system_reflection_assembly_type_ref,
                                            gettype_name, assembly_get_type_signature,
                                            offset, &assembly_get_type_member_ref);
        if (FAILED(hr)) {
          Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
          return hr;
        }

        //
        // Create method signature for System.Reflection.MethodInfo System.Type.GetMethod(string name)
        //
        BYTE system_reflection_methodinfo_type_ref_compressed_token[4];
        ULONG system_reflection_methodinfo_type_ref_compressed_token_length =
                CorSigCompressToken(system_reflection_methodinfo_type_ref, system_reflection_methodinfo_type_ref_compressed_token);
        
        COR_SIGNATURE type_get_method_signature[50];
        offset = 0;
        type_get_method_signature[offset++] = IMAGE_CEE_CS_CALLCONV_HASTHIS;
        type_get_method_signature[offset++] = 1;
        type_get_method_signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&type_get_method_signature[offset],
               system_reflection_methodinfo_type_ref_compressed_token,
               system_reflection_methodinfo_type_ref_compressed_token_length);
        offset += system_reflection_methodinfo_type_ref_compressed_token_length;
        type_get_method_signature[offset++] = ELEMENT_TYPE_STRING;

        mdMemberRef type_get_method_member_ref;
        hr = metadata_emit->DefineMemberRef(system_type_type_ref,
                                            getmethod_name, type_get_method_signature,
                                            offset, &type_get_method_member_ref);
        if (FAILED(hr)) {
          Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
          return hr;
        }

        //
        // Create method signature for object System.Reflection.MethodBase.Invoke(object instance, object[] args)
        //
        COR_SIGNATURE methodbase_invoke_signature[] = {
            IMAGE_CEE_CS_CALLCONV_HASTHIS,
            2,
            ELEMENT_TYPE_OBJECT,
            ELEMENT_TYPE_OBJECT,
            ELEMENT_TYPE_SZARRAY,
            ELEMENT_TYPE_OBJECT,
        };
        mdMemberRef methodbase_invoke_member_ref;
        hr = metadata_emit->DefineMemberRef(system_reflection_methodbase_type_ref,
                                            invoke_name, methodbase_invoke_signature,
                                            6, &methodbase_invoke_member_ref);
        if (FAILED(hr)) {
          Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
          return hr;
        }

        // Create a string representing
        // "Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader" 
        mdString load_helper_token;
        hr = metadata_emit->DefineUserString(managed_loader_startup_type, (ULONG)WStrLen(managed_loader_startup_type), &load_helper_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString failed");
            return hr;
        }
        
        // Create a string representing
        // "Run" 
        mdString run_string_token;
        hr = metadata_emit->DefineUserString(run_name, (ULONG)WStrLen(run_name), &run_string_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString failed");
            return hr;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // Define a new static method __DDVoidMethodCall__ on the new type that has a
        // void return type and takes no arguments
        //
        BYTE initialize_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
                ELEMENT_TYPE_OBJECT             // List of parameter types
        };

        mdMethodDef startup_method_def;
        hr = metadata_emit->DefineMethod(
                module_type_def, loader_method_name, mdStatic,
                initialize_signature, sizeof(initialize_signature), 0, 0, &startup_method_def);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMethod failed");
            return hr;
        }

        // Generate a locals signature defined in the following way:
        //   [0] System.IntPtr ("assemblyPtr" - address of assembly bytes)
        //   [1] System.Int32  ("assemblySize" - size of assembly bytes)
        //   [2] System.IntPtr ("symbolsPtr" - address of symbols bytes)
        //   [3] System.Int32  ("symbolsSize" - size of symbols bytes)
        //   [4] System.Byte[] ("assemblyBytes" - managed byte array for assembly)
        //   [5] System.Byte[] ("symbolsBytes" - managed byte array for symbols)
        //   [6] class System.Reflection.Assembly ("loadedAssembly" - assembly
        //   instance to save loaded assembly)
        mdSignature locals_signature_token;
        COR_SIGNATURE locals_signature[15] = {
                IMAGE_CEE_CS_CALLCONV_LOCAL_SIG,  // Calling convention
                7,                                // Number of variables
                ELEMENT_TYPE_I,                   // List of variable types
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_I,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
                ELEMENT_TYPE_CLASS
                // insert compressed token for System.Reflection.Assembly TypeRef here
        };
        CorSigCompressToken(system_reflection_assembly_type_ref,
                            &locals_signature[11]);
        hr = metadata_emit->GetTokenFromSig(locals_signature, sizeof(locals_signature), &locals_signature_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Unable to generate locals signature. ModuleID=" + ToString(module_id));
            return hr;
        }

        ///////////////////////////////////////////////////////////////////////

        // Add IL instructions into the void method
        ILRewriter rewriter_void(this->info_, nullptr, module_id, startup_method_def);
        rewriter_void.InitializeTiny();
        rewriter_void.SetTkLocalVarSig(locals_signature_token);

        ILInstr* pFirstInstr = rewriter_void.GetILList()->m_pNext;
        ILInstr* pNewInstr = NULL;

        // Step 1) Call bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out
        // int assemblySize, out IntPtr symbolsPtr, out int symbolsSize, ulong appDomainId)

        // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index
        // 0)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOCA_S;
        pNewInstr->m_Arg32 = 0;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloca.s 1 : Load the address of the "assemblySize" variable (locals index
        // 1)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOCA_S;
        pNewInstr->m_Arg32 = 1;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloca.s 2 : Load the address of the "symbolsPtr" variable (locals index 2)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOCA_S;
        pNewInstr->m_Arg32 = 2;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloca.s 3 : Load the address of the "symbolsSize" variable (locals index
        // 3)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOCA_S;
        pNewInstr->m_Arg32 = 3;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i8 appDomainId : Load the AppDomainID to the stack
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I8;
        pNewInstr->m_Arg64 = app_domain_id;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // call bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int
        // assemblySize, out IntPtr symbolsPtr, out int symbolsSize, ulong
        // appDomainID)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALL;
        pNewInstr->m_Arg32 = pinvoke_method_def;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // check if the return of the method call is true or false
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_BRTRUE_S;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);
        ILInstr* pBranchTrueInstr = pNewInstr;

        // ret instruction if the Call returns a false boolean
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_RET;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 2) Call void Marshal.Copy(IntPtr source, byte[] destination, int
        // startIndex, int length) to populate the managed assembly bytes

        // ldloc.1 : Load the "assemblySize" variable (locals index 1)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_1;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Set the true branch target
        pBranchTrueInstr->m_pTarget = pNewInstr;

        // newarr System.Byte : Create a new Byte[] to hold a managed copy of the
        // assembly data
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_NEWARR;
        pNewInstr->m_Arg32 = byte_type_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // stloc.s 4 : Assign the Byte[] to the "assemblyBytes" variable (locals index
        // 4)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_STLOC_S;
        pNewInstr->m_Arg8 = 4;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.0 : Load the "assemblyPtr" variable (locals index 0)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_0;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_S;
        pNewInstr->m_Arg8 = 4;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4_0;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.1 : Load the "assemblySize" variable (locals index 1) for the
        // Marshal.Copy length parameter
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_1;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // call Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int
        // length)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALL;
        pNewInstr->m_Arg32 = marshal_copy_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 3) Call void Marshal.Copy(IntPtr source, byte[] destination, int
        // startIndex, int length) to populate the symbols bytes

        // ldloc.3 : Load the "symbolsSize" variable (locals index 3)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_3;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // newarr System.Byte : Create a new Byte[] to hold a managed copy of the
        // symbols data
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_NEWARR;
        pNewInstr->m_Arg32 = byte_type_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // stloc.s 5 : Assign the Byte[] to the "symbolsBytes" variable (locals index
        // 5)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_STLOC_S;
        pNewInstr->m_Arg8 = 5;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.2 : Load the "symbolsPtr" variables (locals index 2)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_2;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_S;
        pNewInstr->m_Arg8 = 5;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4_0;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.3 : Load the "symbolsSize" variable (locals index 3) for the
        // Marshal.Copy length parameter
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_3;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex,
        // int length)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALL;
        pNewInstr->m_Arg32 = marshal_copy_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 4) Call System.Reflection.Assembly
        // System.AppDomain.CurrentDomain.Load(byte[], byte[]))

        // call System.AppDomain System.AppDomain.CurrentDomain property
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALL;
        pNewInstr->m_Arg32 = appdomain_get_current_domain_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the
        // first byte[] parameter of AppDomain.Load(byte[], byte[])
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_S;
        pNewInstr->m_Arg8 = 4;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the
        // second byte[] parameter of AppDomain.Load(byte[], byte[])
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_S;
        pNewInstr->m_Arg8 = 5;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // callvirt System.Reflection.Assembly System.AppDomain.Load(uint8[], uint8[])
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALLVIRT;
        pNewInstr->m_Arg32 = appdomain_load_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // stloc.s 6 : Assign the System.Reflection.Assembly object to the
        // "loadedAssembly" variable (locals index 6)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_STLOC_S;
        pNewInstr->m_Arg8 = 6;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 5) Call instance method
        // Assembly.GetType("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader", true)

        // ldloc.s 6 : Load the "loadedAssembly" variable (locals index 6) to call
        // Assembly.GetType
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_S;
        pNewInstr->m_Arg8 = 6;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldstr "Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader"
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDSTR;
        pNewInstr->m_Arg32 = load_helper_token;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i4.1 load true for boolean type
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4_1;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // callvirt System.Type System.Reflection.Assembly.GetType(string, bool)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALLVIRT;
        pNewInstr->m_Arg32 = assembly_get_type_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 6) Call instance method
        // System.Type.GetMethod("Run");

        // ldstr "Run"
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDSTR;
        pNewInstr->m_Arg32 = run_string_token;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // callvirt instance class System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALLVIRT;
        pNewInstr->m_Arg32 = type_get_method_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 7) Call instance method
        // System.Reflection.MethodBase.Invoke(null, new object[] { "Assembly1", "Assembly2" });

        // ldnull
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDNULL;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // create an array of 1 element with the single string[] parameter
        
        // ldc.i4.1 = const int 1 => array length
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4_2;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // newarr System.Object
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_NEWARR;
        pNewInstr->m_Arg32 = system_object_type_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // *************************************************************************************************************** FIRST PARAMETER

        // dup
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_DUP;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i4.0 = const int 0 => array index 0
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4_0;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);
        
        hr = WriteAssembliesStringArray(rewriter_void, metadata_emit, assembly_string_default_appdomain_vector_, pFirstInstr, string_type_ref);
        if (FAILED(hr)) {
          return hr;
        }
        
        // stelem.ref
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_STELEM_REF;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // *************************************************************************************************************** SECOND PARAMETER

        // dup
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_DUP;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i4.0 = const int 0 => array index 0
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4_1;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);
        
        hr = WriteAssembliesStringArray(rewriter_void, metadata_emit, assembly_string_nondefault_appdomain_vector_, pFirstInstr, string_type_ref);
        if (FAILED(hr)) {
          return hr;
        }

        // stelem.ref
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_STELEM_REF;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ***************************************************************************************************************

        // callvirt instance class object System.Reflection.MethodBase::Invoke(object, object[])
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALLVIRT;
        pNewInstr->m_Arg32 = methodbase_invoke_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Step 8) Pop and return

        // pop the returned object
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_POP;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // return
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_RET;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        hr = rewriter_void.Export();
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Export() failed for ModuleID=" + ToString(module_id));
            return hr;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // rewrite ..ctor to call the startup loader.
        //
        ILRewriter rewriter(this->info_, nullptr, module_id, cctor_method_def);
        hr = rewriter.Import();
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Import() failed for ModuleID=" + ToString(module_id) +
                 ", CCTORMethodDef" + ToString(cctor_method_def));
            return hr;
        }
        ILRewriterWrapper rewriter_wrapper(&rewriter);
        ILInstr* pInstr = rewriter.GetILList()->m_pNext;
        rewriter_wrapper.SetILPosition(pInstr);
        rewriter_wrapper.CallMember(startup_method_def, false);
        hr = rewriter.Export();
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Export() failed for ModuleID=" + ToString(module_id) +
                 ", CCTORMethodDef" + ToString(cctor_method_def));
            return hr;
        }

        Info("Loader::InjectLoaderToModuleInitializer [ModuleID=" + ToString(module_id) +
              ", AssemblyID=" + ToString(assembly_id) +
              ", AssemblyName=" + ToString(assembly_name_string) +
              ", AppDomainID=" + ToString(app_domain_id) +
              ", ModuleTypeDef=" + ToString(module_type_def) +
              ", ModuleCCTORDef=" + ToString(cctor_method_def) + "]");
        return S_OK;
    }

    HRESULT Loader::WriteAssembliesStringArray(
        ILRewriter& rewriter_void,
        const ComPtr<IMetaDataEmit2> metadata_emit,
        const std::vector<WSTRING>& assembly_string_vector,
        ILInstr* pFirstInstr, mdTypeRef string_type_ref) {
      ILInstr* pNewInstr;

      // ldc.i4 = const int (array length)
      pNewInstr = rewriter_void.NewILInstr();
      pNewInstr->m_opcode = CEE_LDC_I4;
      pNewInstr->m_Arg64 = assembly_string_vector.size();
      rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

      // newarr System.String
      pNewInstr = rewriter_void.NewILInstr();
      pNewInstr->m_opcode = CEE_NEWARR;
      pNewInstr->m_Arg32 = string_type_ref;
      rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

      // loading array index
      for (ULONG i = 0; i < assembly_string_vector.size(); i++) {
        // dup
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_DUP;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldc.i4 = const int array index 0
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDC_I4;
        pNewInstr->m_Arg32 = i;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // Create a string token
        mdString string_token;
        auto hr = metadata_emit->DefineUserString(assembly_string_vector[i].c_str(), (ULONG)assembly_string_vector[i].size(), &string_token);
        if (FAILED(hr)) {
          Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString for string array failed");
          return hr;
        }

        // ldstr assembly index value
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDSTR;
        pNewInstr->m_Arg32 = string_token;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // stelem.ref
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_STELEM_REF;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);
      }

      return S_OK;
    }

    bool Loader::GetAssemblyAndSymbolsBytes(void** pAssemblyArray,
                                            int* assemblySize, void** pSymbolsArray,
                                            int* symbolsSize,
                                            AppDomainID appDomainId) {
        //
        // global lock
        //
        std::lock_guard<std::mutex> guard(loaders_loaded_mutex_);

        //
        // check if the loader has been already loaded for this AppDomain
        //
        if (loaders_loaded_.find(appDomainId) != loaders_loaded_.end()) {
            Warn("Loader::GetAssemblyAndSymbolsBytes: The loader was already loaded for AppDomainID=" + ToString(appDomainId));
            return false;
        }

        Info("Loader::GetAssemblyAndSymbolsBytes: Loading loader data for AppDomainID=" + ToString(appDomainId));
        loaders_loaded_.insert(appDomainId);

#ifdef _WIN32
        HINSTANCE hInstance = DllHandle;
        LPCWSTR dllLpName;
        LPCWSTR symbolsLpName;

        if (runtime_information_.is_desktop()) {
          dllLpName = MAKEINTRESOURCE(resourceMonikerIDs_.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll);
          symbolsLpName = MAKEINTRESOURCE(resourceMonikerIDs_.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb);
        } else {
          dllLpName = MAKEINTRESOURCE(resourceMonikerIDs_.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll);
          symbolsLpName = MAKEINTRESOURCE(resourceMonikerIDs_.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb);
        }

        HRSRC hResAssemblyInfo = FindResource(hInstance, dllLpName, L"ASSEMBLY");
        HGLOBAL hResAssembly = LoadResource(hInstance, hResAssemblyInfo);
        *assemblySize = SizeofResource(hInstance, hResAssemblyInfo);
        *pAssemblyArray = (LPBYTE)LockResource(hResAssembly);

        HRSRC hResSymbolsInfo = FindResource(hInstance, symbolsLpName, L"SYMBOLS");
        HGLOBAL hResSymbols = LoadResource(hInstance, hResSymbolsInfo);
        *symbolsSize = SizeofResource(hInstance, hResSymbolsInfo);
        *pSymbolsArray = (LPBYTE)LockResource(hResSymbols);

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for AppDomainID=" + ToString(appDomainId) + " (platform=_WIN32)."
              " *assemblySize=" + ToString(*assemblySize) + ","
              " *pAssemblyArray=" + ToString(reinterpret_cast<std::uint64_t>(*pAssemblyArray)) + ","
              " *symbolsSize=" + ToString(*symbolsSize) + ","
              " *pSymbolsArray=" + ToString(reinterpret_cast<std::uint64_t>(*pSymbolsArray)) + ".");

        Debug("Loader::GetAssemblyAndSymbolsBytes: resourceMonikerIDs_: _.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll=" + ToString(resourceMonikerIDs_.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll) + ","
            " _.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb=" + ToString(resourceMonikerIDs_.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb) + ","
            " _.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll=" + ToString(resourceMonikerIDs_.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll) + ","
            " _.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb=" + ToString(resourceMonikerIDs_.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb) + ".");
#elif LINUX
        *assemblySize = dll_end - dll_start;
        *pAssemblyArray = (void*)dll_start;

        *symbolsSize = pdb_end - pdb_start;
        *pSymbolsArray = (void*)pdb_start;

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for AppDomainID=" + ToString(appDomainId) + " (platform=LINUX)."
            " *assemblySize=" + ToString(*assemblySize) + ", "
            " *pAssemblyArray=" + ToString(reinterpret_cast<std::uint64_t>(*pAssemblyArray)) + ", "
            " *symbolsSize=" + ToString(*symbolsSize) + ", "
            " *pSymbolsArray=" + ToString(reinterpret_cast<std::uint64_t>(*pSymbolsArray)) + ".");
#elif MACOS
        const unsigned int imgCount = _dyld_image_count();

        std::string native_profiler_file_macos = native_profiler_library_filename_;
        for(auto i = 0; i < imgCount; i++) {
            const std::string name = std::string(_dyld_get_image_name(i));

            if (name.rfind(native_profiler_file_macos) != std::string::npos) {
                const mach_header_64* header = (const struct mach_header_64 *) _dyld_get_image_header(i);

                unsigned long dllSize;
                const auto dllData = getsectiondata(header, "binary", "dll", &dllSize);
                *assemblySize = dllSize;
                *pAssemblyArray = (void*)dllData;

                unsigned long pdbSize;
                const auto pdbData = getsectiondata(header, "binary", "pdb", &pdbSize);
                *symbolsSize = pdbSize;
                *pSymbolsArray = (void*)pdbData;
                break;
            }
        }

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for AppDomainID=" + ToString(appDomainId) + " (platform=MACOS)."
            " *assemblySize=" + ToString(*assemblySize) + ", "
            " *pAssemblyArray=" + ToString(reinterpret_cast<std::uint64_t>(*pAssemblyArray)) + ", "
            " *symbolsSize=" + ToString(*symbolsSize) + ", "
            " *pSymbolsArray=" + ToString(reinterpret_cast<std::uint64_t>(*pSymbolsArray)) + ".");
#else
        Error("Loader::GetAssemblyAndSymbolsBytes. Platform not supported.");
        return false;
#endif
        return true;
    }

}