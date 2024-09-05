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
#include "fault_tolerant_tracker.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "integration.h"
#include "logger.h"
#include "metadata_builder.h"
#include "module_metadata.h"
#include "resource.h"
#include "stats.h"

#include "../../../shared/src/native-src/pal.h"
#include "../../../shared/src/native-src/version.h"

#include "iast/dataflow.h"

#ifdef MACOS
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

using namespace std::chrono_literals;

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
        Logger::EnableDebug(true);
    }

    CorProfilerBase::Initialize(cor_profiler_info_unknown);

    // we used to bail-out if tracing was disabled, but we now allow the tracer to be loaded
    // in all cases, so that we can enable other products

    const auto process_name = shared::GetCurrentProcessName();
    Logger::Info("ProcessName: ", process_name);

    const auto [process_command_line , tokenized_command_line ] = GetCurrentProcessCommandLine();
    Logger::Info("Process CommandLine: ", process_command_line);

    // CI visibility checks
    if (!process_command_line.empty())
    {
        bool is_ci_visibility_enabled = false;
        if (shared::TryParseBooleanEnvironmentValue(shared::GetEnvironmentValue(environment::ci_visibility_enabled),
                                                    is_ci_visibility_enabled) &&
            is_ci_visibility_enabled)
        {
            const auto isDotNetProcess = process_name == WStr("dotnet") || process_name == WStr("dotnet.exe");
            const auto token_count = tokenized_command_line.size();
            if (isDotNetProcess &&
                token_count > 1 &&
                tokenized_command_line[1] != WStr("test") &&
                // these are executed with exec, so we could check for that, but the
                // below check is more conservative, so leaving at that
                process_command_line.find(WStr("testhost")) == WSTRING::npos &&
                process_command_line.find(WStr("datacollector")) == WSTRING::npos &&
                process_command_line.find(WStr("vstest.console.dll")) == WSTRING::npos)
            {
                Logger::Info("The Tracer Profiler has been disabled because the process is running in CI Visibility "
                    "mode, the name is 'dotnet' but the commandline doesn't contain 'testhost'");
                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
            }
        }
    }

#if !defined(_WIN32) && (defined(ARM64) || defined(ARM))
    //
    // In ARM64 and ARM, complete ReJIT support is only available from .NET 5.0 (on .NET Core)
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

    const auto& include_process_names = shared::GetEnvironmentValues(environment::include_process_names);

    // if there is a process inclusion list, attach clrprofiler only if this
    // process's name is on the list
    if (!include_process_names.empty() && !shared::Contains(include_process_names, process_name))
    {
        Logger::Info("DATADOG TRACER DIAGNOSTICS - ClrProfiler disabled: ", process_name, " not found in ",
                     environment::include_process_names, ".");
        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
    }

    // if we were on the explicit include list, don't check the block list
    if (include_process_names.empty())
    {
        // attach clrprofiler only if this process's name is NOT on the blocklists
        const auto& exclude_process_names = shared::GetEnvironmentValues(environment::exclude_process_names);
        if (!exclude_process_names.empty() && shared::Contains(exclude_process_names, process_name))
        {
            Logger::Info("DATADOG TRACER DIAGNOSTICS - ClrProfiler disabled: ", process_name, " found in ",
                         environment::exclude_process_names, ".");
            return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
        }

        for (auto&& exclude_assembly : default_exclude_assemblies)
        {
            if (process_name == exclude_assembly)
            {
                Logger::Info("DATADOG TRACER DIAGNOSTICS - ClrProfiler disabled: ", process_name," found in default exclude list");
                return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
            }
        }
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

    // get ICorProfilerInfo for >= .NET Core 2.0
    ICorProfilerInfo8* info8 = nullptr;
    hr = cor_profiler_info_unknown->QueryInterface(__uuidof(ICorProfilerInfo8), (void**) &info8);
    if (SUCCEEDED(hr))
    {
        Logger::Debug("Interface ICorProfilerInfo8 found.");
    }
    else
    {
        info8 = nullptr;
    }

    runtime_information_ = GetRuntimeInformation(this->info_);
    if (info8 == nullptr && runtime_information_.is_core())
    {
        Logger::Warn(
            "DATADOG TRACER DIAGNOSTICS - Profiler disabled: .NET Core 2.0 or greater runtime is required for .NET Core automatic instrumentation.");
        return E_FAIL;
    }

    WSTRING runtimeType = runtime_information_.is_core()
                              ? (runtime_information_.major_version > 4 ? WStr(".NET") : WStr(".NET Core"))
                              : WStr(".NET Framework");
    Logger::Info("Runtime Information: ", runtimeType, " ", runtime_information_.major_version, ".",
                 runtime_information_.minor_version, ".", runtime_information_.build_version);

    // Check if we have to disable tiered compilation (due to https://github.com/dotnet/runtime/issues/77973)
    // see https://github.com/DataDog/dd-trace-dotnet/pull/3579 for more details
    bool disableTieredCompilation = false;
    bool internal_workaround_77973_enabled = false;
    shared::TryParseBooleanEnvironmentValue(shared::GetEnvironmentValue(environment::internal_workaround_77973_enabled),
                                            internal_workaround_77973_enabled);
    if (internal_workaround_77973_enabled)
    {
        if (runtime_information_.major_version == 5 ||
            (runtime_information_.major_version == 6 && runtime_information_.minor_version == 0 && runtime_information_.
             build_version <= 12) ||
            (runtime_information_.major_version == 7 && runtime_information_.minor_version == 0 && runtime_information_.
             build_version <= 1))
        {
            Logger::Info("Tiered Compilation was disabled due to https://github.com/dotnet/runtime/issues/77973. This action can be disabled by setting the environment variable DD_INTERNAL_WORKAROUND_77973_ENABLED=false");
            disableTieredCompilation = true;
        }
    }

    auto pInfo = info10 != nullptr ? info10 : this->info_;
    auto work_offloader = std::make_shared<RejitWorkOffloader>(pInfo);

    rejit_handler = info10 != nullptr
                        ? std::make_shared<RejitHandler>(info10, work_offloader)
                        : std::make_shared<RejitHandler>(this->info_, work_offloader);
    tracer_integration_preprocessor = std::make_unique<TracerRejitPreprocessor>(this, rejit_handler, work_offloader);

    fault_tolerant_method_duplicator = std::make_shared<fault_tolerant::FaultTolerantMethodDuplicator>(this, rejit_handler, work_offloader);

    debugger_instrumentation_requester = std::make_unique<debugger::DebuggerProbesInstrumentationRequester>(
        this, rejit_handler, work_offloader, fault_tolerant_method_duplicator);

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
        Logger::Debug("JIT Inlining is enabled.");
    }

    if (DisableOptimizations())
    {
        Logger::Info("Disabling all code optimizations.");
        event_mask |= COR_PRF_DISABLE_OPTIMIZATIONS;
    }

    if (IsNGENEnabled())
    {
        Logger::Debug("NGEN is enabled.");
        event_mask |= COR_PRF_MONITOR_CACHE_SEARCHES;
    }
    else
    {
        Logger::Info("NGEN is disabled.");
        event_mask |= COR_PRF_DISABLE_ALL_NGEN_IMAGES;
    }

    DWORD high_event_mask = COR_PRF_HIGH_ADD_ASSEMBLY_REFERENCES;
    if (disableTieredCompilation)
    {
        high_event_mask |= COR_PRF_HIGH_DISABLE_TIERED_COMPILATION;
    }

    // set event mask to subscribe to events
    hr = this->info_->SetEventMask2(event_mask, high_event_mask);
    if (FAILED(hr))
    {
        Logger::Warn("DATADOG TRACER DIAGNOSTICS - Failed to attach profiler: unable to set event mask.");
        return E_FAIL;
    }

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

    // iast stuff
    
    bool isRaspEnabled = IsRaspEnabled();
    bool isIastEnabled = IsIastEnabled();

    Logger::Info(isIastEnabled ? "IAST Callsite instrumentation is enabled."
                               : "IAST Callsite instrumentation is disabled.");

    Logger::Info(isRaspEnabled ? "RASP Callsite instrumentation is enabled."
                               : "RASP Callsite instrumentation is disabled.");

    if (isIastEnabled || isRaspEnabled)
    {
        _dataflow = new iast::Dataflow(info_, rejit_handler);
        if (FAILED(_dataflow->Init()))
        {
            Logger::Error("Callsite Dataflow failed to initialize");
            DEL(_dataflow);
        }
    }
    else
    {
        Logger::Info("Callsite instrumentation is disabled.");
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
    if (!is_attached_)
    {
        return S_OK;
    }

    auto _ = trace::Stats::Instance()->AssemblyLoadFinishedMeasure();

    if (FAILED(hr_status))
    {
        // if assembly failed to load, skip it entirely,
        // otherwise we can crash the process if module is not valid
        CorProfilerBase::AssemblyLoadFinished(assembly_id, hr_status);
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
        auto hr = this->info_->GetModuleMetaData(assembly_info.manifest_module_id, ofRead,
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
            managed_profiler_loaded_app_domains.insert({assembly_info.app_domain_id, assembly_metadata.version});

            // Load defaults values if the version are the same as expected
            if (assembly_metadata.version == expected_assembly_reference.version)
            {
                EnableByRefInstrumentation();
                EnableCallTargetStateByRef();
                auto traceAnnotationIntegrationId = WSTRING(WStr("9C6EB897BD4946D0BB492E062FB0AE67"));
                auto traceAnnotationType = WSTRING(
                    WStr("Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations.TraceAnnotationsIntegration"));
                AddTraceAttributeInstrumentation(traceAnnotationIntegrationId.data(),
                                                 expected_assembly_reference.str().data(), traceAnnotationType.data());
            }

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

void CorProfiler::RewritingPInvokeMaps(const ModuleMetadata& module_metadata,
                                       const shared::WSTRING& rewrite_reason,
                                       const shared::WSTRING& nativemethods_type_name,
                                       const shared::WSTRING& library_path)
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
            Logger::Warn("Unable to rewrite PInvokes for ", rewrite_reason, ". Native library not found: ", native_profiler_file);
            return;
        }

        Logger::Info("Rewriting PInvokes to native for ", rewrite_reason, ": ", native_profiler_file);

        // Define the actual profiler file path as a ModuleRef
        mdModuleRef profiler_ref;
        hr = metadata_emit->DefineModuleRef(native_profiler_file.c_str(), &profiler_ref);
        if (SUCCEEDED(hr))
        {
            // Enumerate all methods inside the native methods type with the PInvokes
            Enumerator<mdMethodDef> enumMethods = Enumerator<mdMethodDef>(
                [metadata_import, nativeMethodsTypeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt)
                -> HRESULT {
                    return metadata_import->EnumMethods(ptr, nativeMethodsTypeDef, arr, max, cnt);
                },
                [metadata_import](HCORENUM ptr) -> void { metadata_import->CloseEnum(ptr); });

            EnumeratorIterator<mdMethodDef> enumIterator = enumMethods.begin();
            while (enumIterator != enumMethods.end())
            {
                auto methodDef = *enumIterator;

                const auto& caller = GetFunctionInfo(module_metadata.metadata_import, methodDef);
                Logger::Debug("Rewriting PInvoke method: ", caller.name);

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
                        hr = metadata_emit->DefinePinvokeMap(methodDef, pdwMappingFlags,
                                                             shared::WSTRING(importName).c_str(),
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

void __stdcall CorProfiler::NativeLog(int32_t level, const WCHAR* message, int32_t length)
{
    auto str = shared::ToString(message, length);

    if (level == 0)
    {
        Logger::Debug(str);
    }
    else if (level == 1)
    {
        Logger::Info(str);
    }
    else if (level == 2)
    {
        Logger::Warn(str);
    }
    else
    {
        Logger::Error(str);
    }
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID module_id, HRESULT hr_status)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    auto _ = trace::Stats::Instance()->ModuleLoadFinishedMeasure();

    if (FAILED(hr_status))
    {
        // if module failed to load, skip it entirely,
        // otherwise we can crash the process if module is not valid
        CorProfilerBase::ModuleLoadFinished(module_id, hr_status);
        return S_OK;
    }

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    auto modules = module_ids.Get();
    
    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_ || rejit_handler == nullptr)
    {
        return S_OK;
    }

    auto hr = TryRejitModule(module_id, modules.Ref());

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
                    IntegrationDefinition(methodReference, *trace_annotation_integration_type.get(), false, false,
                                          false));
            }

            rejit_module_method_pairs.pop_front();
        }

        // We call the function to analyze the module and request the ReJIT of integrations defined in this module.
        if (tracer_integration_preprocessor != nullptr && !integration_definitions_.empty())
        {
            auto promise = std::make_shared<std::promise<ULONG>>();
            std::future<ULONG> future = promise->get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(rejitModuleIds, integration_definitions_,
                                                                                promise);

            // wait and get the value from the future<ULONG>            
            const auto status = future.wait_for(100ms);

            if (status != std::future_status::timeout)
            {
                const auto& numReJITs = future.get();
                Logger::Debug("Total number of ReJIT Requested: ", numReJITs);    
            }
            else
            {
                Logger::Warn("Timeout while waiting for the rejit requests to be processed. Rejit will continue asynchronously, but some initial calls may not be instrumented");
            }
        }
    }

    if (debugger_instrumentation_requester != nullptr)
    {
        debugger_instrumentation_requester->ModuleLoadFinished(module_id);
    }

    if (_dataflow != nullptr)
    {
        _dataflow->ModuleLoaded(module_id);
    }

    return hr;
}

