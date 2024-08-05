#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_

#include "environment_variables.h"
#include "../../../shared/src/native-src/string.h"
#include "../../../shared/src/native-src/util.h"

#define IsTrue(EXPR) EXPR == WStr("1") || EXPR == WStr("true")
#define IsFalse(EXPR) EXPR == WStr("0") || EXPR == WStr("false")

#define CheckIfTrue(EXPR)                                                                                              \
    static int sValue = -1;                                                                                            \
    if (sValue == -1)                                                                                                  \
    {                                                                                                                  \
        const auto envValue = EXPR;                                                                                    \
        sValue = IsTrue(envValue) ? 1 : 0;                                                                             \
    }                                                                                                                  \
    return sValue == 1;

#define CheckIfFalse(EXPR)                                                                                             \
    static int sValue = -1;                                                                                            \
    if (sValue == -1)                                                                                                  \
    {                                                                                                                  \
        const auto envValue = EXPR;                                                                                    \
        sValue = IsFalse(envValue) ? 1 : 0;                                                                            \
    }                                                                                                                  \
    return sValue == 1;

#define ToBooleanWithDefault(EXPR, DEFAULT)                                                                            \
    static int sValue = -1;                                                                                            \
    if (sValue == -1)                                                                                                  \
    {                                                                                                                  \
        const auto envValue = EXPR;                                                                                    \
        if (IsTrue(envValue))                                                                                          \
        {                                                                                                              \
            sValue = 1;                                                                                                \
        }                                                                                                              \
        else if (IsFalse(envValue))                                                                                    \
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
bool IsAzureAppServices();
bool IsTraceAnnotationEnabled();
bool IsAzureFunctionsEnabled();
bool IsVersionCompatibilityEnabled();
bool IsIastEnabled();
bool IsRaspEnabled();

} // namespace trace

#endif // DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_UTIL_H_