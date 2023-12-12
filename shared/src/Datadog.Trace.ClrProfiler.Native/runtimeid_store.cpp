#include "runtimeid_store.h"

#include "util.h"

#include "../../../shared/src/native-src/util.h"

namespace datadog::shared::nativeloader
{
RuntimeIdStore::RuntimeIdStore() : RuntimeIdStore(IsRunningOnIIS())
{
}

RuntimeIdStore::RuntimeIdStore(bool isIis) :
    m_isIis{isIis}
{
    if (isIis)
    {
        m_process_runtime_id = ::shared::GenerateRuntimeId();
    }
    else
    {
        const auto internalRuntimeId = ::shared::GetEnvironmentValue(EnvironmentVariables::InternalRuntimeId);
        if (internalRuntimeId.empty())
        {
            m_process_runtime_id = ::shared::GenerateRuntimeId();
        }
        else
        {
            m_process_runtime_id = ::shared::ToString(internalRuntimeId);
        }
    }
}

const std::string& RuntimeIdStore::Get(AppDomainID app_domain)
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
} // namespace datadog::shared::nativeloader