#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_METHOD_DUPLICATOR_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_METHOD_DUPLICATOR_H_

#include "clr_helpers.h"
#include "cor_profiler.h"
#include "corhlpr.h"

#include <corprof.h>

namespace fault_tolerant
{
class FaultTolerantMethodDuplicator : public shared::Singleton<FaultTolerantMethodDuplicator>
{
    friend class shared::Singleton<FaultTolerantMethodDuplicator>;

private:
    bool is_fault_tolerant_instrumentation_enabled = false;

public:
    FaultTolerantMethodDuplicator() = default;

    static void DuplicateOne(ModuleID moduleId, const trace::ModuleInfo& moduleInfo, ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit,
                             mdTypeDef typeDef, mdMethodDef methodDef, ICorProfilerInfo10* profilerInfo,
                             bool shouldCallApplyMetadata = true);

    static void DuplicateAll(const ModuleID moduleId, const trace::ModuleInfo& moduleInfo,
                             ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit, ICorProfilerInfo10* profilerInfo);
};

} // namespace fault_tolerant

#endif