bool ShouldRewriteProfilerMaps()
{
    const auto envVarKey = WStr("DD_PROFILING_ENABLED");
    if (shared::EnvironmentExist(envVarKey))
    {
        bool is_profiler_enabled;
        auto strValue = shared::GetEnvironmentValue(envVarKey);

        if (shared::TryParseBooleanEnvironmentValue(strValue, is_profiler_enabled))
        {
            return is_profiler_enabled;
        }

        return (strValue == WStr("auto"));
    }

    return shared::EnvironmentExist(WStr("DD_INJECTION_ENABLED"));
}

std::string GetNativeLoaderFilePath()
{
    // should be set by native loader
    shared::WSTRING nativeLoaderPath = shared::GetEnvironmentValue(WStr("DD_INTERNAL_NATIVE_LOADER_PATH"));
    if (!nativeLoaderPath.empty())
    {
        return shared::ToString(nativeLoaderPath);
    }

    // variable not set - try to infer the location instead
    Logger::Debug("DD_INTERNAL_NATIVE_LOADER_PATH variable not found. Inferring native loader path");

    auto native_loader_filename =
#ifdef LINUX
        "Datadog.Trace.ClrProfiler.Native.so";
#elif MACOS
        "Datadog.Trace.ClrProfiler.Native.dylib";
#else
        "Datadog.Trace.ClrProfiler.Native.dll";
#endif

    auto module_file_path = fs::path(shared::GetCurrentModuleFileName());

    auto native_loader_file_path = module_file_path.parent_path() / native_loader_filename;
    return native_loader_file_path.string();
}

HRESULT CorProfiler::TryRejitModule(ModuleID module_id, std::vector<ModuleID>& modules)
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
        auto hr = this->info_->GetModuleMetaData(module_id, ofRead, IID_IMetaDataImport2,
                                                 metadata_interfaces.GetAddressOf());

        // Get the IMetaDataAssemblyImport interface to get metadata from the
        // managed assembly
        const auto& assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        const auto& assembly_metadata = GetAssemblyImportMetadata(assembly_import);

        hr = assembly_import->GetAssemblyProps(assembly_metadata.assembly_token, &corAssemblyProperty.ppbPublicKey,
                                               &corAssemblyProperty.pcbPublicKey, &corAssemblyProperty.pulHashAlgId,
                                               nullptr, 0, nullptr, &corAssemblyProperty.pMetaData,
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
            bool is_included = false;
            // The assembly matches the "skip" prefix, but check if it's specifically included
            for (auto&& include_assembly : include_assemblies)
            {
                if (module_info.assembly.name == include_assembly)
                {
                    is_included = true;
                    break;
                }
            }

            if (is_included)
            {
                Logger::Debug("ModuleLoadFinished matched module by pattern: ", module_id, " ",
                              module_info.assembly.name,
                              "but assembly is explicitly included");
                break;
            }
            else
            {
                Logger::Debug("ModuleLoadFinished skipping module by pattern: ", module_id, " ",
                              module_info.assembly.name);
                return S_OK;
            }
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
        managedProfilerModuleId_ = module_id;
#ifdef _WIN32
        RewritingPInvokeMaps(module_metadata, WStr("windows"), windows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, WStr("ASM"), appsec_windows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, WStr("debugger"), debugger_windows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, WStr("fault_tolerant"), fault_tolerant_windows_nativemethods_type);
#else
        RewritingPInvokeMaps(module_metadata, WStr("non-windows"), nonwindows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, WStr("ASM"), appsec_nonwindows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, WStr("debugger"), debugger_nonwindows_nativemethods_type);
        RewritingPInvokeMaps(module_metadata, WStr("fault_tolerant"), fault_tolerant_nonwindows_nativemethods_type);
#endif // _WIN32

        mdTypeDef bubbleUpTypeDef;
        call_target_bubble_up_exception_available = EnsureCallTargetBubbleUpExceptionTypeAvailable(module_metadata, &bubbleUpTypeDef);
        if(call_target_bubble_up_exception_available)
        {
            call_target_bubble_up_exception_function_available = EnsureIsCallTargetBubbleUpExceptionFunctionAvailable(module_metadata, bubbleUpTypeDef);
        }

        auto native_loader_library_path = GetNativeLoaderFilePath();
        if (fs::exists(native_loader_library_path))
        {
            auto native_loader_file_path = shared::ToWSTRING(native_loader_library_path);
            RewritingPInvokeMaps(module_metadata, WStr("native loader"), native_loader_nativemethods_type, native_loader_file_path);
        }

        if (ShouldRewriteProfilerMaps())
        {
            auto profiler_library_path = shared::GetEnvironmentValue(WStr("DD_INTERNAL_PROFILING_NATIVE_ENGINE_PATH"));
            RewritingPInvokeMaps(module_metadata, WStr("continuous profiler"), profiler_nativemethods_type, profiler_library_path);
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
                if (runtime_information_.is_core() && assemblyImport.version > managed_profiler_assembly_reference->
                    version)
                {
                    Logger::Debug("Skipping version conflict fix for ", assemblyVersion,
                                  " because running on .NET Core with a higher version than expected");
                }
                else
                {
                    RewriteForDistributedTracing(module_metadata, module_id);
                }
            }
        }
        else
        {
            Logger::Debug("Skipping version conflict fix for ", assemblyVersion,
                          " because the version matches the expected one");
        }

        // Rewrite methods for exposing the native tracer version to managed for telemetry purposes
        RewriteForTelemetry(module_metadata, module_id);
    }
    else
    {
        // Datadog.Trace.Manual is _mostly_ treated as a third-party assembly,
        // but we do some rewriting to support manual-only scenarios
        // If/when we go with v3 part deux, we will need to update this to
        // also rewrite to support version mismatch via IDistributedTracer
        if (module_info.assembly.name == manual_instrumentation_name)
        {
            // Rewrite key methods for version mismatch + 
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

            // NOTE: I'm not entirely comfortable that we're passing corAssemblyProperty in here...
            // but I don't know if I _should_ worry, or if we can avoid it 
            const auto& module_metadata =
                ModuleMetadata(metadata_import, metadata_emit, assembly_import, assembly_emit, module_info.assembly.name,
                               module_info.assembly.app_domain_id, &corAssemblyProperty, false, false);
            const auto& assemblyImport = GetAssemblyImportMetadata(assembly_import);

            const auto& assemblyVersion = assemblyImport.version.str();

            Logger::Info("ModuleLoadFinished: ", manual_instrumentation_name, " v", assemblyVersion, " - RewriteIsManualInstrumentationOnly");

            // Rewrite Instrumentation.IsManualInstrumentationOnly()
            RewriteIsManualInstrumentationOnly(module_metadata, module_id);
        }

        modules.push_back(module_id);

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
            auto hr = this->info_->GetModuleMetaData(module_id, ofRead, IID_IMetaDataImport2,
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
                Logger::Debug("ModuleLoadFinished found the TypeDef for ", traceattribute_typename,
                              " defined in Module ", module_info.assembly.name);
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
                                    Logger::Warn("    * Skipping ", shared::TokenStr(&parent_token),
                                        ": the methoddef is not valid!");
                                    customAttributesIterator = ++customAttributesIterator;
                                    continue;
                                }

                                // We create a new function info into the heap from the caller functionInfo in the
                                // stack, to be used later in the ReJIT process
                                auto functionInfo = FunctionInfo(caller);
                                auto hr = functionInfo.method_signature.TryParse();
                                if (FAILED(hr))
                                {
                                    Logger::Warn("    * Skipping ", functionInfo.method_signature.str(),
                                                 ": the method signature cannot be parsed.");
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
                    Logger::Debug(
                        "ModuleLoadFinished pushing [Trace] methods to rejit_module_method_pairs for a later ReJIT, ModuleId=",
                        module_id,
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
                            methodReference, *trace_annotation_integration_type.get(), false, false, false));
                    }
                }
            }
        }

        // We call the function to analyze the module and request the ReJIT of integrations defined in this module.
        if (tracer_integration_preprocessor != nullptr && !integration_definitions_.empty())
        {
            auto promise = std::make_shared<std::promise<ULONG>>();
            std::future<ULONG> future = promise->get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(std::vector<ModuleID>{module_id}, integration_definitions_,
                                                                                promise);

            // wait and get the value from the future<ULONG>
            const auto status = future.wait_for(100ms);

            if (status != std::future_status::timeout)
            {
                const auto& numReJITs = future.get();
            Logger::Debug("[Tracer] Total number of ReJIT Requested: ", numReJITs);
            }
            else
            {
                Logger::Warn("Timeout while waiting for the rejit requests to be processed. Rejit will continue asynchronously, but some initial calls may not be instrumented");
            }
        }
    }

    return S_OK;
}

