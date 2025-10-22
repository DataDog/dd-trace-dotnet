#pragma once

extern "C"
{
#include <dd/policies/eval_ctx.h>
#include <dd/policies/policies.h>
}

namespace datadog::shared::nativeloader
{

/// TBD
bool is_workload_allowed(const ::shared::WSTRING& process_name, const std::vector<::shared::WSTRING>& argv,
                         const ::shared::WSTRING& application_pool);

} // namespace datadog::shared::nativeloader
