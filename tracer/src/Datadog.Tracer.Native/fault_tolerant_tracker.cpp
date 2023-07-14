#include "fault_tolerant_tracker.h"

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

void fault_tolerant::FaultTolerantTracker::KeepILBodyAndSize(ModuleID moduleId, mdMethodDef methodId,
    LPCBYTE pMethodBytes, ULONG methodSize)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    _methodBodies[methodIdentifier] = std::tuple(pMethodBytes, methodSize);
}

std::tuple<LPCBYTE, ULONG> fault_tolerant::FaultTolerantTracker::GetILBodyAndSize(ModuleID moduleId,
    mdMethodDef methodId)
{
    std::lock_guard lock(_faultTolerantMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);
    return _methodBodies[methodIdentifier];
}