bool CorProfiler::IsCallTargetBubbleUpExceptionTypeAvailable() const
{
    return call_target_bubble_up_exception_available;
}

bool CorProfiler::IsCallTargetBubbleUpFunctionAvailable() const
{
    return call_target_bubble_up_exception_function_available;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID module_id)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    auto _ = trace::Stats::Instance()->ModuleUnloadStartedMeasure();

    // take this lock so we block until the
    // module metadata is not longer being used
    auto scopedModules = module_ids.Get();
    auto& modules = scopedModules.Ref();

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_)
    {
        return S_OK;
    }

    if (rejit_handler != nullptr)
    {
        rejit_handler->RemoveModule(module_id);
    }

    if (_dataflow != nullptr)
    {
        _dataflow->ModuleUnloaded(module_id);
    }

    auto new_end = std::remove(modules.begin(), modules.end(), module_id);
    modules.erase(new_end, modules.end());

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
        managed_profiler_loaded_app_domains.erase(appDomainId);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown()
{
    if (!is_attached_)
    {
        return S_OK;
    }

    is_attached_.store(false);

    CorProfilerBase::Shutdown();

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    auto modules = module_ids.Get();

    if (rejit_handler != nullptr)
    {
        rejit_handler->Shutdown();
        rejit_handler = nullptr;
    }

    DEL(_dataflow);

    auto definitions = definitions_ids.Get();

    Logger::Info("Exiting...");
    Logger::Debug("   ModuleIds: ", modules->size());
    Logger::Debug("   IntegrationDefinitions: ", integration_definitions_.size());
    Logger::Debug("   DefinitionsIds: ", definitions->size());
    Logger::Debug("   ManagedProfilerLoadedAppDomains: ", managed_profiler_loaded_app_domains.size());
    Logger::Debug("   FirstJitCompilationAppDomains: ", first_jit_compilation_app_domains.size());
    Logger::Info("Stats: ", Stats::Instance()->ToString());
    return S_OK;
}

void CorProfiler::DisableTracerCLRProfiler()
{
    // A full profiler detach request cannot be made because:
    // 1. We use the COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST event mask (CORPROF_E_IMMUTABLE_FLAGS_SET)
    // 2. We instrument code with SetILFunctionBody for the Loader injection. (CORPROF_E_IRREVERSIBLE_INSTRUMENTATION_PRESENT)
    // https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo3-requestprofilerdetach-method
    Logger::Info("Disabling Tracer CLR Profiler...");
    Shutdown();
}

void CorProfiler::UpdateSettings(WCHAR* keys[], WCHAR* values[], int length)
{
    const WSTRING debugVarName = WStr("DD_TRACE_DEBUG");

    for (int i = 0; i < length; i++)
    {
        if (WSTRING(keys[i]) == debugVarName)
        {
            if (values[i] == nullptr || *values[i] == WStr('\0'))
            {
                continue;
            }

            WSTRING value(values[i]);

            if (IsTrue(value))
            {
                Logger::EnableDebug(true);
                Logger::Info("Debug logging has been turned on by remote configuration");
            }
            else if (IsFalse(value))
            {
                Logger::EnableDebug(false);
                Logger::Info("Debug logging has been turned off by remote configuration");
            }
            else
            {
                Logger::Warn("Received an invalid value for DD_TRACE_DEBUG: ", value);
            }
        }
    }
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
    auto modules = module_ids.Get();

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
    if (!is_attached_)
    {
        return S_OK;
    }

    auto _ = trace::Stats::Instance()->JITCompilationStartedMeasure();

    if (!is_safe_to_block)
    {
        return S_OK;
    }

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    auto modules = module_ids.Get();

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
    if (!shared::Contains(modules.Ref(), module_id))
    {
        if (debugger_instrumentation_requester != nullptr)
        {
            debugger_instrumentation_requester->PerformInstrumentAllIfNeeded(module_id, function_token);
        }

        return S_OK;
    }

    const auto& module_info = GetModuleInfo(this->info_, module_id);

    bool has_loader_injected_in_appdomain =
        first_jit_compilation_app_domains.find(module_info.assembly.app_domain_id) !=
        first_jit_compilation_app_domains.end();

    if (has_loader_injected_in_appdomain)
    {
        // Loader was already injected in a calltarget scenario, we don't need to do anything else here

        if (_dataflow != nullptr)
        {
            _dataflow->JITCompilationStarted(module_id, function_token);
        }

        if (debugger_instrumentation_requester != nullptr)
        {
            debugger_instrumentation_requester->PerformInstrumentAllIfNeeded(module_id, function_token);
        }

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

    // In NETFx, NInject creates a temporary appdomain where the tracer can be loaded
    // If Runtime metrics are enabled, we can encounter a CannotUnloadAppDomainException
    // certainly because we are initializing perf counters at that time.
    // As there are no use case where we would like to load the tracer in that appdomain, just don't
    if (module_info.assembly.app_domain_name == WStr("NinjectModuleLoader") && !runtime_information_.is_core())
    {
        Logger::Info("JITCompilationStarted: NInjectModuleLoader appdomain detected. Not registering startup hook.");
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
             module_metadata->assemblyName == WStr("System.Net.Http") ||
             module_metadata->assemblyName == WStr("System.Linq")) // Avoid instrumenting System.Linq which is used as part of the async state machine
    {
        valid_startup_hook_callsite = false;
    }

    // List individual methods that we know we do not want to instrument
    if (caller.type.name == WStr("Costura.AssemblyLoader")) // Avoid inserting the startup hook in methods generated by Costura.Fody. It will set up its own AssemblyResolve handlers so we cannot inject yet
    {
        valid_startup_hook_callsite = false;
    }

    // The first time a method is JIT compiled in an AppDomain, insert our startup
    // hook, which, at a minimum, must add an AssemblyResolve event so we can find
    // Datadog.Trace.dll and its dependencies on disk.
    if (valid_startup_hook_callsite && !has_loader_injected_in_appdomain)
    {
        // *********************************************************************
        // Checking if the caller is inside of the <Module> type
        // *********************************************************************

        // The <Module> typeDef is always the first entry in the typeDef table.
        // The CLR profiling api can return mdTypeDefNil for the <Module> type, so we skip that as well.
        constexpr auto moduleTypeDef = mdTypeDefNil + (BYTE)1;

        if (caller.type.id == mdTypeDefNil || caller.type.id == moduleTypeDef)
        {
            Logger::Debug("JITCompilationStarted: Startup hook skipped from <Module>.", caller.name, "()");
            return S_OK;
        }

        // Look at the type parents in case we are in a nested type
        auto pType = caller.type.parent_type;
        while (pType != nullptr)
        {
            if (pType->id == mdTypeDefNil || pType->id == moduleTypeDef)
            {
                Logger::Debug("JITCompilationStarted: Startup hook skipped from a type with <Module> as a parent. ", caller.type.name, ".", caller.name, "()");
                return S_OK;
            }
                
            pType = pType->parent_type;
        }

        // *********************************************************************
        // Checking if the caller is inside of the <CrtImplementationDetails> type
        // *********************************************************************

        if (caller.type.name.find(WStr("<CrtImplementationDetails>")) != shared::WSTRING::npos)
        {
            Logger::Debug("JITCompilationStarted: Startup hook skipped from ", caller.type.name, ".", caller.name, "()");
            return S_OK;
        }

        // *********************************************************************

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
    // take this lock so we block until the
    // module metadata is not longer being used
    auto modules = module_ids.Get();

    // double check if is_attached_ has changed to avoid possible race condition with shutdown function
    if (!is_attached_)
    {
        return S_OK;
    }

    if (_dataflow != nullptr)
    {
        _dataflow->AppDomainShutdown(appDomainId);
    }

    // remove appdomain metadata from map
    const auto& count = first_jit_compilation_app_domains.erase(appDomainId);

    Logger::Debug("AppDomainShutdownFinished: AppDomain: ", appDomainId, ", removed ", count, " elements");

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    auto _ = trace::Stats::Instance()->JITInliningMeasure();

    if (rejit_handler == nullptr)
    {
        return S_OK;
    }

    ModuleID calleeModuleId;
    mdToken calleFunctionToken = mdTokenNil;
    auto hr = this->info_->GetFunctionInfo(calleeId, nullptr, &calleeModuleId, &calleFunctionToken);

    *pfShouldInline = true;

    if (FAILED(hr))
    {
        Logger::Warn("*** JITInlining: Failed to get the function info of the calleId: ", calleeId);
        return S_OK;
    }

    if (is_attached_ &&
            (
                (rejit_handler != nullptr && rejit_handler->HasModuleAndMethod(calleeModuleId, calleFunctionToken)) ||
                (_dataflow != nullptr && !_dataflow->IsInlineEnabled(calleeModuleId, calleFunctionToken))
            )
       )
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
        InternalAddInstrumentation(id, items, size, false, false, true);
    }
}

void CorProfiler::RemoveCallTargetDefinitions(WCHAR* id, CallTargetDefinition* items, int size)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    Logger::Info("RemoveCallTargetDefinitions: received id: ", definitionsId, " from managed side with ", size,
                 " integrations.");

    if (size > 0)
    {
        InternalAddInstrumentation(id, items, size, false, false, false);
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
        InternalAddInstrumentation(id, items, size, true, false);
    }
}

