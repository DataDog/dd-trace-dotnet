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

private:
    // only for test only
    friend class runtimeid_store_EnsureRuntimeIsDifferentFor2DifferentAppDomains_Test;
    RuntimeIdStore(bool isIis);

public:
    const std::string& Get(AppDomainID appDomain);

private:
    bool m_isIis;
    std::string m_process_runtime_id;
    std::unordered_map<AppDomainID, std::string> m_appDomainToRuntimeId;
    std::mutex m_mutex;
};

} // namespace datadog::shared::nativeloader