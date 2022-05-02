#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_

#include "environment_variables.h"
#include "../../../shared/src/native-src/string.h"
#include "../../../shared/src/native-src/util.h"

#define CheckIfTrue(EXPR)                                                                                              \
    static int sValue = -1;                                                                                            \
    if (sValue == -1)                                                                                                  \
    {                                                                                                                  \
        const auto envValue = EXPR;                                                                                    \
        sValue = envValue == WStr("1") || envValue == WStr("true") ? 1 : 0;                                            \
    }                                                                                                                  \
    return sValue == 1;

#define CheckIfFalse(EXPR)                                                                                             \
    static int sValue = -1;                                                                                            \
    if (sValue == -1)                                                                                                  \
    {                                                                                                                  \
        const auto envValue = EXPR;                                                                                    \
        sValue = envValue == WStr("0") || envValue == WStr("false") ? 1 : 0;                                           \
    }                                                                                                                  \
    return sValue == 1;

#define ToBooleanWithDefault(EXPR, DEFAULT)                                                                            \
    static int sValue = -1;                                                                                            \
    if (sValue == -1)                                                                                                  \
    {                                                                                                                  \
        const auto envValue = EXPR;                                                                                    \
        if (envValue == WStr("1") || envValue == WStr("true"))                                                         \
        {                                                                                                              \
            sValue = 1;                                                                                                \
        }                                                                                                              \
        else if (envValue == WStr("0") || envValue == WStr("false"))                                                   \
        {                                                                                                              \
            sValue = 0;                                                                                                \
        }                                                                                                              \
        else                                                                                                           \
        {                                                                                                              \
            sValue = DEFAULT;                                                                                          \
        }                                                                                                              \
    }                                                                                                                  \
    return sValue == 1;

namespace trace
{

bool DisableOptimizations();
bool EnableInlining();
bool IsNGENEnabled();
bool IsDebugEnabled();
bool IsDumpILRewriteEnabled();
bool IsTracingDisabled();
bool IsAzureAppServices();
bool NeedsAgentInAAS();
bool NeedsDogstatsdInAAS();
bool IsTraceAnnotationEnabled();
bool IsAzureFunctionsEnabled();
bool IsVersionCompatibilityEnabled();

} // namespace trace

#endif // DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_