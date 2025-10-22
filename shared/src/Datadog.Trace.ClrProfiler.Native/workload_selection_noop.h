#pragma once
#include "../../../shared/src/native-src/string.h"

namespace datadog::shared::nativeloader
{

inline bool is_workload_allowed(const ::shared::WSTRING&, const std::vector<::shared::WSTRING>&, const ::shared::WSTRING&)
{
    return true;
}

} // namespace datadog::shared::nativeloader
