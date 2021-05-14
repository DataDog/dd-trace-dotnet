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

    // We exclude here the direct references of the loader to avoid a cyclic reference problem.
    // Also well-known assemblies we want to avoid.
    const WSTRING assemblies_exclusion_list_[] = {
            WStr("netstandard"),
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

    ILInstr* CreateILInstr(ILRewriter& rewriter, ILInstr* firstInstr, unsigned int opcode) {
        ILInstr* instr = rewriter.NewILInstr();
        instr->m_opcode = opcode;
        rewriter.InsertBefore(firstInstr, instr);
        return instr;
    }

    ILInstr* CreateILInstrArg8(ILRewriter& rewriter, ILInstr* firstInstr, unsigned int opcode, INT8 arg8) {
        ILInstr* instr = rewriter.NewILInstr();
        instr->m_opcode = opcode;
        instr->m_Arg8 = arg8;
        rewriter.InsertBefore(firstInstr, instr);
        return instr;
    }

    ILInstr* CreateILInstrArg32(ILRewriter& rewriter, ILInstr* firstInstr, unsigned int opcode, INT32 arg32) {
        ILInstr* instr = rewriter.NewILInstr();
        instr->m_opcode = opcode;
        instr->m_Arg32 = arg32;
        rewriter.InsertBefore(firstInstr, instr);
        return instr;
    }

    ILInstr* CreateILInstrArg64(ILRewriter& rewriter, ILInstr* firstInstr, unsigned int opcode, INT64 arg64) {
        ILInstr* instr = rewriter.NewILInstr();
        instr->m_opcode = opcode;
        instr->m_Arg64 = arg64;
        rewriter.InsertBefore(firstInstr, instr);
        return instr;
    }

    void Loader::CreateNewSingeltonInstance(ICorProfilerInfo4* pCorProfilerInfo,
                                            std::function<void(const std::string& str)> log_debug_callback,
                                            std::function<void(const std::string& str)> log_info_callback,
                                            std::function<void(const std::string& str)> log_warn_callback,
                                            const LoaderResourceMonikerIDs& resource_moniker_ids,
                                            WCHAR const * native_profiler_library_filename,
                                            const std::vector<WSTRING>& assembliesToLoad_adDefault_procNonIIS,
                                            const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procNonIIS,
                                            const std::vector<WSTRING>& assembliesToLoad_adDefault_procIIS,
                                            const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procIIS)
    {
        Loader* newSingeltonInstance = Loader::CreateNewLoaderInstance(pCorProfilerInfo,
                                                                       log_debug_callback,
                                                                       log_info_callback,
                                                                       log_warn_callback,
                                                                       resource_moniker_ids,
                                                                       native_profiler_library_filename,
                                                                       assembliesToLoad_adDefault_procNonIIS,
                                                                       assembliesToLoad_adNonDefault_procNonIIS,
                                                                       assembliesToLoad_adDefault_procIIS,
                                                                       assembliesToLoad_adNonDefault_procIIS);

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
                WCHAR const* native_profiler_library_filename,
                const std::vector<WSTRING>& assembliesToLoad_adDefault_procNonIIS,
                const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procNonIIS,
                const std::vector<WSTRING>& assembliesToLoad_adDefault_procIIS,
                const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procIIS) {

        WSTRING process_name = GetCurrentProcessName();
        const bool is_iis = process_name == WStr("w3wp.exe") ||
            process_name == WStr("iisexpress.exe");

        if (is_iis && log_info_callback != nullptr) {
            log_info_callback("Loader::InjectLoaderToModuleInitializer: Process name: " + ToString(process_name));
            log_info_callback("Loader::InjectLoaderToModuleInitializer: IIS process detected.");
        }

        return new Loader(pCorProfilerInfo,
            is_iis ? assembliesToLoad_adDefault_procIIS : assembliesToLoad_adDefault_procNonIIS,
            is_iis ? assembliesToLoad_adNonDefault_procIIS : assembliesToLoad_adNonDefault_procNonIIS,
            log_debug_callback,
            log_info_callback,
            log_warn_callback,
            resourceMonikerIDs,
            native_profiler_library_filename);
    }

    Loader::Loader(
                ICorProfilerInfo4* info,
                const std::vector<WSTRING>& assembly_string_default_appdomain_vector,
                const std::vector<WSTRING>& assembly_string_nondefault_appdomain_vector,
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

        // *****************************************************************************************
        //
        // retrieve AssemblyID from ModuleID
        //
        AssemblyID assembly_id = 0;
        WCHAR module_file_name[stringMaxSize];
        ULONG module_file_name_len = 0;
        HRESULT hr = this->info_->GetModuleInfo2(module_id, NULL, stringMaxSize, &module_file_name_len, module_file_name, &assembly_id, NULL);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching AssemblyID for ModuleID=" + ToString(module_id));
            return hr;
        }
        auto module_file_name_wstring = WSTRING(module_file_name);
        auto module_file_name_string = ToString(module_file_name_wstring);
        // *****************************************************************************************

        // *****************************************************************************************
        //
        // retrieve AppDomainID from AssemblyID
        //
        AppDomainID app_domain_id = 0;
        WCHAR assembly_name[stringMaxSize];
        ULONG assembly_name_len = 0;
        hr = this->info_->GetAssemblyInfo(assembly_id, stringMaxSize, &assembly_name_len, assembly_name, &app_domain_id, NULL);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching AppDomainID for AssemblyID=" +
                ToString(assembly_id) + " [Module=" + module_file_name_string + "]");
            return hr;
        }
        auto assembly_name_wstring = WSTRING(assembly_name);
        auto assembly_name_string = ToString(assembly_name_wstring);
        // *****************************************************************************************

        //
        // check if the module is not the loader itself
        //
        if (assembly_name_wstring == managed_loader_assembly_name) {
            Debug("Loader::InjectLoaderToModuleInitializer: The module is the loader itself, skipping it.");
            return E_FAIL;
        }

        //
        // skip libraries from the exclusion list.
        //
        for (const auto asm_name : assemblies_exclusion_list_) {
            if (assembly_name_wstring == asm_name) {
                Debug("Loader::InjectLoaderToModuleInitializer: Skipping " +
                    assembly_name_string +
                    " [AppDomainID=" + ToString(app_domain_id) + "]");
                return E_FAIL;
            }
        }

        // **************************************************************************************************************
        //
        // Retrieve the metadata interfaces for the ModuleID
        //
        ComPtr<IUnknown> metadata_interfaces;
        hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2, metadata_interfaces.GetAddressOf());
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching metadata interfaces for ModuleID=" +
                ToString(module_id) + " [Module=" + module_file_name_string + "]");
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
        // Check and store assembly metadata if the corlib is found.
        //
        if (assembly_name_wstring == WStr("mscorlib") || assembly_name_wstring == WStr("System.Private.CoreLib")) {
            Debug("Loader::InjectLoaderToModuleInitializer: extracting metadata of the corlib assembly.");

            mdAssembly corLibAssembly;
            assembly_import->GetAssemblyFromScope(&corLibAssembly);

            corlib_metadata.module_id = module_id;
            corlib_metadata.id = assembly_id;
            corlib_metadata.token = corLibAssembly;
            corlib_metadata.app_domain_id = app_domain_id;

            WCHAR name[stringMaxSize];
            DWORD name_len = 0;

            hr = assembly_import->GetAssemblyProps(
                corlib_metadata.token,
                &corlib_metadata.public_key,
                &corlib_metadata.public_key_length,
                &corlib_metadata.hash_alg_id,
                name,
                stringMaxSize,
                &name_len,
                &corlib_metadata.metadata,
                &corlib_metadata.flags);
            if (FAILED(hr) || assembly_name_wstring != WSTRING(name)) {
                Warn("Loader::InjectLoaderToModuleInitializer: failed fetching metadata for corlib assembly.");
                return hr;
            }

            corlib_metadata.name = WSTRING(name);
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
        //  class <Module> {
        //
        //      [DllImport("NativeProfilerFile.extension")]
        //      static extern bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize);
        //
        //      static <Module>() {
        //          DD_LoadInitializationAssemblies();
        //      }
        //
        //      static void DD_LoadInitializationAssemblies()
        //      {
        //          if (GetAssemblyAndSymbolsBytes(out var assemblyPtr, out var assemblySize, out var symbolsPtr, out var symbolsSize))
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
        const LPCWSTR module_type_name = WStr("<Module>");
        mdTypeDef module_type_def = mdTypeDefNil;
        hr = metadata_import->FindTypeDefByName(module_type_name, mdTokenNil, &module_type_def);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: failed fetching " +
                ToString(module_type_name) + " (module type) typedef for ModuleID=" + ToString(module_id));
            return hr;
        }

        //
        // Check if the static void <Module>.DD_LoadInitializationAssemblies() mdMethodDef has been already injected.
        //
        const LPCWSTR loader_method_name = WStr("DD_LoadInitializationAssemblies");
        COR_SIGNATURE loader_method_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
        };
        mdMethodDef loader_method_method_def;
        hr = metadata_import->FindMethod(module_type_def, loader_method_name, loader_method_signature, sizeof(loader_method_signature), &loader_method_method_def);
        if (SUCCEEDED(hr)) {
            Info("Loader::InjectLoaderToModuleInitializer: Loader was already injected in ModuleID=" + ToString(module_id));
            return hr;
        }

        Debug("Loader::InjectLoaderToModuleInitializer: Creating all mdTypeRefs and mdMemberRefs required in ModuleID=" + ToString(module_id));


        // **************************************************************************************************************
        //
        //  Emit all definitions required before writing loader method.
        //

        //
        // create assembly ref to mscorlib
        //
        if (corlib_metadata.token == mdAssemblyNil) {
            Warn("Loader::InjectLoaderToModuleInitializer: Loader cannot be injected, corlib was not found.");
            return E_FAIL;
        }
        mdAssemblyRef mscorlib_ref = mdAssemblyRefNil;
        hr = assembly_emit->DefineAssemblyRef(
            corlib_metadata.public_key,
            corlib_metadata.public_key_length,
            corlib_metadata.name.c_str(),
            &corlib_metadata.metadata,
            NULL,
            0,
            corlib_metadata.flags,
            &mscorlib_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Error creating assembly reference to mscorlib.");
            return hr;
        }

        //
        // TypeRefs
        //

        //
        // get a TypeRef for System.Object
        //
        const LPCWSTR system_object_name = WStr("System.Object");
        mdTypeRef system_object_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_object_name, &system_object_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Type
        //
        const LPCWSTR system_type_name = WStr("System.Type");
        mdTypeRef system_type_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_type_name, &system_type_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Byte
        //
        const LPCWSTR system_byte_name = WStr("System.Byte");
        mdTypeRef byte_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_byte_name, &byte_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.String
        //
        const LPCWSTR system_string_name = WStr("System.String");
        mdTypeRef string_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_string_name, &string_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.Assembly
        //
        const LPCWSTR system_reflection_assembly_name = WStr("System.Reflection.Assembly");
        mdTypeRef system_reflection_assembly_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_reflection_assembly_name, &system_reflection_assembly_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }


        //
        // get a TypeRef for System.AppDomain
        //
        const LPCWSTR system_appdomain_name = WStr("System.AppDomain");
        mdTypeRef system_appdomain_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_appdomain_name, &system_appdomain_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.MethodInfo
        //
        const LPCWSTR system_reflection_methodinfo_name = WStr("System.Reflection.MethodInfo");
        mdTypeRef system_reflection_methodinfo_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_reflection_methodinfo_name, &system_reflection_methodinfo_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Reflection.MethodBase
        //
        const LPCWSTR system_reflection_methodbase_name = WStr("System.Reflection.MethodBase");
        mdTypeRef system_reflection_methodbase_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_reflection_methodbase_name, &system_reflection_methodbase_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // get a TypeRef for System.Runtime.InteropServices.Marshal
        //
        const LPCWSTR system_runtime_interopservices_marshal_name = WStr("System.Runtime.InteropServices.Marshal");
        mdTypeRef marshal_type_ref;
        hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, system_runtime_interopservices_marshal_name, &marshal_type_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineTypeRefByName failed");
            return hr;
        }

        //
        // MemberRefs
        //

        //
        // get a MemberRef for System.Runtime.InteropServices.Marshal.Copy(IntPtr, Byte[], int, int)
        //
        const LPCWSTR copy_name = WStr("Copy");
        mdMemberRef marshal_copy_member_ref;
        COR_SIGNATURE marshal_copy_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                4,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
                ELEMENT_TYPE_I,                 // List of parameter types
                ELEMENT_TYPE_SZARRAY,
                ELEMENT_TYPE_U1,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_I4 };
        hr = metadata_emit->DefineMemberRef(marshal_type_ref, copy_name, marshal_copy_signature, sizeof(marshal_copy_signature), &marshal_copy_member_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMemberRef failed");
            return hr;
        }

        ULONG offset = 0;


        //
        // get a MemberRef for System.AppDomain.get_CurrentDomain()
        //
        const LPCWSTR get_currentdomain_name = WStr("get_CurrentDomain");
        BYTE system_appdomain_type_ref_compressed_token[4];
        ULONG system_appdomain_type_ref_compressed_token_length = CorSigCompressToken(system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

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
        const LPCWSTR load_name = WStr("Load");
        BYTE system_reflection_assembly_type_ref_compressed_token[4];
        ULONG system_reflection_assembly_type_ref_compressed_token_length = CorSigCompressToken(system_reflection_assembly_type_ref, system_reflection_assembly_type_ref_compressed_token);

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
        const LPCWSTR gettype_name = WStr("GetType");
        BYTE system_type_type_ref_compressed_token[4];
        ULONG system_type_type_ref_compressed_token_length = CorSigCompressToken(system_type_type_ref, system_type_type_ref_compressed_token);

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
        const LPCWSTR getmethod_name = WStr("GetMethod");
        BYTE system_reflection_methodinfo_type_ref_compressed_token[4];
        ULONG system_reflection_methodinfo_type_ref_compressed_token_length = CorSigCompressToken(system_reflection_methodinfo_type_ref, system_reflection_methodinfo_type_ref_compressed_token);

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
        const LPCWSTR invoke_name = WStr("Invoke");
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

        //
        // UserStrings definitions
        //

        //
        // Create a string representing
        // "Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader"
        //
        const LPCWSTR managed_loader_startup_type = WStr("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader");
        mdString load_helper_token;
        hr = metadata_emit->DefineUserString(managed_loader_startup_type, (ULONG)WStrLen(managed_loader_startup_type), &load_helper_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString failed");
            return hr;
        }

        //
        // Create a string representing
        // "Run"
        //
        const LPCWSTR run_name = WStr("Run");
        mdString run_string_token;
        hr = metadata_emit->DefineUserString(run_name, (ULONG)WStrLen(run_name), &run_string_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString failed");
            return hr;
        }

        // **************************************************************************************************************

        //
        // create PInvoke method definition to the native GetAssemblyAndSymbolsBytes method.
        //
        mdMethodDef pinvoke_method_def = mdMethodDefNil;
        hr = GetGetAssemblyAndSymbolsBytesMethodDef(metadata_emit, module_type_def, &pinvoke_method_def);
        if (FAILED(hr)) {
            return hr;
        }

        Debug("Loader::InjectLoaderToModuleInitializer: Creating <Module>.DD_LoadInitializationAssemblies() in ModuleID=" + ToString(module_id));

        //
        // If the loader method cannot be found we create <Module>.DD_LoadInitializationAssemblies() mdMethodDef
        //
        hr = metadata_emit->DefineMethod(module_type_def, loader_method_name, mdStatic, loader_method_signature, sizeof(loader_method_signature), 0, 0, &loader_method_method_def);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Error creating the loader method.");
            return hr;
        }

        //
        // Generate a locals signature defined in the following way:
        //   [0] System.IntPtr ("assemblyPtr" - address of assembly bytes)
        //   [1] System.Int32  ("assemblySize" - size of assembly bytes)
        //   [2] System.IntPtr ("symbolsPtr" - address of symbols bytes)
        //   [3] System.Int32  ("symbolsSize" - size of symbols bytes)
        //   [4] System.Byte[] ("assemblyBytes" - managed byte array for assembly)
        //   [5] System.Byte[] ("symbolsBytes" - managed byte array for symbols)
        //
        mdSignature locals_signature_token;
        COR_SIGNATURE locals_signature[10] = {
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
        hr = metadata_emit->GetTokenFromSig(locals_signature, sizeof(locals_signature), &locals_signature_token);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Unable to generate locals signature. ModuleID=" + ToString(module_id));
            return hr;
        }

        //
        // Write method body
        //
        ILRewriter rewriter_void(this->info_, nullptr, module_id, loader_method_method_def);
        rewriter_void.InitializeTiny();
        rewriter_void.SetTkLocalVarSig(locals_signature_token);
        ILInstr* pFirstInstr = rewriter_void.GetILList()->m_pNext;

        // Step 1) Call bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize, int appDomainId)

        // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index 0)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDLOCA_S, 0);
        // ldloca.s 1 : Load the address of the "assemblySize" variable (locals index 1)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDLOCA_S, 1);
        // ldloca.s 2 : Load the address of the "symbolsPtr" variable (locals index 2)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDLOCA_S, 2);
        // ldloca.s 3 : Load the address of the "symbolsSize" variable (locals index 3)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDLOCA_S, 3);
        // call bool GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALL, pinvoke_method_def);
        // check if the return of the method call is true or false
        ILInstr* pBranchFalseInstr = CreateILInstr(rewriter_void, pFirstInstr, CEE_BRFALSE);

        // Step 2) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the managed assembly bytes

        // ldloc.1 : Load the "assemblySize" variable (locals index 1)
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDLOC_1);
        // newarr System.Byte : Create a new Byte[] to hold a managed copy of the assembly data
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_NEWARR, byte_type_ref);
        // stloc.s 4 : Assign the Byte[] to the "assemblyBytes" variable (locals index 4)
        CreateILInstrArg8(rewriter_void, pFirstInstr, CEE_STLOC_S, 4);
        // ldloc.0 : Load the "assemblyPtr" variable (locals index 0)
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDLOC_0);
        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4)
        CreateILInstrArg8(rewriter_void, pFirstInstr, CEE_LDLOC_S, 4);
        // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDC_I4_0);
        // ldloc.1 : Load the "assemblySize" variable (locals index 1) for the Marshal.Copy length parameter
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDLOC_1);
        // call Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALL, marshal_copy_member_ref);

        // Step 3) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the symbols bytes

        // ldloc.3 : Load the "symbolsSize" variable (locals index 3)
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDLOC_3);
        // newarr System.Byte : Create a new Byte[] to hold a managed copy of the symbols data
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_NEWARR, byte_type_ref);
        // stloc.s 5 : Assign the Byte[] to the "symbolsBytes" variable (locals index 5)
        CreateILInstrArg8(rewriter_void, pFirstInstr, CEE_STLOC_S, 5);
        // ldloc.2 : Load the "symbolsPtr" variables (locals index 2)
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDLOC_2);
        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5)
        CreateILInstrArg8(rewriter_void, pFirstInstr, CEE_LDLOC_S, 5);
        // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDC_I4_0);
        // ldloc.3 : Load the "symbolsSize" variable (locals index 3) for the Marshal.Copy length parameter
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDLOC_3);
        // call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALL, marshal_copy_member_ref);

        // Step 4) Call System.Reflection.Assembly System.AppDomain.CurrentDomain.Load(byte[], byte[]))

        // call System.AppDomain System.AppDomain.CurrentDomain property
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALL, appdomain_get_current_domain_member_ref);
        // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the first byte[] parameter of AppDomain.Load(byte[], byte[])
        CreateILInstrArg8(rewriter_void, pFirstInstr, CEE_LDLOC_S, 4);
        // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the second byte[] parameter of AppDomain.Load(byte[], byte[])
        CreateILInstrArg8(rewriter_void, pFirstInstr, CEE_LDLOC_S, 5);
        // callvirt System.Reflection.Assembly System.AppDomain.Load(uint8[], uint8[])
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALLVIRT, appdomain_load_member_ref);

        // Step 5) Call instance method
        // Assembly.GetType("Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader", true)

        // ldstr "Datadog.AutoInstrumentation.ManagedLoader.AssemblyLoader"
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDSTR, load_helper_token);
        // ldc.i4.1 load true for boolean type
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDC_I4_1);
        // callvirt System.Type System.Reflection.Assembly.GetType(string, bool)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALLVIRT, assembly_get_type_member_ref);

        // Step 6) Call instance method
        // System.Type.GetMethod("Run");

        // ldstr "Run"
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDSTR, run_string_token);
        // callvirt instance class System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string)
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALLVIRT, type_get_method_member_ref);

        // Step 7) Call instance method
        // System.Reflection.MethodBase.Invoke(null, new object[] { new string[] { "Assembly1" }, new string[] { "Assembly2" } });

        // ldnull
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDNULL);
        // create an array of 2 elements with two string[] parameter
        // ldc.i4.2 = const int 2 => array length
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDC_I4_2);
        // newarr System.Object
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_NEWARR, system_object_type_ref);

        // *************************************************************************************************************** FIRST PARAMETER

        // dup
        CreateILInstr(rewriter_void, pFirstInstr, CEE_DUP);
        // ldc.i4.0 = const int 0 => array index 0
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDC_I4_0);
        // write string array
        hr = WriteAssembliesStringArray(rewriter_void, metadata_emit, assembly_string_default_appdomain_vector_, pFirstInstr, string_type_ref);
        if (FAILED(hr)) {
            return hr;
        }
        // stelem.ref
        CreateILInstr(rewriter_void, pFirstInstr, CEE_STELEM_REF);

        // *************************************************************************************************************** SECOND PARAMETER

        // dup
        CreateILInstr(rewriter_void, pFirstInstr, CEE_DUP);
        // ldc.i4.1 = const int 1 => array index 1
        CreateILInstr(rewriter_void, pFirstInstr, CEE_LDC_I4_1);
        // write string array
        hr = WriteAssembliesStringArray(rewriter_void, metadata_emit, assembly_string_nondefault_appdomain_vector_, pFirstInstr, string_type_ref);
        if (FAILED(hr)) {
            return hr;
        }
        // stelem.ref
        CreateILInstr(rewriter_void, pFirstInstr, CEE_STELEM_REF);

        // ***************************************************************************************************************

        // callvirt instance class object System.Reflection.MethodBase::Invoke(object, object[])
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_CALLVIRT, methodbase_invoke_member_ref);

        // Step 8) Pop and return

        // pop the returned object
        CreateILInstr(rewriter_void, pFirstInstr, CEE_POP);
        // return
        pBranchFalseInstr->m_pTarget = CreateILInstr(rewriter_void, pFirstInstr, CEE_RET);

        hr = rewriter_void.Export();
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Export() failed for ModuleID=" + ToString(module_id));
            return hr;
        }


        // ***************************************************************************************************************

        //
        // Check if the <Module> type has already a ..ctor, if not we create an empty one.
        //
        const LPCWSTR constructor_name = WStr(".cctor");
        COR_SIGNATURE cctor_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                0,                              // Number of parameters
                ELEMENT_TYPE_VOID,              // Return type
        };

        mdMethodDef cctor_method_def = mdMethodDefNil;
        hr = metadata_import->FindMethod(module_type_def, constructor_name, cctor_signature, sizeof(cctor_signature), &cctor_method_def);
        if (FAILED(hr)) {
            Debug("Loader::InjectLoaderToModuleInitializer: failed fetching <Module>..ctor mdMethodDef, creating new .ctor [ModuleID=" +
                ToString(module_id) + ", AppDomainID=" + ToString(app_domain_id) + ", Module=" + module_file_name_string + "]");

            //
            // Define a new ..ctor for the <Module> type
            //
            hr = metadata_emit->DefineMethod(module_type_def, constructor_name,
                                             mdPublic | mdStatic | mdRTSpecialName | mdSpecialName, cctor_signature,
                                             sizeof(cctor_signature), 0, 0, &cctor_method_def);

            if (FAILED(hr)) {
              Warn("Loader::InjectLoaderToModuleInitializer: Error creating .cctor for <Module> [ModuleID=" +
                  ToString(module_id) + ", AppDomainID=" + ToString(app_domain_id) + ", Module=" + module_file_name_string + "]");
                return hr;
            }

            //
            // Create a simple method body with only the `ret` opcode instruction.
            //
            ILRewriter rewriter(this->info_, nullptr, module_id, cctor_method_def);
            rewriter.InitializeTiny();
            ILRewriterWrapper rewriter_wrapper(&rewriter);
            rewriter_wrapper.SetILPosition(rewriter.GetILList()->m_pNext);
            rewriter_wrapper.Return();
            hr = rewriter.Export();
            if (FAILED(hr)) {
                Warn("Loader::InjectLoaderToModuleInitializer: ILRewriter.Export failed creating .cctor for <Module> [ModuleID=" +
                    ToString(module_id) + ", AppDomainID=" + ToString(app_domain_id) + ", Module=" + module_file_name_string + "]");
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
        ILRewriter rewriter(this->info_, nullptr, module_id, cctor_method_def);
        hr = rewriter.Import();
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Import() failed for ModuleID=" + ToString(module_id) +
                 ", CCTORMethodDef" + ToString(cctor_method_def));
            return hr;
        }
        ILRewriterWrapper rewriter_wrapper(&rewriter);
        rewriter_wrapper.SetILPosition(rewriter.GetILList()->m_pNext);
        rewriter_wrapper.CallMember(loader_method_method_def, false);
        hr = rewriter.Export();
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: Call to ILRewriter.Export() failed for ModuleID=" + ToString(module_id) +
                 ", CCTORMethodDef" + ToString(cctor_method_def));
            return hr;
        }

        Info("Loader::InjectLoaderToModuleInitializer: Loader injected successfully. [ModuleID=" + ToString(module_id) +
              ", AssemblyID=" + ToString(assembly_id) +
              ", AssemblyName=" + ToString(assembly_name_string) +
              ", AppDomainID=" + ToString(app_domain_id) +
              ", ModuleTypeDef=" + ToString(module_type_def) +
              ", ModuleCCTORDef=" + ToString(cctor_method_def) +
              ", Module=" + module_file_name_string + "]");
        return S_OK;
    }

    HRESULT Loader::WriteAssembliesStringArray(
        ILRewriter& rewriter_void,
        const ComPtr<IMetaDataEmit2> metadata_emit,
        const std::vector<WSTRING>& assembly_string_vector,
        ILInstr* pFirstInstr, mdTypeRef string_type_ref) {
      ILInstr* pNewInstr;

      // ldc.i4 = const int (array length)
      CreateILInstrArg64(rewriter_void, pFirstInstr, CEE_LDC_I4, assembly_string_vector.size());
      // newarr System.String
      CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_NEWARR, string_type_ref);

      // loading array index
      for (ULONG i = 0; i < assembly_string_vector.size(); i++) {
        // dup
        CreateILInstr(rewriter_void, pFirstInstr, CEE_DUP);
        // ldc.i4 = const int array index 0
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDC_I4, i);

        // Create a string token
        mdString string_token;
        auto hr = metadata_emit->DefineUserString(assembly_string_vector[i].c_str(), (ULONG)assembly_string_vector[i].size(), &string_token);
        if (FAILED(hr)) {
          Warn("Loader::InjectLoaderToModuleInitializer: DefineUserString for string array failed");
          return hr;
        }

        // ldstr assembly index value
        CreateILInstrArg32(rewriter_void, pFirstInstr, CEE_LDSTR, string_token);
        // stelem.ref
        CreateILInstr(rewriter_void, pFirstInstr, CEE_STELEM_REF);
      }

      return S_OK;
    }

    HRESULT Loader::GetGetAssemblyAndSymbolsBytesMethodDef(const ComPtr<IMetaDataEmit2> metadata_emit, mdTypeDef module_type_def, mdMethodDef* result_method_def) {
        //
        // create PInvoke method definition to the native GetAssemblyAndSymbolsBytes method (interop.cpp)
        //
        // Define a method on the managed side that will PInvoke into the profiler
        // method: C++: bool GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int*
        // assemblySize, BYTE** pSymbolsArray, int* symbolsSize)
        // C#: static extern void GetAssemblyAndSymbolsBytes(out IntPtr
        // assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int
        // symbolsSize)
        const LPCWSTR get_assembly_and_symbols_bytes_name = WStr("GetAssemblyAndSymbolsBytes");
        COR_SIGNATURE get_assembly_bytes_signature[] = {
                IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
                4,                              // Number of parameters
                ELEMENT_TYPE_BOOLEAN,           // Return type
                ELEMENT_TYPE_BYREF,             // List of parameter types
                ELEMENT_TYPE_I,
                ELEMENT_TYPE_BYREF,
                ELEMENT_TYPE_I4,
                ELEMENT_TYPE_BYREF,
                ELEMENT_TYPE_I,
                ELEMENT_TYPE_BYREF,
                ELEMENT_TYPE_I4,
        };
        HRESULT hr = metadata_emit->DefineMethod(module_type_def, get_assembly_and_symbols_bytes_name,
            mdStatic | mdPinvokeImpl | mdHideBySig, get_assembly_bytes_signature,
            sizeof(get_assembly_bytes_signature), 0, 0, result_method_def);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefineMethod for GetAssemblyAndSymbolsBytes failed.");
            return hr;
        }

        metadata_emit->SetMethodImplFlags(*result_method_def, miPreserveSig);
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

        hr = metadata_emit->DefinePinvokeMap(*result_method_def, 0, get_assembly_and_symbols_bytes_name, profiler_ref);
        if (FAILED(hr)) {
            Warn("Loader::InjectLoaderToModuleInitializer: DefinePinvokeMap for GetAssemblyAndSymbolsBytes failed");
            return hr;
        }

        return hr;
    }

    bool Loader::GetAssemblyAndSymbolsBytes(void** pAssemblyArray, int* assemblySize, void** pSymbolsArray, int* symbolsSize) {
        //
        // global lock
        //
        std::lock_guard<std::mutex> guard(loaders_loaded_mutex_);

        //
        // gets the current thread id
        //
        ThreadID thread_id;
        HRESULT hr = this->info_->GetCurrentThreadID(&thread_id);
        if (FAILED(hr)) {
            Warn("Loader::GetAssemblyAndSymbolsBytes: ThreadId could not be retrieved.");
        }

        //
        // gets the current appdomain id
        //
        AppDomainID app_domain_id;
        hr = this->info_->GetThreadAppDomain(thread_id, &app_domain_id);
        if (FAILED(hr)) {
            Warn("Loader::GetAssemblyAndSymbolsBytes: AppDomainID could not be retrieved.");
        }

        std::string trait = "[AppDomainId=" + ToString(app_domain_id) + ", ThreadId=" + ToString(thread_id) + "]";

        //
        // check if the loader has been already loaded for this AppDomain
        //
        if (loaders_loaded_.find(app_domain_id) != loaders_loaded_.end()) {
            Debug("Loader::GetAssemblyAndSymbolsBytes: The loader was already loaded. " + trait);
            return false;
        }

        Info("Loader::GetAssemblyAndSymbolsBytes: Loading loader data. " + trait);
        loaders_loaded_.insert(app_domain_id);

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

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for " + trait + " (platform=_WIN32)."
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

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for AppDomainID=" + ToString(app_domain_id) + " (platform=LINUX)."
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

        Debug("Loader::GetAssemblyAndSymbolsBytes: Loaded resouces for AppDomainID=" + ToString(app_domain_id) + " (platform=MACOS)."
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