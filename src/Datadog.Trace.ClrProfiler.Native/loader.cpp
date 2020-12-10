#include "loader.h"

#include "dllmain.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "logging.h"
#include "resource.h"

#ifdef OSX
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

namespace trace {

#ifdef LINUX
extern uint8_t dll_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_start");
extern uint8_t dll_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_end");

extern uint8_t pdb_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_start");
extern uint8_t pdb_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_end");
#endif

Loader* loader = nullptr;

Loader::Loader(ICorProfilerInfo4* info) {
  info_ = info;
  runtime_information_ = GetRuntimeInformation(info);
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
  AssemblyID assembly_id = NULL;
  auto hr = this->info_->GetModuleInfo2(module_id, NULL, NULL, NULL, NULL, &assembly_id, NULL);
  if (FAILED(hr)) {
    Warn("Loader::InjectLoaderToModuleInitializer: ",
         "failed fetching AssemblyID for ModuleID=", module_id);
    return hr;
  }

  //
  // retrieve AppDomainID from AssemblyID
  //
  AppDomainID app_domain_id = NULL;
  WCHAR assembly_name[100];
  DWORD assembly_name_len = 0;
  hr = this->info_->GetAssemblyInfo(assembly_id, 100, &assembly_name_len,
                                    assembly_name, &app_domain_id, NULL);
  if (FAILED(hr)) {
    Warn("Loader::InjectLoaderToModuleInitializer: ",
         "failed fetching AppDomainID for AssemblyID=", assembly_id);
    return hr;
  }

  //
  // check if the module is not the loader itself
  //
  if (WSTRING(assembly_name) == "Datadog.Trace.ClrProfiler.Managed.Loader"_W) {
    Warn("Loader::InjectLoaderToModuleInitializer: The module is the loader itself, skipping it.");
    loaders_loaded_.insert(app_domain_id);
    return E_FAIL;
  }

  //
  // check if the loader has been already loaded for this AppDomain
  //
  if (loaders_loaded_.find(app_domain_id) != loaders_loaded_.end()) {
    return E_FAIL;
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
    Warn("Loader::InjectLoaderToModuleInitializer: ",
         "failed fetching metadata interfaces for ModuleID=", module_id);
    return hr;
  }

  //
  // Extract both IMetaDataImport2 and IMetaDataEmit2 interfaces
  //
  const auto metadata_import = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit = metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);

  //
  // Get the mdTypeDef of <Module> type
  //
  mdTypeDef module_type_def = mdTypeDefNil;
  hr = metadata_import->FindTypeDefByName(L"<Module>", mdTokenNil, &module_type_def);
  if (FAILED(hr)) {
    Warn("Loader::InjectLoaderToModuleInitializer: ",
         "failed fetching <Module> typedef for ModuleID=", module_id);
    return hr;
  }

  //
  // Check if the <Module> type has already a ..ctor, if not we create an empty one.
  //
  BYTE cctor_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
      0,                              // Number of parameters
      ELEMENT_TYPE_VOID,              // Return type
      ELEMENT_TYPE_OBJECT             // List of parameter types
  };

  mdMethodDef cctor_method_def = mdMethodDefNil;
  hr = metadata_import->FindMethod(module_type_def, L".cctor", cctor_signature,
                                   sizeof(cctor_signature), &cctor_method_def);
  if (FAILED(hr)) {
    Debug("Loader::InjectLoaderToModuleInitializer: ",
         "failed fetching <Module>..ctor methoddef for ModuleID=", module_id, ". Creating new .cctor");

    // Define a new ..ctor for the <Module> type
    hr = metadata_emit->DefineMethod(module_type_def, L".cctor",
        mdPublic | mdStatic | mdRTSpecialName | mdSpecialName, cctor_signature,
        sizeof(cctor_signature), 0, 0, &cctor_method_def);

    if (FAILED(hr)) {
      Warn("Error creating .cctor for <Module> ModuleID=", module_id);
      return hr;
    }

    // Create a simple method body with only the `ret` opcode instruction.
    ILRewriter rewriter(this->info_, nullptr, module_id, cctor_method_def);
    rewriter.InitializeTiny();
    ILInstr* pFirstInstr = rewriter.GetILList()->m_pNext;
    ILInstr* pNewInstr = NULL;

    pNewInstr = rewriter.NewILInstr();
    pNewInstr->m_opcode = CEE_RET;
    rewriter.InsertBefore(pFirstInstr, pNewInstr);

    hr = rewriter.Export();
    if (FAILED(hr)) {
      Warn("ILRewriter.Export failed creating .cctor for <Module> ModuleID=", module_id);
      return hr;
    }
  }

  Info("Loader::InjectLoaderToModuleInitializer [ModuleID=", module_id,
       ", AssemblyID=", assembly_id, ", AppDomainID=", app_domain_id,
       ", ModuleTypeDef=", module_type_def,
       ", ModuleCCTORDef=", cctor_method_def, "]");
  return S_OK;
}