void CorProfiler::AddInterfaceInstrumentations(WCHAR* id, CallTargetDefinition* items, int size)
{
    auto _ = trace::Stats::Instance()->InitializeProfilerMeasure();
    shared::WSTRING definitionsId = shared::WSTRING(id);
    Logger::Info("AddInterfaceInstrumentations: received id: ", definitionsId, " from managed side with ", size,
                 " integrations.");

    if (size > 0)
    {
        InternalAddInstrumentation(id, items, size, false, true);
    }
}

void CorProfiler::InternalAddInstrumentation(WCHAR* id, CallTargetDefinition* items, int size, bool isDerived,
                                             bool isInterface, bool enable)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    auto definitions = definitions_ids.Get();

    auto defsIdFound = definitions->find(definitionsId) != definitions->end();
    if (enable && defsIdFound)
    {
        Logger::Info("InitializeProfiler: Id already processed.");
        return;
    }
    if (!enable && !defsIdFound)
    {
        Logger::Info("UninitializeProfiler: Id not processed.");
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
                isInterface,
                true,
                -1,
                enable ? -1 : 0);

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetAssembly, " | ", targetType, ".", targetMethod, "(",
                              signatureTypes.size(), ") { ", minVersion.str(), " - ", maxVersion.str(), " } [",
                              integrationAssembly, " | ", integrationType, "]");
            }

            integrationDefinitions.push_back(integration);
        }

        auto modules = module_ids.Get();

        if (enable)
        {
            definitions->emplace(definitionsId);
        }
        else
        {
            definitions->erase(definitionsId);
        }

        if (enable)
        {
            integration_definitions_.reserve(integration_definitions_.size() + integrationDefinitions.size());
            for (const auto& integration : integrationDefinitions)
            {
                integration_definitions_.push_back(integration);
            }
        }
        else
        {
            // remove the call target definitions
            std::vector<IntegrationDefinition> integration_definitions = integration_definitions_;
            integration_definitions_.clear();
            for (auto& integration : integration_definitions)
            {
                if (std::find(integrationDefinitions.begin(), integrationDefinitions.end(), integration) ==
                    integrationDefinitions.end())
                {
                    integration_definitions_.push_back(integration);
                }
            }
        }

        Logger::Info("Total number of modules to analyze: ", modules->size());
        if (rejit_handler != nullptr)
        {
            auto promise = std::make_shared<std::promise<ULONG>>();
            std::future<ULONG> future = promise->get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(modules.Ref(), integrationDefinitions,
                                                                                 promise);

            // wait and get the value from the future<int>
            const auto& numReJITs = future.get();
            Logger::Debug("Total number of ReJIT Requested: ", numReJITs);
        }

        Logger::Info("InitializeProfiler: Total integrations in profiler: ", integration_definitions_.size());
    }
}

long CorProfiler::RegisterCallTargetDefinitions(WCHAR* id, CallTargetDefinition2* items, int size, UINT32 enabledCategories)
{
    long numReJITs = 0;
    long enabledTargets = 0;
    shared::WSTRING definitionsId = shared::WSTRING(id);
    auto definitions = definitions_ids.Get();

    auto defsIdFound = definitions->find(definitionsId) != definitions->end();
    if (defsIdFound)
    {
        Logger::Info("RegisterCallTargetDefinitions: Id already processed.");
        return 0;
    }
    definitions->emplace(definitionsId);

    if (items != nullptr && rejit_handler != nullptr)
    {
        std::vector<IntegrationDefinition> integrationDefinitions;

        for (int i = 0; i < size; i++)
        {
            const auto& current = items[i];

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

            const Version& minVersion = Version(current.targetMinimumMajor, current.targetMinimumMinor, current.targetMinimumPatch, 0);
            const Version& maxVersion = Version(current.targetMaximumMajor, current.targetMaximumMinor, current.targetMaximumPatch, 0);

            const auto& integration = IntegrationDefinition(
                MethodReference(targetAssembly, targetType, targetMethod, minVersion, maxVersion, signatureTypes),
                TypeReference(integrationAssembly, integrationType, {}, {}), current.GetIsDerived(),
                current.GetIsInterface(), true, current.categories, enabledCategories);

            if (integration.GetEnabled())
            {
                enabledTargets++;
            }

            if (Logger::IsDebugEnabled())
            {
                std::string kind = current.GetIsDerived() ? "DERIVED" : "DEFAULT";
                if (current.GetIsInterface())
                {
                    kind += " INTERFACE";
                }
                Logger::Debug("  * Target: ", targetAssembly, " | ", targetType, ".", targetMethod, "(",
                              signatureTypes.size(), ") { ", minVersion.str(), " - ", maxVersion.str(), " } [",
                              integrationAssembly, " | ", integrationType, " | kind: ", kind, " | categories: ", current.categories,
                              integration.GetEnabled() ? " ENABLED " : " DISABLED ", "]");
            }

            integrationDefinitions.push_back(integration);
        }

        auto modules = module_ids.Get();

        integration_definitions_.reserve(integration_definitions_.size() + integrationDefinitions.size());
        for (const auto& integration : integrationDefinitions)
        {
            integration_definitions_.push_back(integration);
        }

        Logger::Info("Total number of modules to analyze: ", modules->size());
        if (rejit_handler != nullptr)
        {
            auto promise = std::make_shared<std::promise<ULONG>>();
            std::future<ULONG> future = promise->get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(modules.Ref(), integrationDefinitions, promise);

            // wait and get the value from the future<int>
            numReJITs = future.get();
            Logger::Debug("Total number of ReJIT Requested: ", numReJITs);
        }

        Logger::Info("RegisterCallTargetDefinitions: Added ", size, " call targets (enabled: ", enabledTargets,
                      ", enabled categories: ", enabledCategories ,") ");
    }

    return enabledTargets;
}
long CorProfiler::EnableCallTargetDefinitions(UINT32 enabledCategories)
{
    long numReJITs = 0;
    if (rejit_handler != nullptr)
    {
        auto _ = trace::Stats::Instance()->InitializeProfilerMeasure();
        Logger::Info("EnableCallTargetDefinitions: enabledCategories: ", enabledCategories, " from managed side.");

        std::vector<IntegrationDefinition> affectedDefinitions;
        for (auto& integration : integration_definitions_)
        {
            if (!integration.GetEnabled() && integration.SetEnabled(true, enabledCategories))
            {
                affectedDefinitions.push_back(integration);
            }
        }

        if (affectedDefinitions.size() > 0)
        {
            auto modules = module_ids.Get();
            auto promise = std::make_shared<std::promise<ULONG>>();
            std::future<ULONG> future = promise->get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(modules.Ref(), affectedDefinitions,
                                                                                 promise);

            // wait and get the value from the future<int>
            numReJITs = future.get();
        }
        Logger::Debug("  Total number of ReJIT Requested: ", numReJITs);
    }
    return numReJITs;
}
long CorProfiler::DisableCallTargetDefinitions(UINT32 disabledCategories)
{
    long numReverts = 0;
    if (rejit_handler != nullptr)
    {
        auto _ = trace::Stats::Instance()->InitializeProfilerMeasure();
        Logger::Info("DisableCallTargetDefinitions: enabledCategories: ", disabledCategories, " from managed side.");

        std::vector<IntegrationDefinition> affectedDefinitions;
        for (auto& integration : integration_definitions_)
        {
            if (integration.GetEnabled() && !integration.SetEnabled(false, disabledCategories))
            {
                affectedDefinitions.push_back(integration);
            }
        }

        if (affectedDefinitions.size() > 0)
        {
            auto modules = module_ids.Get();
            auto promise = std::make_shared<std::promise<ULONG>>();
            std::future<ULONG> future = promise->get_future();
            tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(modules.Ref(), affectedDefinitions, promise);

            // wait and get the value from the future<int>
            numReverts = future.get();
        }
        Logger::Debug("  Total number of Reverts Requested: ", numReverts);
    }
    return numReverts;
}

int CorProfiler::RegisterIastAspects(WCHAR** aspects, int aspectsLength)
{
    auto _ = trace::Stats::Instance()->InitializeProfilerMeasure();

    if (_dataflow != nullptr)
    {
        Logger::Info("Registering Callsite Aspects.");
        _dataflow->LoadAspects(aspects, aspectsLength);
        return aspectsLength;
    }
    else
    {
        Logger::Info("Callsite instrumentation is disabled.");
    }
    return 0;
}


void CorProfiler::AddTraceAttributeInstrumentation(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                                   WCHAR* integration_type_name_ptr)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    auto definitions = definitions_ids.Get();

    if (definitions->find(definitionsId) != definitions->end())
    {
        Logger::Info("AddTraceAttributeInstrumentation: Id already processed.");
        return;
    }

    definitions->emplace(definitionsId);
    shared::WSTRING integration_assembly_name = shared::WSTRING(integration_assembly_name_ptr);
    shared::WSTRING integration_type_name = shared::WSTRING(integration_type_name_ptr);
    trace_annotation_integration_type =
        std::unique_ptr<TypeReference>(new TypeReference(integration_assembly_name, integration_type_name, {}, {}));

    Logger::Info("AddTraceAttributeInstrumentation: Initialized assembly=", integration_assembly_name, ", type=",
                 integration_type_name);
}

