#include "runtimeid_store.h"

#include "util.h"

#include "../../../shared/src/native-src/util.h"

datadog::shared::nativeloader::RuntimeIdStore::RuntimeIdStore()
{
    m_isIis = IsRunningOnIIS();
    m_process_runtime_id = ::shared::GenerateRuntimeId();
}

const std::string& datadog::shared::nativeloader::RuntimeIdStore::Get(AppDomainID app_domain)
{
    if (!m_isIis) return m_process_runtime_id;

    std::lock_guard<std::mutex> l(m_mutex);
    auto& rid = m_appDomainToRuntimeId[app_domain];
    if (rid.empty())
    {
        rid = ::shared::GenerateRuntimeId();
    }

    return rid;
}