void Loader::GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray, int* symbolsSize) const {
  Info("Loader::GetAssemblyAndSymbolsBytes");

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
#else
  const auto imgCount = _dyld_image_count();

  for (auto i = 0; i < imgCount; i++) {
    const auto name = std::string(_dyld_get_image_name(i));

    if (name.rfind("Datadog.Trace.ClrProfiler.Native.dylib") !=
        std::string::npos) {
      const auto header =
          (const struct mach_header_64*)_dyld_get_image_header(i);

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
#endif
  return;
}

HRESULT Loader::RunILStartupHook(
    const ComPtr<IMetaDataEmit2>& metadata_emit, const ModuleID module_id,
    const mdToken function_token) {

  mdMethodDef ret_method_token;
  auto hr = loader->GenerateVoidILStartupMethod(module_id, &ret_method_token);

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to GenerateVoidILStartupMethod failed for ",
         module_id);
    return hr;
  }

  ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
  hr = rewriter.Import();

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to ILRewriter.Import() failed for ", module_id,
         " ", function_token);
    return hr;
  }

  ILRewriterWrapper rewriter_wrapper(&rewriter);

  // Get first instruction and set the rewriter to that location
  ILInstr* pInstr = rewriter.GetILList()->m_pNext;
  rewriter_wrapper.SetILPosition(pInstr);
  rewriter_wrapper.CallMember(ret_method_token, false);
  hr = rewriter.Export();

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to ILRewriter.Export() failed for ModuleID=",
         module_id, " ", function_token);
    return hr;
  }

  return S_OK;
}

