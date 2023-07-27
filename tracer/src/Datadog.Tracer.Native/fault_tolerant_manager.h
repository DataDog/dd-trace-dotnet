#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_MANAGER_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_MANAGER_H_

#include "clr_helpers.h"
#include "cor_profiler.h"
#include "corhlpr.h"

#include <corprof.h>

namespace fault_tolerant
{
class FaultTolerantManager
{
private:
    CorProfiler* m_corProfiler;
    std::shared_ptr<RejitHandler> m_rejit_handler = nullptr;
    std::shared_ptr<RejitWorkOffloader> m_work_offloader = nullptr;

    bool is_fault_tolerant_instrumentation_enabled = false;

public:
    FaultTolerantManager(CorProfiler* corProfiler, std::shared_ptr<trace::RejitHandler> rejit_handler,
                         std::shared_ptr<trace::RejitWorkOffloader> work_offloader);

    void Apply(const ModuleID moduleId, const trace::ModuleInfo& moduleInfo, ComPtr<IMetaDataImport2> metadataImport,
               ComPtr<IMetaDataEmit2> metadataEmit, mdTypeDef typeDef) const;
};

} // namespace fault_tolerant

#endif