void CorProfiler::InitializeTraceMethods(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                         WCHAR* integration_type_name_ptr,
                                         WCHAR* configuration_string_ptr)
{
    shared::WSTRING definitionsId = shared::WSTRING(id);
    auto definitions = definitions_ids.Get();

    if (definitions->find(definitionsId) != definitions->end())
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
    definitions->emplace(definitionsId);
    if (rejit_handler != nullptr)
    {
        if (trace_annotation_integration_type == nullptr)
        {
            Logger::Warn(
                "InitializeTraceMethods: Integration type was not initialized. AddTraceAttributeInstrumentation must be called first");
        }
        else if (trace_annotation_integration_type.get()->assembly.str() != integration_assembly_name
                 || trace_annotation_integration_type.get()->name != integration_type_name)
        {
            Logger::Warn("InitializeTraceMethods: Integration type was initialized to assembly=",
                         trace_annotation_integration_type.get()->assembly.str(), ", type=",
                         trace_annotation_integration_type.get()->name,
                         ". InitializeTraceMethods was invoked with assembly=", integration_assembly_name, ", type=",
                         integration_type_name, ". Exiting InitializeTraceMethods.");
        }
        else if (configuration_string.size() > 0)
        {
            std::vector<IntegrationDefinition> integrationDefinitions = GetIntegrationsFromTraceMethodsConfiguration(
                *trace_annotation_integration_type.get(), configuration_string);
            auto modules = module_ids.Get();

            Logger::Debug("InitializeTraceMethods: Total number of modules to analyze: ", modules->size());
            if (rejit_handler != nullptr)
            {
                auto promise = std::make_shared<std::promise<ULONG>>();
                std::future<ULONG> future = promise->get_future();
                tracer_integration_preprocessor->EnqueueRequestRejitForLoadedModules(
                    modules.Ref(), integrationDefinitions,
                    promise);

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
                                   debugger::DebuggerMethodSpanProbeDefinition* spanProbes, int spanProbesLength,
                                   debugger::DebuggerRemoveProbesDefinition* removeProbes, int revertProbesLength) const
{
    if (debugger_instrumentation_requester != nullptr)
    {
        debugger_instrumentation_requester->InstrumentProbes(methodProbes, methodProbesLength, lineProbes,
                                                             lineProbesLength, spanProbes, spanProbesLength, removeProbes, revertProbesLength);
    }
}

int CorProfiler::GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses)
{
    if (debugger_instrumentation_requester != nullptr)
    {
        return debugger_instrumentation_requester->GetProbesStatuses(probeIds, probeIdsLength, probeStatuses);
    }

    return 0;
}

//
// Fault-Tolerant Instrumentation methods
//
void CorProfiler::ReportSuccessfulInstrumentation(ModuleID moduleId, int methodToken, const WCHAR* instrumentationId,
    int products)
{
    const auto instrumentationIdString = shared::WSTRING(instrumentationId);
    const auto instrumentingProducts = static_cast<InstrumentingProducts>(products);
    const auto methodId = static_cast<mdMethodDef>(methodToken);
    fault_tolerant::FaultTolerantTracker::Instance()->AddSuccessfulInstrumentationId(moduleId, methodId, instrumentationIdString, instrumentingProducts, rejit_handler);
}

bool CorProfiler::ShouldHeal(ModuleID moduleId, int methodToken, const WCHAR* instrumentationId, int products)
{
    const auto instrumentationIdString = shared::WSTRING(instrumentationId);
    const auto instrumentingProducts = static_cast<InstrumentingProducts>(products);
    const auto methodId = static_cast<mdMethodDef>(methodToken);
    return fault_tolerant::FaultTolerantTracker::Instance()->ShouldHeal(moduleId, methodId, instrumentationIdString, instrumentingProducts, rejit_handler);
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
        ELEMENT_TYPE_OBJECT,           // Return type
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

    LogManagedProfilerAssemblyDetails();

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
    hr = module_metadata.metadata_emit->DefineMemberRef(distributedTracerTypeRef,
                                                        distributed_tracer_target_method_name.c_str(), targetSignature,
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

HRESULT CorProfiler::RewriteForTelemetry(const ModuleMetadata& module_metadata, ModuleID module_id)
{
    HRESULT hr = S_OK;

    LogManagedProfilerAssemblyDetails();

    //
    // *** Get Instrumentation TypeDef
    //
    mdTypeDef instrumentationTypeDef;
    hr = module_metadata.metadata_import->FindTypeDefByName(instrumentation_type_name.c_str(),
                                                            mdTokenNil, &instrumentationTypeDef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Telemetry on getting Instrumentation TypeDef");
        return hr;
    }

    //
    // *** GetNativeTracerVersion MethodDef ***
    //
    constexpr COR_SIGNATURE getNativeTracerVersion[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT, 0, ELEMENT_TYPE_STRING};
    mdMethodDef getNativeTracerVersionMethodDef;
    hr = module_metadata.metadata_import->FindMethod(instrumentationTypeDef, WStr("GetNativeTracerVersion"),
                                                     getNativeTracerVersion, 3, &getNativeTracerVersionMethodDef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Telemetry on getting GetNativeTracerVersion MethodDef");
        return hr;
    }

    // Define NativeTracerVersion as a string
    mdString nativeTracerVersionToken;
    const auto nativeTracerVersion = ToWSTRING(PROFILER_VERSION);
    hr = module_metadata.metadata_emit->DefineUserString(
        nativeTracerVersion.c_str(), static_cast<ULONG>(nativeTracerVersion.length()), &nativeTracerVersionToken);

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting for Telemetry on DefineUserString for NativeProfilerVersion");
        return hr;
    }

    ILRewriter methodRewriter(this->info_, nullptr, module_id, getNativeTracerVersionMethodDef);
    methodRewriter.InitializeTiny();

    // Modify first instruction from ldstr "None" to ldstr PROFILER_VERSION
    ILRewriterWrapper wrapper(&methodRewriter);
    wrapper.SetILPosition(methodRewriter.GetILList()->m_pNext);
    wrapper.LoadStr(nativeTracerVersionToken);
    wrapper.Return();

    hr = methodRewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting Instrumentation.GetNativeTracerVersion() => PROFILER_VERSION");
        return hr;
    }

    Logger::Info("Rewriting Instrumentation.GetNativeTracerVersion() => PROFILER_VERSION");

    if (IsDumpILRewriteEnabled())
    {
        Logger::Info(GetILCodes("After -> Instrumentation.GetNativeTracerVersion(). ", &methodRewriter,
                                GetFunctionInfo(module_metadata.metadata_import, getNativeTracerVersionMethodDef),
                                module_metadata.metadata_import));
    }

    return hr;
}

HRESULT CorProfiler::RewriteIsManualInstrumentationOnly(const ModuleMetadata& module_metadata, ModuleID module_id)
{
    //
    // *** Get Instrumentation TypeDef
    //
    mdTypeDef instrumentationTypeDef;
    HRESULT hr = module_metadata.metadata_import->FindTypeDefByName(instrumentation_type_name.c_str(),
                                                                    mdTokenNil, &instrumentationTypeDef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting IsManualInstrumentationOnly on getting Instrumentation TypeDef");
        return hr;
    }

    //
    // *** IsManualInstrumentationOnly MethodDef ***
    //
    constexpr COR_SIGNATURE isAutoEnabledSignature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT, 0, ELEMENT_TYPE_BOOLEAN};
    mdMethodDef isAutoEnabledMethodDef;
    hr = module_metadata.metadata_import->FindMethod(instrumentationTypeDef, WStr("IsManualInstrumentationOnly"),
                                                     isAutoEnabledSignature, 3, &isAutoEnabledMethodDef);
    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting IsManualInstrumentationOnly on getting IsManualInstrumentationOnly MethodDef");
        return hr;
    }

    ILRewriter methodRewriter(this->info_, nullptr, module_id, isAutoEnabledMethodDef);
    methodRewriter.InitializeTiny();

    // Modify method from this:
    // IL_0000: ldc.i4.1
    // IL_0001: ret
    //
    // to this:
    // IL_0000: ldc.i4.0
    // IL_0001: ret
    ILRewriterWrapper wrapper(&methodRewriter);
    wrapper.SetILPosition(methodRewriter.GetILList()->m_pNext);
    wrapper.LoadInt32(0);
    wrapper.Return();

    hr = methodRewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("Error rewriting Instrumentation.IsManualInstrumentationOnly() => false");
        return hr;
    }

    Logger::Info("Rewriting Instrumentation.IsManualInstrumentationOnly() => false");

    if (IsDumpILRewriteEnabled())
    {
        Logger::Info(GetILCodes("After -> Instrumentation.IsManualInstrumentationOnly(). ", &methodRewriter,
                                GetFunctionInfo(module_metadata.metadata_import, isAutoEnabledMethodDef),
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
    orig_sstream << title << std::endl << std::endl;
    orig_sstream << "Name: " << shared::ToString(caller.type.name);
    orig_sstream << ".";
    orig_sstream << shared::ToString(caller.name);
    const auto callerNumOfArgs = caller.signature.NumberOfArguments();
    if (callerNumOfArgs > 0)
    {
        orig_sstream << "(";
        orig_sstream << callerNumOfArgs;
        if (callerNumOfArgs == 1)
        {
            orig_sstream << " argument";
        }
        else
        {
            orig_sstream << " arguments";
        }
        orig_sstream << ")";
    }
    else
    {
        orig_sstream << "()";
    }
    orig_sstream << std::endl << "Signature: " << ToString(caller.signature.str()) << std::endl;
    orig_sstream << "Max Stack: ";
    orig_sstream << rewriter->GetMaxStackValue() << std::endl;

    const auto ehCount = rewriter->GetEHCount();
    const auto ehPtr = rewriter->GetEHPointer();
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
            orig_sstream << "Local Var Signature: "
                << shared::ToString(shared::HexStr(originalSignature, originalSignatureSize))
                << std::endl;
        }
    }

    orig_sstream << "{" << std::endl;
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
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "try" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
                        indent++;
                    }
                    if (currentEH.m_pTryEnd == cInstr)
                    {
                        indent--;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "}" << std::endl;
                    }
                    if (currentEH.m_pHandlerBegin == cInstr)
                    {
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "finally" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
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
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "try" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
                        indent++;
                    }
                    if (currentEH.m_pTryEnd == cInstr)
                    {
                        indent--;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "}" << std::endl;
                    }
                    if (currentEH.m_pHandlerBegin == cInstr)
                    {
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "catch";
                        if (currentEH.m_ClassToken != mdTokenNil)
                        {
                            const auto typeInfo = GetTypeInfo(metadata_import, (mdToken) currentEH.m_ClassToken);
                            orig_sstream << " (" << shared::ToString(typeInfo.name) << " [0x" << std::hex << currentEH.
                                m_ClassToken << "])";
                        }
                        orig_sstream << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
                        indent++;
                    }
                }
            }
            for (unsigned int i = 0; i < ehCount; i++)
            {
                const auto& currentEH = ehPtr[i];
                if (currentEH.m_Flags == COR_ILEXCEPTION_CLAUSE_FILTER)
                {
                    if (currentEH.m_pTryBegin == cInstr)
                    {
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "try" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
                        indent++;
                    }
                    if (currentEH.m_pTryEnd == cInstr)
                    {
                        indent--;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "}" << std::endl;
                    }
                    if (currentEH.m_pFilter == cInstr)
                    {
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "filter" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
                        indent++;
                    }
                    if (currentEH.m_pHandlerBegin == cInstr)
                    {
                        indent--;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "}" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "catch" << std::endl;
                        orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "{" << std::endl;
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
        if (cInstr->m_pTarget != nullptr)
        {
            orig_sstream << "  ";

            bool augmented = false;
            if (cInstr->m_opcode == CEE_CALL || cInstr->m_opcode == CEE_CALLVIRT || cInstr->m_opcode == CEE_NEWOBJ)
            {
                const auto memberInfo = GetFunctionInfo(metadata_import, (mdMemberRef) cInstr->m_Arg32);
                augmented = true;
                if (memberInfo.signature.IsInstanceMethod())
                {
                    orig_sstream << "instance ";
                }
                orig_sstream << shared::ToString(memberInfo.type.name);
                orig_sstream << ".";
                orig_sstream << shared::ToString(memberInfo.name);
                const auto numOfArgs = memberInfo.signature.NumberOfArguments();
                if (numOfArgs > 0)
                {
                    orig_sstream << "(";
                    orig_sstream << numOfArgs;
                    if (numOfArgs == 1)
                    {
                        orig_sstream << " argument";
                    }
                    else
                    {
                        orig_sstream << " arguments";
                    }
                    orig_sstream << ")";
                }
                else
                {
                    orig_sstream << "()";
                }
            }
            else if (cInstr->m_opcode == CEE_CASTCLASS || cInstr->m_opcode == CEE_BOX ||
                     cInstr->m_opcode == CEE_UNBOX_ANY || cInstr->m_opcode == CEE_NEWARR ||
                     cInstr->m_opcode == CEE_INITOBJ || cInstr->m_opcode == CEE_ISINST)
            {
                const auto typeInfo = GetTypeInfo(metadata_import, (mdTypeRef) cInstr->m_Arg32);
                augmented = true;
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
                    augmented = true;
                    orig_sstream << "\"";
                    orig_sstream << shared::ToString(shared::WSTRING(szString, szStringLength));
                    orig_sstream << "\"";
                }
            }

            if (augmented)
            {
                orig_sstream << " [";
                orig_sstream << cInstr->m_pTarget;
                orig_sstream << "]";
            }
            else
            {
                orig_sstream << cInstr->m_pTarget;
            }
        }
        else if (cInstr->m_Arg64 != 0)
        {
            orig_sstream << "  ";
            orig_sstream << cInstr->m_Arg64;
        }
        else if (cInstr->m_opcode == CEE_STLOC || cInstr->m_opcode == CEE_STLOC_S ||
                 cInstr->m_opcode == CEE_LDLOC || cInstr->m_opcode == CEE_LDLOC_S ||
                 cInstr->m_opcode == CEE_LDLOCA || cInstr->m_opcode == CEE_LDLOCA_S)
        {
            orig_sstream << "  ";
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
                    orig_sstream << indent_values[(indent >= 0) ? indent : 0] << "}" << std::endl;
                }
            }
        }
    }
    orig_sstream << "}" << std::endl;
    return orig_sstream.str();
}