HRESULT Loader::GenerateVoidILStartupMethod(const ModuleID module_id, mdMethodDef* ret_method_token) {
  ComPtr<IUnknown> metadata_interfaces;
  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: failed to get metadata interface for ",
         module_id);
    return hr;
  }

  const auto metadata_import =
      metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit =
      metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_emit =
      metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  mdModuleRef mscorlib_ref;
  hr = CreateAssemblyRefToMscorlib(assembly_emit, &mscorlib_ref);

  if (FAILED(hr)) {
    Warn(
        "GenerateVoidILStartupMethod: failed to define AssemblyRef to "
        "mscorlib");
    return hr;
  }

  // Define a TypeRef for System.Object
  mdTypeRef object_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.Object"_W.c_str(), &object_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Define a new TypeDef __DDVoidMethodType__ that extends System.Object
  mdTypeDef new_type_def;
  hr = metadata_emit->DefineTypeDef("__DDVoidMethodType__"_W.c_str(),
                                    tdAbstract | tdSealed, object_type_ref,
                                    NULL, &new_type_def);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeDef failed");
    return hr;
  }

  // Define a new static method __DDVoidMethodCall__ on the new type that has a
  // void return type and takes no arguments
  BYTE initialize_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
      0,                              // Number of parameters
      ELEMENT_TYPE_VOID,              // Return type
      ELEMENT_TYPE_OBJECT             // List of parameter types
  };
  hr = metadata_emit->DefineMethod(
      new_type_def, "__DDVoidMethodCall__"_W.c_str(), mdStatic,
      initialize_signature, sizeof(initialize_signature), 0, 0,
      ret_method_token);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMethod failed");
    return hr;
  }

  /////////////////////////////////////////////
  // Define a new static int field _isAssemblyLoaded on the new type.
  BYTE field_signature[] = {IMAGE_CEE_CS_CALLCONV_FIELD, ELEMENT_TYPE_I4};
  mdFieldDef isAssemblyLoadedFieldToken;
  hr = metadata_emit->DefineField(new_type_def, "_isAssemblyLoaded"_W.c_str(),
                                  fdStatic | fdPrivate, field_signature,
                                  sizeof(field_signature), 0, nullptr, 0,
                                  &isAssemblyLoadedFieldToken);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineField _isAssemblyLoaded failed");
    return hr;
  }

  // Define a new static method IsAlreadyLoaded on the new type that has a bool
  // return type and takes no arguments;
  BYTE already_loaded_signature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT, 0,
                                     ELEMENT_TYPE_BOOLEAN, ELEMENT_TYPE_OBJECT};
  mdMethodDef alreadyLoadedMethodToken;
  hr = metadata_emit->DefineMethod(
      new_type_def, "IsAlreadyLoaded"_W.c_str(), mdStatic | mdPrivate,
      already_loaded_signature, sizeof(already_loaded_signature), 0, 0,
      &alreadyLoadedMethodToken);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMethod IsAlreadyLoaded failed");
    return hr;
  }

  // Get a TypeRef for System.Threading.Interlocked
  mdTypeRef interlocked_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.Threading.Interlocked"_W.c_str(),
      &interlocked_type_ref);
  if (FAILED(hr)) {
    Warn(
        "GenerateVoidILStartupMethod: DefineTypeRefByName interlocked_type_ref "
        "failed");
    return hr;
  }

  // Create method signature for
  // System.Threading.Interlocked::CompareExchange(int32&, int32, int32)
  COR_SIGNATURE interlocked_compare_exchange_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,
      3,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_I4};

  mdMemberRef interlocked_compare_member_ref;
  hr = metadata_emit->DefineMemberRef(
      interlocked_type_ref, "CompareExchange"_W.c_str(),
      interlocked_compare_exchange_signature,
      sizeof(interlocked_compare_exchange_signature),
      &interlocked_compare_member_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef CompareExchange failed");
    return hr;
  }

  /////////////////////////////////////////////
  // Add IL instructions into the IsAlreadyLoaded method
  //
  //  static int _isAssemblyLoaded = 0;
  //
  //  public static bool IsAlreadyLoaded() {
  //      return Interlocked.CompareExchange(ref _isAssemblyLoaded, 1, 0) == 1;
  //  }
  //
  ILRewriter rewriter_already_loaded(this->info_, nullptr, module_id,
                                     alreadyLoadedMethodToken);
  rewriter_already_loaded.InitializeTiny();

  ILInstr* pALFirstInstr = rewriter_already_loaded.GetILList()->m_pNext;
  ILInstr* pALNewInstr = NULL;

  // ldsflda _isAssemblyLoaded : Load the address of the "_isAssemblyLoaded"
  // static var
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDSFLDA;
  pALNewInstr->m_Arg32 = isAssemblyLoadedFieldToken;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ldc.i4.1 : Load the constant 1 (int) to the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDC_I4_1;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ldc.i4.0 : Load the constant 0 (int) to the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDC_I4_0;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // call int Interlocked.CompareExchange(ref int, int, int) method
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_CALL;
  pALNewInstr->m_Arg32 = interlocked_compare_member_ref;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ldc.i4.1 : Load the constant 1 (int) to the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDC_I4_1;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ceq : Compare equality from two values from the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_CEQ;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ret : Return the value of the comparison
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_RET;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  hr = rewriter_already_loaded.Export();
  if (FAILED(hr)) {
    Warn(
        "GenerateVoidILStartupMethod: Call to ILRewriter.Export() failed for "
        "ModuleID=",
        module_id);
    return hr;
  }

  // Define a method on the managed side that will PInvoke into the profiler
  // method: C++: void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int*
  // assemblySize, BYTE** pSymbolsArray, int* symbolsSize) C#: static extern
  // void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int
  // assemblySize, out IntPtr symbolsPtr, out int symbolsSize)
  mdMethodDef pinvoke_method_def;
  COR_SIGNATURE get_assembly_bytes_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,  // Calling convention
      4,                              // Number of parameters
      ELEMENT_TYPE_VOID,              // Return type
      ELEMENT_TYPE_BYREF,             // List of parameter types
      ELEMENT_TYPE_I,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I4,
  };
  hr = metadata_emit->DefineMethod(
      new_type_def, "GetAssemblyAndSymbolsBytes"_W.c_str(),
      mdStatic | mdPinvokeImpl | mdHideBySig, get_assembly_bytes_signature,
      sizeof(get_assembly_bytes_signature), 0, 0, &pinvoke_method_def);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMethod failed");
    return hr;
  }

  metadata_emit->SetMethodImplFlags(pinvoke_method_def, miPreserveSig);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: SetMethodImplFlags failed");
    return hr;
  }

#ifdef _WIN32
  WSTRING native_profiler_file = "DATADOG.TRACE.CLRPROFILER.NATIVE.DLL"_W;
#else  // _WIN32

