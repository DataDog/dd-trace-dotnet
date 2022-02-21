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

    std::lock_guard<std::mutex> l(m_mutex);
    m_appDomainToRuntimeId[app_domain] = GenerateRuntimeId();
}

const std::string& RuntimeIdStore::Get(AppDomainID app_domain)
{
    if (!m_isIis) return m_process_runtime_id;

    std::lock_guard<std::mutex> l(m_mutex);
    return m_appDomainToRuntimeId[app_domain];
}