bool CorProfiler::EnsureCallTargetBubbleUpExceptionTypeAvailable(const ModuleMetadata& module_metadata, mdTypeDef* typeDef)
{
    // *** Ensure Datadog.Trace.ClrProfiler.CallTarget.CallTargetBubbleUpException available
    const auto bubble_up_type_name = calltargetbubbleexception_tracer_type_name.c_str();
    const auto found_call_target_bubble_up_exception_type = module_metadata.metadata_import->FindTypeDefByName(bubble_up_type_name, mdTokenNil, typeDef);
    Logger::Debug("CallTargetBubbleUpException type available test, hresult is: ", found_call_target_bubble_up_exception_type);
    return SUCCEEDED(found_call_target_bubble_up_exception_type);
}

bool CorProfiler::EnsureIsCallTargetBubbleUpExceptionFunctionAvailable(const ModuleMetadata& module_metadata, mdTypeDef typeDef)
{
    const auto bubble_up_function_name = calltargetbubbleexception_tracer_function_name.c_str();

    mdMethodDef methodDef = mdTokenNil;
    const auto found_call_target_bubble_up_exception_function = module_metadata.metadata_import->FindMethod(typeDef, bubble_up_function_name, 0, 0, &methodDef);

    auto res = SUCCEEDED(found_call_target_bubble_up_exception_function);
    Logger::Debug("CallTargetBubbleUpException.IsCallTargetBubbleUpException method found: ", res);
    return res;
}

