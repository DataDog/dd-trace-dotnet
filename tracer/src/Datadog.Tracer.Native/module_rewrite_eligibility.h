#ifndef DD_CLR_PROFILER_MODULE_REWRITE_ELIGIBILITY_H_
#define DD_CLR_PROFILER_MODULE_REWRITE_ELIGIBILITY_H_

#include "clr_helpers.h"
#include "dd_profiler_constants.h"

namespace trace
{

inline bool IsAssemblyExplicitlyIncluded(const shared::WSTRING& assemblyName)
{
    for (auto&& include_assembly : include_assemblies)
    {
        if (assemblyName == include_assembly)
        {
            return true;
        }
    }

    return false;
}

inline bool IsSkippedAssemblyName(const shared::WSTRING& assemblyName)
{
    for (auto&& skip_assembly : skip_assemblies)
    {
        if (assemblyName == skip_assembly)
        {
            return true;
        }
    }

    for (auto&& skip_assembly_pattern : skip_assembly_prefixes)
    {
        if (assemblyName.rfind(skip_assembly_pattern, 0) == 0 && !IsAssemblyExplicitlyIncluded(assemblyName))
        {
            return true;
        }
    }

    return false;
}

// Modules we never rewrite and must not take module_ids for (APMS-20456).
inline bool ShouldSkipModuleIdsLockForNonRewriteModule(const ModuleInfo& moduleInfo)
{
    return !moduleInfo.IsValid() || moduleInfo.IsDynamic() || moduleInfo.IsResource() || moduleInfo.IsWindowsRuntime();
}

inline bool IsModuleLoadSpecialCase(const shared::WSTRING& assemblyName)
{
    return assemblyName == mscorlib_assemblyName || assemblyName == system_private_corelib_assemblyName ||
           assemblyName == datadog_trace_clrprofiler_managed_loader_assemblyName ||
           assemblyName == managed_profiler_name || assemblyName == manual_instrumentation_name;
}

// ModuleLoadFinished still has to run for corlib/loader/Datadog.Trace setup, even when
// the assembly would otherwise be skipped for CallTarget rewrite.
inline bool ShouldSkipModuleIdsLockForModuleLoad(const ModuleInfo& moduleInfo)
{
    if (ShouldSkipModuleIdsLockForNonRewriteModule(moduleInfo))
    {
        return true;
    }

    if (IsModuleLoadSpecialCase(moduleInfo.assembly.name))
    {
        return false;
    }

    return IsSkippedAssemblyName(moduleInfo.assembly.name);
}

// Skipped NGEN modules can still participate in inliner tracking from JITCachedFunctionSearchStarted.
inline bool ShouldRegisterModuleLifetime(const ModuleInfo& moduleInfo)
{
    return !ShouldSkipModuleIdsLockForModuleLoad(moduleInfo) || (moduleInfo.IsValid() && moduleInfo.IsNGEN());
}

// JITCompilationStarted only needs module_ids when we may rewrite this module.
inline bool ShouldSkipModuleIdsLockForJit(const ModuleInfo& moduleInfo)
{
    if (ShouldSkipModuleIdsLockForNonRewriteModule(moduleInfo))
    {
        return true;
    }

    const auto& assemblyName = moduleInfo.assembly.name;
    if (assemblyName == datadog_trace_clrprofiler_managed_loader_assemblyName ||
        assemblyName == mscorlib_assemblyName || assemblyName == system_private_corelib_assemblyName)
    {
        return true;
    }

    return IsSkippedAssemblyName(assemblyName);
}

} // namespace trace

#endif
