#pragma once

extern "C"
{
#include <dd/policies/eval_ctx.h>
#include <dd/policies/policies.h>
}

namespace datadog::shared::nativeloader
{

/// TBD
bool is_workload_allowed();

} // namespace datadog::shared::nativeloader