//
// Startup methods
//
HRESULT CorProfiler::RunILStartupHook(const ComPtr<IMetaDataEmit2>& metadata_emit, const ModuleID module_id,
                                      const mdToken function_token, const FunctionInfo& caller,
                                      const ModuleMetadata& module_metadata)
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

    // ****************************************************************************************************************
    // Assembly Refs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Define an AssemblyRef for CorLib
    mdAssemblyRef corlib_ref;
    hr = GetCorLibAssemblyRef(assembly_emit, corAssemblyProperty, &corlib_ref);

    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: failed to define AssemblyRef to mscorlib");
        return hr;
    }

    // ****************************************************************************************************************
    // Type Refs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Define a TypeRef for System.Object
    mdTypeRef object_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Object"), &object_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Object failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.Threading.Interlocked
    mdTypeRef interlocked_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Threading.Interlocked"), &interlocked_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Threading.Interlocked failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.Byte
    mdTypeRef byte_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Byte"), &byte_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Byte failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.Char
    mdTypeRef char_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Char"), &char_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Char failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.String
    mdTypeRef string_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.String"), &string_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.String failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.Exception
    mdTypeRef exception_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Exception"), &exception_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Exception failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.Runtime.InteropServices.Marshal
    mdTypeRef marshal_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Runtime.InteropServices.Marshal"),
                                            &marshal_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Runtime.InteropServices.Marshal failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Get a TypeRef for System.Reflection.Assembly
    mdTypeRef system_reflection_assembly_type_ref;
    hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.Reflection.Assembly"),
                                            &system_reflection_assembly_type_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.Reflection.Assembly failed");
        return hr;
    }

    mdTypeRef system_appdomain_type_ref = mdTypeRefNil;
    if (runtime_information_.is_desktop())
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Get a TypeRef for System.AppDomain
        hr = metadata_emit->DefineTypeRefByName(corlib_ref, WStr("System.AppDomain"), &system_appdomain_type_ref);
        if (FAILED(hr))
        {
            Logger::Warn("GenerateVoidILStartupMethod: DefineTypeRefByName::System.AppDomain failed");
            return hr;
        }
    }

    // ****************************************************************************************************************
    // Member Refs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for System.Threading.Interlocked::CompareExchange(int32&, int32, int32)
    mdMemberRef interlocked_compare_member_ref;
    COR_SIGNATURE interlocked_compare_exchange_signature[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT,
                                                              3,
                                                              ELEMENT_TYPE_I4,
                                                              ELEMENT_TYPE_BYREF,
                                                              ELEMENT_TYPE_I4,
                                                              ELEMENT_TYPE_I4,
                                                              ELEMENT_TYPE_I4};
    hr = metadata_emit->DefineMemberRef(
        interlocked_type_ref, WStr("CompareExchange"), interlocked_compare_exchange_signature,
        sizeof(interlocked_compare_exchange_signature), &interlocked_compare_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef::CompareExchange failed");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for Marshal.Copy");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for System.Reflection.Assembly.Load(byte[], byte[])
    mdMemberRef appdomain_load_member_ref;
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

    hr = metadata_emit->DefineMemberRef(system_reflection_assembly_type_ref, WStr("Load"), appdomain_load_signature,
                                        appdomain_load_signature_length, &appdomain_load_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for Assembly.Load");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for Assembly.CreateInstance(string)
    mdMemberRef assembly_create_instance_member_ref;
    COR_SIGNATURE assembly_create_instance_signature[] = {IMAGE_CEE_CS_CALLCONV_HASTHIS, 1,
                                                          ELEMENT_TYPE_OBJECT, // ret = System.Object
                                                          ELEMENT_TYPE_STRING};

    hr = metadata_emit->DefineMemberRef(system_reflection_assembly_type_ref, WStr("CreateInstance"),
                                        assembly_create_instance_signature, sizeof(assembly_create_instance_signature),
                                        &assembly_create_instance_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for Assembly.CreateInstance");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for Object.ToString()
    mdMemberRef object_to_string_member_ref;
    COR_SIGNATURE object_to_string_signature[] = { IMAGE_CEE_CS_CALLCONV_HASTHIS, 0,
                                                          ELEMENT_TYPE_STRING };

    hr = metadata_emit->DefineMemberRef(object_type_ref, WStr("ToString"),
        object_to_string_signature, sizeof(object_to_string_signature),
        &object_to_string_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for Object.ToString");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for string.ToCharArray()
    mdMemberRef string_to_char_array_member_ref;
    COR_SIGNATURE string_to_char_array_signature[] = { IMAGE_CEE_CS_CALLCONV_HASTHIS, 0,
                                                          ELEMENT_TYPE_SZARRAY, ELEMENT_TYPE_CHAR };

    hr = metadata_emit->DefineMemberRef(string_type_ref, WStr("ToCharArray"),
        string_to_char_array_signature, sizeof(string_to_char_array_signature),
        &string_to_char_array_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for String.ToCharArray");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for String.get_Length()
    mdMemberRef string_get_length_member_ref;
    COR_SIGNATURE string_get_length_signature[] = { IMAGE_CEE_CS_CALLCONV_HASTHIS, 0,
                                                          ELEMENT_TYPE_I4 };

    hr = metadata_emit->DefineMemberRef(string_type_ref, WStr("get_Length"),
        string_get_length_signature, sizeof(string_get_length_signature),
        &string_get_length_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for String.get_Length");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method signature for String.Concat()
    mdMemberRef string_concat_member_ref;
    COR_SIGNATURE string_concat_signature[] = { IMAGE_CEE_CS_CALLCONV_DEFAULT, 2,
                                                          ELEMENT_TYPE_STRING, ELEMENT_TYPE_STRING, ELEMENT_TYPE_STRING };

    hr = metadata_emit->DefineMemberRef(string_type_ref, WStr("Concat"),
        string_concat_signature, sizeof(string_concat_signature),
        &string_concat_member_ref);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for String.Concat");
        return hr;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create method member ref for AppDomain tokens
    mdMemberRef appdomain_get_currentdomain_member_ref = mdMemberRefNil;
    mdMemberRef appdomain_get_isfullytrusted_member_ref = mdMemberRefNil;
    mdMemberRef appdomain_get_ishomogenous_member_ref = mdMemberRefNil;
    if (system_appdomain_type_ref != mdTypeRefNil)
    {
        // Get a mdMemberRef for System.AppDomain.get_CurrentDomain()
        COR_SIGNATURE appdomain_get_currentdomain_signature_start[] = {IMAGE_CEE_CS_CALLCONV_DEFAULT, 0, ELEMENT_TYPE_CLASS};
        ULONG appdomain_get_currentdomain_signature_start_length = sizeof(appdomain_get_currentdomain_signature_start);

        BYTE system_appdomain_type_ref_compressed_token[4];
        ULONG system_appdomain_type_ref_compressed_token_length = CorSigCompressToken(system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

        const auto appdomain_get_currentdomain_signature_length = appdomain_get_currentdomain_signature_start_length + system_appdomain_type_ref_compressed_token_length;
        COR_SIGNATURE appdomain_get_currentdomain_signature[250];
        memcpy(appdomain_get_currentdomain_signature, appdomain_get_currentdomain_signature_start, appdomain_get_currentdomain_signature_start_length);
        memcpy(&appdomain_get_currentdomain_signature[appdomain_get_currentdomain_signature_start_length], system_appdomain_type_ref_compressed_token, system_appdomain_type_ref_compressed_token_length);

        hr = metadata_emit->DefineMemberRef(system_appdomain_type_ref, WStr("get_CurrentDomain"), appdomain_get_currentdomain_signature,
                                            appdomain_get_currentdomain_signature_length, &appdomain_get_currentdomain_member_ref);
        if (FAILED(hr))
        {
            Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
            return hr;
        }

        // Get a mdMemberRef for System.AppDomain.get_IsFullyTrusted()
        COR_SIGNATURE appdomain_get_isfullytrusted_signature[] = {IMAGE_CEE_CS_CALLCONV_HASTHIS, 0, ELEMENT_TYPE_BOOLEAN};
        hr = metadata_emit->DefineMemberRef(system_appdomain_type_ref, WStr("get_IsFullyTrusted"),
                                            appdomain_get_isfullytrusted_signature, sizeof(appdomain_get_isfullytrusted_signature),
                                            &appdomain_get_isfullytrusted_member_ref);
        if (FAILED(hr))
        {
            Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for get_IsFullyTrusted");
            return hr;
        }

        // Get a mdMemberRef for System.AppDomain.get_IsHomogenous()
        COR_SIGNATURE appdomain_get_ishomogenous_signature[] = {IMAGE_CEE_CS_CALLCONV_HASTHIS, 0,
                                                                  ELEMENT_TYPE_BOOLEAN};
        hr = metadata_emit->DefineMemberRef(
            system_appdomain_type_ref, WStr("get_IsHomogenous"), appdomain_get_ishomogenous_signature,
            sizeof(appdomain_get_ishomogenous_signature), &appdomain_get_ishomogenous_member_ref);

        if (FAILED(hr))
        {
            Logger::Warn("GenerateVoidILStartupMethod: DefineMemberRef failed for get_IsHomogenous");
            return hr;
        }
    }

    // ****************************************************************************************************************
    // String Defs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Create a string representing "An error occured in the managed loader: "

#ifdef _WIN32
    LPCWSTR error_str = L"An error occured in the managed loader: ";
    auto error_str_size = wcslen(error_str);
#else
    char16_t error_str[] = u"An error occured in the managed loader: ";
    auto error_str_size = std::char_traits<char16_t>::length(error_str);
#endif

    mdString error_token;
    hr = metadata_emit->DefineUserString(error_str, (ULONG)error_str_size, &error_token);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineUserString failed for the error message");
        return hr;
    }

    // ****************************************************************************************************************
    // Type Defs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Define a new TypeDef __DDVoidMethodType__ that extends System.Object
    mdTypeDef new_type_def;
    hr = metadata_emit->DefineTypeDef(WStr("__DDVoidMethodType__"), tdAbstract | tdSealed, object_type_ref, nullptr,
                                      &new_type_def);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: DefineTypeDef failed");
        return hr;
    }

    // ****************************************************************************************************************
    // Field Defs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

    // ****************************************************************************************************************
    // Method Defs
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

    // ****************************************************************************************************************
    // Locals signature
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Generate a locals signature defined in the following way:
    //   [0] System.IntPtr ("assemblyPtr" - address of assembly bytes)
    //   [1] System.Int32  ("assemblySize" - size of assembly bytes)
    //   [2] System.IntPtr ("symbolsPtr" - address of symbols bytes)
    //   [3] System.Int32  ("symbolsSize" - size of symbols bytes)
    //   [4] System.Byte[] ("assemblyBytes" - managed byte array for assembly)
    //   [5] System.Byte[] ("symbolsBytes" - managed byte array for symbols)
    //   [6] System.String (exception message)
    //   [7] char*         (pointer to the exception message)
    //   [8] char[] pinned (pinned char array for the exception message)
    mdSignature locals_signature_token;
    COR_SIGNATURE locals_signature[] = {
        IMAGE_CEE_CS_CALLCONV_LOCAL_SIG, // Calling convention
        9,                               // Number of variables
        ELEMENT_TYPE_I,                  // List of variable types
        ELEMENT_TYPE_I4,
        ELEMENT_TYPE_I,
        ELEMENT_TYPE_I4,
        ELEMENT_TYPE_SZARRAY,
        ELEMENT_TYPE_U1,
        ELEMENT_TYPE_SZARRAY,
        ELEMENT_TYPE_U1,
        ELEMENT_TYPE_STRING,
        ELEMENT_TYPE_PTR,
        ELEMENT_TYPE_CHAR,
        ELEMENT_TYPE_PINNED,
        ELEMENT_TYPE_SZARRAY,
        ELEMENT_TYPE_CHAR,
    };
    hr = metadata_emit->GetTokenFromSig(locals_signature, sizeof(locals_signature), &locals_signature_token);
    if (FAILED(hr))
    {
        Logger::Warn("GenerateVoidILStartupMethod: Unable to generate locals signature. ModuleID=", module_id);
        return hr;
    }

    // ****************************************************************************************************************
    // Standalone signature
    // ****************************************************************************************************************

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Generate the signature used to call the native logging method
    COR_SIGNATURE log_signature[] = {
        IMAGE_CEE_CS_CALLCONV_STDCALL, // Calling convention
        3,                             // Number of parameters
        ELEMENT_TYPE_VOID,             // Return type
        ELEMENT_TYPE_I4,               // First parameter type
        ELEMENT_TYPE_I,                // Second parameter type
        ELEMENT_TYPE_I4,               // Second parameter type
    };

    mdSignature log_signature_token;
    metadata_emit->GetTokenFromSig(log_signature, sizeof(log_signature), &log_signature_token);

    // ****************************************************************************************************************
    // IL instructions
    // ****************************************************************************************************************

    /////////////////////////////////////////////
    // Add IL instructions into the void method
    ILRewriter rewriter_void(this->info_, nullptr, module_id, *ret_method_token);
    rewriter_void.InitializeTiny();
    rewriter_void.SetTkLocalVarSig(locals_signature_token);
    ILRewriterWrapper rewriterWrapper_void(&rewriter_void);
    rewriterWrapper_void.SetILPosition(rewriter_void.GetILList()->m_pNext);

    auto returnInstr = rewriter_void.NewILInstr();
    returnInstr->m_opcode = CEE_RET;

    // Step 0) Check if the assembly was already loaded

    // ldsflda _isAssemblyLoaded : Load the address of the "_isAssemblyLoaded" static var
    auto first_instruction = rewriterWrapper_void.LoadFieldAddress(isAssemblyLoadedFieldToken, true);
    // ldc.i4.1 : Load the constant 1 (int) to the stack
    rewriterWrapper_void.LoadInt32(1);
    // ldc.i4.0 : Load the constant 0 (int) to the stack
    rewriterWrapper_void.LoadInt32(0);
    // call int Interlocked.CompareExchange(ref int, int, int) method
    rewriterWrapper_void.CallMember(interlocked_compare_member_ref, false);
    // ldc.i4.1 : Load the constant 1 (int) to the stack
    rewriterWrapper_void.LoadInt32(1);
    // ceq : Compare equality from two values from the stack
    rewriterWrapper_void.CreateInstr(CEE_CEQ);
    // check if the return of the method call is true or false
    ILInstr* pIsNotAlreadyLoadedBranch = rewriterWrapper_void.CreateInstr(CEE_BRFALSE_S);
    // return if IsAlreadyLoaded is true
    rewriterWrapper_void.CreateInstr(CEE_LEAVE_S, returnInstr);

    ILInstr* pIsFullyTrustedBranch = nullptr;
    ILInstr* pIsHomogenousBranch = nullptr;

    if (appdomain_get_currentdomain_member_ref != mdMemberRefNil
        && appdomain_get_isfullytrusted_member_ref != mdMemberRefNil
        && appdomain_get_ishomogenous_member_ref != mdMemberRefNil)
    {
        // Step 1) Check if the assembly is loaded in a fully trusted domain.

        // call System.AppDomain.get_CurrentDomain() and set the false branch target for IsAlreadyLoaded()
        pIsNotAlreadyLoadedBranch->m_pTarget = rewriterWrapper_void.CallMember(appdomain_get_currentdomain_member_ref, false);

        // callvirt System.AppDomain.get_Homogenous()
        rewriterWrapper_void.CallMember(appdomain_get_ishomogenous_member_ref, true);

        // check if the return of the method call is true or false
        pIsHomogenousBranch = rewriterWrapper_void.CreateInstr(CEE_BRTRUE_S);
        // return if IsHomogenous is false
        rewriterWrapper_void.CreateInstr(CEE_LEAVE_S, returnInstr);

        // call System.AppDomain.get_CurrentDomain()
        pIsHomogenousBranch->m_pTarget = rewriterWrapper_void.CallMember(appdomain_get_currentdomain_member_ref, false);

        // callvirt System.AppDomain.get_IsFullyTrusted()
        rewriterWrapper_void.CallMember(appdomain_get_isfullytrusted_member_ref, true);
        // check if the return of the method call is true or false
        pIsFullyTrustedBranch = rewriterWrapper_void.CreateInstr(CEE_BRTRUE_S);
        // return if IsFullyTrusted is false
        rewriterWrapper_void.CreateInstr(CEE_LEAVE_S, returnInstr);
    }

    // Step 2) Call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr,
    // out int symbolsSize)

    // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index 0)
    if (pIsFullyTrustedBranch != nullptr)
    {
        // Set the true branch target for AppDomain.CurrentDomain.IsFullyTrusted
        pIsFullyTrustedBranch->m_pTarget = rewriterWrapper_void.LoadLocalAddress(0);
    }
    else
    {
        // Set the false branch target for IsAlreadyLoaded()
        pIsNotAlreadyLoadedBranch->m_pTarget = rewriterWrapper_void.LoadLocalAddress(0);
    }

    // ldloca.s 1 : Load the address of the "assemblySize" variable (locals index 1)
    rewriterWrapper_void.LoadLocalAddress(1);
    // ldloca.s 2 : Load the address of the "symbolsPtr" variable (locals index 2)
    rewriterWrapper_void.LoadLocalAddress(2);
    // ldloca.s 3 : Load the address of the "symbolsSize" variable (locals index 3)
    rewriterWrapper_void.LoadLocalAddress(3);
    // call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize)
    rewriterWrapper_void.CallMember(pinvoke_method_def, false);

    // Step 3) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the
    // managed assembly bytes

    // ldloc.1 : Load the "assemblySize" variable (locals index 1)
    rewriterWrapper_void.LoadLocal(1);
    // newarr System.Byte : Create a new Byte[] to hold a managed copy of the assembly data
    rewriterWrapper_void.CreateInstr(CEE_NEWARR)->m_Arg32 = byte_type_ref;
    // stloc.s 4 : Assign the Byte[] to the "assemblyBytes" variable (locals index 4)
    rewriterWrapper_void.StLocal(4);

    // ldloc.0 : Load the "assemblyPtr" variable (locals index 0)
    rewriterWrapper_void.LoadLocal(0);
    // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4)
    rewriterWrapper_void.LoadLocal(4);
    // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
    rewriterWrapper_void.LoadInt32(0);
    // ldloc.1 : Load the "assemblySize" variable (locals index 1) for the Marshal.Copy length parameter
    rewriterWrapper_void.LoadLocal(1);
    // call Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
    rewriterWrapper_void.CallMember(marshal_copy_member_ref, false);

    // Step 4) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the
    // symbols bytes

    // ldloc.3 : Load the "symbolsSize" variable (locals index 3)
    rewriterWrapper_void.LoadLocal(3);
    // newarr System.Byte : Create a new Byte[] to hold a managed copy of the symbols data
    rewriterWrapper_void.CreateInstr(CEE_NEWARR)->m_Arg32 = byte_type_ref;
    // stloc.s 5 : Assign the Byte[] to the "symbolsBytes" variable (locals index 5)
    rewriterWrapper_void.StLocal(5);

    // ldloc.2 : Load the "symbolsPtr" variables (locals index 2)
    rewriterWrapper_void.LoadLocal(2);
    // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5)
    rewriterWrapper_void.LoadLocal(5);
    // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
    rewriterWrapper_void.LoadInt32(0);
    // ldloc.3 : Load the "symbolsSize" variable (locals index 3) for the Marshal.Copy length parameter
    rewriterWrapper_void.LoadLocal(3);
    // call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
    rewriterWrapper_void.CallMember(marshal_copy_member_ref, false);

    // Step 5) Call System.Reflection.Assembly System.Reflection.Assembly.Load(byte[], byte[]))

    // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the first byte[] parameter of
    // AppDomain.Load(byte[], byte[])
    rewriterWrapper_void.LoadLocal(4);
    // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the second byte[] parameter of
    // AppDomain.Load(byte[], byte[])
    rewriterWrapper_void.LoadLocal(5);
    // call System.Reflection.Assembly System.Reflection.Assembly.Load(uint8[], uint8[])
    rewriterWrapper_void.CallMember(appdomain_load_member_ref, false);

    // Step 6) Call instance method Assembly.CreateInstance("Datadog.Trace.ClrProfiler.Managed.Loader.Startup")

    // ldstr "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"
    rewriterWrapper_void.LoadStr(load_helper_token);
    // callvirt System.Object System.Reflection.Assembly.CreateInstance(string)
    rewriterWrapper_void.CallMember(assembly_create_instance_member_ref, true);
    // pop the returned object
    rewriterWrapper_void.Pop();
    // return
    rewriterWrapper_void.CreateInstr(CEE_LEAVE_S, returnInstr);

    // Step 7) Catch block
    // catch (Exception ex)
    // {
    //      var message = "An error occured in the managed loader: " + ex.ToString();
    //      var chars = message.ToCharArray();
    // 
    //      fixed (char* p = chars)
    //      {
    //          var nativeLog = (delegate* unmanaged<int, IntPtr, int, void>)0xFFFFFFFF; // Replaced with the actual address
    //          nativeLog(3, p, chars.Length);
    //      }
    // }
    auto catchBegin = rewriterWrapper_void.CallMember(object_to_string_member_ref, true);
    rewriterWrapper_void.StLocal(6);
    rewriterWrapper_void.LoadStr(error_token);
    rewriterWrapper_void.LoadLocal(6);
    rewriterWrapper_void.CallMember(string_concat_member_ref, false);
    rewriterWrapper_void.StLocal(6);
    rewriterWrapper_void.LoadLocal(6);
    rewriterWrapper_void.CallMember(string_to_char_array_member_ref, true);
    rewriterWrapper_void.StLocal(8);
    rewriterWrapper_void.LoadLocal(8);
    rewriterWrapper_void.CreateInstr(CEE_LDC_I4_0);
    rewriterWrapper_void.CreateInstr(CEE_LDELEMA, char_type_ref);
    rewriterWrapper_void.CreateInstr(CEE_CONV_U);
    rewriterWrapper_void.StLocal(7);
    rewriterWrapper_void.CreateInstr(CEE_LDC_I4_3);
    rewriterWrapper_void.LoadLocal(7);
    rewriterWrapper_void.LoadLocal(6);
    rewriterWrapper_void.CallMember(string_get_length_member_ref, true);
    rewriterWrapper_void.LoadInt64((INT64)&NativeLog);
    rewriterWrapper_void.CreateInstr(CEE_CONV_I);
    rewriterWrapper_void.CreateInstr(CEE_CALLI, log_signature_token);
    rewriterWrapper_void.CreateInstr(CEE_LDNULL);
    rewriterWrapper_void.StLocal(8);
    auto catchEnd = rewriterWrapper_void.CreateInstr(CEE_LEAVE_S, returnInstr);

    rewriterWrapper_void.GetILRewriter()->InsertAfter(catchEnd, returnInstr);

    auto catchClause = new EHClause();
    catchClause->m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    catchClause->m_pTryBegin = first_instruction;
    catchClause->m_pTryEnd = catchBegin;
    catchClause->m_pHandlerBegin = catchBegin;
    catchClause->m_pHandlerEnd = catchEnd;
    catchClause->m_ClassToken = exception_type_ref;

    rewriter_void.SetEHClause(catchClause, 1);

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
                                                    2,                             // Number of parameters
                                                    ELEMENT_TYPE_VOID,             // Return type
                                                    ELEMENT_TYPE_STRING,           // List of parameter types
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
    ILInstr* pCurrentInstr = nullptr;
    ILInstr* pNewInstr = nullptr;

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
                                             int* symbolsSize) 
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

HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                          ICorProfilerFunctionControl* pFunctionControl)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    Logger::Debug("GetReJITParameters: [moduleId: ", moduleId, ", methodId: ", Hex(methodId), "]");

    // we notify the reJIT handler of this event and pass the module_metadata.
    return rejit_handler->NotifyReJITParameters(moduleId, methodId, pFunctionControl);
}

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                                  HRESULT hrStatus)
{
    Logger::Warn("ReJITError: [functionId: ", functionId, ", moduleId: ", moduleId, ", methodId: ", methodId,
                 ", hrStatus: ", hrStatus, "]");

    if (!is_attached_)
    {
        return S_OK;
    }

    if (debugger_instrumentation_requester != nullptr)
    {
        return debugger_instrumentation_requester->NotifyReJITError(moduleId, methodId, functionId, hrStatus);
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction)
{
    if (!is_attached_)
    {
        return S_OK;
    }

    auto _ = trace::Stats::Instance()->JITCachedFunctionSearchStartedMeasure();
    if (!pbUseCachedFunction)
    {
        return S_OK;
    }

    // keep this lock until we are done using the module,
    // to prevent it from unloading while in use
    auto modulesOpt = module_ids.TryGet();

    if (!modulesOpt.has_value())
    {
        Logger::Error(
            "JITCachedFunctionSearchStarted: Failed on exception while tried to acquire the lock for the module_ids collection for functionId ",
            functionId);
        return S_OK;
    }

    auto& modules = modulesOpt.value();

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

    // Check for Dataflow call site instrumentation
    if (_dataflow != nullptr && !_dataflow->IsInlineEnabled(module_id, function_token))
    {
        // The function has been instrumented by Dataflow
        // so we reject the NGEN image
        *pbUseCachedFunction = false;
        return S_OK;
    }

    // Verify that we have the metadata for this module
    if (!shared::Contains(modules.Ref(), module_id))
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
        // The loader is missing in this AppDomain, we skip the NGEN image to allow the JITCompilationStart inject
        // it.
        *pbUseCachedFunction = false;
        return S_OK;
    }

    *pbUseCachedFunction = true;
    return S_OK;
}

} // namespace trace
