// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for WINDOWS

#include "resource.h"

#include "OsSpecificApi.h"

#include "StackFramesCollectorBase.h"
#include "Windows32BitStackFramesCollector.h"
#include "Windows64BitStackFramesCollector.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {
void InitializeLoaderResourceMonikerIDs(shared::LoaderResourceMonikerIDs* loaderResourceMonikerIDs)
{
    if (loaderResourceMonikerIDs == nullptr)
    {
        return;
    }

    loaderResourceMonikerIDs->Net45_Datadog_AutoInstrumentation_ManagedLoader_dll = NET45_Datadog_AutoInstrumentation_ManagedLoader_dll;
    loaderResourceMonikerIDs->NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll = NETCOREAPP20_Datadog_AutoInstrumentation_ManagedLoader_dll;
    loaderResourceMonikerIDs->Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb = NET45_Datadog_AutoInstrumentation_ManagedLoader_pdb;
    loaderResourceMonikerIDs->NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb = NETCOREAPP20_Datadog_AutoInstrumentation_ManagedLoader_pdb;
}

StackFramesCollectorBase* CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
#ifdef BIT64
    static_assert(8 * sizeof(void*) == 64);
    return new Windows64BitStackFramesCollector(pCorProfilerInfo);
#else
    assert(8 * sizeof(void*) == 32);
    return new Windows32BitStackFramesCollector(pCorProfilerInfo);
#endif
}
} // namespace OsSpecificApi