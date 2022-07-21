#include "cor_profiler.h"

#include "corhlpr.h"
#include <corprof.h>
#include <string>
#include <typeinfo>

#include "clr_helpers.h"
#include "dd_profiler_constants.h"
#include "dllmain.h"
#include "environment_variables.h"
#include "environment_variables_util.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "integration.h"
#include "logger.h"
#include "metadata_builder.h"
#include "module_metadata.h"
#include "resource.h"
#include "stats.h"
#include "version.h"

#include "../../../shared/src/native-src/pal.h"

#ifdef MACOS
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

namespace trace
{

CorProfiler* profiler = nullptr;

//
// ICorProfilerCallback methods
//
HRESULT STDMETHODCALLTYPE CorProfiler::Initialize(IUnknown* cor_profiler_info_unknown)
{
    auto _ = trace::Stats::Instance()->InitializeMeasure();

    // check if debug mode is enabled
    if (IsDebugEnabled())
    {
        Logger::EnableDebug();
    }

    CorProfilerBase::Initialize(cor_profiler_info_unknown);

    // check if tracing is completely disabled.
    if (IsTracingDisabled())
    {
        if (IsAzureAppServices() && (NeedsAgentInAAS() || NeedsDogstatsdInAAS()))
        {
            // In AAS, the profiler is used to load other processes, so we bypass this check. If the tracer is disabled, the managed loader won't initialize instrumentation
            Logger::Info("DATADOG TRACER DIAGNOSTICS - In AAS, automatic tracing is disabled but keeping the profiler up to start child processes.");
        }
        else
        {
            Logger::Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled in ", environment::tracing_enabled);
            return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
        }
    }

#if defined(ARM64) || defined(ARM)
    //
    // In ARM64 and ARM, complete ReJIT support is only available from .NET 5.0
    //
    ICorProfilerInfo12* info12;
    HRESULT hrInfo12 = cor_profiler_info_unknown->QueryInterface(__uuidof(ICorProfilerInfo12), (void**) &info12);
    if (SUCCEEDED(hrInfo12))
    {
        Logger::Info(".NET 5.0 runtime or greater was detected.");
    }
    else
    {
        Logger::Warn("DATADOG TRACER DIAGNOSTICS - Profiler disabled: .NET 5.0 runtime or greater is required on this "
                     "architecture.");
        return E_FAIL;
    }
#endif

    // get Profiler interface (for net46+)
    HRESULT hr = cor_profiler_info_unknown->QueryInterface(__uuidof(ICorProfilerInfo7), (void**) &this->info_);
    if (FAILED(hr))
    {
        Logger::Warn("DATADOG TRACER DIAGNOSTICS - Failed to attach profiler: interface ICorProfilerInfo7 not found.");
        return E_FAIL;
    }

    const auto& process_name = shared::GetCurrentProcessName();
    Logger::Info("ProcessName: ", process_name);

    const auto& include_process_names = shared::GetEnvironmentValues(environment::include_process_names);

    // if there is a process inclusion list, attach profiler only if this
    // process's name is on the list
    if (!include_process_names.empty() && !shared::Contains(include_process_names, process_name))
    {
        Logger::Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", process_name, " not found in ",
                     environment::include_process_names, ".");
        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
    }

    const auto& exclude_process_names = shared::GetEnvironmentValues(environment::exclude_process_names);

    // attach profiler only if this process's name is NOT on the list
    if (!exclude_process_names.empty() && shared::Contains(exclude_process_names, process_name))
    {
        Logger::Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", process_name, " found in ",
                     environment::exclude_process_names, ".");
        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
    }

    Logger::Info("Environment variables:");
    for (auto&& env_var : env_vars_to_display)
    {
        shared::WSTRING env_var_value = shared::GetEnvironmentValue(env_var);
        if (IsDebugEnabled() || !env_var_value.empty())
        {
            Logger::Info("  ", env_var, "=", env_var_value);
        }
    }

    if (IsAzureAppServices())
    {
        Logger::Info("Profiler is operating within Azure App Services context.");

        const auto& app_pool_id_value = shared::GetEnvironmentValue(environment::azure_app_services_app_pool_id);

        if (app_pool_id_value.size() > 1 && app_pool_id_value.at(0) == '~')
        {
            Logger::Info(
                "DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", environment::azure_app_services_app_pool_id, " ",
                app_pool_id_value, " is recognized as an Azure App Services infrastructure process.");
            return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
        }

        const auto& cli_telemetry_profile_value =
            shared::GetEnvironmentValue(environment::azure_app_services_cli_telemetry_profile_value);

        if (cli_telemetry_profile_value == WStr("AzureKudu"))
        {
            Logger::Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", app_pool_id_value,
                         " is recognized as Kudu, an Azure App Services reserved process.");
            return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
        }

        const auto& functions_worker_runtime_value =
            shared::GetEnvironmentValue(environment::azure_app_services_functions_worker_runtime);

        if (!functions_worker_runtime_value.empty() && !IsAzureFunctionsEnabled())
        {
            Logger::Info("DATADOG TRACER DIAGNOSTICS - Profiler explicitly disabled for Azure Functions.");
            return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
        }
    }

    trace_annotations_enabled = IsTraceAnnotationEnabled();

    // get ICorProfilerInfo10 for >= .NET Core 3.0
    ICorProfilerInfo10* info10 = nullptr;
    hr = cor_profiler_info_unknown->QueryInterface(__uuidof(ICorProfilerInfo10), (void**) &info10);
    if (SUCCEEDED(hr))
    {
        Logger::Debug("Interface ICorProfilerInfo10 found.");
    }
    else
    {
        info10 = nullptr;
    }

    auto pInfo = info10 != nullptr ? info10 : this->info_;
    auto work_offloader = std::make_shared<RejitWorkOffloader>(pInfo);

    rejit_handler = info10 != nullptr ? std::make_shared<RejitHandler>(info10, work_offloader)
                                      : std::make_shared<RejitHandler>(this->info_, work_offloader);
    tracer_integration_preprocessor = std::make_unique<TracerRejitPreprocessor>(rejit_handler, work_offloader);

    debugger_instrumentation_requester = std::make_unique<debugger::DebuggerProbesInstrumentationRequester>(rejit_handler, work_offloader);

    DWORD event_mask = COR_PRF_MONITOR_JIT_COMPILATION | COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST |
                       COR_PRF_MONITOR_MODULE_LOADS | COR_PRF_MONITOR_ASSEMBLY_LOADS | COR_PRF_MONITOR_APPDOMAIN_LOADS |
                       COR_PRF_ENABLE_REJIT;

    if (!EnableInlining())
    {
        Logger::Info("JIT Inlining is disabled.");
        event_mask |= COR_PRF_DISABLE_INLINING;
    }
    else
    {
        Logger::Info("JIT Inlining is enabled.");
    }

    if (DisableOptimizations())
    {
        Logger::Info("Disabling all code optimizations.");
        event_mask |= COR_PRF_DISABLE_OPTIMIZATIONS;
    }

    if (IsNGENEnabled())
    {
        Logger::Info("NGEN is enabled.");
        event_mask |= COR_PRF_MONITOR_CACHE_SEARCHES;
    }
    else
    {
        Logger::Info("NGEN is disabled.");
        event_mask |= COR_PRF_DISABLE_ALL_NGEN_IMAGES;
    }

    // set event mask to subscribe to events and disable NGEN images
    hr = this->info_->SetEventMask2(event_mask, COR_PRF_HIGH_ADD_ASSEMBLY_REFERENCES);
    if (FAILED(hr))
    {
        Logger::Warn("DATADOG TRACER DIAGNOSTICS - Failed to attach profiler: unable to set event mask.");
        return E_FAIL;
    }

    runtime_information_ = GetRuntimeInformation(this->info_);
    if (process_name == WStr("w3wp.exe") || process_name == WStr("iisexpress.exe"))
    {
        is_desktop_iis = runtime_information_.is_desktop();
    }

    // writing opcodes vector for the IL dumper
    if (IsDumpILRewriteEnabled())
    {
#define OPDEF(c, s, pop, push, args, type, l, s1, s2, flow) opcodes_names.push_back(s);
#include "opcode.def"
#undef OPDEF
        opcodes_names.push_back("(count)"); // CEE_COUNT
        opcodes_names.push_back("->");      // CEE_SWITCH_ARG
    }

    //
    managed_profiler_assembly_reference = AssemblyReference::GetFromCache(managed_profiler_full_assembly_version);

    const auto currentModuleFileName = shared::GetCurrentModuleFileName();
    if (currentModuleFileName == shared::EmptyWStr)
    {
        Logger::Error("Profiler filepath: cannot be calculated.");
        return E_FAIL;
    }

    // we're in!
    Logger::Info("Profiler filepath: ", currentModuleFileName);
    Logger::Info("Profiler attached.");
    this->info_->AddRef();
    is_attached_.store(true);
    profiler = this;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadFinished(AssemblyID assembly_id, HRESULT hr_status)
{
    auto _ = trace::Stats::Instance()->AssemblyLoadFinishedMeasure();

    if (FAILED(hr_status))
    {
        // if assembly failed to load, skip it entirely,
        // otherwise we can crash the process if module is not valid
        CorProfilerBase::AssemblyLoadFinished(assembly_id, hr_status);
        return S_OK;
    }

    if (!is_attached_)
    {
        return S_OK;
    }

    const auto& assembly_info = GetAssemblyInfo(this->info_, assembly_id);
    if (!assembly_info.IsValid())
    {
        Logger::Debug("AssemblyLoadFinished: ", assembly_id, " ", hr_status);
        return S_OK;
    }

    const auto& is_instrumentation_assembly = assembly_info.name == managed_profiler_name;

    if (is_instrumentation_assembly)
    {
        if (Logger::IsDebugEnabled())
        {
            Logger::Debug("AssemblyLoadFinished: ", assembly_id, " ", hr_status);
        }

        ComPtr<IUnknown> metadata_interfaces;
        auto hr = this->info_->GetModuleMetaData(assembly_info.manifest_module_id, ofRead | ofWrite,
                                                 IID_IMetaDataImport2, metadata_interfaces.GetAddressOf());

        if (FAILED(hr))
        {
            Logger::Warn("AssemblyLoadFinished failed to get metadata interface for module id ",
                         assembly_info.manifest_module_id, " from assembly ", assembly_info.name, " HRESULT=0x",
                         std::setfill('0'), std::setw(8), std::hex, hr);
            return S_OK;
        }

        // Get the IMetaDataAssemblyImport interface to get metadata from the managed assembly
        const auto& assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        const auto& assembly_metadata = GetAssemblyImportMetadata(assembly_import);

        // used multiple times for logging
        const auto& assembly_version = assembly_metadata.version.str();

        if (Logger::IsDebugEnabled())
        {
            Logger::Debug("AssemblyLoadFinished: AssemblyName=", assembly_info.name,
                          " AssemblyVersion=", assembly_version);
        }

        const auto& expected_assembly_reference = trace::AssemblyReference(managed_profiler_full_assembly_version);

        // used multiple times for logging
        const auto& expected_version = expected_assembly_reference.version.str();

        bool is_viable_version;

        if (runtime_information_.is_core())
        {
            is_viable_version = (assembly_metadata.version >= expected_assembly_reference.version);
        }
        else
        {
            is_viable_version = (assembly_metadata.version == expected_assembly_reference.version);
        }

        // Check that Major.Minor.Build matches the profiler version.
        // On .NET Core, allow managed library to be a higher version than the native library.
        if (is_viable_version)
        {
            Logger::Info("AssemblyLoadFinished: Datadog.Trace.dll v", assembly_version, " matched profiler version v",
                         expected_version);
            managed_profiler_loaded_app_domains.insert(assembly_info.app_domain_id);

            if (runtime_information_.is_desktop() && corlib_module_loaded)
            {
                // Set the managed_profiler_loaded_domain_neutral flag whenever the
                // managed profiler is loaded shared
                if (assembly_info.app_domain_id == corlib_app_domain_id)
                {
                    Logger::Info("AssemblyLoadFinished: Datadog.Trace.dll was loaded domain-neutral");
                    managed_profiler_loaded_domain_neutral = true;
                }
                else
                {
                    Logger::Info("AssemblyLoadFinished: Datadog.Trace.dll was not loaded domain-neutral");
                }
            }
        }
        else
        {
            Logger::Warn("AssemblyLoadFinished: Datadog.Trace.dll v", assembly_version,
                         " did not match profiler version v", expected_version);
        }
    }

    return S_OK;
}

