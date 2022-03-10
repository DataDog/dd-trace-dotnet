#pragma once

#include <corhlpr.h>
#include <corprof.h>

#include <mutex>
#include <string>
#include <unordered_map>

namespace datadog::shared::nativeloader
{
class RuntimeIdStore
{
public:
    RuntimeIdStore();

    const std::string& Get(AppDomainID appDomain);

private:
    bool m_isIis;
    std::string m_process_runtime_id;
    std::unordered_map<AppDomainID, std::string> m_appDomainToRuntimeId;
    std::mutex m_mutex;
};

} // namespace datadog::shared::nativeloader