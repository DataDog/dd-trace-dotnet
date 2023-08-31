#include "fault_tolerant_tracker.h"

void fault_tolerant::FaultTolerantTracker::RequestRevert(ModuleID moduleId, mdMethodDef methodId, std::shared_ptr<RejitHandler> rejit_handler)
{
    std::vector<MethodIdentifier> requests = {{moduleId, methodId}};
    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    rejit_handler->EnqueueRequestRevert(requests, promise);
    future.get();
}

void fault_tolerant::FaultTolerantTracker::RequestRejit(ModuleID moduleId, mdMethodDef methodId, std::shared_ptr<RejitHandler> rejit_handler)
{
    std::vector<MethodIdentifier> requests = {{moduleId, methodId}};
    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    rejit_handler->EnqueueRequestRejit(requests, promise);
    future.get();
}

void fault_tolerant::FaultTolerantTracker::AddFaultTolerant(ModuleID fromModuleId, mdMethodDef fromMethodId,
                                                            mdMethodDef toOriginalMethodId,
                                                            mdMethodDef toInstrumentedMethodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto lookupMethodIdentifier = trace::MethodIdentifier(fromModuleId, fromMethodId);
    const auto mappedOriginalMethodIdentifier = trace::MethodIdentifier(fromModuleId, toOriginalMethodId);
    const auto mappedInstrumentedMethodIdentifier = trace::MethodIdentifier(fromModuleId, toInstrumentedMethodId);

    _originalMethods[mappedOriginalMethodIdentifier] = lookupMethodIdentifier;
    _instrumentedMethods[mappedInstrumentedMethodIdentifier] = lookupMethodIdentifier;

    _faultTolerantMethods[lookupMethodIdentifier] =
        std::tuple(mappedOriginalMethodIdentifier, mappedInstrumentedMethodIdentifier);
}

mdMethodDef fault_tolerant::FaultTolerantTracker::GetOriginalMethod(ModuleID moduleId,
                                                                                  mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    const auto iter = _faultTolerantMethods.find(methodIdentifier);
    auto [original, instrumented] =  _faultTolerantMethods[methodIdentifier];
    return original.methodToken;
}

mdMethodDef fault_tolerant::FaultTolerantTracker::GetInstrumentedMethod(ModuleID moduleId, mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    auto [original, instrumented] = _faultTolerantMethods[methodIdentifier];
    return instrumented.methodToken;
}

mdMethodDef fault_tolerant::FaultTolerantTracker::GetKickoffMethodFromOriginalMethod(ModuleID moduleId,
    mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    return _originalMethods[methodIdentifier].methodToken;
}

mdMethodDef fault_tolerant::FaultTolerantTracker::GetKickoffMethodFromInstrumentedMethod(ModuleID moduleId,
    mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    return _instrumentedMethods[methodIdentifier].methodToken;
}

bool fault_tolerant::FaultTolerantTracker::IsKickoffMethod(ModuleID moduleId, mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    const auto iter = _faultTolerantMethods.find(methodIdentifier);
    return iter != _faultTolerantMethods.end();
}

bool fault_tolerant::FaultTolerantTracker::IsOriginalMethod(ModuleID moduleId, mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    const auto iter = _originalMethods.find(methodIdentifier);
    return iter != _originalMethods.end();
}

bool fault_tolerant::FaultTolerantTracker::IsInstrumentedMethod(ModuleID moduleId, mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    const auto iter = _instrumentedMethods.find(methodIdentifier);
    return iter != _instrumentedMethods.end();
}

void fault_tolerant::FaultTolerantTracker::CacheILBodyIfEmpty(ModuleID moduleId, mdMethodDef methodId,
    LPCBYTE pMethodBytes, ULONG methodSize)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);

    if (_methodBodies.find(methodIdentifier) == _methodBodies.end())
    {
        // Entry does not exist, insert it
        _methodBodies[methodIdentifier] = std::tuple(pMethodBytes, methodSize);
    }
}

std::tuple<LPCBYTE, ULONG> fault_tolerant::FaultTolerantTracker::GetILBodyAndSize(ModuleID moduleId,
    mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    return _methodBodies[methodIdentifier];
}

void fault_tolerant::FaultTolerantTracker::AddSuccessfulInstrumentationVersion(
    ModuleID moduleId, mdMethodDef methodId, const shared::WSTRING& instrumentationVersion,
    trace::InstrumentingProducts products, std::shared_ptr<RejitHandler> rejit_handler)
{
    std::lock_guard lock(_successfulInstrumentationVersionsMutex);

    RequestRevert(moduleId, methodId, rejit_handler);
    RequestRejit(moduleId, methodId, rejit_handler);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);

    auto [iter, _] = _successfulInstrumentationVersions.emplace(methodIdentifier, std::set<shared::WSTRING>());
    iter->second.insert(instrumentationVersion);
}

bool fault_tolerant::FaultTolerantTracker::IsInstrumentationVersionSucceeded(
    ModuleID moduleId, mdMethodDef methodId, const shared::WSTRING& instrumentationVersion,
    trace::InstrumentingProducts products)
{
    std::lock_guard lock(_successfulInstrumentationVersionsMutex);
    
    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);

    auto it = _successfulInstrumentationVersions.find(methodIdentifier);

    if (it != _successfulInstrumentationVersions.end())
    {
        return it->second.find(instrumentationVersion) != it->second.end();
    }

    return false;
}

bool fault_tolerant::FaultTolerantTracker::ShouldHeal(ModuleID moduleId, mdMethodDef methodId,
                                                      const shared::WSTRING& instrumentationVersion,
                                                      trace::InstrumentingProducts products,
                                                      std::shared_ptr<RejitHandler> rejit_handler)
{
    const auto shouldHeal = !IsInstrumentationVersionSucceeded(moduleId, methodId, instrumentationVersion, products);

    if (shouldHeal)
    {
        RequestRevert(moduleId, methodId, rejit_handler);
    }

    return shouldHeal;
}