void CorProfiler::RewritingPInvokeMaps(const ModuleMetadata& module_metadata, const shared::WSTRING& nativemethods_type_name, const shared::WSTRING& library_path)
{
    HRESULT hr;
    const auto& metadata_import = module_metadata.metadata_import;
    const auto& metadata_emit = module_metadata.metadata_emit;

    // We are in the right module, so we try to load the mdTypeDef from the target type name.
    mdTypeDef nativeMethodsTypeDef = mdTypeDefNil;
    auto foundType =
        FindTypeDefByName(nativemethods_type_name, module_metadata.assemblyName, metadata_import, nativeMethodsTypeDef);
    if (foundType)
    {
        // get the native profiler file path.
        auto native_profiler_file = library_path.empty() ? shared::GetCurrentModuleFileName() : library_path;

        if (!fs::exists(native_profiler_file))
        {
            Logger::Warn("Unable to rewrite PInvokes. Native library not found: ", native_profiler_file);
            return;
        }

        Logger::Info("Rewriting PInvokes to native: ", native_profiler_file);

        // Define the actual profiler file path as a ModuleRef
        mdModuleRef profiler_ref;
        hr = metadata_emit->DefineModuleRef(native_profiler_file.c_str(), &profiler_ref);
        if (SUCCEEDED(hr))
        {
            // Enumerate all methods inside the native methods type with the PInvokes
            Enumerator<mdMethodDef> enumMethods = Enumerator<mdMethodDef>(
                [metadata_import, nativeMethodsTypeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt)
                    -> HRESULT { return metadata_import->EnumMethods(ptr, nativeMethodsTypeDef, arr, max, cnt); },
                [metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });

            EnumeratorIterator<mdMethodDef> enumIterator = enumMethods.begin();
            while (enumIterator != enumMethods.end())
            {
                auto methodDef = *enumIterator;

                const auto& caller = GetFunctionInfo(module_metadata.metadata_import, methodDef);
                Logger::Info("Rewriting PInvoke method: ", caller.name);

                // Get the current PInvoke map to extract the flags and the entrypoint name
                DWORD pdwMappingFlags;
                WCHAR importName[kNameMaxSize]{};
                DWORD importNameLength = 0;
                mdModuleRef importModule;
                hr = metadata_import->GetPinvokeMap(methodDef, &pdwMappingFlags, importName, kNameMaxSize,
                                                    &importNameLength, &importModule);
                if (SUCCEEDED(hr))
                {
                    // Delete the current PInvoke map
                    hr = metadata_emit->DeletePinvokeMap(methodDef);
                    if (SUCCEEDED(hr))
                    {
                        // Define a new PInvoke map with the new ModuleRef of the actual profiler file path
                        hr = metadata_emit->DefinePinvokeMap(methodDef, pdwMappingFlags, shared::WSTRING(importName).c_str(),
                                                             profiler_ref);
                        if (FAILED(hr))
                        {
                            Logger::Warn("ModuleLoadFinished: DefinePinvokeMap to the actual profiler file path "
                                         "failed, trying to restore the previous one.");
                            hr = metadata_emit->DefinePinvokeMap(methodDef, pdwMappingFlags,
                                                                 shared::WSTRING(importName).c_str(), importModule);
                            if (FAILED(hr))
                            {
                                // We only warn that we cannot rewrite the PInvokeMap but we still continue the module
                                // load. These errors must be handled on the caller with a try/catch.
                                Logger::Warn("ModuleLoadFinished: Error trying to restore the previous PInvokeMap.");
                            }
                        }
                    }
                    else
                    {
                        // We only warn that we cannot rewrite the PInvokeMap but we still continue the module load.
                        // These errors must be handled on the caller with a try/catch.
                        Logger::Warn("ModuleLoadFinished: DeletePinvokeMap failed");
                    }
                }

                enumIterator = ++enumIterator;
            }
        }
        else
        {
            // We only warn that we cannot rewrite the PInvokeMap but we still continue the module load.
            // These errors must be handled on the caller with a try/catch.
            Logger::Warn("ModuleLoadFinished: Native Profiler DefineModuleRef failed");
        }
    }
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID module_id, HRESULT hr_status)
{
    auto _ = trace::Stats::Instance()->ModuleLoadFinishedMeasure();

    if (FAILED(hr_status))
    {
        // if module failed to load, skip it entirely,
        // otherwise we can crash the process if module is not valid
        CorProfilerBase::ModuleLoadFinished(module_id, hr_status);
        return S_OK;
    }

    if (!is_attached_)
    {
        return S_OK;
    }

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_ || rejit_handler == nullptr)
    {
        return S_OK;
    }

    auto hr = TryRejitModule(module_id);

    // Push integration definitions from past modules that were unable to be added
    auto rejit_size = rejit_module_method_pairs.size();
    if (rejit_size > 0 && trace_annotation_integration_type != nullptr)
    {
        std::vector<ModuleID> rejitModuleIds;
        for (size_t i = 0; i < rejit_size; i++)
        {
            auto rejit_module_method_pair = rejit_module_method_pairs.front();
            rejitModuleIds.push_back(rejit_module_method_pair.first);

            const auto& methodReferences = rejit_module_method_pair.second;
            integration_definitions_.reserve(integration_definitions_.size() + methodReferences.size());

            Logger::Debug("ModuleLoadFinished requesting ReJIT now for ModuleId=", module_id,
                          ", methodReferences.size()=", methodReferences.size());

            // Push integration definitions from the given module
            for (const auto& methodReference : methodReferences)
            {
                integration_definitions_.push_back(
                    IntegrationDefinition(methodReference, *trace_annotation_integration_type.get(), false, false));
            }

            rejit_module_method_pairs.pop_front();
        }

        // We call the function to analyze the module and request the ReJIT of integrations defined in this module.
        if (tracer_integration_preprocessor != nullptr && !integration_definitions_.empty())
        {
            const auto numReJITs = tracer_integration_preprocessor->RequestRejitForLoadedModules(rejitModuleIds, integration_definitions_);
            Logger::Debug("Total number of ReJIT Requested: ", numReJITs);
        }
    }

    return hr;
}

bool ShouldRewriteProfilerMaps()
{
    auto strValue = shared::GetEnvironmentValue(WStr("DD_PROFILING_ENABLED"));
    bool is_profiler_enabled;
    return shared::TryParseBooleanEnvironmentValue(strValue, is_profiler_enabled) && is_profiler_enabled;
}

std::string GetNativeLoaderFilePath()
{
    auto native_loader_filename =
#ifdef LINUX
        "Datadog.Trace.ClrProfiler.Native.so";
#elif MACOS
        "Datadog.Trace.ClrProfiler.Native.dylib";
#else
        "Datadog.Trace.ClrProfiler.Native.dll";
#endif

    auto module_file_path = fs::path(shared::GetCurrentModuleFileName());

    auto native_loader_file_path =
        module_file_path.parent_path() / ".." /
#ifdef _WIN32
        ".." / // On Windows, the tracer native library is 2 levels away from the native loader.
        
#endif
        native_loader_filename;
    return native_loader_file_path.string();
}

HRESULT CorProfiler::TryRejitModule(ModuleID module_id)
{
    const auto& module_info = GetModuleInfo(this->info_, module_id);
    if (!module_info.IsValid())
    {
        return S_OK;
    }

    if (Logger::IsDebugEnabled())
    {
        Logger::Debug("ModuleLoadFinished: ", module_id, " ", module_info.assembly.name, " AppDomain ",
                      module_info.assembly.app_domain_id, " ", module_info.assembly.app_domain_name, std::boolalpha,
                      " | IsNGEN = ", module_info.IsNGEN(), " | IsDynamic = ", module_info.IsDynamic(),
                      " | IsResource = ", module_info.IsResource(), std::noboolalpha);
    }

    if (module_info.IsNGEN())
    {
        // We check if the Module contains NGEN images and added to the
        // rejit handler list to verify the inlines.
        rejit_handler->AddNGenInlinerModule(module_id);
    }

    AppDomainID app_domain_id = module_info.assembly.app_domain_id;

    // Identify the AppDomain ID of mscorlib which will be the Shared Domain
    // because mscorlib is always a domain-neutral assembly
    if (!corlib_module_loaded && (module_info.assembly.name == mscorlib_assemblyName ||
                                  module_info.assembly.name == system_private_corelib_assemblyName))
    {
        corlib_module_loaded = true;
        corlib_app_domain_id = app_domain_id;

        ComPtr<IUnknown> metadata_interfaces;
        auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                 metadata_interfaces.GetAddressOf());

        // Get the IMetaDataAssemblyImport interface to get metadata from the
        // managed assembly
        const auto& assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        const auto& assembly_metadata = GetAssemblyImportMetadata(assembly_import);

        hr = assembly_import->GetAssemblyProps(assembly_metadata.assembly_token, &corAssemblyProperty.ppbPublicKey,
                                               &corAssemblyProperty.pcbPublicKey, &corAssemblyProperty.pulHashAlgId,
                                               NULL, 0, NULL, &corAssemblyProperty.pMetaData,
                                               &corAssemblyProperty.assemblyFlags);

        if (FAILED(hr))
        {
            Logger::Warn("AssemblyLoadFinished failed to get properties for COR assembly ");
        }

        corAssemblyProperty.szName = module_info.assembly.name;

        Logger::Info("COR library: ", corAssemblyProperty.szName, " ", corAssemblyProperty.pMetaData.usMajorVersion,
                     ".", corAssemblyProperty.pMetaData.usMinorVersion, ".",
                     corAssemblyProperty.pMetaData.usRevisionNumber);

        if (rejit_handler != nullptr)
        {
            rejit_handler->SetCorAssemblyProfiler(&corAssemblyProperty);
        }

        return S_OK;
    }

    // In IIS, the startup hook will be inserted into a method in System.Web (which is domain-neutral)
    // but the Datadog.Trace.ClrProfiler.Managed.Loader assembly that the startup hook loads from a
    // byte array will be loaded into a non-shared AppDomain.
    // In this case, do not insert another startup hook into that non-shared AppDomain
    if (module_info.assembly.name == datadog_trace_clrprofiler_managed_loader_assemblyName)
    {
        Logger::Info("ModuleLoadFinished: Datadog.Trace.ClrProfiler.Managed.Loader loaded into AppDomain ",
                     app_domain_id, " ", module_info.assembly.app_domain_name);
        first_jit_compilation_app_domains.insert(app_domain_id);
        return S_OK;
    }

    if (module_info.IsWindowsRuntime())
    {
        // We cannot obtain writable metadata interfaces on Windows Runtime modules
        // or instrument their IL.
        Logger::Debug("ModuleLoadFinished skipping Windows Metadata module: ", module_id, " ",
                      module_info.assembly.name);
        return S_OK;
    }

    if (module_info.IsResource())
    {
        // We don't need to load metadata on resources modules.
        Logger::Debug("ModuleLoadFinished skipping Resources module: ", module_id, " ", module_info.assembly.name);
        return S_OK;
    }

    if (module_info.IsDynamic())
    {
        // For CallTarget we don't need to load metadata on dynamic modules.
        Logger::Debug("ModuleLoadFinished skipping Dynamic module: ", module_id, " ", module_info.assembly.name);
        return S_OK;
    }

    for (auto&& skip_assembly : skip_assemblies)
    {
        if (module_info.assembly.name == skip_assembly)
        {
            Logger::Debug("ModuleLoadFinished skipping known module: ", module_id, " ", module_info.assembly.name);
            return S_OK;
        }
    }

    for (auto&& skip_assembly_pattern : skip_assembly_prefixes)
    {
        if (module_info.assembly.name.rfind(skip_assembly_pattern, 0) == 0)
        {
            Logger::Debug("ModuleLoadFinished skipping module by pattern: ", module_id, " ", module_info.assembly.name);
            return S_OK;
        }
    }

    if (module_info.assembly.name == managed_profiler_name)
    {
        // Fix PInvoke Rewriting
        ComPtr<IUnknown> metadata_interfaces;
        auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                 metadata_interfaces.GetAddressOf());

        if (FAILED(hr))
        {
            Logger::Warn("ModuleLoadFinished failed to get metadata interface for ", module_id, " ",
                         module_info.assembly.name);
            return S_OK;
        }

        const auto& metadata_import = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        const auto& metadata_emit = metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        const auto& assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        const auto& assembly_emit = metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

        const auto& module_metadata =
            ModuleMetadata(metadata_import, metadata_emit, assembly_import, assembly_emit, module_info.assembly.name,
                           module_info.assembly.app_domain_id, &corAssemblyProperty, enable_by_ref_instrumentation,
                           enable_calltarget_state_by_ref);

        const auto& assemblyImport = GetAssemblyImportMetadata(assembly_import);
        const auto& assemblyVersion = assemblyImport.version.str();

        Logger::Info("ModuleLoadFinished: ", managed_profiler_name, " v", assemblyVersion, " - Fix PInvoke maps");
#ifdef _WIN32
        RewritingPInvokeMaps(module_metadata, windows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, appsec_windows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, debugger_windows_nativemethods_type);
#else
        RewritingPInvokeMaps(module_metadata, nonwindows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, appsec_nonwindows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, debugger_nonwindows_nativemethods_type);