#ifdef BIT64
  WSTRING native_profiler_file =
      GetEnvironmentValue("CORECLR_PROFILER_PATH_64"_W);
  Debug(
      "GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH_64 defined "
      "as: ",
      native_profiler_file);
  if (native_profiler_file == ""_W) {
    native_profiler_file = GetEnvironmentValue("CORECLR_PROFILER_PATH"_W);
    Debug(
        "GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH defined "
        "as: ",
        native_profiler_file);
  }
#else   // BIT64
  WSTRING native_profiler_file =
      GetEnvironmentValue("CORECLR_PROFILER_PATH_32"_W);
  Debug(
      "GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH_32 defined "
      "as: ",
      native_profiler_file);
  if (native_profiler_file == ""_W) {
    native_profiler_file = GetEnvironmentValue("CORECLR_PROFILER_PATH"_W);
    Debug(
        "GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH defined "
        "as: ",
        native_profiler_file);
  }
#endif  // BIT64
  Debug(
      "GenerateVoidILStartupMethod: Linux: Setting the PInvoke native profiler "
      "library path to ",
      native_profiler_file);

#endif  // _WIN32

  mdModuleRef profiler_ref;
  hr = metadata_emit->DefineModuleRef(native_profiler_file.c_str(),
                                      &profiler_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineModuleRef failed");
    return hr;
  }

  hr = metadata_emit->DefinePinvokeMap(pinvoke_method_def, 0,
                                       "GetAssemblyAndSymbolsBytes"_W.c_str(),
                                       profiler_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefinePinvokeMap failed");
    return hr;
  }

  // Get a TypeRef for System.Byte
  mdTypeRef byte_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, "System.Byte"_W.c_str(),
                                          &byte_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a TypeRef for System.Runtime.InteropServices.Marshal
  mdTypeRef marshal_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.Runtime.InteropServices.Marshal"_W.c_str(),
      &marshal_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a MemberRef for System.Runtime.InteropServices.Marshal.Copy(IntPtr,
  // Byte[], int, int)
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
  hr = metadata_emit->DefineMemberRef(
      marshal_type_ref, "Copy"_W.c_str(), marshal_copy_signature,
      sizeof(marshal_copy_signature), &marshal_copy_member_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Get a TypeRef for System.Reflection.Assembly
  mdTypeRef system_reflection_assembly_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.Reflection.Assembly"_W.c_str(),
      &system_reflection_assembly_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a MemberRef for System.Object.ToString()
  mdTypeRef system_object_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.Object"_W.c_str(), &system_object_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a TypeRef for System.AppDomain
  mdTypeRef system_appdomain_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.AppDomain"_W.c_str(), &system_appdomain_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a MemberRef for System.AppDomain.get_CurrentDomain()
  // and System.AppDomain.Assembly.Load(byte[], byte[])

  // Create method signature for AppDomain.CurrentDomain property
  COR_SIGNATURE appdomain_get_current_domain_signature_start[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT, 0,
      ELEMENT_TYPE_CLASS,  // ret = System.AppDomain
      // insert compressed token for System.AppDomain TypeRef here
  };
  ULONG start_length = sizeof(appdomain_get_current_domain_signature_start);

  BYTE system_appdomain_type_ref_compressed_token[4];
  ULONG token_length = CorSigCompressToken(
      system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

  COR_SIGNATURE* appdomain_get_current_domain_signature =
      new COR_SIGNATURE[start_length + token_length];
  memcpy(appdomain_get_current_domain_signature,
         appdomain_get_current_domain_signature_start, start_length);
  memcpy(&appdomain_get_current_domain_signature[start_length],
         system_appdomain_type_ref_compressed_token, token_length);

  mdMemberRef appdomain_get_current_domain_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_appdomain_type_ref, "get_CurrentDomain"_W.c_str(),
      appdomain_get_current_domain_signature, start_length + token_length,
      &appdomain_get_current_domain_member_ref);
  delete[] appdomain_get_current_domain_signature;

  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Create method signature for AppDomain.Load(byte[], byte[])
  COR_SIGNATURE appdomain_load_signature_start[] = {
      IMAGE_CEE_CS_CALLCONV_HASTHIS, 2,
      ELEMENT_TYPE_CLASS  // ret = System.Reflection.Assembly
      // insert compressed token for System.Reflection.Assembly TypeRef here
  };
  COR_SIGNATURE appdomain_load_signature_end[] = {
      ELEMENT_TYPE_SZARRAY, ELEMENT_TYPE_U1, ELEMENT_TYPE_SZARRAY,
      ELEMENT_TYPE_U1};
  start_length = sizeof(appdomain_load_signature_start);
  ULONG end_length = sizeof(appdomain_load_signature_end);

  BYTE system_reflection_assembly_type_ref_compressed_token[4];
  token_length =
      CorSigCompressToken(system_reflection_assembly_type_ref,
                          system_reflection_assembly_type_ref_compressed_token);

  COR_SIGNATURE* appdomain_load_signature =
      new COR_SIGNATURE[start_length + token_length + end_length];
  memcpy(appdomain_load_signature, appdomain_load_signature_start,
         start_length);
  memcpy(&appdomain_load_signature[start_length],
         system_reflection_assembly_type_ref_compressed_token, token_length);
  memcpy(&appdomain_load_signature[start_length + token_length],
         appdomain_load_signature_end, end_length);

  mdMemberRef appdomain_load_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_appdomain_type_ref, "Load"_W.c_str(), appdomain_load_signature,
      start_length + token_length + end_length, &appdomain_load_member_ref);
  delete[] appdomain_load_signature;

  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Create method signature for Assembly.CreateInstance(string)
  COR_SIGNATURE assembly_create_instance_signature[] = {
      IMAGE_CEE_CS_CALLCONV_HASTHIS, 1,
      ELEMENT_TYPE_OBJECT,  // ret = System.Object
      ELEMENT_TYPE_STRING};

  mdMemberRef assembly_create_instance_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_reflection_assembly_type_ref, "CreateInstance"_W.c_str(),
      assembly_create_instance_signature,
      sizeof(assembly_create_instance_signature),
      &assembly_create_instance_member_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Create a string representing
  // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup" Create OS-specific
  // implementations because on Windows, creating the string via
  // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"_W.c_str() does not
  // create the proper string for CreateInstance to successfully call
#ifdef _WIN32
  LPCWSTR load_helper_str = L"Datadog.Trace.ClrProfiler.Managed.Loader.Startup";
  auto load_helper_str_size = wcslen(load_helper_str);
#else
  char16_t load_helper_str[] =
      u"Datadog.Trace.ClrProfiler.Managed.Loader.Startup";
  auto load_helper_str_size =
      std::char_traits<char16_t>::length(load_helper_str);
#endif

  mdString load_helper_token;
  hr = metadata_emit->DefineUserString(
      load_helper_str, (ULONG)load_helper_str_size, &load_helper_token);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineUserString failed");
    return hr;
  }

  ULONG string_len = 0;
  WCHAR string_contents[kNameMaxSize]{};
  hr = metadata_import->GetUserString(load_helper_token, string_contents,
                                      kNameMaxSize, &string_len);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: fail quickly", module_id);
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
  hr = metadata_emit->GetTokenFromSig(
      locals_signature, sizeof(locals_signature), &locals_signature_token);
  if (FAILED(hr)) {
    Warn(
        "GenerateVoidILStartupMethod: Unable to generate locals signature. "
        "ModuleID=",
        module_id);
    return hr;
  }

  /////////////////////////////////////////////
  // Add IL instructions into the void method
  ILRewriter rewriter_void(this->info_, nullptr, module_id, *ret_method_token);
  rewriter_void.InitializeTiny();
  rewriter_void.SetTkLocalVarSig(locals_signature_token);

  ILInstr* pFirstInstr = rewriter_void.GetILList()->m_pNext;
  ILInstr* pNewInstr = NULL;

  // Step 0) Check if the assembly was already loaded

  // call bool IsAlreadyLoaded()
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = alreadyLoadedMethodToken;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // check if the return of the method call is true or false
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_BRFALSE_S;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);
  ILInstr* pBranchFalseInstr = pNewInstr;

  // return if IsAlreadyLoaded is true
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_RET;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 1) Call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out
  // int assemblySize, out IntPtr symbolsPtr, out int symbolsSize)

  // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index
  // 0)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOCA_S;
  pNewInstr->m_Arg32 = 0;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Set the false branch target
  pBranchFalseInstr->m_pTarget = pNewInstr;

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

  // call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int
  // assemblySize, out IntPtr symbolsPtr, out int symbolsSize)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = pinvoke_method_def;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 2) Call void Marshal.Copy(IntPtr source, byte[] destination, int
  // startIndex, int length) to populate the managed assembly bytes

  // ldloc.1 : Load the "assemblySize" variable (locals index 1)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_1;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

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
    Warn(
        "GenerateVoidILStartupMethod: Call to ILRewriter.Export() failed for "
        "ModuleID=",
        module_id);
    return hr;
  }

  return S_OK;
}

}