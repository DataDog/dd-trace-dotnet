#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_

#include "environment_variables.h"
#include "string.h"
#include "util.h"

#define CheckIfTrue(EXPR)                                               \
  static int sValue = -1;                                               \
  if (sValue == -1) {                                                   \
    const auto envValue = EXPR;                                         \
    sValue = envValue == WStr("1") || envValue == WStr("true") ? 1 : 0; \
  }                                                                     \
  return sValue == 1;

#define CheckIfFalse(EXPR)                                               \
  static int sValue = -1;                                                \
  if (sValue == -1) {                                                    \
    const auto envValue = EXPR;                                          \
    sValue = envValue == WStr("0") || envValue == WStr("false") ? 1 : 0; \
  }                                                                      \
  return sValue == 1;

#define ToBooleanWithDefault(EXPR, DEFAULT)                          \
  static int sValue = -1;                                            \
  if (sValue == -1) {                                                \
    const auto envValue = EXPR;                                      \
    if (envValue == WStr("1") || envValue == WStr("true")) {         \
      sValue = 1;                                                    \
    } else if (envValue == WStr("0") || envValue == WStr("false")) { \
      sValue = 0;                                                    \
    } else {                                                         \
      sValue = DEFAULT;                                              \
    }                                                                \
  }                                                                  \
  return sValue == 1;

namespace trace {

bool DisableOptimizations() {
  CheckIfTrue(GetEnvironmentValue(environment::clr_disable_optimizations));
}

bool EnableInlining(bool defaultValue) {
  ToBooleanWithDefault(GetEnvironmentValue(environment::clr_enable_inlining),
                       defaultValue);
}

bool IsCallTargetEnabled() {
#if defined(ARM64) || defined(ARM)
  //
  // If the architecture is ARM64 or ARM, we enable CallTarget instrumentation by default
  //
  ToBooleanWithDefault(GetEnvironmentValue(environment::calltarget_enabled), true);
#else
  ToBooleanWithDefault(GetEnvironmentValue(environment::calltarget_enabled), false);
#endif
}

bool IsDebugEnabled() {
  CheckIfTrue(GetEnvironmentValue(environment::debug_enabled));
}

bool IsDumpILRewriteEnabled() {
  CheckIfTrue(GetEnvironmentValue(environment::dump_il_rewrite_enabled));
}

bool IsTracingDisabled() {
  CheckIfFalse(GetEnvironmentValue(environment::tracing_enabled));
}

bool IsAzureAppServices() {
  CheckIfTrue(GetEnvironmentValue(environment::azure_app_services));
}

bool IsNetstandardEnabled() {
  CheckIfTrue(GetEnvironmentValue(environment::netstandard_enabled));
}

bool IsDomainNeutralInstrumentation() {
  CheckIfTrue(GetEnvironmentValue(environment::domain_neutral_instrumentation));
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_