#endif // _WIN32

        auto native_loader_library_path = GetNativeLoaderFilePath();
        if (fs::exists(native_loader_library_path))
        {
            auto native_loader_file_path = shared::ToWSTRING(native_loader_library_path);
            RewritingPInvokeMaps(module_metadata, native_loader_nativemethods_type, native_loader_file_path);
        }

        if (ShouldRewriteProfilerMaps())
        {
            auto profiler_library_path = shared::GetEnvironmentValue(WStr("DD_INTERNAL_PROFILING_NATIVE_ENGINE_PATH"));
            RewritingPInvokeMaps(module_metadata, profiler_nativemethods_type, profiler_library_path);
        }

        if (IsVersionCompatibilityEnabled())
        {
            // We need to call EmitDistributedTracerTargetMethod on every Datadog.Trace.dll, not just on the automatic one.
            // That's because if the binding fails (for instance, if there's a custom AssemblyLoadContext), the manual tracer
            // might call the target method on itself instead of calling it on the automatic tracer.
            EmitDistributedTracerTargetMethod(module_metadata, module_id);

            // No need to rewrite if the target assembly matches the expected version
            if (assemblyImport.version != managed_profiler_assembly_reference->version)
            {
                if (runtime_information_.is_core() && assemblyImport.version > managed_profiler_assembly_reference->version)
                {
                    Logger::Debug("Skipping version conflict fix for ", assemblyVersion,
                                  " because running on .NET Core with a higher version than expected");
                }
                else
                {
                    RewriteForDistributedTracing(module_metadata, module_id);
                }
            }
            else
            {
                Logger::Debug("Skipping version conflict fix for ", assemblyVersion,
                              " because the version matches the expected one");
            }
        }
    }
    else
    {
        module_ids_.push_back(module_id);

        bool searchForTraceAttribute = trace_annotations_enabled;
        if (searchForTraceAttribute)
        {
            for (auto&& skip_assembly_pattern : skip_traceattribute_assembly_prefixes)
            {
                if (module_info.assembly.name.rfind(skip_assembly_pattern, 0) == 0)
                {
                    Logger::Debug("ModuleLoadFinished skipping [Trace] search for module by pattern: ", module_id, " ",
                                  module_info.assembly.name);
                    searchForTraceAttribute = false;
                    break;
                }
            }
        }

        // Scan module for [Trace] methods
        if (searchForTraceAttribute)
        {
            mdTypeDef typeDef = mdTypeDefNil;
            mdTypeRef typeRef = mdTypeRefNil;
            bool foundType = false;

            ComPtr<IUnknown> metadata_interfaces;
            auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                     metadata_interfaces.GetAddressOf());

            if (FAILED(hr))
            {
                Logger::Warn("ModuleLoadFinished failed to get metadata interface for ", module_id, " ",
                             module_info.assembly.name);
                return S_OK;
            }

            const auto& metadata_import = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);

            // First, detect if the assembly has defined its own Datadog trace attribute.
            // If this fails, detect if the assembly references the Datadog trace atttribute or other trace attributes
            hr = metadata_import->FindTypeDefByName(traceAttribute_typename_cstring, mdTypeDefNil, &typeDef);
            if (SUCCEEDED(hr))
            {
                foundType = true;
                Logger::Debug("ModuleLoadFinished found the TypeDef for ", traceattribute_typename, " defined in Module ", module_info.assembly.name);
            }
            else
            {
                // Now we enumerate all type refs in this assembly to see if the trace attribute is referenced
                auto enumTypeRefs = Enumerator<mdTypeRef>(
                    [&metadata_import](HCORENUM* ptr, mdTypeRef arr[], ULONG max, ULONG* cnt) -> HRESULT {
                        return metadata_import->EnumTypeRefs(ptr, arr, max, cnt);
                    },
                    [&metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });

                auto enumIterator = enumTypeRefs.begin();
                while (enumIterator != enumTypeRefs.end())
                {
                    mdTypeRef typeRef = *enumIterator;

                    // Check if the typeref matches
                    mdToken parent_token = mdTokenNil;
                    WCHAR type_name[kNameMaxSize]{};
                    DWORD type_name_len = 0;

                    hr = metadata_import->GetTypeRefProps(typeRef, &parent_token, type_name, kNameMaxSize,
                                                          &type_name_len);

                    if (TypeNameMatchesTraceAttribute(type_name, type_name_len))
                    {
                        foundType = true;
                        Logger::Debug("ModuleLoadFinished found the TypeRef for ", traceattribute_typename,
                                     " defined in Module ", module_info.assembly.name);
                        break;
                    }

                    enumIterator = ++enumIterator;
                }
            }

            // We have a typeRef and it matches the trace attribute
            // Since it is referenced, it should be in-use somewhere in this module
            // So iterate over all methods in the module
            if (foundType)
            {
                std::vector<MethodReference> methodReferences;
                std::vector<IntegrationDefinition> integrationDefinitions;

                // Now we enumerate all custom attributes in this assembly to see if the trace attribute is used
                auto enumCustomAttributes = Enumerator<mdCustomAttribute>(
                    [&metadata_import](HCORENUM* ptr, mdCustomAttribute arr[], ULONG max, ULONG* cnt) -> HRESULT {
                        return metadata_import->EnumCustomAttributes(ptr, mdTokenNil, mdTokenNil, arr, max, cnt);
                    },
                    [&metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });
                auto customAttributesIterator = enumCustomAttributes.begin();

                while (customAttributesIterator != enumCustomAttributes.end())
                {
                    mdCustomAttribute customAttribute = *customAttributesIterator;

                    // Check if the typeref matches
                    mdToken parent_token = mdTokenNil;
                    mdToken attribute_ctor_token = mdTokenNil;
                    const void* attribute_data = nullptr; // Pointer to receive attribute data, which is not needed for our purposes
                    DWORD data_size = 0;

                    hr = metadata_import->GetCustomAttributeProps(customAttribute, &parent_token, &attribute_ctor_token,
                                                                  &attribute_data, &data_size);

                    // We are only concerned with the trace attribute on method definitions
                    if (TypeFromToken(parent_token) == mdtMethodDef)
                    {
                        mdTypeDef attribute_type_token = mdTypeDefNil;
                        WCHAR function_name[kNameMaxSize]{};
                        DWORD function_name_len = 0;

                        // Get the type name from the constructor
                        const auto attribute_ctor_token_type = TypeFromToken(attribute_ctor_token);
                        if (attribute_ctor_token_type == mdtMemberRef)
                        {
                            hr = metadata_import->GetMemberRefProps(attribute_ctor_token, &attribute_type_token,
                                                                    function_name, kNameMaxSize, &function_name_len,
                                                                    nullptr, nullptr);
                        }
                        else if (attribute_ctor_token_type == mdtMethodDef)
                        {
                            hr = metadata_import->GetMemberProps(attribute_ctor_token, &attribute_type_token,
                                                                 function_name, kNameMaxSize, &function_name_len,
                                                                 nullptr, nullptr, nullptr, nullptr, nullptr, nullptr,
                                                                 nullptr, nullptr);
                        }
                        else
                        {
                            hr = E_FAIL;
                        }

                        if (SUCCEEDED(hr))
                        {
                            mdToken resolution_token = mdTokenNil;
                            WCHAR type_name[kNameMaxSize]{};
                            DWORD type_name_len = 0;

                            const auto token_type = TypeFromToken(attribute_type_token);
                            if (token_type == mdtTypeDef)
                            {
                                DWORD type_flags;
                                mdToken type_extends = mdTokenNil;
                                hr = metadata_import->GetTypeDefProps(attribute_type_token, type_name, kNameMaxSize,
                                                                      &type_name_len, &type_flags, &type_extends);
                            }
                            else if (token_type == mdtTypeRef)
                            {
                                hr = metadata_import->GetTypeRefProps(attribute_type_token, &resolution_token,
                                                                      type_name, kNameMaxSize, &type_name_len);
                            }
                            else
                            {
                                type_name_len = 0;
                            }

                            if (TypeNameMatchesTraceAttribute(type_name, type_name_len))
                            {
                                mdMethodDef methodDef = (mdMethodDef) parent_token;

                                // Matches! Let's mark the attached method for ReJIT
                                // Extract the function info from the mdMethodDef
                                const auto caller = GetFunctionInfo(metadata_import, methodDef);
                                if (!caller.IsValid())
                                {
                                    Logger::Warn("    * The caller for the methoddef: ",
                                                 shared::TokenStr(&parent_token), " is not valid!");
                                    customAttributesIterator = ++customAttributesIterator;
                                    continue;
                                }

                                // We create a new function info into the heap from the caller functionInfo in the
                                // stack, to be used later in the ReJIT process
                                auto functionInfo = FunctionInfo(caller);
                                auto hr = functionInfo.method_signature.TryParse();
                                if (FAILED(hr))
                                {
                                    Logger::Warn("    * The method signature: ", functionInfo.method_signature.str(),
                                                 " cannot be parsed.");
                                    customAttributesIterator = ++customAttributesIterator;
                                    continue;
                                }

                                // As we are in the right method, we gather all information we need and stored it in to
                                // the ReJIT handler.
                                std::vector<shared::WSTRING> signatureTypes;
                                methodReferences.push_back(MethodReference(
                                    tracemethodintegration_assemblyname, caller.type.name, caller.name,
                                    Version(0, 0, 0, 0), Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX),
                                    signatureTypes));
                            }
                        }
                    }

                    customAttributesIterator = ++customAttributesIterator;
                }

                if (trace_annotation_integration_type == nullptr)
                {
                    Logger::Debug("ModuleLoadFinished pushing [Trace] methods to rejit_module_method_pairs for a later ReJIT, ModuleId=", module_id,
                                 ", ModuleName=", module_info.assembly.name,
                                 ", methodReferences.size()=", methodReferences.size());

                    if (methodReferences.size() > 0)
                    {
                        rejit_module_method_pairs.push_back(std::make_pair(module_id, methodReferences));
                    }
                }
                else
                {
                    Logger::Debug("ModuleLoadFinished including [Trace] methods for ReJIT, ModuleId=", module_id,
                                 ", ModuleName=", module_info.assembly.name,
                                 ", methodReferences.size()=", methodReferences.size());

                    integration_definitions_.reserve(integration_definitions_.size() + methodReferences.size());

                    // Push integration definitions from this module
                    for (const auto& methodReference : methodReferences)
                    {
                        integration_definitions_.push_back(IntegrationDefinition(
                            methodReference, *trace_annotation_integration_type.get(), false, false));
                    }
                }
            }
        }

        // We call the function to analyze the module and request the ReJIT of integrations defined in this module.
        if (tracer_integration_preprocessor != nullptr && !integration_definitions_.empty())
        {
            const auto numReJITs = tracer_integration_preprocessor->RequestRejitForLoadedModules(std::vector<ModuleID>{module_id}, integration_definitions_);
            Logger::Debug("[Tracer] Total number of ReJIT Requested: ", numReJITs);
        }

        if (debugger_instrumentation_requester != nullptr)
        {
            const auto& probes = debugger_instrumentation_requester->GetProbes();
            if (!probes.empty())
            {
                const auto numReJITs = debugger_instrumentation_requester->RequestRejitForLoadedModule(module_id);
                 Logger::Debug("[Debugger] Total number of ReJIT Requested: ", numReJITs);
            }
        }
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID module_id)
{
    auto _ = trace::Stats::Instance()->ModuleUnloadStartedMeasure();

    if (!is_attached_)
    {
        return S_OK;
    }

    // take this lock so we block until the
    // module metadata is not longer being used
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_)
    {
        return S_OK;
    }

    if (rejit_handler != nullptr)
    {
        rejit_handler->RemoveModule(module_id);
    }

    const auto& moduleInfo = GetModuleInfo(this->info_, module_id);
    if (!moduleInfo.IsValid())
    {
        Logger::Debug("ModuleUnloadStarted: ", module_id);
        return S_OK;
    }

    if (Logger::IsDebugEnabled())
    {
        Logger::Debug("ModuleUnloadStarted: ", module_id, " ", moduleInfo.assembly.name, " AppDomain ",
                      moduleInfo.assembly.app_domain_id, " ", moduleInfo.assembly.app_domain_name);
    }

    const auto is_instrumentation_assembly = moduleInfo.assembly.name == managed_profiler_name;
    if (is_instrumentation_assembly)
    {
        const auto appDomainId = moduleInfo.assembly.app_domain_id;

        // remove appdomain id from managed_profiler_loaded_app_domains set
        if (managed_profiler_loaded_app_domains.find(appDomainId) != managed_profiler_loaded_app_domains.end())
        {
            managed_profiler_loaded_app_domains.erase(appDomainId);
        }
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown()
{
    is_attached_.store(false);

    CorProfilerBase::Shutdown();

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    if (rejit_handler != nullptr)
    {
        rejit_handler->Shutdown();
        rejit_handler = nullptr;
    }
    Logger::Info("Exiting...");
    Logger::Debug("   ModuleIds: ", module_ids_.size());
    Logger::Debug("   IntegrationDefinitions: ", integration_definitions_.size());
    Logger::Debug("   DefinitionsIds: ", definitions_ids_.size());
    Logger::Debug("   ManagedProfilerLoadedAppDomains: ", managed_profiler_loaded_app_domains.size());
    Logger::Debug("   FirstJitCompilationAppDomains: ", first_jit_compilation_app_domains.size());
    Logger::Info("Stats: ", Stats::Instance()->ToString());
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded()
{
    if (!is_attached_)
    {
        return S_OK;
    }

    CorProfilerBase::ProfilerDetachSucceeded();

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_)
    {
        return S_OK;
    }

    Logger::Info("Detaching profiler.");
    Logger::Flush();
    is_attached_.store(false);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID function_id, BOOL is_safe_to_block)
{
    auto _ = trace::Stats::Instance()->JITCompilationStartedMeasure();

    if (!is_attached_ || !is_safe_to_block)
    {
        return S_OK;
    }

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_)
    {
        return S_OK;
    }

    ModuleID module_id;
    mdToken function_token = mdTokenNil;

    HRESULT hr = this->info_->GetFunctionInfo(function_id, nullptr, &module_id, &function_token);
    if (FAILED(hr))
    {
        Logger::Warn("JITCompilationStarted: Call to ICorProfilerInfo4.GetFunctionInfo() failed for ", function_id);
        return S_OK;
    }

    // we have to check if the Id is in the module_ids_ vector.
    // In case is True we create a local ModuleMetadata to inject the loader.
    if (!shared::Contains(module_ids_, module_id))
    {
        debugger_instrumentation_requester->PerformInstrumentAllIfNeeded(module_id, function_token);
        return S_OK;
    }

    const auto& module_info = GetModuleInfo(this->info_, module_id);

    bool has_loader_injected_in_appdomain =
        first_jit_compilation_app_domains.find(module_info.assembly.app_domain_id) !=
        first_jit_compilation_app_domains.end();

    if (has_loader_injected_in_appdomain)
    {
        // Loader was already injected in a calltarget scenario, we don't need to do anything else here

        debugger_instrumentation_requester->PerformInstrumentAllIfNeeded(module_id, function_token);

        return S_OK;
    }

    ComPtr<IUnknown> metadataInterfaces;
    hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                        metadataInterfaces.GetAddressOf());

    const auto& metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
    const auto& metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
    const auto& assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
    const auto& assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

    Logger::Debug("Temporaly allocating the ModuleMetadata for injection. ModuleId=", module_id,
                  " ModuleName=", module_info.assembly.name);

    std::unique_ptr<ModuleMetadata> module_metadata = std::make_unique<ModuleMetadata>(
        metadataImport, metadataEmit, assemblyImport, assemblyEmit, module_info.assembly.name,
        module_info.assembly.app_domain_id, &corAssemblyProperty, enable_by_ref_instrumentation,
        enable_calltarget_state_by_ref);

    // get function info
    const auto& caller = GetFunctionInfo(module_metadata->metadata_import, function_token);
    if (!caller.IsValid())
    {
        return S_OK;
    }

    if (Logger::IsDebugEnabled())
    {
        Logger::Debug("JITCompilationStarted: function_id=", function_id, " token=", function_token,
                      " name=", caller.type.name, ".", caller.name, "()");
    }

    // In NETFx, NInject creates a temporary appdomain where the tracer can be laoded
    // If Runtime metrics are enabled, we can encounter a CannotUnloadAppDomainException
    // certainly because we are initializing perf counters at that time.
    // As there are no use case where we would like to load the tracer in that appdomain, just don't
    if (module_info.assembly.app_domain_name == WStr("NinjectModuleLoader") && !runtime_information_.is_core())
    {
        Logger::Info("JITCompilationStarted: NInjectModuleLoader appdomain deteceted. Not registering startup hook.");
        return S_OK;
    }

    // IIS: Ensure that the startup hook is inserted into System.Web.Compilation.BuildManager.InvokePreStartInitMethods.
    // This will be the first call-site considered for the startup hook injection,
    // which correctly loads Datadog.Trace.ClrProfiler.Managed.Loader into the application's
    // own AppDomain because at this point in the code path, the ApplicationImpersonationContext
    // has been started.
    //
    // Note: This check must only run on desktop because it is possible (and the default) to host
    // ASP.NET Core in-process, so a new .NET Core runtime is instantiated and run in the same w3wp.exe process
    auto valid_startup_hook_callsite = true;
    if (is_desktop_iis)
    {
        valid_startup_hook_callsite = module_metadata->assemblyName == WStr("System.Web") &&
                                      caller.type.name == WStr("System.Web.Compilation.BuildManager") &&
                                      caller.name == WStr("InvokePreStartInitMethods");
    }
    else if (module_metadata->assemblyName == WStr("System") ||
             module_metadata->assemblyName == WStr("System.Net.Http"))
    {
        valid_startup_hook_callsite = false;
    }

    // The first time a method is JIT compiled in an AppDomain, insert our startup
    // hook, which, at a minimum, must add an AssemblyResolve event so we can find
    // Datadog.Trace.dll and its dependencies on disk.
    if (valid_startup_hook_callsite && !has_loader_injected_in_appdomain)
    {
        bool domain_neutral_assembly = runtime_information_.is_desktop() && corlib_module_loaded &&
                                       module_metadata->app_domain_id == corlib_app_domain_id;
        Logger::Info("JITCompilationStarted: Startup hook registered in function_id=", function_id,
                     " token=", function_token, " name=", caller.type.name, ".", caller.name,
                     "(), assembly_name=", module_metadata->assemblyName,
                     " app_domain_id=", module_metadata->app_domain_id, " domain_neutral=", domain_neutral_assembly);

        first_jit_compilation_app_domains.insert(module_metadata->app_domain_id);

        hr = RunILStartupHook(module_metadata->metadata_emit, module_id, function_token, caller, *module_metadata);
        if (FAILED(hr))
        {
            Logger::Warn("JITCompilationStarted: Call to RunILStartupHook() failed for ", module_id, " ",
                         function_token);
            return S_OK;
        }

        if (is_desktop_iis)
        {
            hr = AddIISPreStartInitFlags(module_id, function_token);
            if (FAILED(hr))
            {
                Logger::Warn("JITCompilationStarted: Call to AddIISPreStartInitFlags() failed for ", module_id, " ",
                             function_token);
                return S_OK;
            }
        }

        Logger::Debug("JITCompilationStarted: Startup hook registered.");
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    // take this lock so we block until the
    // module metadata is not longer being used
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_)
    {
        return S_OK;
    }

    // remove appdomain metadata from map
    const auto& count = first_jit_compilation_app_domains.erase(appDomainId);

    Logger::Debug("AppDomainShutdownFinished: AppDomain: ", appDomainId, ", removed ", count, " elements");

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
{
    auto _ = trace::Stats::Instance()->JITInliningMeasure();

    if (!is_attached_ || rejit_handler == nullptr)
    {
        return S_OK;
    }

    ModuleID calleeModuleId;
    mdToken calleFunctionToken = mdTokenNil;
    auto hr = this->info_->GetFunctionInfo(calleeId, NULL, &calleeModuleId, &calleFunctionToken);

    *pfShouldInline = true;

    if (FAILED(hr))
    {
        Logger::Warn("*** JITInlining: Failed to get the function info of the calleId: ", calleeId);
        return S_OK;
    }

    if (is_attached_ && rejit_handler != nullptr &&
        rejit_handler->HasModuleAndMethod(calleeModuleId, calleFunctionToken))
    {
        Logger::Debug("*** JITInlining: Inlining disabled for [ModuleId=", calleeModuleId,
                      ", MethodDef=", shared::TokenStr(&calleFunctionToken), "]");
        *pfShouldInline = false;
    }

    return S_OK;
}

