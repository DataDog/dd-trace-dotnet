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

std::unique_ptr<RejitWorkItem> RejitWorkItem::CreateTerminatingWorkItem()
{
    return std::make_unique<RejitWorkItem>();
}

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

            // Last-resort backstop: an uncaught exception escaping this thread's function calls
            // std::terminate(), taking the whole process down. The queued item's own producer
            // (e.g. RejitPreprocessor::EnqueueRequestRejitForLoadedModules) already catches and
            // handles what it can, but this covers anything else enqueued here, now or in the
            // future, that doesn't.
            try
            {
                item->func();
            }
            catch (const std::exception& ex)
            {
                Logger::Error("RejitWorkOffloader: uncaught exception from a queued rejit work item, "
                              "dropping it and continuing: ", ex.what());
            }
            catch (...)
            {
                Logger::Error("RejitWorkOffloader: uncaught non-standard exception from a queued rejit "
                              "work item, dropping it and continuing.");
            }
        }
    }
    Logger::Info("Exiting ReJIT request thread.");
}

} // namespace trace