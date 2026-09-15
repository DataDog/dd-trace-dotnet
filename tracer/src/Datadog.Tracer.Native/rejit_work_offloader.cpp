#include "rejit_work_offloader.h"
#include "logger.h"
#include "threadUtils.h"

namespace trace
{

//
// RejitWorkItem
//

RejitWorkItem::RejitWorkItem() : terminating(true), func(nullptr)
{
}

RejitWorkItem::RejitWorkItem(std::function<void()>&& func) :
    terminating(false), func(std::forward<std::function<void()>>(func))
{
}

RejitWorkItem::RejitWorkItem(std::function<void()>&& func, std::function<void()>&& abandon) :
    terminating(false),
    func(std::forward<std::function<void()>>(func)),
    abandon(std::forward<std::function<void()>>(abandon))
{
}

std::unique_ptr<RejitWorkItem> RejitWorkItem::CreateTerminatingWorkItem()
{
    return std::make_unique<RejitWorkItem>();
}

namespace
{
// Releases anyone blocked on a work item's promise after the item failed. Catching the exception is not
// enough on its own: the caller keeps its own reference to the promise (it needs it for the
// enqueue-refused path), so the promise object outlives the work item and an unresolved promise is a
// permanent hang rather than a broken_promise. An already-satisfied promise is tolerated, because the
// item may equally have thrown after resolving it.
void AbandonWorkItem(const RejitWorkItem& item)
{
    if (item.abandon == nullptr)
    {
        return;
    }

    try
    {
        item.abandon();
    }
    catch (const std::future_error&)
    {
    }
    catch (...)
    {
        Logger::Error("Unknown exception while releasing a failed ReJIT work item.");
    }
}
} // namespace

//
// RejitWorkOffloader
//

RejitWorkOffloader::RejitWorkOffloader(ICorProfilerInfo7* pInfo) :
    m_profilerInfo(pInfo),
    m_offloader_queue(std::make_unique<shared::UniqueBlockingQueue<RejitWorkItem>>()),
    m_offloader_queue_thread(std::make_unique<std::thread>([this] 
        {
            Threads::SetNativeThreadName(WStr("DD_rejit"));
            EnqueueThreadLoop(this);
        }))
{
}

void RejitWorkOffloader::Enqueue(std::unique_ptr<RejitWorkItem>&& item)
{
    m_offloader_queue->push(std::move(item));
}

bool RejitWorkOffloader::WaitForTermination()
{
    if (m_offloader_queue_thread->joinable())
    {
        m_offloader_queue_thread->join();
        return true;
    }

    return false;
}

void RejitWorkOffloader::EnqueueThreadLoop(RejitWorkOffloader* offloader)
{
    auto queue = offloader->m_offloader_queue.get();
    auto profilerInfo = offloader->m_profilerInfo;

    Logger::Info("Initializing ReJIT request thread.");

    // The profiler info is owned by the CLR and is always present in production. Tolerating its absence lets
    // the queue protocol be driven in unit tests without a full ICorProfilerInfo7 test double.
    if (profilerInfo != nullptr)
    {
        HRESULT hr = profilerInfo->InitializeCurrentThread();
        if (FAILED(hr))
        {
            Logger::Warn("Call to InitializeCurrentThread fail.");
        }
    }

    while (true)
    {
        const auto item = queue->pop();

        if (item->terminating)
        {
            // *************************************
            // Exit ReJIT thread
            // *************************************

            break;
        }
        else if (item->func != nullptr)
        {
            // *************************************
            // Execute given work
            // *************************************

            // An exception escaping here would unwind the thread function and terminate the process, turning a
            // recoverable allocation failure into a hard crash in the customer's application.
            try
            {
                item->func();
            }
            catch (const std::exception& ex)
            {
                Logger::Error("Exception while executing a ReJIT work item: ", ex.what());
                AbandonWorkItem(*item);
            }
            catch (...)
            {
                Logger::Error("Unknown exception while executing a ReJIT work item.");
                AbandonWorkItem(*item);
            }
        }
    }
    Logger::Info("Exiting ReJIT request thread.");
}

} // namespace trace