//
// InitializeProfiler method
//
void CorProfiler::InitializeProfiler(WCHAR* id, CallTargetDefinition* items, int size)
{
    auto _ = trace::Stats::Instance()->InitializeProfilerMeasure();
    shared::WSTRING definitionsId = shared::WSTRING(id);
    Logger::Info("InitializeProfiler: received id: ", definitionsId, " from managed side with ", size,
                 " integrations.");

    if (size > 0)
    {
        InternalAddInstrumentation(id, items, size, false);
    }
}

void CorProfiler::EnableByRefInstrumentation()
{
    enable_by_ref_instrumentation = true;
    if (rejit_handler != nullptr)
    {
        rejit_handler->SetEnableByRefInstrumentation(true);
    }

    Logger::Info("ByRef Instrumentation enabled.");
}

void CorProfiler::EnableCallTargetStateByRef()
{
    enable_calltarget_state_by_ref = true;
    if (rejit_handler != nullptr)
    {
        rejit_handler->SetEnableCallTargetStateByRef(true);
    }

    Logger::Info("CallTargetState ByRef enabled.");
}

void CorProfiler::AddDerivedInstrumentations(WCHAR* id, CallTargetDefinition* items, int size)
{
    auto _ = trace::Stats::Instance()->InitializeProfilerMeasure();
    shared::WSTRING definitionsId = shared::WSTRING(id);
    Logger::Info("AddDerivedInstrumentations: received id: ", definitionsId, " from managed side with ", size,
                 " integrations.");

    if (size > 0)
    {
        InternalAddInstrumentation(id, items, size, true);
    }
}

void CorProfiler::InternalAddInstrumentation(WCHAR* id, CallTargetDefinition* items, int size, bool isDerived)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    std::scoped_lock<std::mutex> definitionsLock(definitions_ids_lock_);

    if (definitions_ids_.find(definitionsId) != definitions_ids_.end())
    {
        Logger::Info("InitializeProfiler: Id already processed.");
        return;
    }

    if (items != nullptr && rejit_handler != nullptr)
    {
        std::vector<IntegrationDefinition> integrationDefinitions;

        for (int i = 0; i < size; i++)
        {
            const CallTargetDefinition& current = items[i];

            const shared::WSTRING& targetAssembly = shared::WSTRING(current.targetAssembly);
            const shared::WSTRING& targetType = shared::WSTRING(current.targetType);
            const shared::WSTRING& targetMethod = shared::WSTRING(current.targetMethod);

            const shared::WSTRING& integrationAssembly = shared::WSTRING(current.integrationAssembly);
            const shared::WSTRING& integrationType = shared::WSTRING(current.integrationType);

            std::vector<shared::WSTRING> signatureTypes;
            for (int sIdx = 0; sIdx < current.signatureTypesLength; sIdx++)
            {
                const auto& currentSignature = current.signatureTypes[sIdx];
                if (currentSignature != nullptr)
                {
                    signatureTypes.push_back(shared::WSTRING(currentSignature));
                }
            }

            const Version& minVersion =
                Version(current.targetMinimumMajor, current.targetMinimumMinor, current.targetMinimumPatch, 0);
            const Version& maxVersion =
                Version(current.targetMaximumMajor, current.targetMaximumMinor, current.targetMaximumPatch, 0);

            const auto& integration = IntegrationDefinition(
                MethodReference(targetAssembly, targetType, targetMethod, minVersion, maxVersion, signatureTypes),
                TypeReference(integrationAssembly, integrationType, {}, {}),
                isDerived,
                true);

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetAssembly, " | ", targetType, ".", targetMethod, "(",
                              signatureTypes.size(), ") { ", minVersion.str(), " - ", maxVersion.str(), " } [",
                              integrationAssembly, " | ", integrationType, "]");
            }

            integrationDefinitions.push_back(integration);
        }

        std::scoped_lock<std::mutex> moduleLock(module_ids_lock_);

        definitions_ids_.emplace(definitionsId);

        Logger::Info("Total number of modules to analyze: ", module_ids_.size());
        if (rejit_handler != nullptr)
        {
            std::promise<ULONG> promise;
            std::future<ULONG> future = promise.get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(module_ids_, integrationDefinitions, &promise);

            // wait and get the value from the future<int>
            const auto& numReJITs = future.get();
            Logger::Debug("Total number of ReJIT Requested: ", numReJITs);
        }

        integration_definitions_.reserve(integration_definitions_.size() + integrationDefinitions.size());
        for (const auto& integration : integrationDefinitions)
        {
            integration_definitions_.push_back(integration);
        }

        Logger::Info("InitializeProfiler: Total integrations in profiler: ", integration_definitions_.size());
    }
}

void CorProfiler::AddTraceAttributeInstrumentation(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                                   WCHAR* integration_type_name_ptr)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    std::scoped_lock<std::mutex> definitionsLock(definitions_ids_lock_);

    if (definitions_ids_.find(definitionsId) != definitions_ids_.end())
    {
        Logger::Info("AddTraceAttributeInstrumentation: Id already processed.");
        return;
    }

    definitions_ids_.emplace(definitionsId);
    shared::WSTRING integration_assembly_name = shared::WSTRING(integration_assembly_name_ptr);
    shared::WSTRING integration_type_name = shared::WSTRING(integration_type_name_ptr);
    trace_annotation_integration_type =
        std::unique_ptr<TypeReference>(new TypeReference(integration_assembly_name, integration_type_name, {}, {}));

    Logger::Info("AddTraceAttributeInstrumentation: Initialized assembly=", integration_assembly_name, ", type=",
                 integration_type_name);
}

void CorProfiler::InitializeTraceMethods(WCHAR* id, WCHAR* integration_assembly_name_ptr, WCHAR* integration_type_name_ptr,
                                         WCHAR* configuration_string_ptr)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    std::scoped_lock<std::mutex> definitionsLock(definitions_ids_lock_);

    if (definitions_ids_.find(definitionsId) != definitions_ids_.end())
    {
        Logger::Info("InitializeTraceMethods: Id already processed.");
        return;
    }

    shared::WSTRING integration_assembly_name = shared::WSTRING(integration_assembly_name_ptr);
    shared::WSTRING integration_type_name = shared::WSTRING(integration_type_name_ptr);
    shared::WSTRING configuration_string = shared::WSTRING(configuration_string_ptr);

    if (trace_annotation_integration_type == nullptr)
    {
        Logger::Warn("InitializeTraceMethods: Integration type was not initialized. AddTraceAttributeInstrumentation "
                     "must be called first");
        return;
    }
    else if (trace_annotation_integration_type.get()->assembly.str() != integration_assembly_name ||
             trace_annotation_integration_type.get()->name != integration_type_name)
    {
        Logger::Warn("InitializeTraceMethods: Integration type was already initialized to assembly=",
                     trace_annotation_integration_type.get()->assembly.str(),
                     ", type=", trace_annotation_integration_type.get()->name,
                     ". InitializeTraceMethods was now invoked with assembly=", integration_assembly_name,
                     ", type=", integration_type_name, ". Exiting InitializeTraceMethods.");
        return;
    }

    // TODO we do a handful of string splits here. We could probably do this with indexOf operations instead, but I'm gonna
    // first make sure this works
    definitions_ids_.emplace(definitionsId);
    if (rejit_handler != nullptr)
    {
        if (trace_annotation_integration_type == nullptr)
        {
            Logger::Warn("InitializeTraceMethods: Integration type was not initialized. AddTraceAttributeInstrumentation must be called first");
        }
        else if (trace_annotation_integration_type.get()->assembly.str() != integration_assembly_name
            || trace_annotation_integration_type.get()->name != integration_type_name)
        {
            Logger::Warn("InitializeTraceMethods: Integration type was initialized to assembly=",
                         trace_annotation_integration_type.get()->assembly.str(), ", type=", trace_annotation_integration_type.get()->name,
                         ". InitializeTraceMethods was invoked with assembly=", integration_assembly_name , ", type=", integration_type_name, ". Exiting InitializeTraceMethods.");
        }
        else if (configuration_string.size() > 0)
        {
            std::vector<IntegrationDefinition> integrationDefinitions = GetIntegrationsFromTraceMethodsConfiguration(*trace_annotation_integration_type.get(), configuration_string);
            std::scoped_lock<std::mutex> moduleLock(module_ids_lock_);

            Logger::Debug("InitializeTraceMethods: Total number of modules to analyze: ", module_ids_.size());
            if (rejit_handler != nullptr)
            {
                std::promise<ULONG> promise;
                std::future<ULONG> future = promise.get_future();
                tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(module_ids_, integrationDefinitions,
                                                                                    &promise);

                // wait and get the value from the future<int>
                const auto& numReJITs = future.get();
                Logger::Debug("Total number of ReJIT Requested: ", numReJITs);
            }

            integration_definitions_.reserve(integration_definitions_.size() + integrationDefinitions.size());
            for (const auto& integration : integrationDefinitions)
            {
                integration_definitions_.push_back(integration);
            }

            Logger::Info("InitializeTraceMethods: Total integrations in profiler: ", integration_definitions_.size());
        }
    }
}

