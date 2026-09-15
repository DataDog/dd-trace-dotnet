#include "rejit_work_offloader.h"

#include <exception>

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
// Releases anyone blocked on a failed work item's promise. Catching the exception is not enough on its
// own: the caller keeps its own reference to the promise (it needs it for the enqueue-refused path), so
// the promise object outlives the work item and an unresolved promise is a permanent hang rather than a
// broken_promise.
//
// This has to be the first thing the failure path does, and it must not throw. Logger::Error formats
// through an ostringstream and therefore allocates, while the likeliest reason an item failed at all is
// that allocation is already failing. Logging first would risk an exception escaping the catch handler,
// unwinding the thread function and terminating the process with the waiter still unreleased.
void AbandonWorkItem(const RejitWorkItem& item) noexcept
{
    if (item.abandon == nullptr)
    {
        return;
    }

    try
    {
        item.abandon();
    }
    catch (...)
    {
        // An item that threw after resolving its promise lands here as promise_already_satisfied. There
        // is nothing useful left to do on any of these paths, and deliberately no logging: see above.
    }
}

// Best effort by design — failing to report a failure must not take the process down.
void LogWorkItemFailure(const char* what) noexcept
{
    try
    {
        if (what == nullptr)
        {
            Logger::Error("Unknown exception while executing a ReJIT work item.");
        }
        else
        {
            Logger::Error("Exception while executing a ReJIT work item: ", what);
        }
    }
    catch (...)
    {
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
    HRESULT hr = profilerInfo->InitializeCurrentThread();
    if (FAILED(hr))
    {
        Logger::Warn("Call to InitializeCurrentThread fail.");
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
                // Release the waiter before logging, never after: see AbandonWorkItem.
                AbandonWorkItem(*item);
                LogWorkItemFailure(ex.what());
            }
            catch (...)
            {
                AbandonWorkItem(*item);
                LogWorkItemFailure(nullptr);
            }
        }
    }
    Logger::Info("Exiting ReJIT request thread.");
}

} // namespace trace