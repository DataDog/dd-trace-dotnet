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
        const auto internalRuntimeId = ::shared::GetEnvironmentValue(environment::internal_runtime_id);
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

void RuntimeIdStore::OnAppDomainCreationStarted(AppDomainID app_domain)
{
    if (!m_isIis) return;

    std::lock_guard<std::mutex> l(m_mutex);
    auto node = m_appDomainToRuntimeId.extract(app_domain);
    if (!node.empty())
    {
        // The CLR can reuse the AppDomainID of an unloaded AppDomain. Keep the old runtime id alive because
        // callers (e.g. the Continuous Profiler) hold pointers to it.
        m_retiredRuntimeIds.push_back(std::move(node));
    }
}
} // namespace datadog::shared::nativeloader