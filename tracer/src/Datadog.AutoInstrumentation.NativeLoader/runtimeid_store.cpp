#include "runtimeid_store.h"
#include "util.h"

RuntimeIdStore::RuntimeIdStore()
{
    m_isIis = IsRunningOnIIS();
    m_process_runtime_id = GenerateRuntimeId();
}

void RuntimeIdStore::Generate(AppDomainID app_domain)
{
    if (!m_isIis) return;

    decltype(m_appDomainToRuntimeId)::const_iterator it;
    {
        std::lock_guard<std::mutex> l(m_mutex);
        it = m_appDomainToRuntimeId.find(app_domain);
    }

    if (it != m_appDomainToRuntimeId.cend()) return;

    auto runtime_id = GenerateRuntimeId();

    std::lock_guard<std::mutex> l(m_mutex);
    m_appDomainToRuntimeId[app_domain] = std::move(runtime_id);
}

const std::string& RuntimeIdStore::Get(AppDomainID app_domain)
{
    if (!m_isIis) return m_process_runtime_id;

    std::lock_guard<std::mutex> l(m_mutex);
    return m_appDomainToRuntimeId[app_domain];
}