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
        std::unordered_map<trace::MethodIdentifier, int> _methodIndexMap{};
        // Holds incremental index that is used on the managed side for grabbing an InstrumentedMethodInfo instance (per
        // instrumented method)
        inline static std::atomic<int> _nextInstrumentedMethodIndex{0};
        // Holds incremental index that is used on the managed side for grabbing a ProbeData instance (per
        // instrumented probe)
        inline static std::atomic<int> _nextInstrumentedProbeIndex{0};
        // Holds indices that were previously used and freed upon probes removal.
        // the `_probeMetadataMapMutex` is used for syncing
        inline static std::queue<int> _freeProbeIndices{};
        // Holds incremental number that is uniquely given for each and every instrumentation instance. If a probe is added/removed from
        // a specific method, then this method is going to get a new number.
        inline static std::atomic<int> _nextInstrumentationVersion{0};
    public:
        ProbesMetadataTracker() = default;

        bool TryGetMetadata(const shared::WSTRING& probeId, std::shared_ptr<ProbeMetadata>& probeMetadata);
        std::set<WSTRING> GetProbeIds(ModuleID moduleId, mdMethodDef methodId);
        void CreateNewProbeIfNotExists(const shared::WSTRING& probeId);
        bool ProbeExists(const shared::WSTRING& probeId);
        void AddMethodToProbe(const shared::WSTRING& probeId, ModuleID moduleId, mdMethodDef methodId);
        bool TryGetNextInstrumentedProbeIndex(const shared::WSTRING& probeId, const ModuleID moduleId,
                                              const mdMethodDef methodId, int& probeIndex);
        bool SetProbeStatus(const shared::WSTRING& probeId, ProbeStatus newStatus);
        bool SetErrorProbeStatus(const shared::WSTRING& probeId, const shared::WSTRING& errorMessage);
        int RemoveProbes(const std::vector<shared::WSTRING>& probes);
        int GetInstrumentedMethodIndex(const ModuleID moduleId, const mdMethodDef methodId);
        static int GetNextInstrumentationVersion();
    };

} // namespace debugger

#endif