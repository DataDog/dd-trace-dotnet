#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_

#include "string.h"  // NOLINT

namespace trace {
namespace environment {

const WSTRING tracing_enabled = "DD_TRACE_ENABLED"_W;

const WSTRING debug_enabled = "DD_TRACE_DEBUG"_W;

// supports multiple values
const WSTRING integrations_path = "DD_INTEGRATIONS"_W;

// supports multiple values
const WSTRING process_names = "DD_PROFILER_PROCESSES"_W;

const WSTRING agent_host = "DD_AGENT_HOST"_W;

const WSTRING agent_port = "DD_TRACE_AGENT_PORT"_W;

const WSTRING env = "DD_ENV"_W;

const WSTRING service_name = "DD_SERVICE_NAME"_W;

// supports multiple values
const WSTRING disabled_integrations = "DD_DISABLED_INTEGRATIONS"_W;

}  // namespace environment
}  // namespace trace

#endif