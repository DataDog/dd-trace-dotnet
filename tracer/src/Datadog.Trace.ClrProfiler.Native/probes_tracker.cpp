#include "probes_tracker.h"

 bool debugger::ProbesTracker::TryGetMethods(const shared::WSTRING& probeId, std::set<trace::MethodIdentifier>& methods)
 {
     std::lock_guard lock(_probeIdMapMutex);

     if (!_probeIdMap.empty())
     {
         const auto iter = _probeIdMap.find(probeId);

         if (iter != std::end(_probeIdMap))
         {
             methods = iter->second;
             return true;
         }
     }

     return false;
 }

 void debugger::ProbesTracker::AddProbe(const shared::WSTRING& probeId, const ModuleID moduleId,
                                       const mdMethodDef methodId)
{
     std::lock_guard lock(_probeIdMapMutex);

     const auto methodIdentifierToAdd = trace::MethodIdentifier(moduleId, methodId);

     if (_probeIdMap.find(probeId) == _probeIdMap.end())
     {
         auto methods = std::set<trace::MethodIdentifier>();
         methods.emplace(methodIdentifierToAdd);
         _probeIdMap[probeId] = std::move(methods);
     }
     else
     {
         _probeIdMap[probeId].emplace(methodIdentifierToAdd);
     }
 }

std::set<trace::MethodIdentifier> debugger::ProbesTracker::RemoveProbes(const std::vector<shared::WSTRING>& probes)
{
    std::set<trace::MethodIdentifier> probesIdentifiers;

    std::lock_guard lock(_probeIdMapMutex);

    if (!_probeIdMap.empty())
    {
        for (const auto& probe : probes)
        {
            const auto iter = _probeIdMap.find(probe);

            if (iter != _probeIdMap.end())
            {
                probesIdentifiers.insert(iter->second.begin(), iter->second.end());
                _probeIdMap.erase(iter);
            }
        }
    }

    return probesIdentifiers;
}