void CorProfiler::InstrumentProbes(debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
                            debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
                            debugger::DebuggerRemoveProbesDefinition* removeProbes, int revertProbesLength) const
{
    debugger_instrumentation_requester->InstrumentProbes(methodProbes, methodProbesLength, lineProbes, lineProbesLength,
                                                  removeProbes, revertProbesLength);
}

int CorProfiler::GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses)
{
    return debugger_instrumentation_requester->GetProbesStatuses(probeIds, probeIdsLength, probeStatuses);
}

//
// ICorProfilerCallback6 methods
//
HRESULT STDMETHODCALLTYPE CorProfiler::GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                             ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
{
    if (IsAzureAppServices())
    {
        Logger::Debug(
            "GetAssemblyReferences skipping entire callback because this is running in Azure App Services, which "
            "isn't yet supported for this feature. AssemblyPath=",
            wszAssemblyPath);
        return S_OK;
    }

    // Convert the assembly path to the assembly name, assuming the assembly name
    // is either <assembly_name.ni.dll> or <assembly_name>.dll
    const auto& assemblyPathString = shared::ToString(wszAssemblyPath);
    auto filename = assemblyPathString.substr(assemblyPathString.find_last_of("\\/") + 1);
    const auto& lastNiDllPeriodIndex = filename.rfind(".ni.dll");
    const auto& lastDllPeriodIndex = filename.rfind(".dll");
    if (lastNiDllPeriodIndex != std::string::npos)
    {
        filename.erase(lastNiDllPeriodIndex, 7);
    }
    else if (lastDllPeriodIndex != std::string::npos)
    {
        filename.erase(lastDllPeriodIndex, 4);
    }

    const shared::WSTRING& assembly_name = shared::ToWSTRING(filename);

    // Skip known framework assemblies that we will not instrument and,
    // as a result, will not need an assembly reference to the
    // managed profiler
    for (auto&& skip_assembly_pattern : skip_assembly_prefixes)
    {
        if (assembly_name.rfind(skip_assembly_pattern, 0) == 0)
        {
            Logger::Debug("GetAssemblyReferences skipping module by pattern: Name=", assembly_name,
                          " Path=", wszAssemblyPath);
            return S_OK;
        }
    }

    for (auto&& skip_assembly : skip_assemblies)
    {
        if (assembly_name == skip_assembly)
        {
            Logger::Debug("GetAssemblyReferences skipping known assembly: Name=", assembly_name,
                          " Path=", wszAssemblyPath);
            return S_OK;
        }
    }

    // Construct an ASSEMBLYMETADATA structure for the managed profiler that can
    // be consumed by the runtime
    ASSEMBLYMETADATA assembly_metadata{};

    assembly_metadata.usMajorVersion = managed_profiler_assembly_reference->version.major;
    assembly_metadata.usMinorVersion = managed_profiler_assembly_reference->version.minor;
    assembly_metadata.usBuildNumber = managed_profiler_assembly_reference->version.build;
    assembly_metadata.usRevisionNumber = managed_profiler_assembly_reference->version.revision;
    if (managed_profiler_assembly_reference->locale == WStr("neutral"))
    {
        assembly_metadata.szLocale = const_cast<WCHAR*>(WStr("\0"));
        assembly_metadata.cbLocale = 0;
    }
    else
    {
        assembly_metadata.szLocale = const_cast<WCHAR*>(managed_profiler_assembly_reference->locale.c_str());
        assembly_metadata.cbLocale = (DWORD) (managed_profiler_assembly_reference->locale.size());
    }

    DWORD public_key_size = 8;
    if (managed_profiler_assembly_reference->public_key == trace::PublicKey())
    {
        public_key_size = 0;
    }

    COR_PRF_ASSEMBLY_REFERENCE_INFO asmRefInfo;
    asmRefInfo.pbPublicKeyOrToken = (void*) &managed_profiler_assembly_reference->public_key.data[0];
    asmRefInfo.cbPublicKeyOrToken = public_key_size;
    asmRefInfo.szName = managed_profiler_assembly_reference->name.c_str();
    asmRefInfo.pMetaData = &assembly_metadata;
    asmRefInfo.pbHashValue = nullptr;
    asmRefInfo.cbHashValue = 0;
    asmRefInfo.dwAssemblyRefFlags = 0;

    // Attempt to extend the assembly closure of the provided assembly to include
    // the managed profiler
    auto hr = pAsmRefProvider->AddAssemblyReference(&asmRefInfo);
    if (FAILED(hr))
    {
        Logger::Warn("GetAssemblyReferences failed for call from ", wszAssemblyPath);
        return S_OK;
    }

    Logger::Debug("GetAssemblyReferences extending assembly closure for ", assembly_name, " to include ",
                  asmRefInfo.szName, ". Path=", wszAssemblyPath);

    return S_OK;
}

bool CorProfiler::IsAttached() const
{
    return is_attached_;
}

//
// Helper methods
//
bool CorProfiler::GetIntegrationTypeRef(ModuleMetadata& module_metadata, ModuleID module_id,
                                        const IntegrationDefinition& integration_definition,
                                        mdTypeRef& integration_type_ref)
{
    const auto& integration_key = integration_definition.integration_type.get_cache_key();

    if (!module_metadata.TryGetIntegrationTypeRef(integration_key, integration_type_ref))
    {
        const auto& module_info = GetModuleInfo(this->info_, module_id);
        if (!module_info.IsValid())
        {
            return false;
        }

        mdModule module;
        auto hr = module_metadata.metadata_import->GetModuleFromScope(&module);
        if (FAILED(hr))
        {
            Logger::Warn("GetIntegrationTypeRef failed to get module metadata token for "
                         "module_id=",
                         module_id, " module_name=", module_info.assembly.name);
            return false;
        }

        const MetadataBuilder metadata_builder(module_metadata, module, module_metadata.metadata_import,
                                               module_metadata.metadata_emit, module_metadata.assembly_import,
                                               module_metadata.assembly_emit);

        // for each wrapper assembly, emit an assembly reference
        hr = metadata_builder.EmitAssemblyRef(integration_definition.integration_type.assembly);
        if (FAILED(hr))
        {
            Logger::Warn("GetIntegrationTypeRef failed to emit wrapper assembly ref for assembly=",
                         integration_definition.integration_type.assembly.name,
                         ", Version=", integration_definition.integration_type.assembly.version.str(),
                         ", Culture=", integration_definition.integration_type.assembly.locale,
                         " PublicKeyToken=", integration_definition.integration_type.assembly.public_key.str());
            return false;
        }

        // for each method replacement in each enabled integration,
        // emit a reference to the instrumentation wrapper type
        hr = metadata_builder.FindIntegrationTypeRef(integration_definition, integration_type_ref);
        if (FAILED(hr))
        {
            Logger::Warn("GetIntegrationTypeRef failed to obtain wrapper method ref for ",
                         integration_definition.integration_type.name, ".");
            return false;
        }
    }

    return true;
}

bool CorProfiler::ProfilerAssemblyIsLoadedIntoAppDomain(AppDomainID app_domain_id)
{
    return managed_profiler_loaded_domain_neutral ||
           managed_profiler_loaded_app_domains.find(app_domain_id) != managed_profiler_loaded_app_domains.end();
}

HRESULT CorProfiler::EmitDistributedTracerTargetMethod(const ModuleMetadata& module_metadata, ModuleID module_id)
{
    // Emit a public DistributedTracer.__GetInstanceForProfiler__() method, that will be used by RewriteForDistributedTracing
    HRESULT hr = S_OK;

    //
    // *** Get DistributedTracer TypeDef
    //
    mdTypeDef distributedTracerTypeDef;
    hr = module_metadata.metadata_import->FindTypeDefByName(distributed_tracer_type_name.c_str(),
                                                            mdTokenNil, &distributedTracerTypeDef);

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting DistributedTracer TypeDef");
        return hr;
    }

    //
    // *** get_Instance MemberRef ***
    //
    mdTypeDef iDistributedTracerTypeDef;
    hr = module_metadata.metadata_import->FindTypeDefByName(distributed_tracer_interface_name.c_str(),
                                                            mdTokenNil, &iDistributedTracerTypeDef);

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting IDistributedTracer TypeDef");
        return hr;
    }

    COR_SIGNATURE instanceSignature[16];

    unsigned type_buffer;
    const auto type_size = CorSigCompressToken(iDistributedTracerTypeDef, &type_buffer);

    unsigned offset = 0;

    instanceSignature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    instanceSignature[offset++] = 0;
    instanceSignature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&instanceSignature[offset], &type_buffer, type_size);
    offset += type_size;

    mdMemberRef instanceMemberRef;
    hr = module_metadata.metadata_emit->DefineMemberRef(distributedTracerTypeDef, WStr("get_Instance"),
                                                        instanceSignature, offset, &instanceMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on defining get_Instance MemberRef");
        return hr;
    }


    constexpr COR_SIGNATURE signature[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
        0,                             // Number of parameters
        ELEMENT_TYPE_OBJECT,             // Return type
    };

    mdMethodDef targetMethodDef;

    hr = module_metadata.metadata_emit->DefineMethod(
        distributedTracerTypeDef, distributed_tracer_target_method_name.c_str(), mdStatic | mdPublic,
                                                     signature, sizeof(signature), 0, 0, &targetMethodDef);

    if (FAILED(hr))
    {
        Logger::Warn("Error defining target method for Distributed Tracing");
        return hr;
    }

    /////////////////////////////////////////////
    // Add IL instructions into the target __GetInstanceForProfiler__ method
    //
    //  public static object __GetInstanceForProfiler__() {
    //      return Instance;
    //  }
    //
    ILRewriter rewriter_get_instance(this->info_, nullptr, module_id, targetMethodDef);
    rewriter_get_instance.InitializeTiny();

    auto pALFirstInstr = rewriter_get_instance.GetILList()->m_pNext;

    // Call the get_Instance() property getter
    auto pALNewInstr = rewriter_get_instance.NewILInstr();
    pALNewInstr->m_opcode = CEE_CALL;
    pALNewInstr->m_Arg32 = instanceMemberRef;
    rewriter_get_instance.InsertBefore(pALFirstInstr, pALNewInstr);

    // ret : Return the value
    pALNewInstr = rewriter_get_instance.NewILInstr();
    pALNewInstr->m_opcode = CEE_RET;
    rewriter_get_instance.InsertBefore(pALFirstInstr, pALNewInstr);

    hr = rewriter_get_instance.Export();
    if (FAILED(hr))
    {
        Logger::Warn("Failed to emit IL for DistributedTracer.__GetInstanceForProfiler__ for ModuleID=", module_id);
        return hr;
    }

    Logger::Info("Successfully added method DistributedTracer.__GetInstanceForProfiler__ for ModuleID=", module_id);

    return S_OK;
}

