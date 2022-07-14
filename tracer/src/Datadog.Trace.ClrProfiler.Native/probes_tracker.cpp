#include "probes_tracker.h"

 bool debugger::ProbesMetadataTracker::TryGetMetadata(const shared::WSTRING& probeId, /* out */ std::shared_ptr<ProbeMetadata>& probeMetadata)
 {
     std::lock_guard lock(_probeMetadataMapMutex);

     if (!_probeMetadataMap.empty())
     {
         const auto iter = _probeMetadataMap.find(probeId);

         if (iter != _probeMetadataMap.end())
         {
             probeMetadata = iter->second;
             return true;
         }
     }

     return false;
 }

 std::set<WSTRING> debugger::ProbesMetadataTracker::GetProbeIds(const ModuleID moduleId, const mdMethodDef methodId)
 {
     std::lock_guard lock(_probeMetadataMapMutex);

     const auto lookupMethodIdentifier = trace::MethodIdentifier(moduleId, methodId);
     std::set<WSTRING> probeIds;

     auto probeMetadataMapIterator = _probeMetadataMap.begin();
     while (probeMetadataMapIterator != _probeMetadataMap.end())
     {
         auto probeMetadataPair = *probeMetadataMapIterator;
         const auto methods = probeMetadataPair.second->methods;

         if (methods.find(lookupMethodIdentifier) != methods.end())
         {
             probeIds.emplace(probeMetadataPair.first);
         }

         probeMetadataMapIterator = ++probeMetadataMapIterator;
     }

     return probeIds;
 }

 void debugger::ProbesMetadataTracker::CreateNewProbeIfNotExists(const shared::WSTRING& probeId)
 {
     std::lock_guard lock(_probeMetadataMapMutex);

     if (_probeMetadataMap.find(probeId) == _probeMetadataMap.end())
     {
         auto methods = std::set<trace::MethodIdentifier>();
         _probeMetadataMap[probeId] = std::make_shared<ProbeMetadata>(probeId, std::move(methods), ProbeStatus::RECEIVED);
     }
 }

 void debugger::ProbesMetadataTracker::AddMethodToProbe(const shared::WSTRING& probeId, const ModuleID moduleId, const mdMethodDef methodId)
{
     std::lock_guard lock(_probeMetadataMapMutex);

     const auto methodIdentifierToAdd = trace::MethodIdentifier(moduleId, methodId);
     CreateNewProbeIfNotExists(probeId);

     _probeMetadataMap[probeId]->methods.emplace(methodIdentifierToAdd);

     // Mark the probe as Installed (if it was not marked as Error before)
     if (_probeMetadataMap[probeId]->status != ProbeStatus::_ERROR)
     {
         _probeMetadataMap[probeId]->status = ProbeStatus::INSTALLED;
     }
}

bool debugger::ProbesMetadataTracker::SetProbeStatus(const shared::WSTRING& probeId, ProbeStatus newStatus)
{
    std::shared_ptr<ProbeMetadata> probeMetadata;
    if (TryGetMetadata(probeId, probeMetadata))
    {
        if (probeMetadata->status != ProbeStatus::_ERROR)
        {
            probeMetadata->status = newStatus;
        }
        return true;
    }

    return false;
}

int debugger::ProbesMetadataTracker::RemoveProbes(const std::vector<shared::WSTRING>& probes)
 {
    std::lock_guard lock(_probeMetadataMapMutex);

    auto removedProbesCount = 0;

    if (!_probeMetadataMap.empty())
    {
        for (const auto& probe : probes)
        {
            const auto iter = _probeMetadataMap.find(probe);

            if (iter != _probeMetadataMap.end())
            {
                removedProbesCount++;
                _probeMetadataMap.erase(iter);
            }
        }
    }

    return removedProbesCount;
 }
