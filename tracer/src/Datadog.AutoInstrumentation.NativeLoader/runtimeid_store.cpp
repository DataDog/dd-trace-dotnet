#include "runtimeid_store.h"
#include "util.h"

RuntimeIdStore::RuntimeIdStore()
{
    m_isIis = IsRunningOnIIS();
    m_process_runtime_id = GenerateRuntimeId();
}

const std::string& RuntimeIdStore::Get(AppDomainID app_domain)
{
    if (!m_isIis) return m_process_runtime_id;

    std::lock_guard<std::mutex> l(m_mutex);
    auto& rid = m_appDomainToRuntimeId[app_domain];
    if (rid.empty())
    {
        rid = GenerateRuntimeId();
    }

    return rid;
}