HRESULT CorProfiler::RewriteForDistributedTracing(const ModuleMetadata& module_metadata, ModuleID module_id)
{
    HRESULT hr = S_OK;

    if (IsDebugEnabled())
    {
        Logger::Info("pcbPublicKey: ", managed_profiler_assembly_property.pcbPublicKey);
        Logger::Info("ppbPublicKey: ", shared::HexStr(managed_profiler_assembly_property.ppbPublicKey,
                                                      managed_profiler_assembly_property.pcbPublicKey));
        Logger::Info("pcbPublicKey: ");
        const auto ppbPublicKey = (BYTE*) managed_profiler_assembly_property.ppbPublicKey;
        for (ULONG i = 0; i < managed_profiler_assembly_property.pcbPublicKey; i++)
        {
            Logger::Info(" -> ", (int) ppbPublicKey[i]);
        }
        Logger::Info("szName: ", managed_profiler_assembly_property.szName);

        Logger::Info("Metadata.cbLocale: ", managed_profiler_assembly_property.pMetaData.cbLocale);
        Logger::Info("Metadata.szLocale: ", managed_profiler_assembly_property.pMetaData.szLocale);

        if (managed_profiler_assembly_property.pMetaData.rOS != nullptr)
        {
            Logger::Info("Metadata.rOS.dwOSMajorVersion: ",
                         managed_profiler_assembly_property.pMetaData.rOS->dwOSMajorVersion);
            Logger::Info("Metadata.rOS.dwOSMinorVersion: ",
                         managed_profiler_assembly_property.pMetaData.rOS->dwOSMinorVersion);
            Logger::Info("Metadata.rOS.dwOSPlatformId: ",
                         managed_profiler_assembly_property.pMetaData.rOS->dwOSPlatformId);
        }

        Logger::Info("Metadata.usBuildNumber: ", managed_profiler_assembly_property.pMetaData.usBuildNumber);
        Logger::Info("Metadata.usMajorVersion: ", managed_profiler_assembly_property.pMetaData.usMajorVersion);
        Logger::Info("Metadata.usMinorVersion: ", managed_profiler_assembly_property.pMetaData.usMinorVersion);
        Logger::Info("Metadata.usRevisionNumber: ", managed_profiler_assembly_property.pMetaData.usRevisionNumber);

        Logger::Info("pulHashAlgId: ", managed_profiler_assembly_property.pulHashAlgId);
        Logger::Info("sizeof(pulHashAlgId): ", sizeof(managed_profiler_assembly_property.pulHashAlgId));
        Logger::Info("assemblyFlags: ", managed_profiler_assembly_property.assemblyFlags);
    }

    //
    // *** Get DistributedTracer TypeDef
    //
    mdTypeDef distributedTracerTypeDef;
    hr = module_metadata.metadata_import->FindTypeDefByName(distributed_tracer_type_name.c_str(),
                                                            mdTokenNil, &distributedTracerTypeDef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting DistributedTracer TypeDef");
        return hr;
    }

    //
    // *** Import Current Version of assembly
    //
    mdAssemblyRef managed_profiler_assemblyRef;
    hr = module_metadata.assembly_emit->DefineAssemblyRef(
        managed_profiler_assembly_property.ppbPublicKey, managed_profiler_assembly_property.pcbPublicKey,
        managed_profiler_assembly_property.szName.data(), &managed_profiler_assembly_property.pMetaData,
        &managed_profiler_assembly_property.pulHashAlgId, sizeof(managed_profiler_assembly_property.pulHashAlgId),
        managed_profiler_assembly_property.assemblyFlags, &managed_profiler_assemblyRef);

    if (FAILED(hr) || managed_profiler_assemblyRef == mdAssemblyRefNil)
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting ManagedProfiler AssemblyRef");
        return hr;
    }

    mdTypeRef distributedTracerTypeRef;
    hr = module_metadata.metadata_emit->DefineTypeRefByName(
        managed_profiler_assemblyRef, distributed_tracer_type_name.c_str(), &distributedTracerTypeRef);

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting DistributedTracer TypeRef");
        return hr;
    }

    mdTypeRef iDistributedTracerTypeRef;
    hr = module_metadata.metadata_emit->DefineTypeRefByName(
        managed_profiler_assemblyRef, distributed_tracer_interface_name.c_str(), &iDistributedTracerTypeRef);

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting IDistributedTracer TypeRef");
        return hr;
    }

    //
    // *** GetDistributedTracer MethodDef ***
    //
    constexpr COR_SIGNATURE getDistributedTracerSignature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT, 0, ELEMENT_TYPE_OBJECT};
    mdMethodDef getDistributedTraceMethodDef;
    hr = module_metadata.metadata_import->FindMethod(distributedTracerTypeDef, WStr("GetDistributedTracer"),
                                                     getDistributedTracerSignature, 3, &getDistributedTraceMethodDef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on getting GetDistributedTracer MethodDef");
        return hr;
    }

    //
    // *** Target (__GetInstanceForProfiler__) MemberRef ***
    //
    constexpr COR_SIGNATURE targetSignature[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        0,
        ELEMENT_TYPE_OBJECT,
    };

    mdMemberRef targetMemberRef;
    hr = module_metadata.metadata_emit->DefineMemberRef(distributedTracerTypeRef, distributed_tracer_target_method_name.c_str(), targetSignature,
                                                        sizeof(targetSignature), &targetMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Distributed Tracing on defining __GetInstanceForProfiler__ MemberRef");
        return hr;
    }

    ILRewriter getterRewriter(this->info_, nullptr, module_id, getDistributedTraceMethodDef);
    getterRewriter.InitializeTiny();

    // Modify first instruction from ldnull to call
    ILRewriterWrapper getterWrapper(&getterRewriter);
    getterWrapper.SetILPosition(getterRewriter.GetILList()->m_pNext);
    getterWrapper.CallMember(targetMemberRef, false);
    getterWrapper.Return();

    hr = getterRewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting GetDistributedTracer->[AutoInstrumentation]__GetInstanceForProfiler__");
        return hr;
    }

    Logger::Info("Rewriting GetDistributedTracer->[AutoInstrumentation]__GetInstanceForProfiler__");

    if (IsDumpILRewriteEnabled())
    {
        Logger::Info(GetILCodes("After -> GetDistributedTracer. ", &getterRewriter,
                                GetFunctionInfo(module_metadata.metadata_import, getDistributedTraceMethodDef),
                                module_metadata.metadata_import));
    }

    return hr;
}

bool CorProfiler::TypeNameMatchesTraceAttribute(WCHAR type_name[], DWORD type_name_len)
{
    static size_t traceAttributeLength = traceattribute_typename.length();
    static size_t newrelic_traceAttributeLength = newrelic_traceattribute_typename.length();
    static size_t newrelic_transactionAttributeLength = newrelic_transactionattribute_typename.length();

    // Name must match exactly. Subract 1 from the input length to account for the trailing '\0'
    if (type_name_len - 1 == traceAttributeLength)
    {
        for (size_t i = 0; i < traceAttributeLength; i++)
        {
            if (type_name[i] != traceAttribute_typename_cstring[i])
            {
                return false;
            }
        }

        return true;
    }
    else if (type_name_len - 1 == newrelic_traceAttributeLength)
    {
        for (size_t i = 0; i < newrelic_traceAttributeLength; i++)
        {
            if (type_name[i] != newrelic_traceattribute_typename_cstring[i])
            {
                return false;
            }
        }

        return true;
    }
    else if (type_name_len - 1 == newrelic_transactionAttributeLength)
    {
        for (size_t i = 0; i < newrelic_transactionAttributeLength; i++)
        {
            if (type_name[i] != newrelic_transactionattribute_typename_cstring[i])
            {
                return false;
            }
        }

        return true;
    }

    return false;
}

const std::string indent_values[] = {
    "",
    std::string(2 * 1, ' '),
    std::string(2 * 2, ' '),
    std::string(2 * 3, ' '),
    std::string(2 * 4, ' '),
    std::string(2 * 5, ' '),
    std::string(2 * 6, ' '),
    std::string(2 * 7, ' '),
    std::string(2 * 8, ' '),
    std::string(2 * 9, ' '),
    std::string(2 * 10, ' '),
};

std::string CorProfiler::GetILCodes(const std::string& title, ILRewriter* rewriter, const FunctionInfo& caller,
                                    const ComPtr<IMetaDataImport2>& metadata_import)
{
    std::stringstream orig_sstream;
    orig_sstream << title;
    orig_sstream << shared::ToString(caller.type.name);
    orig_sstream << ".";
    orig_sstream << shared::ToString(caller.name);
    orig_sstream << " => (max_stack: ";
    orig_sstream << rewriter->GetMaxStackValue();
    orig_sstream << ")" << std::endl;

    const auto& ehCount = rewriter->GetEHCount();
    const auto& ehPtr = rewriter->GetEHPointer();
    int indent = 1;

    PCCOR_SIGNATURE originalSignature = nullptr;
    ULONG originalSignatureSize = 0;
    mdToken localVarSig = rewriter->GetTkLocalVarSig();

    if (localVarSig != mdTokenNil)
    {
        auto hr =
            metadata_import->GetSigFromToken(localVarSig, &originalSignature, &originalSignatureSize);
        if (SUCCEEDED(hr))
        {
            orig_sstream << std::endl
                         << ". Local Var Signature: "
                         << shared::ToString(shared::HexStr(originalSignature, originalSignatureSize))
                         << std::endl;
        }
    }

    orig_sstream << std::endl;
    for (ILInstr* cInstr = rewriter->GetILList()->m_pNext; cInstr != rewriter->GetILList(); cInstr = cInstr->m_pNext)
    {

        if (ehCount > 0)
        {
            for (unsigned int i = 0; i < ehCount; i++)
            {
                const auto& currentEH = ehPtr[i];
                if (currentEH.m_Flags == COR_ILEXCEPTION_CLAUSE_FINALLY)
                {
                    if (currentEH.m_pTryBegin == cInstr)
                    {
                        if (indent > 0)
                        {
                            orig_sstream << indent_values[indent];
                        }
                        orig_sstream << ".try {" << std::endl;
                        indent++;
                    }
                    if (currentEH.m_pTryEnd == cInstr)
                    {
                        indent--;
                        if (indent > 0)
                        {
                            orig_sstream << indent_values[indent];
                        }
                        orig_sstream << "}" << std::endl;
                    }
                    if (currentEH.m_pHandlerBegin == cInstr)
                    {
                        if (indent > 0)
                        {
                            orig_sstream << indent_values[indent];
                        }
                        orig_sstream << ".finally {" << std::endl;
                        indent++;
                    }
                }
            }
            for (unsigned int i = 0; i < ehCount; i++)
            {
                const auto& currentEH = ehPtr[i];
                if (currentEH.m_Flags == COR_ILEXCEPTION_CLAUSE_NONE)
                {
                    if (currentEH.m_pTryBegin == cInstr)
                    {
                        if (indent > 0)
                        {
                            orig_sstream << indent_values[indent];
                        }
                        orig_sstream << ".try {" << std::endl;
                        indent++;
                    }
                    if (currentEH.m_pTryEnd == cInstr)
                    {
                        indent--;
                        if (indent > 0)
                        {
                            orig_sstream << indent_values[indent];
                        }
                        orig_sstream << "}" << std::endl;
                    }
                    if (currentEH.m_pHandlerBegin == cInstr)
                    {
                        if (indent > 0)
                        {
                            orig_sstream << indent_values[indent];
                        }
                        orig_sstream << ".catch {" << std::endl;
                        indent++;
                    }
                }
            }
        }

        if (indent > 0)
        {
            orig_sstream << indent_values[indent];
        }
        orig_sstream << cInstr;
        orig_sstream << ": ";
        if (cInstr->m_opcode < opcodes_names.size())
        {
            orig_sstream << std::setw(10) << opcodes_names[cInstr->m_opcode];
        }
        else
        {
            orig_sstream << "0x";
            orig_sstream << std::setfill('0') << std::setw(2) << std::hex << cInstr->m_opcode;
        }
        if (cInstr->m_pTarget != NULL)
        {
            orig_sstream << "  ";
            orig_sstream << cInstr->m_pTarget;

            if (cInstr->m_opcode == CEE_CALL || cInstr->m_opcode == CEE_CALLVIRT || cInstr->m_opcode == CEE_NEWOBJ)
            {
                const auto memberInfo = GetFunctionInfo(metadata_import, (mdMemberRef) cInstr->m_Arg32);
                orig_sstream << "  | ";
                orig_sstream << shared::ToString(memberInfo.type.name);
                orig_sstream << ".";
                orig_sstream << shared::ToString(memberInfo.name);
                if (memberInfo.signature.NumberOfArguments() > 0)
                {
                    orig_sstream << "(";
                    orig_sstream << memberInfo.signature.NumberOfArguments();
                    orig_sstream << " argument{s}";
                    orig_sstream << ")";
                }
                else
                {
                    orig_sstream << "()";
                }
            }
            else if (cInstr->m_opcode == CEE_CASTCLASS || cInstr->m_opcode == CEE_BOX ||
                     cInstr->m_opcode == CEE_UNBOX_ANY || cInstr->m_opcode == CEE_NEWARR ||
                     cInstr->m_opcode == CEE_INITOBJ)
            {
                const auto typeInfo = GetTypeInfo(metadata_import, (mdTypeRef) cInstr->m_Arg32);
                orig_sstream << "  | ";
                orig_sstream << shared::ToString(typeInfo.name);
            }
            else if (cInstr->m_opcode == CEE_LDSTR)
            {
                WCHAR szString[1024];
                ULONG szStringLength;
                auto hr = metadata_import->GetUserString((mdString) cInstr->m_Arg32, szString, 1024,
                                                                         &szStringLength);
                if (SUCCEEDED(hr))
                {
                    orig_sstream << "  | \"";
                    orig_sstream << shared::ToString(shared::WSTRING(szString, szStringLength));
                    orig_sstream << "\"";
                }
            }
        }
        else if (cInstr->m_Arg64 != 0)
        {
            orig_sstream << " ";
            orig_sstream << cInstr->m_Arg64;
        }
        orig_sstream << std::endl;

        if (ehCount > 0)
        {
            for (unsigned int i = 0; i < ehCount; i++)
            {
                const auto& currentEH = ehPtr[i];
                if (currentEH.m_pHandlerEnd == cInstr)
                {
                    indent--;
                    if (indent > 0)
                    {
                        orig_sstream << indent_values[indent];
                    }
                    orig_sstream << "}" << std::endl;
                }
            }
        }
    }
    return orig_sstream.str();
}

//
// Startup methods
//
HRESULT CorProfiler::RunILStartupHook(const ComPtr<IMetaDataEmit2>& metadata_emit, const ModuleID module_id,
                                      const mdToken function_token, const FunctionInfo& caller, const ModuleMetadata& module_metadata)
{
    mdMethodDef ret_method_token;
    auto hr = GenerateVoidILStartupMethod(module_id, &ret_method_token);

    if (FAILED(hr))
    {
        Logger::Warn("RunILStartupHook: Call to GenerateVoidILStartupMethod failed for ", module_id);
        return hr;
    }

    ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
    hr = rewriter.Import();

    if (FAILED(hr))
    {
        Logger::Warn("RunILStartupHook: Call to ILRewriter.Import() failed for ", module_id, " ", function_token);
        return hr;
    }

    ILRewriterWrapper rewriter_wrapper(&rewriter);

    // Get first instruction and set the rewriter to that location
    ILInstr* pInstr = rewriter.GetILList()->m_pNext;
    rewriter_wrapper.SetILPosition(pInstr);
    rewriter_wrapper.CallMember(ret_method_token, false);
    hr = rewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("RunILStartupHook: Call to ILRewriter.Export() failed for ModuleID=", module_id, " ",
                     function_token);
        return hr;
    }

    return S_OK;
}

