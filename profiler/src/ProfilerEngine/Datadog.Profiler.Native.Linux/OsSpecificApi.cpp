// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for LINUX

#include "OsSpecificApi.h"

#include "LinuxStackFramesCollector.h"
#include "StackFramesCollectorBase.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {
void InitializeLoaderResourceMonikerIDs(shared::LoaderResourceMonikerIDs*)
{
}

StackFramesCollectorBase* CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
    return new LinuxStackFramesCollector(const_cast<ICorProfilerInfo4* const>(pCorProfilerInfo));
}
} // namespace OsSpecificApi