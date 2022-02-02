#include "debugger_probes_instrumentation_requester.h"

#include "debugger_members.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "stats.h"
#include "version.h"
#include "debugger_rejit_preprocessor.h"

namespace debugger
{

DebuggerProbesInstrumentationRequester::DebuggerProbesInstrumentationRequester(std::shared_ptr<trace::RejitHandler> rejit_handler, std::shared_ptr<trace::RejitWorkOffloader> work_offloader)
{
    debugger_rejit_preprocessor = std::make_unique<DebuggerRejitPreprocessor>(std::move(rejit_handler), std::move(work_offloader));
}

void DebuggerProbesInstrumentationRequester::InstrumentProbes(WCHAR* id, DebuggerMethodProbeDefinition* items, int size,
                                                     CorProfiler* corProfiler)
{
    // TODO:
    //auto _ = trace::Stats::Instance()->InitializeLiveDebuggerMeasure();

    WSTRING definitionsId = WSTRING(id);
    Logger::Info("InitializeLiveDebugger: received id: ", definitionsId, " from managed side with ", size,
                 " integrations.");

    if (items != nullptr)
    {
        std::vector<MethodProbeDefinition> methodProbeDefinitions;

        for (int i = 0; i < size; i++)
        {
            const DebuggerMethodProbeDefinition& current = items[i];

            const WSTRING& targetAssembly = WSTRING(current.targetAssembly);
            const WSTRING& targetType = WSTRING(current.targetType);
            const WSTRING& targetMethod = WSTRING(current.targetMethod);

            std::vector<WSTRING> signatureTypes;
            for (int sIdx = 0; sIdx < current.targetParameterTypesLength; sIdx++)
            {
                const auto& currentSignature = current.targetParameterTypes[sIdx];
                if (currentSignature != nullptr)
                {
                    signatureTypes.push_back(WSTRING(currentSignature));
                }
            }

            const auto methodProbe = MethodProbeDefinition(
                MethodReference(targetAssembly, targetType, targetMethod, {}, {}, signatureTypes));

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetAssembly, " | ", targetType, ".", targetMethod, "(",
                              signatureTypes.size(), ")");
            }

            methodProbeDefinitions.push_back(methodProbe);
        }

        std::scoped_lock<std::mutex> moduleLock(corProfiler->module_ids_lock_);

        Logger::Info("Total number of modules to analyze: ", corProfiler->module_ids_.size());

        std::promise<ULONG> promise;
        std::future<ULONG> future = promise.get_future();
        debugger_rejit_preprocessor->EnqueueRequestRejitForLoadedModules(corProfiler->module_ids_,
                                                                          methodProbeDefinitions, &promise);

        // wait and get the value from the future<int>
        const auto& numReJITs = future.get();
        Logger::Debug("Total number of ReJIT Requested: ", numReJITs);

        method_probes_.reserve(method_probes_.size() + methodProbeDefinitions.size());
        for (const auto& methodProbe : methodProbeDefinitions)
        {
            method_probes_.push_back(methodProbe);
        }

        Logger::Info("InitializeLiveDebugger: Total startup method probes: ", method_probes_.size());
    }
}

const std::vector<MethodProbeDefinition>& DebuggerProbesInstrumentationRequester::GetProbes() const
{
    return method_probes_;
}

DebuggerRejitPreprocessor* DebuggerProbesInstrumentationRequester::GetPreprocessor()
{
    return debugger_rejit_preprocessor.get();
}

} // namespace debugger