HRESULT CorProfiler::GenerateVoidILStartupMethod(const ModuleID module_id, mdMethodDef* ret_method_token)
{
    ComPtr<IUnknown> metadata_interfaces;
    auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                             metadata_interfaces.GetAddressOf());
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: failed to get metadata interface for ", module_id);
        return hr;
    }

    const auto& metadata_import = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
    const auto& metadata_emit = metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
    const auto& assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
    const auto& assembly_emit = metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

    mdAssemblyRef corlib_ref;
    hr = GetCorLibAssemblyRef(assembly_emit, corAssemblyProperty, &corlib_ref);

    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: failed to define AssemblyRef to mscorlib");
        return hr;
    }

    // Define a TypeRef for System.Object
    mdTypeRef object_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Object"), &object_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
        return hr;
    }

    // Define a new TypeDef __DDVoidMethodType__ that extends System.Object
    mdTypeDef new_type_def;
    hr = metadata_emit->DefineTypeDef(WStr("__DDVoidMethodType__"), tdAbstract | tdSealed, object_type_ref, NULL,
                                      &new_type_def);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeDef failed");
        return hr;
    }

    // Define a new static method __DDVoidMethodCall__ on the new type that has a void return type and takes no
    // arguments
    BYTE initialize_signature[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
        0,                             // Number of parameters
        ELEMENT_TYPE_VOID,             // Return type
    };
    hr = metadata_emit->DefineMethod(new_type_def, WStr("__DDVoidMethodCall__"), mdStatic, initialize_signature,
                                     sizeof(initialize_signature), 0, 0, ret_method_token);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMethod failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Define IsAlreadyLoaded() method
    //

    //
    // Define a new static method IsAlreadyLoaded on the new type that has a bool return type and takes no arguments;
    //
    mdMethodDef alreadyLoadedMethodToken;
    BYTE already_loaded_signature[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        0,
        ELEMENT_TYPE_BOOLEAN,
    };
    hr = metadata_emit->DefineMethod(new_type_def, WStr("IsAlreadyLoaded"), mdStatic | mdPrivate,
                                     already_loaded_signature, sizeof(already_loaded_signature), 0, 0,
                                     &alreadyLoadedMethodToken);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMethod IsAlreadyLoaded failed");
        return hr;
    }

    // Define a new static int field _isAssemblyLoaded on the new type.
    mdFieldDef isAssemblyLoadedFieldToken = mdFieldDefNil;
    BYTE field_signature[] = {IMAGE_CEE_CS_CALLCONV_FIELD, ELEMENT_TYPE_I4};
    hr = metadata_emit->DefineField(new_type_def, WStr("_isAssemblyLoaded"), fdStatic | fdPrivate, field_signature,
                                    sizeof(field_signature), 0, nullptr, 0, &isAssemblyLoadedFieldToken);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineField _isAssemblyLoaded failed");
        return hr;
    }

    // Get a TypeRef for System.Threading.Interlocked
    mdTypeRef interlocked_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Threading.Interlocked"), &interlocked_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName interlocked_type_ref failed");
        return hr;
    }

    // Create method signature for System.Threading.Interlocked::CompareExchange(int32&, int32, int32)
    COR_SIGNATURE interlocked_compare_exchange_signature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT,
                                                              3,
                                                              ELEMENT_TYPE_I4,
                                                              ELEMENT_TYPE_BYREF,
                                                              ELEMENT_TYPE_I4,
                                                              ELEMENT_TYPE_I4,
                                                              ELEMENT_TYPE_I4};

    mdMemberRef interlocked_compare_member_ref;
    hr = metadata_emit->DefineMemberRef(
        interlocked_type_ref, WStr("CompareExchange"), interlocked_compare_exchange_signature,
        sizeof(interlocked_compare_exchange_signature), &interlocked_compare_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef CompareExchange failed");
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
    ILRewriter rewriter_already_loaded(this->info_, nullptr, module_id, alreadyLoadedMethodToken);
    rewriter_already_loaded.InitializeTiny();

    ILInstr* pALFirstInstr = rewriter_already_loaded.GetILList()->m_pNext;
    ILInstr* pALNewInstr = NULL;

    // ldsflda _isAssemblyLoaded : Load the address of the "_isAssemblyLoaded" static var
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
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: Call to ILRewriter.Export() failed for ModuleID=", module_id);
        return hr;
    }

    // Define a method on the managed side that will PInvoke into the profiler method:
    // C++: void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray, int*
    // symbolsSize) C#: static extern void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out
    // IntPtr symbolsPtr, out int symbolsSize)
    mdMethodDef pinvoke_method_def;
    COR_SIGNATURE get_assembly_bytes_signature[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
        4,                             // Number of parameters
        ELEMENT_TYPE_VOID,             // Return type
        ELEMENT_TYPE_BYREF,            // List of parameter types
        ELEMENT_TYPE_I,
        ELEMENT_TYPE_BYREF,
        ELEMENT_TYPE_I4,
        ELEMENT_TYPE_BYREF,
        ELEMENT_TYPE_I,
        ELEMENT_TYPE_BYREF,
        ELEMENT_TYPE_I4,
    };
    hr = metadata_emit->DefineMethod(new_type_def, WStr("GetAssemblyAndSymbolsBytes"),
                                     mdStatic | mdPinvokeImpl | mdHideBySig, get_assembly_bytes_signature,
                                     sizeof(get_assembly_bytes_signature), 0, 0, &pinvoke_method_def);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMethod failed");
        return hr;
    }

    metadata_emit->SetMethodImplFlags(pinvoke_method_def, miPreserveSig);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: SetMethodImplFlags failed");
        return hr;
    }

    shared::WSTRING native_profiler_file = shared::GetCurrentModuleFileName();
    Logger::Debug("GenerateVoidILStartupMethod: Setting the PInvoke native profiler library path to ",
                  native_profiler_file);

    mdModuleRef profiler_ref;
    hr = metadata_emit->DefineModuleRef(native_profiler_file.c_str(), &profiler_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineModuleRef failed");
        return hr;
    }

    hr = metadata_emit->DefinePinvokeMap(pinvoke_method_def, 0, WStr("GetAssemblyAndSymbolsBytes"), profiler_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefinePinvokeMap failed");
        return hr;
    }

    // Get a TypeRef for System.Byte
    mdTypeRef byte_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Byte"), &byte_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
        return hr;
    }

    // Get a TypeRef for System.Runtime.InteropServices.Marshal
    mdTypeRef marshal_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Runtime.InteropServices.Marshal"),
                                            &marshal_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
        return hr;
    }

    // Get a MemberRef for System.Runtime.InteropServices.Marshal.Copy(IntPtr, Byte[], int, int)
    mdMemberRef marshal_copy_member_ref;
    COR_SIGNATURE marshal_copy_signature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
                                              4,                             // Number of parameters
                                              ELEMENT_TYPE_VOID,             // Return type
                                              ELEMENT_TYPE_I,                // List of parameter types
                                              ELEMENT_TYPE_SZARRAY,
                                              ELEMENT_TYPE_U1,
                                              ELEMENT_TYPE_I4,
                                              ELEMENT_TYPE_I4};
    hr = metadata_emit->DefineMemberRef(marshal_type_ref, WStr("Copy"), marshal_copy_signature,
                                        sizeof(marshal_copy_signature), &marshal_copy_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
        return hr;
    }

    // Get a TypeRef for System.Reflection.Assembly
    mdTypeRef system_reflection_assembly_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Reflection.Assembly"),
                                            &system_reflection_assembly_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
        return hr;
    }

    // Get a MemberRef for System.Object.ToString()
    mdTypeRef system_object_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Object"), &system_object_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
        return hr;
    }

    // Create method signature for System.Reflection.Assembly.Load(byte[], byte[])
    COR_SIGNATURE appdomain_load_signature_start[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT, 2,
        ELEMENT_TYPE_CLASS // ret = System.Reflection.Assembly
        // insert compressed token for System.Reflection.Assembly TypeRef here
    };
    COR_SIGNATURE appdomain_load_signature_end[] = {ELEMENT_TYPE_SZARRAY, ELEMENT_TYPE_U1, ELEMENT_TYPE_SZARRAY,
                                                    ELEMENT_TYPE_U1};
    ULONG start_length = sizeof(appdomain_load_signature_start);
    ULONG end_length = sizeof(appdomain_load_signature_end);

    BYTE system_reflection_assembly_type_ref_compressed_token[4];
    ULONG token_length =
        CorSigCompressToken(system_reflection_assembly_type_ref, system_reflection_assembly_type_ref_compressed_token);

    const auto appdomain_load_signature_length = start_length + token_length + end_length;
    COR_SIGNATURE appdomain_load_signature[250];
    memcpy(appdomain_load_signature, appdomain_load_signature_start, start_length);
    memcpy(&appdomain_load_signature[start_length], system_reflection_assembly_type_ref_compressed_token, token_length);
    memcpy(&appdomain_load_signature[start_length + token_length], appdomain_load_signature_end, end_length);

    mdMemberRef appdomain_load_member_ref;
    hr = metadata_emit->DefineMemberRef(system_reflection_assembly_type_ref, WStr("Load"), appdomain_load_signature,
                                        appdomain_load_signature_length, &appdomain_load_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
        return hr;
    }

    // Create method signature for Assembly.CreateInstance(string)
    COR_SIGNATURE assembly_create_instance_signature[] = {IMAGE_CEE_CS_CALLCONV_HASTHIS, 1,
                                                          ELEMENT_TYPE_OBJECT, // ret = System.Object
                                                          ELEMENT_TYPE_STRING};

    mdMemberRef assembly_create_instance_member_ref;
    hr = metadata_emit->DefineMemberRef(system_reflection_assembly_type_ref, WStr("CreateInstance"),
                                        assembly_create_instance_signature, sizeof(assembly_create_instance_signature),
                                        &assembly_create_instance_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
        return hr;
    }

    // Create a string representing "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"
    // Create OS-specific implementations because on Windows, creating the string via
    // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"_W.c_str() does not create the
    // proper string for CreateInstance to successfully call
#ifdef _WIN32
    LPCWSTR load_helper_str = L"Datadog.Trace.ClrProfiler.Managed.Loader.Startup";
    auto load_helper_str_size = wcslen(load_helper_str);
#else
    char16_t load_helper_str[] = u"Datadog.Trace.ClrProfiler.Managed.Loader.Startup";
    auto load_helper_str_size = std::char_traits<char16_t>::length(load_helper_str);
#endif

    mdString load_helper_token;
    hr = metadata_emit->DefineUserString(load_helper_str, (ULONG) load_helper_str_size, &load_helper_token);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineUserString failed");
        return hr;
    }

    // Generate a locals signature defined in the following way:
    //   [0] System.IntPtr ("assemblyPtr" - address of assembly bytes)
    //   [1] System.Int32  ("assemblySize" - size of assembly bytes)
    //   [2] System.IntPtr ("symbolsPtr" - address of symbols bytes)
    //   [3] System.Int32  ("symbolsSize" - size of symbols bytes)
    //   [4] System.Byte[] ("assemblyBytes" - managed byte array for assembly)
    //   [5] System.Byte[] ("symbolsBytes" - managed byte array for symbols)
    //   [6] class System.Reflection.Assembly ("loadedAssembly" - assembly instance to save loaded assembly)
    mdSignature locals_signature_token;
    COR_SIGNATURE locals_signature[15] = {
        IMAGE_CEE_CS_CALLCONV_LOCAL_SIG, // Calling convention
        7,                               // Number of variables
        ELEMENT_TYPE_I,                  // List of variable types
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
    CorSigCompressToken(system_reflection_assembly_type_ref, &locals_signature[11]);
    hr = metadata_emit->GetTokenFromSig(locals_signature, sizeof(locals_signature), &locals_signature_token);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: Unable to generate locals signature. ModuleID=", module_id);
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

    // Step 1) Call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr,
    // out int symbolsSize)

    // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index 0)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOCA_S;
    pNewInstr->m_Arg32 = 0;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // Set the false branch target
    pBranchFalseInstr->m_pTarget = pNewInstr;

    // ldloca.s 1 : Load the address of the "assemblySize" variable (locals index 1)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOCA_S;
    pNewInstr->m_Arg32 = 1;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // ldloca.s 2 : Load the address of the "symbolsPtr" variable (locals index 2)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOCA_S;
    pNewInstr->m_Arg32 = 2;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // ldloca.s 3 : Load the address of the "symbolsSize" variable (locals index 3)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOCA_S;
    pNewInstr->m_Arg32 = 3;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int
    // symbolsSize)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_CALL;
    pNewInstr->m_Arg32 = pinvoke_method_def;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // Step 2) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the
    // managed assembly bytes

    // ldloc.1 : Load the "assemblySize" variable (locals index 1)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOC_1;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // newarr System.Byte : Create a new Byte[] to hold a managed copy of the assembly data
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_NEWARR;
    pNewInstr->m_Arg32 = byte_type_ref;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // stloc.s 4 : Assign the Byte[] to the "assemblyBytes" variable (locals index 4)
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

    // ldloc.1 : Load the "assemblySize" variable (locals index 1) for the Marshal.Copy length parameter
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOC_1;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // call Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_CALL;
    pNewInstr->m_Arg32 = marshal_copy_member_ref;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // Step 3) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the
    // symbols bytes

    // ldloc.3 : Load the "symbolsSize" variable (locals index 3)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOC_3;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // newarr System.Byte : Create a new Byte[] to hold a managed copy of the symbols data
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_NEWARR;
    pNewInstr->m_Arg32 = byte_type_ref;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // stloc.s 5 : Assign the Byte[] to the "symbolsBytes" variable (locals index 5)
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

    // ldloc.3 : Load the "symbolsSize" variable (locals index 3) for the Marshal.Copy length parameter
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOC_3;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_CALL;
    pNewInstr->m_Arg32 = marshal_copy_member_ref;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // Step 4) Call System.Reflection.Assembly System.Reflection.Assembly.Load(byte[], byte[]))

    // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the first byte[] parameter of
    // AppDomain.Load(byte[], byte[])
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOC_S;
    pNewInstr->m_Arg8 = 4;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the second byte[] parameter of
    // AppDomain.Load(byte[], byte[])
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_LDLOC_S;
    pNewInstr->m_Arg8 = 5;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // call System.Reflection.Assembly System.Reflection.Assembly.Load(uint8[], uint8[])
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_CALL;
    pNewInstr->m_Arg32 = appdomain_load_member_ref;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // stloc.s 6 : Assign the System.Reflection.Assembly object to the "loadedAssembly" variable (locals index 6)
    pNewInstr = rewriter_void.NewILInstr();
    pNewInstr->m_opcode = CEE_STLOC_S;
    pNewInstr->m_Arg8 = 6;
    rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

    // Step 4) Call instance method Assembly.CreateInstance("Datadog.Trace.ClrProfiler.Managed.Loader.Startup")

    // ldloc.s 6 : Load the "loadedAssembly" variable (locals index 6) to call Assembly.CreateInstance
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

    if (IsDumpILRewriteEnabled())
    {
        mdToken token = 0;
        TypeInfo typeInfo{};
        shared::WSTRING methodName = WStr("__DDVoidMethodCall__");
        FunctionInfo caller(token, methodName, typeInfo, MethodSignature(), FunctionMethodSignature());
        Logger::Info(
            GetILCodes("*** GenerateVoidILStartupMethod(): Modified Code: ", &rewriter_void, caller, metadata_import));
    }

    hr = rewriter_void.Export();
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: Call to ILRewriter.Export() failed for ModuleID=", module_id);
        return hr;
    }

    return S_OK;
}

