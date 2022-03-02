#include "rejit_work_offloader.h"
#include "logger.h"

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

RejitWorkOffloader::RejitWorkOffloader(ICorProfilerInfo7* pInfo)
{
    m_profilerInfo = pInfo;
    m_offloader_queue = std::make_unique<shared::UniqueBlockingQueue<RejitWorkItem>>();
    m_offloader_queue_thread = std::make_unique<std::thread>(EnqueueThreadLoop, this);
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

            item->func();
        }
    }
    Logger::Info("Exiting ReJIT request thread.");
}

} // namespace trace