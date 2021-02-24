#include "loader.h"

#include "dllmain.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "resource.h"

#ifdef MACOS    
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

#define stringMaxSize 1024

namespace trace {
    
    Loader* loader = nullptr;

#ifdef _WIN32
    const WSTRING native_profiler_file_win32                    = WStr("DATADOG.TRACE.CLRPROFILER.NATIVE.DLL");
#elif LINUX
    extern uint8_t dll_start[]                                  asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_start");
    extern uint8_t dll_end[]                                    asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_end");

    extern uint8_t pdb_start[]                                  asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_start");
    extern uint8_t pdb_end[]                                    asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_end");
#elif MACOS
    const std::string native_profiler_file_macos                = "Datadog.Trace.ClrProfiler.Native.dylib";
#endif
    
    const WSTRING managed_loader_assembly_name                  = WStr("Datadog.Trace.ClrProfiler.Managed.Loader");
    const WSTRING empty_string                                  = WStr("");
    const WSTRING default_domain_name                           = WStr("DefaultDomain");

    const LPCWSTR managed_loader_startup_type                   = WStr("Datadog.Trace.ClrProfiler.Managed.Loader.Startup");
    const LPCWSTR module_type_name                              = WStr("<Module>");
    const LPCWSTR constructor_name                              = WStr(".cctor");
    const LPCWSTR get_assembly_and_symbols_bytes_name           = WStr("GetAssemblyAndSymbolsBytes");
    const LPCWSTR mscorlib_name                                 = WStr("mscorlib");
    const LPCWSTR system_byte_name                              = WStr("System.Byte");
    const LPCWSTR system_runtime_interopservices_marshal_name   = WStr("System.Runtime.InteropServices.Marshal");
    const LPCWSTR copy_name                                     = WStr("Copy");
    const LPCWSTR system_reflection_assembly_name               = WStr("System.Reflection.Assembly");
    const LPCWSTR system_object_name                            = WStr("System.Object");
    const LPCWSTR system_appdomain_name                         = WStr("System.AppDomain");
    const LPCWSTR get_currentdomain_name                        = WStr("get_CurrentDomain");
    const LPCWSTR load_name                                     = WStr("Load");
    const LPCWSTR createinstance_name                           = WStr("CreateInstance");
    const LPCWSTR loader_method_name                            = WStr("DD_LoadInitializationAssemblies");
    
    const WSTRING profiler_path_64                              = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_64"));
    const WSTRING profiler_path_32                              = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_32"));
    const WSTRING profiler_path                                 = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH"));

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

    Loader::Loader(
        ICorProfilerInfo4* info, bool isIIS,
        std::function<void(const std::string& str)> log_debug_callback,
        std::function<void(const std::string& str)> log_info_callback,
        std::function<void(const std::string& str)> log_warn_callback) {
        info_ = info;
        runtime_information_ = GetRuntimeInformation(info);
        is_iis_ = isIIS;
        log_debug_callback_ = log_debug_callback;
        log_info_callback_ = log_info_callback;
        log_warn_callback_ = log_warn_callback;
        loader = this;
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
        // retrieve AppDomain Name
        //
        WSTRING app_domain_name_string = empty_string;
        WCHAR app_domain_name[stringMaxSize];
        ULONG app_domain_name_len = 0;
        hr = this->info_->GetAppDomainInfo(app_domain_id, stringMaxSize, &app_domain_name_len, app_domain_name, nullptr);
        if (SUCCEEDED(hr)) {
          app_domain_name_string = WSTRING(app_domain_name);
        }

        // If we are in the IIS process we skip the default domain
        if (is_iis_ && app_domain_name_string == default_domain_name) {
            Debug("Loader::InjectLoaderToModuleInitializer: Skipping " + ToString(assembly_name_string) + ". The module belongs to the DefaultDomain in IIS process.");
            return E_FAIL;
        }

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
                    " [AppDomain=" + ToString(app_domain_id) + 
                    ", AppDomainName=" + ToString(app_domain_name_string) + "]");
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
        // method: C++: void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int*
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

#ifdef _WIN32
        WSTRING native_profiler_file = native_profiler_file_win32;
#else  // _WIN32

#ifdef BIT64
        WSTRING native_profiler_file = profiler_path_64 == empty_string ? profiler_path : profiler_path_64;
#else   // BIT64
        WSTRING native_profiler_file = profiler_path_32 == empty_string ? profiler_path : profiler_path_32;
#endif  // BIT64
        Debug("Loader::InjectLoaderToModuleInitializer: Setting the PInvoke native profiler library path to " + ToString(native_profiler_file));

#endif  // _WIN32

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
        // Create method signature for Assembly.CreateInstance(string)
        //
        COR_SIGNATURE assembly_create_instance_signature[] = {
                IMAGE_CEE_CS_CALLCONV_HASTHIS,
                1,
                ELEMENT_TYPE_OBJECT,  // ret = System.Object
                ELEMENT_TYPE_STRING
        };

