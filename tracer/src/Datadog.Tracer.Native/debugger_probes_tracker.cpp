#include "debugger_probes_tracker.h"

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
         const auto methods = probeMetadataPair.second->methodIndexMap;

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
         auto methods = std::unordered_map<trace::MethodIdentifier, int>();
         _probeMetadataMap[probeId] = std::make_shared<ProbeMetadata>(probeId, std::move(methods), ProbeStatus::RECEIVED);
     }
 }

bool debugger::ProbesMetadataTracker::ProbeExists(const shared::WSTRING& probeId)
{
     return _probeMetadataMap.find(probeId) != _probeMetadataMap.end();
}

void debugger::ProbesMetadataTracker::AddMethodToProbe(const shared::WSTRING& probeId, const ModuleID moduleId, const mdMethodDef methodId)
{
     std::lock_guard lock(_probeMetadataMapMutex);

     CreateNewProbeIfNotExists(probeId);

     const auto methodIdentifierToAdd = trace::MethodIdentifier(moduleId, methodId);
     _probeMetadataMap[probeId]->methodIndexMap.emplace(methodIdentifierToAdd, -1);

     // Mark the probe as Installed (if it was not marked as Error before)
     if (_probeMetadataMap[probeId]->status != ProbeStatus::_ERROR)
     {
         _probeMetadataMap[probeId]->status = ProbeStatus::INSTALLED;
     }
}

bool debugger::ProbesMetadataTracker::TryGetNextInstrumentedProbeIndex(const shared::WSTRING& probeId, const ModuleID moduleId, 
                                                                        const mdMethodDef methodId, int& probeIndex)
{
     std::lock_guard lock(_probeMetadataMapMutex);

     std::shared_ptr<ProbeMetadata> probeMetadata;
     if (TryGetMetadata(probeId, probeMetadata))
     {
         const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);

         // Check if an index was previously assigned for this method
         const auto iter = probeMetadata->methodIndexMap.find(methodIdentifier);
         if (iter != probeMetadata->methodIndexMap.end() && iter->second > -1)
         {
             // An index was already assigned, so let's grab it
             probeIndex = iter->second;
         }
         else
         {
             // An index was not yet assigned for this method.
             // Try to reuse an existing index, if one is available, otherwise create a new one.
             if (!_freeProbeIndices.empty())
             {
                 probeIndex = _freeProbeIndices.front();
                 _freeProbeIndices.pop();
             }
             else
             {
                 probeIndex = std::atomic_fetch_add(&_nextInstrumentedProbeIndex, 1);
             }

            probeMetadata->methodIndexMap.insert_or_assign(methodIdentifier, probeIndex);
         }

         return true;
     }

     return false;
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

bool debugger::ProbesMetadataTracker::SetErrorProbeStatus(const shared::WSTRING& probeId,
                                                          const shared::WSTRING& errorMessage)
{
    std::shared_ptr<ProbeMetadata> probeMetadata;
    if (TryGetMetadata(probeId, probeMetadata))
    {
        probeMetadata->status = ProbeStatus::_ERROR;
        probeMetadata->errorMessage = errorMessage;
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
                // Add all the indices that were associated with the removing probe
                // into the `free probe indices` for later reuse.
                for (const auto& methodAndIndexPair : iter->second->methodIndexMap)
                {
                    if (methodAndIndexPair.second > -1)
                    {
                        _freeProbeIndices.push(methodAndIndexPair.second);
                    }
                }
                
                _probeMetadataMap.erase(iter);
                removedProbesCount++;
            }
        }
    }

    return removedProbesCount;
 }

int debugger::ProbesMetadataTracker::GetInstrumentedMethodIndex(const ModuleID moduleId, const mdMethodDef methodId)
 {
    std::lock_guard lock(_probeMetadataMapMutex);

    const auto methodIdentifier = trace::MethodIdentifier(moduleId, methodId);

    const auto iter = _methodIndexMap.find(methodIdentifier);
    if (iter == _methodIndexMap.end())
    {
        _methodIndexMap[methodIdentifier] = std::atomic_fetch_add(&_nextInstrumentedMethodIndex, 1);
    }

     return _methodIndexMap[methodIdentifier];
 }

int debugger::ProbesMetadataTracker::GetNextInstrumentationVersion()
{
     return std::atomic_fetch_add(&_nextInstrumentationVersion, 1);
}
