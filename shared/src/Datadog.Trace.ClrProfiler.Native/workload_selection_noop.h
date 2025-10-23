#pragma once
#include "../../../shared/src/native-src/string.h"
#include <optional>
#include <vector>

namespace datadog::shared::nativeloader
{

inline std::optional<std::vector<uint8_t>> readPolicies()
{
    return std::nullopt;
}

inline bool isWorkloadAllowed(const ::shared::WSTRING&, const std::vector<::shared::WSTRING>&, const ::shared::WSTRING&,
                              const std::vector<uint8_t>&, const bool)
{
    return true;
}

} // namespace datadog::shared::nativeloader