        mdMemberRef assembly_create_instance_member_ref;
        hr = metadata_emit->DefineMemberRef(
                system_reflection_assembly_type_ref, createinstance_name,
                assembly_create_instance_signature,
                sizeof(assembly_create_instance_signature),
                &assembly_create_instance_member_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
            return hr;
        }

        // Create a string representing
        // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup" 
        size_t load_helper_str_size = WStrLen(managed_loader_startup_type);

        mdString load_helper_token;
        hr = metadata_emit->DefineUserString(managed_loader_startup_type, (ULONG)load_helper_str_size, &load_helper_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString failed");
            return hr;
        }

        ULONG string_len = 0;
        WCHAR string_contents[stringMaxSize]{};
        hr = metadata_import->GetUserString(load_helper_token, string_contents, stringMaxSize, &string_len);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: GetUserString failed" + ToString(module_id));
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

        // Step 1) Call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out
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

        // call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int
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

        // Step 4) Call instance method
        // Assembly.CreateInstance("Datadog.Trace.ClrProfiler.Managed.Loader.Startup")

        // ldloc.s 6 : Load the "loadedAssembly" variable (locals index 6) to call
        // Assembly.CreateInstance
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDLOC_S;
        pNewInstr->m_Arg8 = 6;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // ldstr "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_LDSTR;
        pNewInstr->m_Arg32 = load_helper_token;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

        // callvirt System.Object System.Reflection.Assembly.CreateInstance(string)
        pNewInstr = rewriter_void.NewILInstr();
        pNewInstr->m_opcode = CEE_CALLVIRT;
        pNewInstr->m_Arg32 = assembly_create_instance_member_ref;
        rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

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

    bool Loader::GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray,
                                            int* assemblySize, BYTE** pSymbolsArray,
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
            Warn("Loader::GetAssemblyAndSymbolsBytes the loader was already loaded for AppDomainID=" + ToString(appDomainId));
            return false;
        }

        Info("Loader::GetAssemblyAndSymbolsBytes Loading loader data for AppDomainID=" + ToString(appDomainId));
        loaders_loaded_.insert(appDomainId);

#ifdef _WIN32
        HINSTANCE hInstance = DllHandle;
        LPCWSTR dllLpName;
        LPCWSTR symbolsLpName;

        if (runtime_information_.is_desktop()) {
          dllLpName = MAKEINTRESOURCE(NET45_MANAGED_ENTRYPOINT_DLL);
          symbolsLpName = MAKEINTRESOURCE(NET45_MANAGED_ENTRYPOINT_SYMBOLS);
        } else {
          dllLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_DLL);
          symbolsLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_SYMBOLS);
        }

        HRSRC hResAssemblyInfo = FindResource(hInstance, dllLpName, L"ASSEMBLY");
        HGLOBAL hResAssembly = LoadResource(hInstance, hResAssemblyInfo);
        *assemblySize = SizeofResource(hInstance, hResAssemblyInfo);
        *pAssemblyArray = (LPBYTE)LockResource(hResAssembly);

        HRSRC hResSymbolsInfo = FindResource(hInstance, symbolsLpName, L"SYMBOLS");
        HGLOBAL hResSymbols = LoadResource(hInstance, hResSymbolsInfo);
        *symbolsSize = SizeofResource(hInstance, hResSymbolsInfo);
        *pSymbolsArray = (LPBYTE)LockResource(hResSymbols);
#elif LINUX
        *assemblySize = dll_end - dll_start;
        *pAssemblyArray = (BYTE*)dll_start;

        *symbolsSize = pdb_end - pdb_start;
        *pSymbolsArray = (BYTE*)pdb_start;
#elif MACOS
        const unsigned int imgCount = _dyld_image_count();

        for(auto i = 0; i < imgCount; i++) {
            const std::string name = std::string(_dyld_get_image_name(i));

            if (name.rfind(native_profiler_file_macos) != std::string::npos) {
                const mach_header_64* header = (const struct mach_header_64 *) _dyld_get_image_header(i);

                unsigned long dllSize;
                const auto dllData = getsectiondata(header, "binary", "dll", &dllSize);
                *assemblySize = dllSize;
                *pAssemblyArray = (BYTE*)dllData;

                unsigned long pdbSize;
                const auto pdbData = getsectiondata(header, "binary", "pdb", &pdbSize);
                *symbolsSize = pdbSize;
                *pSymbolsArray = (BYTE*)pdbData;
                break;
            }
        }
#else
        Error("Platform not supported.");
        return false;
#endif
        return true;
    }

}