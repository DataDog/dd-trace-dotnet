#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_TRACKER_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_TRACKER_H_

#include "../../../shared/src/native-src/util.h"
#include "corhlpr.h"
#include "instrumenting_product.h"
#include "integration.h"
#include "rejit_handler.h"

#include <corprof.h>
#include <mutex>
#include <unordered_map>

namespace fault_tolerant
{
class FaultTolerantTracker : public shared::Singleton<FaultTolerantTracker>
{
    friend class shared::Singleton<FaultTolerantTracker>;

private:
    std::recursive_mutex _faultTolerantMapMutex;
    std::unordered_map<trace::MethodIdentifier, std::tuple<trace::MethodIdentifier, trace::MethodIdentifier>> _faultTolerantMethods{};
    std::unordered_map < trace::MethodIdentifier, std::tuple<LPCBYTE, ULONG>> _methodBodies{};
    std::unordered_map<trace::MethodIdentifier, trace::MethodIdentifier> _originalMethods;
    std::unordered_map<trace::MethodIdentifier, trace::MethodIdentifier> _instrumentedMethods;
    std::unordered_map<trace::MethodIdentifier, std::set<shared::WSTRING>> _successfulInstrumentationIds;
    std::recursive_mutex _successfulInstrumentationIdsMutex;

    void RequestRejit(ModuleID moduleId, mdMethodDef methodId, std::shared_ptr<RejitHandler> rejit_handler);

public:
    FaultTolerantTracker() = default;
    
    void AddFaultTolerant(ModuleID fromModuleId, mdMethodDef fromMethodId, mdMethodDef toOriginalMethodId,
                          mdMethodDef toInstrumentedMethodId);
    mdMethodDef GetOriginalMethod(ModuleID moduleId, mdMethodDef methodId);
    mdMethodDef GetInstrumentedMethod(ModuleID moduleId, mdMethodDef methodId);
    mdMethodDef GetKickoffMethodFromOriginalMethod(ModuleID moduleId, mdMethodDef methodId);
    mdMethodDef GetKickoffMethodFromInstrumentedMethod(ModuleID moduleId, mdMethodDef methodId);

    bool IsKickoffMethod(ModuleID moduleId, mdMethodDef methodId);
    bool IsOriginalMethod(ModuleID moduleId, mdMethodDef methodId);
    bool IsInstrumentedMethod(ModuleID moduleId, mdMethodDef methodId);
    void CacheILBodyIfEmpty(ModuleID moduleId, mdMethodDef methodId, LPCBYTE pMethodBytes, ULONG methodSize);
    std::tuple<LPCBYTE, ULONG> GetILBodyAndSize(ModuleID moduleId, mdMethodDef methodId);

    void AddSuccessfulInstrumentationId(ModuleID moduleId, mdMethodDef methodId,
                                             const shared::WSTRING& instrumentationId,
                                             trace::InstrumentingProducts products,
                                             std::shared_ptr<RejitHandler> rejit_handler);
    bool IsInstrumentationIdSucceeded(ModuleID moduleId, mdMethodDef methodId,
                                            const shared::WSTRING& instrumentationId,
                                            trace::InstrumentingProducts products);
    bool ShouldHeal(ModuleID moduleId, mdMethodDef methodId, const shared::WSTRING& instrumentationId, trace::InstrumentingProducts products, std::shared_ptr<RejitHandler> rejit_handler);
};

} // namespace fault_tolerant

#endif