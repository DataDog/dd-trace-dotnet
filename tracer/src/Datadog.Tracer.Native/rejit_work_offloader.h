#ifndef DD_CLR_PROFILER_REJIT_WORK_OFFLOADER_H_
#define DD_CLR_PROFILER_REJIT_WORK_OFFLOADER_H_

#include <atomic>
#include <future>
#include <mutex>
#include <shared_mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "cor.h"
#include "corprof.h"
#include "module_metadata.h"

namespace trace
{

struct RejitWorkItem
{
    const bool terminating = false;
    const std::function<void()> func = nullptr;

    // Releases a caller blocked on this item's promise when func could not complete. Callers keep their own
    // shared_ptr to the promise for the enqueue-refused path, so the promise object outlives the work item
    // and an unresolved promise is a permanent hang rather than a broken_promise. Only set by enqueue paths
    // that carry a promise.
    const std::function<void()> abandon = nullptr;

    RejitWorkItem();

    RejitWorkItem(std::function<void()>&& func);

    RejitWorkItem(std::function<void()>&& func, std::function<void()>&& abandon);

    static std::unique_ptr<RejitWorkItem> CreateTerminatingWorkItem();
};

class RejitWorkOffloader
{

private:
    ICorProfilerInfo7* m_profilerInfo;

    std::unique_ptr<shared::UniqueBlockingQueue<RejitWorkItem>> m_offloader_queue;
    std::unique_ptr<std::thread> m_offloader_queue_thread;

    static void EnqueueThreadLoop(RejitWorkOffloader* offloader);

public:
    RejitWorkOffloader(ICorProfilerInfo7* pInfo);

    void Enqueue(std::unique_ptr<RejitWorkItem>&& item);
    bool WaitForTermination();
};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_WORK_OFFLOADER_H_
