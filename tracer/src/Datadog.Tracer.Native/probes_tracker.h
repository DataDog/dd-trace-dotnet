#ifndef DD_CLR_PROFILER_DEBUGGER_INSTRUMENTED_METHOD_INFO_TRACKER_H_
#define DD_CLR_PROFILER_DEBUGGER_INSTRUMENTED_METHOD_INFO_TRACKER_H_

#include "corhlpr.h"
#include "integration.h"
#include <corprof.h>
#include <mutex>
#include <unordered_map>
#include "../../../shared/src/native-src/util.h"
#include "../../../shared/src/native-src/string.h"
#include "debugger_members.h"

namespace debugger
{
    class ProbesMetadataTracker : public shared::Singleton<ProbesMetadataTracker>
    {
        friend class shared::Singleton<ProbesMetadataTracker>;

    private:
        std::recursive_mutex _probeMetadataMapMutex;
        std::unordered_map<shared::WSTRING, std::shared_ptr<ProbeMetadata>> _probeMetadataMap{};

    public:
        ProbesMetadataTracker() = default;

        bool TryGetMetadata(const shared::WSTRING& probeId, std::shared_ptr<ProbeMetadata>& probeMetadata);
        std::set<WSTRING> GetProbeIds(const ModuleID moduleId, const mdMethodDef methodId);
        void CreateNewProbeIfNotExists(const shared::WSTRING& probeId);
        void AddMethodToProbe(const shared::WSTRING& probeId, const ModuleID moduleId, const mdMethodDef methodId);
        bool SetProbeStatus(const shared::WSTRING& probeId, ProbeStatus newStatus);
        int RemoveProbes(const std::vector<shared::WSTRING>& probes);
    };

} // namespace debugger

#endif