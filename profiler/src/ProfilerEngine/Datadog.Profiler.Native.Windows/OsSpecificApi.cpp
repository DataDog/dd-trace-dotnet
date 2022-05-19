// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for WINDOWS

#include "OsSpecificApi.h"

#include "StackFramesCollectorBase.h"
#include "Windows32BitStackFramesCollector.h"
#include "Windows64BitStackFramesCollector.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {

std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
#ifdef BIT64
    static_assert(8 * sizeof(void*) == 64);
    return std::make_unique<Windows64BitStackFramesCollector>(pCorProfilerInfo);
#else
    assert(8 * sizeof(void*) == 32);
    return std::make_unique<Windows32BitStackFramesCollector>(pCorProfilerInfo);
#endif
}
} // namespace OsSpecificApi