HRESULT CorProfiler::AddIISPreStartInitFlags(const ModuleID module_id, const mdToken function_token)
{
    ComPtr<IUnknown> metadata_interfaces;
    auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                             metadata_interfaces.GetAddressOf());
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: failed to get metadata interface for ", module_id);
        return hr;
    }

    const auto& metadata_import = metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
    const auto& metadata_emit = metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
    const auto& assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
    const auto& assembly_emit = metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

    ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
    hr = rewriter.Import();

    if (FAILED(hr))
    {
        Logger::Warn("RunILStartupHook: Call to ILRewriter.Import() failed for ", module_id, " ", function_token);
        return hr;
    }

    ILRewriterWrapper rewriter_wrapper(&rewriter);

    // Get corlib assembly ref
    mdAssemblyRef corlib_ref;
    hr = GetCorLibAssemblyRef(assembly_emit, corAssemblyProperty, &corlib_ref);

    // Get System.Boolean type token
    mdToken boolToken;
    metadata_emit->DefineTypeRefByName(corlib_ref, SystemBoolean, &boolToken);

    // Get System.AppDomain type ref
    mdTypeRef system_appdomain_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.AppDomain"), &system_appdomain_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper objectTypeRef could not be defined.");
        return hr;
    }

    // Get a MemberRef for System.AppDomain.get_CurrentDomain()
    COR_SIGNATURE appdomain_get_current_domain_signature_start[] = {
        IMAGE_CEE_CS_CALLCONV_DEFAULT, 0,
        ELEMENT_TYPE_CLASS, // ret = System.AppDomain
        // insert compressed token for System.AppDomain TypeRef here
    };
    ULONG start_length = sizeof(appdomain_get_current_domain_signature_start);

    BYTE system_appdomain_type_ref_compressed_token[4];
    ULONG token_length = CorSigCompressToken(system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

    const auto appdomain_get_current_domain_signature_length = start_length + token_length;
    COR_SIGNATURE appdomain_get_current_domain_signature[250];
    memcpy(appdomain_get_current_domain_signature, appdomain_get_current_domain_signature_start, start_length);
    memcpy(&appdomain_get_current_domain_signature[start_length], system_appdomain_type_ref_compressed_token,
           token_length);

    mdMemberRef appdomain_get_current_domain_member_ref;
    hr = metadata_emit->DefineMemberRef(
        system_appdomain_type_ref, WStr("get_CurrentDomain"), appdomain_get_current_domain_signature,
        appdomain_get_current_domain_signature_length, &appdomain_get_current_domain_member_ref);

    // Get AppDomain.SetData
    COR_SIGNATURE appdomain_set_data_signature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT |
                                                        IMAGE_CEE_CS_CALLCONV_HASTHIS, // Calling convention
                                                    2,                                 // Number of parameters
                                                    ELEMENT_TYPE_VOID,                 // Return type
                                                    ELEMENT_TYPE_STRING,               // List of parameter types
                                                    ELEMENT_TYPE_OBJECT};
    mdMemberRef appdomain_set_data_member_ref;
    hr = metadata_emit->DefineMemberRef(system_appdomain_type_ref, WStr("SetData"), appdomain_set_data_signature,
                                        sizeof(appdomain_set_data_signature), &appdomain_set_data_member_ref);

    // Define "Datadog_IISPreInitStart" string
    // Create a string representing
    // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup" Create OS-specific
    // implementations because on Windows, creating the string via
    // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"_W.c_str() does not
    // create the proper string for CreateInstance to successfully call
#ifdef _WIN32
    LPCWSTR pre_init_start_str = L"Datadog_IISPreInitStart";
    auto pre_init_start_str_size = wcslen(pre_init_start_str);
#else
    char16_t pre_init_start_str[] = u"Datadog_IISPreInitStart";
    auto pre_init_start_str_size = std::char_traits<char16_t>::length(pre_init_start_str);
#endif

    mdString pre_init_start_string_token;
    hr = metadata_emit->DefineUserString(pre_init_start_str, (ULONG) pre_init_start_str_size,
                                         &pre_init_start_string_token);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineUserString failed");
        return hr;
    }

    // Get first instruction and set the rewriter to that location
    ILInstr* pInstr = rewriter.GetILList()->m_pNext;
    rewriter_wrapper.SetILPosition(pInstr);
    ILInstr* pCurrentInstr = NULL;
    ILInstr* pNewInstr = NULL;

    //////////////////////////////////////////////////
    // At the beginning of the method, call
    // AppDomain.CurrentDomain.SetData(string, true)

    // Call AppDomain.get_CurrentDomain
    rewriter_wrapper.CallMember(appdomain_get_current_domain_member_ref, false);

    // ldstr "Datadog_IISPreInitStart"
    pCurrentInstr = rewriter_wrapper.GetCurrentILInstr();
    pNewInstr = rewriter.NewILInstr();
    pNewInstr->m_opcode = CEE_LDSTR;
    pNewInstr->m_Arg32 = pre_init_start_string_token;
    rewriter.InsertBefore(pCurrentInstr, pNewInstr);

    // load a boxed version of the boolean true
    rewriter_wrapper.LoadInt32(1);
    rewriter_wrapper.Box(boolToken);

    // Call AppDomain.SetData(string, object)
    rewriter_wrapper.CallMember(appdomain_set_data_member_ref, true);

    //////////////////////////////////////////////////
    // At the end of the method, call
    // AppDomain.CurrentDomain.SetData(string, false)
    pInstr = rewriter.GetILList()->m_pPrev; // The last instruction should be a 'ret' instruction

    // Append a ret instruction so we can use the existing ret as the first instruction for our rewriting
    pNewInstr = rewriter.NewILInstr();
    pNewInstr->m_opcode = CEE_RET;
    rewriter.InsertAfter(pInstr, pNewInstr);
    rewriter_wrapper.SetILPosition(pNewInstr);

    // Call AppDomain.get_CurrentDomain
    // Special case: rewrite the previous ret instruction with this call
    pInstr->m_opcode = CEE_CALL;
    pInstr->m_Arg32 = appdomain_get_current_domain_member_ref;

    // ldstr "Datadog_IISPreInitStart"
    pCurrentInstr = rewriter_wrapper.GetCurrentILInstr();
    pNewInstr = rewriter.NewILInstr();
    pNewInstr->m_opcode = CEE_LDSTR;
    pNewInstr->m_Arg32 = pre_init_start_string_token;
    rewriter.InsertBefore(pCurrentInstr, pNewInstr);

    // load a boxed version of the boolean false
    rewriter_wrapper.LoadInt32(0);
    rewriter_wrapper.Box(boolToken);

    // Call AppDomain.SetData(string, object)
    rewriter_wrapper.CallMember(appdomain_set_data_member_ref, true);

    //////////////////////////////////////////////////
    // Finished with the IL rewriting, save the result
    hr = rewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("RunILStartupHook: Call to ILRewriter.Export() failed for ModuleID=", module_id, " ",
                     function_token);
        return hr;
    }

    return S_OK;
}

#ifdef LINUX
extern uint8_t dll_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_start");
extern uint8_t dll_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_end");

extern uint8_t pdb_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_start");
extern uint8_t pdb_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_end");
#endif

void CorProfiler::GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray,
                                             int* symbolsSize) const
{
#ifdef _WIN32
    HINSTANCE hInstance = DllHandle;
    LPCWSTR dllLpName;
    LPCWSTR symbolsLpName;

    if (runtime_information_.is_desktop())
    {
        dllLpName = MAKEINTRESOURCE(NET461_MANAGED_ENTRYPOINT_DLL);
        symbolsLpName = MAKEINTRESOURCE(NET461_MANAGED_ENTRYPOINT_SYMBOLS);
    }
    else
    {
        dllLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_DLL);
        symbolsLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_SYMBOLS);
    }

    HRSRC hResAssemblyInfo = FindResource(hInstance, dllLpName, L"ASSEMBLY");
    HGLOBAL hResAssembly = LoadResource(hInstance, hResAssemblyInfo);
    *assemblySize = SizeofResource(hInstance, hResAssemblyInfo);
    *pAssemblyArray = (LPBYTE) LockResource(hResAssembly);

    HRSRC hResSymbolsInfo = FindResource(hInstance, symbolsLpName, L"SYMBOLS");
    HGLOBAL hResSymbols = LoadResource(hInstance, hResSymbolsInfo);
    *symbolsSize = SizeofResource(hInstance, hResSymbolsInfo);
    *pSymbolsArray = (LPBYTE) LockResource(hResSymbols);
#elif LINUX
    *assemblySize = dll_end - dll_start;
    *pAssemblyArray = (BYTE*) dll_start;

    *symbolsSize = pdb_end - pdb_start;
    *pSymbolsArray = (BYTE*) pdb_start;
#else
    const unsigned int imgCount = _dyld_image_count();

    for (auto i = 0; i < imgCount; i++)
    {
        const std::string name = std::string(_dyld_get_image_name(i));

        if (name.rfind("Datadog.Tracer.Native.dylib") != std::string::npos)
        {
            const mach_header_64* header = (const struct mach_header_64*) _dyld_get_image_header(i);

            unsigned long dllSize;
            const auto dllData = getsectiondata(header, "binary", "dll", &dllSize);
            *assemblySize = dllSize;
            *pAssemblyArray = (BYTE*) dllData;

            unsigned long pdbSize;
            const auto pdbData = getsectiondata(header, "binary", "pdb", &pdbSize);
            *symbolsSize = pdbSize;
            *pSymbolsArray = (BYTE*) pdbData;
            break;
        }
    }
#endif
}

// ***
// * ReJIT Methods
// ***

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                               BOOL fIsSafeToBlock)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    Logger::Debug("ReJITCompilationStarted: [functionId: ", functionId, ", rejitId: ", rejitId,
                  ", safeToBlock: ", fIsSafeToBlock, "]");

    // we notify the reJIT handler of this event
    return rejit_handler->NotifyReJITCompilationStarted(functionId, rejitId);
}

HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                          ICorProfilerFunctionControl* pFunctionControl)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    Logger::Debug("GetReJITParameters: [moduleId: ", moduleId, ", methodId: ", methodId, "]");

    // we notify the reJIT handler of this event and pass the module_metadata.
    return rejit_handler->NotifyReJITParameters(moduleId, methodId, pFunctionControl);
}

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId,
                                                                HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    if (is_attached_ && IsDebugEnabled())
    {
        Logger::Debug("ReJITCompilationFinished: [functionId: ", functionId, ", rejitId: ", rejitId,
                      ", hrStatus: ", hrStatus, ", safeToBlock: ", fIsSafeToBlock, "]");
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                                  HRESULT hrStatus)
{
    if (!is_attached_)
    {
        Logger::Warn("ReJITError: [functionId: ", functionId, ", moduleId: ", moduleId, ", methodId: ", methodId,
                     ", hrStatus: ", hrStatus, "]");
        return S_OK;
    }

    return debugger_instrumentation_requester->NotifyReJITError(moduleId, methodId, functionId, hrStatus);
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction)
{
    auto _ = trace::Stats::Instance()->JITCachedFunctionSearchStartedMeasure();
    if (!is_attached_ || !pbUseCachedFunction)
    {
        return S_OK;
    }

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    std::lock_guard<std::mutex> guard(module_ids_lock_);

    // Extract Module metadata
    ModuleID module_id;
    mdToken function_token = mdTokenNil;

    HRESULT hr = this->info_->GetFunctionInfo(functionId, nullptr, &module_id, &function_token);
    if (FAILED(hr))
    {
        Logger::Warn("JITCachedFunctionSearchStarted: Call to ICorProfilerInfo4.GetFunctionInfo() failed for ",
                     functionId);
        return S_OK;
    }

    // Call RequestRejitOrRevert for register inliners and current NGEN module.
    if (rejit_handler != nullptr)
    {
        // Process the current module to detect inliners.
        rejit_handler->AddNGenInlinerModule(module_id);
    }

    // Verify that we have the metadata for this module
    if (!shared::Contains(module_ids_, module_id))
    {
        // we haven't stored a ModuleMetadata for this module,
        // so there's nothing to do here, we accept the NGEN image.
        *pbUseCachedFunction = true;
        return S_OK;
    }

    const auto& module_info = GetModuleInfo(this->info_, module_id);
    const auto& appDomainId = module_info.assembly.app_domain_id;

    const bool has_loader_injected_in_appdomain =
        first_jit_compilation_app_domains.find(appDomainId) != first_jit_compilation_app_domains.end();

    if (!has_loader_injected_in_appdomain)
    {
        Logger::Debug("Disabling NGEN due to missing loader.");
        // The loader is missing in this AppDomain, we skip the NGEN image to allow the JITCompilationStart inject it.
        *pbUseCachedFunction = false;
        return S_OK;
    }

    *pbUseCachedFunction = true;
    return S_OK;
}

} // namespace trace