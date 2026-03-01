#pragma once
#include "../../../shared/src/native-src/string.h"
#include <optional>
#include <vector>

extern "C"
{
#include <dd/policies/eval_ctx.h>
#include <dd/policies/policies.h>
}

namespace datadog::shared::nativeloader
{

/// Reads the policy configuration from the system or a local source.
///
/// This function attempts to load serialized policy data that controls
/// workload authorization or configuration rules. The policy data is
/// typically stored as a FlatBuffer or another binary format.
///
/// @return
///   - A `std::optional<std::vector<uint8_t>>` containing the binary
///     policy data if available.
///   - `std::nullopt` if no valid policy data could be loaded.
std::optional<std::vector<uint8_t>> readPolicies();

/// Determines whether the specified workload is allowed to run under the current policies.
///
/// This function evaluates the provided process information, arguments,
/// application pool, and policy data to decide if the workload should
/// be permitted to execute. The logic typically involves comparing
/// the workload metadata against preloaded security or configuration
/// policies.
///
/// @param process_name The name of the process attempting to run (e.g., executable name).
///
/// @param argv The command-line arguments associated with the process.
///
/// @param application_pool The application pool or logical hosting environment associated with the workload (for
/// example, in IIS).
///
/// @param policies A vector of bytes representing serialized policy data, typically loaded from `readPolicies()`.
///
/// @param is_iis A boolean flag indicating whether the workload is running under IIS.
///
/// @return
///   `true` if the workload is allowed according to the policies;
///   `false` otherwise.
bool isWorkloadAllowed(const ::shared::WSTRING& process_name, const std::vector<::shared::WSTRING>& argv,
                       const ::shared::WSTRING& application_pool, const std::vector<uint8_t>& policies,
                       const bool is_iis);

} // namespace datadog::shared::nativeloader
