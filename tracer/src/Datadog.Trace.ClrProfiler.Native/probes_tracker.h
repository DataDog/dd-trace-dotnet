#ifndef DD_CLR_PROFILER_DEBUGGER_INSTRUMENTED_METHOD_INFO_TRACKER_H_
#define DD_CLR_PROFILER_DEBUGGER_INSTRUMENTED_METHOD_INFO_TRACKER_H_

#include "corhlpr.h"
#include "integration.h"
#include <corprof.h>
#include <mutex>
#include <unordered_map>
#include "../../../shared/src/native-src/util.h"
#include "../../../shared/src/native-src/string.h"

namespace debugger
{
    class ProbesTracker : public shared::Singleton<ProbesTracker>
    {
        friend class shared::Singleton<ProbesTracker>;

    private:
        std::recursive_mutex _probeIdMapMutex;
        std::unordered_map<shared::WSTRING, std::set<trace::MethodIdentifier>> _probeIdMap{};

    public:
        ProbesTracker() = default;

        bool TryGetMethods(const shared::WSTRING& probeId, std::set<trace::MethodIdentifier>& methods);
        void AddProbe(const shared::WSTRING& probeId, const ModuleID moduleId, const mdMethodDef methodId);
        std::set<trace::MethodIdentifier> RemoveProbes(const std::vector<shared::WSTRING>& probes);
    };

} // namespace debugger

#endif